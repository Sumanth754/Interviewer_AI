using System.Security.Cryptography;
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
services.Configure<BootstrapOptions>(config.GetSection("Bootstrap"));

// JWT signing key.
// The dev key lives in appsettings.json, which is committed to a public repository,
// so falling back to it in Production would let anyone mint an Admin token. In
// Production a missing JWT_KEY therefore gets a random per-instance key instead:
// the service still starts, but tokens no longer survive a restart.
var runtimeKey = Environment.GetEnvironmentVariable("JWT_KEY");
if (string.IsNullOrWhiteSpace(runtimeKey))
{
    if (builder.Environment.IsProduction())
    {
        runtimeKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        Console.WriteLine("[startup] JWT_KEY is not set — generated a random key for this instance. " +
                          "Set the JWT_KEY env var to keep logins valid across restarts and deploys.");
    }
    else
    {
        runtimeKey = config["Jwt:Key"]!;
    }
}

// Signing and validation must use the *same* key, so the resolved runtime key
// has to win over whatever appsettings.json holds. TokenService reads this
// option; the JwtBearer handler below validates with runtimeKey.
services.Configure<JwtOptions>(config.GetSection("Jwt"));
services.PostConfigure<JwtOptions>(o => o.Key = runtimeKey);

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

// Swagger is on by default so the live deployment actually documents its API at
// /swagger. Set Swagger__Enabled=false to switch it off on a sensitive deployment.
if (app.Environment.IsDevelopment() || config.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Interview Platform API v1"));
}

app.UseCors("app");

// ---------- Static frontend (single-origin hosting) ----------
// When the Vite build is published into wwwroot the React app is served from the
// same origin as the API. That keeps the deployment on one URL, and leaves
// VITE_API_BASE empty so the browser calls relative /api paths — which means no
// CORS configuration and no separate frontend host.
var webRoot = app.Environment.WebRootPath;
var indexFile = string.IsNullOrEmpty(webRoot) ? null : Path.Combine(webRoot, "index.html");
var hasFrontend = indexFile is not null && File.Exists(indexFile);
if (hasFrontend)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// SPA history fallback: client-side routes such as /dashboard or /admin must
// return index.html, while unknown /api paths must stay 404 JSON.
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { error = "Endpoint not found." });
        return;
    }

    // A request for a concrete file (hashed bundle, favicon, …) that does not
    // exist must 404 rather than silently return HTML, which the browser would
    // reject with a confusing MIME-type error.
    if (Path.HasExtension(context.Request.Path.Value))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    if (hasFrontend)
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexFile!);
        return;
    }

    context.Response.StatusCode = StatusCodes.Status404NotFound;
    await context.Response.WriteAsJsonAsync(new
    {
        error = "Frontend bundle not found. Run the API on its own, or build the frontend into wwwroot."
    });
});

// ---------- Seed ----------
if (config["Seeding:RunOnStartup"] != "false")
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IAppSeeder>();
    await seeder.SeedAsync();
}

app.Logger.LogInformation("AI Interview Platform API ready on http://localhost:5000");
await app.RunAsync();