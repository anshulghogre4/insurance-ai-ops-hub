using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
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
using SentimentAnalyzer.API.Services.Claims;
using SentimentAnalyzer.API.Services.Fraud;
using SentimentAnalyzer.API.Services.Multimodal;
using SentimentAnalyzer.API.Services.CustomerExperience;
using SentimentAnalyzer.API.Services.Documents;
using SentimentAnalyzer.API.Services.Embeddings;
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
// Decorator pattern: PiiRedactingSentimentService wraps OpenAISentimentService
// to add PII redaction before external AI calls without modifying frozen v1 files.
builder.Services.AddSingleton<OpenAISentimentService>();
builder.Services.AddSingleton<ISentimentService>(sp =>
    new PiiRedactingSentimentService(
        sp.GetRequiredService<OpenAISentimentService>(),
        sp.GetRequiredService<IPIIRedactor>(),
        sp.GetRequiredService<ILogger<PiiRedactingSentimentService>>()));

// ==========================================
// v2.0 Insurance Multi-Agent System
// ==========================================

// Bind configuration sections
builder.Services.Configure<AgentSystemSettings>(builder.Configuration.GetSection("AgentSystem"));
builder.Services.Configure<AgentConfiguration>(builder.Configuration.GetSection("AgentConfiguration"));

// Register Resilient Kernel Provider (runtime fallback: Groq → Cerebras → Mistral → Gemini → OpenRouter → OpenAI → Ollama)
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
builder.Services.AddHttpClient<IFinancialSentimentPreScreener, FinBertSentimentService>();

// Vision services: Azure (primary) and Cloudflare (secondary) via keyed services
builder.Services.AddHttpClient<IImageAnalysisService, AzureVisionService>();
builder.Services.AddKeyedSingleton<IImageAnalysisService>("AzureVision", (sp, _) =>
    sp.GetRequiredService<IImageAnalysisService>());
builder.Services.AddHttpClient<CloudflareVisionService>();
builder.Services.AddKeyedSingleton<IImageAnalysisService>("CloudflareVision", (sp, _) =>
    sp.GetRequiredService<CloudflareVisionService>());

// ==========================================
// Embedding Services (RAG - Voyage AI / Ollama)
// ==========================================
builder.Services.AddHttpClient<VoyageAIEmbeddingService>();
builder.Services.AddKeyedSingleton<IEmbeddingService>("VoyageAI", (sp, _) =>
    sp.GetRequiredService<VoyageAIEmbeddingService>());
builder.Services.AddHttpClient<OllamaEmbeddingService>();
builder.Services.AddKeyedSingleton<IEmbeddingService>("Ollama", (sp, _) =>
    sp.GetRequiredService<OllamaEmbeddingService>());
builder.Services.AddSingleton<IEmbeddingService, ResilientEmbeddingProvider>();

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
builder.Services.AddScoped<IClaimsRepository, SqliteClaimsRepository>();

// Register agent orchestration
builder.Services.AddScoped<IAnalysisOrchestrator, InsuranceAnalysisOrchestrator>();

// Register service facades (claims, evidence, fraud)
builder.Services.AddScoped<IClaimsOrchestrationService, ClaimsOrchestrationService>();
builder.Services.AddScoped<IMultimodalEvidenceProcessor, MultimodalEvidenceProcessor>();
builder.Services.AddScoped<IFraudAnalysisService, FraudAnalysisService>();
builder.Services.AddScoped<IFraudCorrelationRepository, SqliteFraudCorrelationRepository>();
builder.Services.AddScoped<IFraudCorrelationService, FraudCorrelationService>();

// Customer Experience Copilot (v4.0 - SSE streaming chat)
builder.Services.AddScoped<ICustomerExperienceService, CustomerExperienceService>();
builder.Services.AddScoped<ICxInteractionRepository, SqliteCxInteractionRepository>();

// Document Intelligence (RAG) services
builder.Services.AddScoped<IDocumentRepository, SqliteDocumentRepository>();
builder.Services.AddScoped<IDocumentChunkingService, InsuranceDocumentChunkingService>();
builder.Services.AddScoped<IDocumentIntelligenceService, DocumentIntelligenceService>();

