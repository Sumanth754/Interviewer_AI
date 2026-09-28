using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
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

// ---------- Storage ----------
// Database:Mode = Mongo means Mongo is REQUIRED. The previous behaviour was to
// fall back to the in-memory store on any connection problem, which produced a
// deployment that reported itself healthy while serving an empty app and
// discarding every write. We now fail closed and let the orchestrator restart us.
var dbMode = config["Database:Mode"] ?? "Auto";
var mongoRequired = dbMode.Equals("Mongo", StringComparison.OrdinalIgnoreCase);

if (dbMode.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("[startup] Storage: in-memory (Database:Mode=InMemory). Data will not persist.");
    services.AddSingleton<IAppStore, InMemoryAppStore>();
}
else
{
    var mongoCs = Environment.GetEnvironmentVariable("MONGO_CONNECTION")
                  ?? config["Database:Mongo:ConnectionString"];
    var mongoDb = config["Database:Mongo:DatabaseName"] ?? "ai_interview_platform";

    if (string.IsNullOrWhiteSpace(mongoCs))
    {
        var message =
            $"Storage: Database:Mode is '{dbMode}' but no MongoDB connection string is " +
            "configured. Set Database__Mongo__ConnectionString to the full " +
            "mongodb+srv:// URI.";

        if (mongoRequired) throw new MongoUnavailableException(message);

        Console.WriteLine($"[startup] {message} Using the in-memory store; data will not persist.");
        services.AddSingleton<IAppStore, InMemoryAppStore>();
    }
    else
    {
        try
        {
            var mongoStore = await MongoAppStore.ConnectAsync(mongoCs, mongoDb);
            await mongoStore.EnsureIndexesAsync();

            services.AddSingleton<IAppStore>(mongoStore);
            Console.WriteLine(
                $"[startup] Storage: MongoDB connected " +
                $"(host={MongoConnectionInfo.Host(mongoCs)}, database={mongoDb}, mode={dbMode}).");
        }
        catch (MongoUnavailableException ex)
        {
            if (mongoRequired) throw;

            Console.WriteLine(
                $"[startup] {ex.Message} Using the in-memory store; data will not persist.");
            services.AddSingleton<IAppStore, InMemoryAppStore>();
        }
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
// The active scorer is reported on /api/health so a deployment can never claim
// to be model-backed when it is only running the rule-based rubric.
var aiProvider = config["AI:Provider"] ?? "Auto";
var geminiModel = config["AI:Gemini:Model"] ?? "gemini-2.5-flash";
GeminiScoreService? gemini = null;
var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? config["AI:Gemini:ApiKey"] ?? "";

if (aiProvider.Equals("Rubric", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("[startup] AI scoring: rule-based rubric only (AI:Provider=Rubric). No model is called.");
}
else if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine(
        "[startup] AI scoring: rule-based rubric only. GEMINI_API_KEY is not set, so no " +
        "model is called. Set GEMINI_API_KEY in the environment to enable Gemini scoring.");
}
else
{
    gemini = new GeminiScoreService(
        apiKey,
        geminiModel,
        config["AI:Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta",
        int.Parse(config["AI:Gemini:TimeoutSeconds"] ?? "25"));
    Console.WriteLine(
        $"[startup] AI scoring: Gemini enabled (model={geminiModel}). " +
        "If a request fails the rubric scorer is used instead.");
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

// Keep model-validation failures in the same { error } shape the service layer
// already returns, so the SPA has one error path instead of two.
services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = context =>
    {
        var message = context.ModelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .SelectMany(kv => kv.Value!.Errors.Select(e =>
                string.IsNullOrWhiteSpace(e.ErrorMessage) ? "is not a valid value" : e.ErrorMessage))
            .FirstOrDefault() ?? "The request was not valid.";

        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { error = message });
    };
});

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
    // Every operation documents the Bearer requirement by default. The three
    // genuinely anonymous endpoints (health, register, login) are a known
    // cosmetic over-declaration; Swashbuckle 10 did not honour a per-operation
    // override here. Enforcement is unaffected - it is enforced by [Authorize].
    c.AddSecurityRequirement((Microsoft.OpenApi.OpenApiDocument doc) =>
        new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", doc, null!)] = new List<string>()
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

// ---------- Rate limiting ----------
// A public deployment with no throttle invites credential stuffing against
// /api/Auth/login. Auth endpoints get a tight per-IP budget; everything else gets
// a generous ceiling that still protects against runaway clients.
var authPermitLimit = config.GetValue("RateLimit:AuthPerMinute", 10);
var globalPermitLimit = config.GetValue("RateLimit:GlobalPerMinute", 300);
services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers["Retry-After"] = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Please slow down and try again shortly." },
            token);
    };
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = httpContext.Request.Path.StartsWithSegments("/api/Auth")
                    ? authPermitLimit
                    : globalPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

// ---------- Security headers ----------
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), geolocation=(), microphone=(self)";
    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        // Vite emits a small inline style block at runtime for the styled-components
        // free build; 'unsafe-inline' is scoped to styles only, never to scripts.
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "media-src 'self' blob:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";
    await next();
});

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
app.UseRateLimiter();
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