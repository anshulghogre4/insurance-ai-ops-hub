using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Endpoints;
using SentimentAnalyzer.API.Middleware;
using SentimentAnalyzer.API.Services;
using SentimentAnalyzer.API.Services.Multimodal;
using SentimentAnalyzer.API.Services.Providers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers(); // Kept for v1 SentimentController
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Register MediatR for CQRS (scans this assembly for handlers)
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

// Register PII redaction service (mandatory before external AI calls)
builder.Services.AddSingleton<IPIIRedactor, PIIRedactionService>();

// Register sentiment analysis service (OpenAI GPT-4o-mini) - v1.0 legacy
builder.Services.AddSingleton<ISentimentService, OpenAISentimentService>();

// ==========================================
// v2.0 Insurance Multi-Agent System
// ==========================================

// Bind configuration sections
builder.Services.Configure<AgentSystemSettings>(builder.Configuration.GetSection("AgentSystem"));
builder.Services.Configure<AgentConfiguration>(builder.Configuration.GetSection("AgentConfiguration"));

// Register Resilient Kernel Provider (runtime fallback: Groq → Mistral → Gemini → OpenRouter → Ollama)
builder.Services.AddSingleton<IResilientKernelProvider>(sp =>
    new ResilientKernelProvider(
        sp.GetRequiredService<IOptions<AgentSystemSettings>>(),
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ILogger<ResilientKernelProvider>>()));

// Backward compatibility: existing code that injects Kernel directly still works
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IResilientKernelProvider>().GetKernel());

// Register Orchestration Profile Factory
builder.Services.AddSingleton<IOrchestrationProfileFactory, OrchestrationProfileFactory>();

// ==========================================
// Multimodal Services (STT, Vision, OCR, NER)
// ==========================================
builder.Services.AddHttpClient<ISpeechToTextService, DeepgramSpeechToTextService>();
builder.Services.AddHttpClient<IDocumentOcrService, OcrSpaceService>();
builder.Services.AddHttpClient<IEntityExtractionService, HuggingFaceNerService>();

// Vision services: Azure (primary) and Cloudflare (secondary) via keyed services
builder.Services.AddHttpClient<IImageAnalysisService, AzureVisionService>();
builder.Services.AddKeyedSingleton<IImageAnalysisService>("AzureVision", (sp, _) =>
    sp.GetRequiredService<IImageAnalysisService>());
builder.Services.AddHttpClient<CloudflareVisionService>();
builder.Services.AddKeyedSingleton<IImageAnalysisService>("CloudflareVision", (sp, _) =>
    sp.GetRequiredService<CloudflareVisionService>());

// Register EF Core - dual provider: PostgreSQL (Supabase) for production, SQLite for development
var dbProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<InsuranceAnalysisDbContext>(options =>
{
    if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

builder.Services.AddScoped<IAnalysisRepository, SqliteAnalysisRepository>();

// Register agent orchestration
builder.Services.AddScoped<IAnalysisOrchestrator, InsuranceAnalysisOrchestrator>();

// Supabase JWT Authentication (optional - enabled when Supabase:Url is configured with a real project)
var supabaseUrl = builder.Configuration["Supabase:Url"] ?? "";
var supabaseJwtSecret = builder.Configuration["Supabase:JwtSecret"] ?? "";
var supabaseAuthEnabled = !string.IsNullOrWhiteSpace(supabaseUrl)
    && !supabaseUrl.Contains("your-project");

if (supabaseAuthEnabled)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            if (!string.IsNullOrWhiteSpace(supabaseJwtSecret))
            {
                // Legacy: HS256 symmetric key validation
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"{supabaseUrl}/auth/v1",
                    ValidateAudience = true,
                    ValidAudience = "authenticated",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(supabaseJwtSecret))
                };
            }
            else
            {
                // New: ES256 JWKS-based validation (asymmetric keys)
                using var httpClient = new HttpClient();
                var jwksJson = httpClient.GetStringAsync(
                    $"{supabaseUrl}/auth/v1/.well-known/jwks.json").GetAwaiter().GetResult();
                var jwks = new JsonWebKeySet(jwksJson);

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"{supabaseUrl}/auth/v1",
                    ValidateAudience = true,
                    ValidAudience = "authenticated",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = jwks.GetSigningKeys()
                };
            }
        });
    builder.Services.AddAuthorization();
}

// Configure CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Validate critical configuration at startup (never log key values)
// With ResilientKernelProvider, we just need at least one provider configured.
// The provider itself validates and skips unconfigured providers at construction time.
var resolvedSettings = builder.Configuration.GetSection("AgentSystem").Get<AgentSystemSettings>();
if (resolvedSettings != null)
{
    var configuredProviders = new List<string>();
    if (!string.IsNullOrWhiteSpace(resolvedSettings.Groq.ApiKey)) configuredProviders.Add("Groq");
    if (!string.IsNullOrWhiteSpace(resolvedSettings.Mistral.ApiKey)) configuredProviders.Add("Mistral");
    if (!string.IsNullOrWhiteSpace(resolvedSettings.Gemini.ApiKey)) configuredProviders.Add("Gemini");
    if (!string.IsNullOrWhiteSpace(resolvedSettings.OpenRouter.ApiKey)) configuredProviders.Add("OpenRouter");
    if (!string.IsNullOrWhiteSpace(builder.Configuration["OpenAI:ApiKey"])) configuredProviders.Add("OpenAI");
    configuredProviders.Add("Ollama"); // Always available (local)

    if (configuredProviders.Count <= 1) // Only Ollama
    {
        Console.WriteLine(
            "WARNING: No cloud AI provider API keys configured. Only Ollama (local) is available. " +
            "Use 'dotnet user-secrets set \"AgentSystem:Groq:ApiKey\" \"your-key\"' to add a provider.");
    }
    else
    {
        Console.WriteLine($"Configured AI providers: {string.Join(" → ", configuredProviders)}");
    }
}

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InsuranceAnalysisDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Global exception handler - catches all unhandled exceptions
app.UseExceptionHandler();

// Only redirect to HTTPS in production (frontend uses HTTP in development)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAngular");

// Enable auth middleware only if Supabase is configured
if (supabaseAuthEnabled)
{
    app.UseAuthentication();
}
app.UseAuthorization();

// v1 controllers (SentimentController)
app.MapControllers();

// v2 Minimal API endpoints (Insurance - CQRS via MediatR)
app.MapInsuranceEndpoints(requireAuth: supabaseAuthEnabled);

app.Run();
