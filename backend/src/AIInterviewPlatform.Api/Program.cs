using System.Text;
using AIInterviewPlatform.Api.Auth;
using AIInterviewPlatform.Api.Data;
using AIInterviewPlatform.Api.Infrastructure;
using AIInterviewPlatform.Api.Scoring;
using AIInterviewPlatform.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

var services = builder.Services;
var config = builder.Configuration;

// ---------- Options ----------
services.Configure<JwtOptions>(config.GetSection("Jwt"));
var runtimeKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? config["Jwt:Key"]!;

// ---------- Storage (Mongo with in-memory fallback) ----------
var dbMode = config["Database:Mode"] ?? "Auto";
if (dbMode != "InMemory")
{
    var mongoCs = Environment.GetEnvironmentVariable("MONGO_CONNECTION")
                  ?? config["Database:Mongo:ConnectionString"];
    var mongoDb = config["Database:Mongo:DatabaseName"] ?? "ai_interview_platform";

    try
    {
        var mongoStore = new MongoAppStore(mongoCs!, mongoDb!);
        var ping = mongoStore.PingAsync().GetAwaiter().GetResult();
        if (ping && dbMode == "Mongo")
        {
            services.AddSingleton<IAppStore>(mongoStore);
            builder.Logging.AddConsole();
        }
        else if (ping)
        {
            services.AddSingleton<IAppStore>(mongoStore);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[startup] MongoDB unavailable ({ex.Message}); falling back to InMemory store.");
    }
}

if (!services.Any(d => d.ServiceType == typeof(IAppStore)))
    services.AddSingleton<IAppStore, InMemoryAppStore>();

// ---------- Cache (Redis with memory fallback) ----------
var cacheMode = config["Cache:Mode"] ?? "Auto";
services.AddMemoryCache();
if (cacheMode == "Memory")
    services.AddSingleton<ICacheService, InMemoryCacheService>();
else
{
    try
    {
        var redisCs = Environment.GetEnvironmentVariable("REDIS_CONNECTION")
                      ?? config["Cache:Redis:ConnectionString"];
        var redis = new RedisCacheService(redisCs!);
        services.AddSingleton<ICacheService>(redis);
        Console.WriteLine("[startup] Redis cache ready.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[startup] Redis unavailable ({ex.Message}); using in-memory cache.");
        services.AddSingleton<ICacheService, InMemoryCacheService>();
    }
}

// ---------- AI scoring ----------
var aiProvider = config["AI:Provider"] ?? "Auto";
GeminiScoreService? gemini = null;
var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? config["AI:Gemini:ApiKey"] ?? "";
if (aiProvider != "Rubric" && !string.IsNullOrWhiteSpace(apiKey))
{
    gemini = new GeminiScoreService(
        apiKey,
        config["AI:Gemini:Model"] ?? "gemini-1.5-flash",
        config["AI:Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta",
        int.Parse(config["AI:Gemini:TimeoutSeconds"] ?? "25"));
    Console.WriteLine("[startup] Gemini scoring enabled (falls back to rubric).");
}

if (gemini is not null)
    services.AddSingleton(gemini);
services.AddSingleton<IScoreService, CompositeScoreService>();
services.AddSingleton<RubricScoreService>();

// ---------- Application services ----------
services.AddSingleton<ITokenService, TokenService>();
services.AddSingleton<IAuthService, AuthService>();
services.AddSingleton<IReportService, ReportService>();
services.AddSingleton<AdaptiveDifficultyPicker>();
services.AddSingleton<IQuestionService, QuestionService>();
services.AddSingleton<ISessionService, SessionService>();
services.AddSingleton<IResumeService, ResumeService>();
services.AddSingleton<IAppSeeder, AppSeeder>();

// ---------- ASP.NET plumbing ----------
services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "AI Interview Platform API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header
    });
});

// ---------- CORS (configurable for local dev + hosted frontends) ----------
var corsOrigins = (config["Cors:Origins"] ?? "http://localhost:5173,http://127.0.0.1:5173,http://localhost:4173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
services.AddCors(o => o.AddPolicy("app", p =>
    p.AllowAnyHeader().AllowAnyMethod()
     .WithOrigins(corsOrigins)
     .AllowCredentials()));

services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(runtimeKey)),
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"] ?? "AIInterviewPlatform",
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"] ?? "AIInterviewPlatformUsers",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

services.AddAuthorization();

var app = builder.Build();

// ---------- Global error handling ----------
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (AppException ex)
    {
        context.Response.StatusCode = ex.StatusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception on {Path}", context.Request.Path);
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred. Please try again." });
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Interview Platform API v1"));
}

app.UseCors("app");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ---------- Seed ----------
if (config["Seeding:RunOnStartup"] != "false")
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IAppSeeder>();
    await seeder.SeedAsync();
}

app.Logger.LogInformation("AI Interview Platform API ready on http://localhost:5000");
await app.RunAsync();