// Supabase JWT Authentication (optional - requires BOTH Url AND JwtSecret)
// Supabase access tokens use HS256 (symmetric), so JWKS/OIDC discovery cannot validate them.
// The JWT secret is required: Supabase Dashboard > Settings > API > JWT Secret
var supabaseUrl = builder.Configuration["Supabase:Url"] ?? "";
var supabaseJwtSecret = builder.Configuration["Supabase:JwtSecret"] ?? "";
var supabaseAuthEnabled = !string.IsNullOrWhiteSpace(supabaseUrl)
    && !supabaseUrl.Contains("your-project")
    && !string.IsNullOrWhiteSpace(supabaseJwtSecret);

if (!string.IsNullOrWhiteSpace(supabaseUrl) && string.IsNullOrWhiteSpace(supabaseJwtSecret))
{
    builder.Services.AddSingleton<ILogger>(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup"));
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("[WARNING] Supabase:Url is configured but Supabase:JwtSecret is missing.");
    Console.WriteLine("         Backend auth is DISABLED. APIs are open without JWT validation.");
    Console.WriteLine("         To enable: dotnet user-secrets set \"Supabase:JwtSecret\" \"<your-jwt-secret>\"");
    Console.WriteLine("         Find it at: Supabase Dashboard > Settings > API > JWT Secret");
    Console.ResetColor();
}

if (supabaseAuthEnabled)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
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
        });
    builder.Services.AddAuthorization();
}

// Rate limiting — protects free-tier AI providers from excessive usage
// Per-endpoint policies: AI-heavy endpoints get stricter limits than read endpoints.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // General API fallback (read endpoints, health checks)
    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = 30;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 5;
    });

    // AI analysis: 10 req/min (multi-agent orchestration, highest token cost)
    options.AddFixedWindowLimiter("analyze", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 2;
    });

    // Claims triage: 5 req/min (AI agent pipeline)
    options.AddFixedWindowLimiter("triage", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 1;
    });

    // Fraud analysis: 5 req/min (AI agent pipeline)
    options.AddFixedWindowLimiter("fraud", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 1;
    });

    // Evidence upload: 3 req/min (multimodal processing: STT/Vision/OCR)
    options.AddFixedWindowLimiter("upload", limiterOptions =>
    {
        limiterOptions.PermitLimit = 3;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 1;
    });
});

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
    if (!string.IsNullOrWhiteSpace(resolvedSettings.Cerebras.ApiKey)) configuredProviders.Add("Cerebras");
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

    // Enable WAL mode for SQLite — allows concurrent readers during writes
    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

        // EnsureCreated() does NOT add new columns to existing tables.
        // This block adds columns introduced after initial table creation (Sprint 4+).
        var conn = db.Database.GetDbConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Helper: returns true if column exists in the given table
        bool ColumnExists(string table, string column)
        {
            cmd.CommandText = $"PRAGMA table_info({table});";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // Sprint 4: EntitiesJson added to ClaimEvidence
        if (!ColumnExists("ClaimEvidence", "EntitiesJson"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE ClaimEvidence ADD COLUMN EntitiesJson TEXT NOT NULL DEFAULT '[]';");
        }
    }
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
app.UseRateLimiter();

// Enable auth middleware only if Supabase is configured
if (supabaseAuthEnabled)
{
    app.UseAuthentication();
}
// UseAuthorization is always registered even without UseAuthentication
// so [AllowAnonymous] and endpoint-level policies still work correctly.
app.UseAuthorization();

// v1 controllers (SentimentController)
app.MapControllers();

// v2 Minimal API endpoints (Insurance - CQRS via MediatR)
app.MapInsuranceEndpoints(requireAuth: supabaseAuthEnabled);

// v2 Claims & Fraud pipeline endpoints
app.MapClaimsEndpoints(requireAuth: supabaseAuthEnabled);
app.MapFraudEndpoints(requireAuth: supabaseAuthEnabled);
app.MapProviderHealthEndpoints();

// v4 Document Intelligence (RAG) endpoints
app.MapDocumentEndpoints(requireAuth: supabaseAuthEnabled);

// v4 Customer Experience Copilot (SSE streaming)
app.MapCustomerExperienceEndpoints(requireAuth: supabaseAuthEnabled);

app.Run();
