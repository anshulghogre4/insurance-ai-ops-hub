# Azure Cloud-Native Adoption Plan

## Insurance Domain Sentiment Analyzer — Cloud Migration Strategy

> **Decided by:** CTO (orchestrator), Solution Architect (design), Developer (feasibility)
> **Date:** 2026-02-21
> **Constraint:** Azure free-tier services only (free credits account)

---

## 1. Current State Assessment

### Architecture Today (Local-Only)

```
Angular 21 SPA (localhost:4200)
    │
.NET 10 Web API (localhost:5143)
    │
    ├── v1 API (legacy, frozen)
    ├── v2 Insurance API (CQRS via MediatR)
    └── Agent Orchestration (Semantic Kernel)
         ├── 6 Agents (CTO, BA, Dev, QA, Architect, UX)
         └── AI Providers: Groq → Gemini → Ollama → OpenAI
              │
         SQLite (local) / Supabase PostgreSQL (cloud)
```

### Deployment Gaps

| Component | Status |
|-----------|--------|
| Containerization (Docker) | Not implemented |
| CI/CD Pipeline | Not implemented |
| Cloud Infrastructure | Not implemented |
| Secrets Management (Cloud) | Not implemented (using .NET User Secrets locally) |
| Monitoring / Telemetry | Not implemented |
| IaC (Infrastructure as Code) | Not implemented |

---

## 2. Azure Free-Tier Service Mapping

| Current Component | Azure Free Service | Free Tier Limits |
|---|---|---|
| Angular 21 Frontend (localhost:4200) | **Azure Static Web Apps** | 100GB bandwidth, 2 custom domains, auto-SSL, free forever |
| .NET 10 Backend (localhost:5143) | **Azure App Service (B1 Linux)** | 750 hrs/month, free for 12 months |
| SQLite (local file) | **Azure SQL Database (Serverless)** | 100K vCore-seconds/month, 32GB storage, always free |
| Supabase PostgreSQL (cloud) | **Keep as-is** | 500MB free, already integrated |
| .NET User Secrets | **Azure Key Vault** | 10,000 operations/month, always free |
| No CI/CD | **GitHub Actions** | 2,000 minutes/month, free |
| No monitoring | **Application Insights** | 5GB ingestion/month, always free |

### Monthly Cost: **$0**

---

## 3. Target Cloud Architecture

```
                    ┌─────────────────────────────────┐
                    │     Azure Static Web Apps        │
                    │     (Angular 21 SPA)             │
                    │     - Auto SSL/TLS               │
                    │     - Global CDN                 │
                    │     - GitHub CI/CD built-in      │
                    │     - SPA route fallback          │
                    └──────────┬──────────────────────┘
                               │ /api/* proxy
                               ▼
                    ┌─────────────────────────────────┐
                    │   Azure App Service (B1 Linux)   │
                    │   (.NET 10 Web API)              │
                    │   - 750 hrs/mo free (12 months)  │
                    │   - Managed Identity             │
                    │   - Application Insights         │
                    └──────┬───────┬──────────────────┘
                           │       │
              ┌────────────┘       └────────────┐
              ▼                                  ▼
  ┌───────────────────────┐          ┌──────────────────┐
  │  Azure SQL Serverless │          │  Azure Key Vault  │
  │  (Free Tier)          │          │  (Secrets Store)  │
  │  100K vCore-s/mo      │          │  10K ops/mo       │
  │  32GB storage         │          │  Managed Identity  │
  │  Auto-pause on idle   │          │  access (no keys)  │
  └───────────────────────┘          └──────────────────┘
              │                               │
              ▼                               ▼
  ┌───────────────────────┐     ┌──────────────────────────┐
  │  Application Insights  │     │  GitHub Actions           │
  │  (Telemetry)           │     │  CI/CD Pipeline           │
  │  5GB/mo free           │     │  - Build + Test           │
  │  Live metrics          │     │  - Deploy Frontend (SWA)  │
  │  Failure diagnostics   │     │  - Deploy Backend (App Svc)|
  └───────────────────────┘     └──────────────────────────┘
```

---

## 4. Key Architectural Decisions

### Decision 1: Frontend Hosting — Azure Static Web Apps

**Why not App Service for frontend?**
- Static Web Apps is purpose-built for SPAs (Angular, React, Vue)
- Free tier includes: global CDN, auto-SSL, custom domains, staging per PR
- Built-in GitHub CI/CD — push to `main` triggers auto-deploy
- API proxy routes `/api/*` to backend — eliminates CORS issues
- Zero configuration for SPA routing (automatic fallback to `index.html`)

### Decision 2: Database Strategy — Dual-Track

| Environment | Database | Rationale |
|---|---|---|
| Local dev | SQLite | Fast, zero-config, already working |
| Cloud primary | Supabase PostgreSQL | Already integrated, no code changes, 500MB free |
| Cloud secondary | Azure SQL Free | Full-Azure option, 100K vCore-s/month, auto-pause |

- EF Core abstracts the provider — switching is a config change, not a code rewrite
- Add `Microsoft.EntityFrameworkCore.SqlServer` as an optional provider
- `Database:Provider` config controls which DB is active

### Decision 3: Ollama in Cloud — DROPPED

| Environment | AI Provider Fallback Chain |
|---|---|
| Local dev | Groq → Gemini → Ollama → OpenAI |
| Cloud | Groq → Gemini → OpenAI (no Ollama) |

- Ollama requires a local GPU — not available on App Service free tier
- Cloud chain configured via environment-specific `appsettings.Production.json`

### Decision 4: Secrets — Azure Key Vault + Managed Identity

- **Zero-secret deployment**: App Service authenticates to Key Vault via Managed Identity
- No API keys in code, config files, or environment variables
- Key Vault secret names use `--` separator: `AgentSystem--Groq--ApiKey`
- Auto-maps to .NET `IConfiguration` — existing `config["AgentSystem:Groq:ApiKey"]` works unchanged

### Decision 5: CI/CD — GitHub Actions (not Azure DevOps)

- GitHub Actions is simpler for a single-repo project
- 2,000 free minutes/month is more than enough
- Azure Static Web Apps provides a GitHub Actions workflow automatically
- Backend deployment via `azure/webapps-deploy` action

---

## 5. Implementation Phases

> **CTO directive:** Ship incrementally. Frontend first (quickest win), backend second, containerization third, hardening last.

### Phase 1: Deploy Frontend — Azure Static Web Apps (Day 1)

**Effort:** ~1 hour | **Code changes:** Minimal

- [ ] Create Azure Static Web App resource (Azure Portal or CLI)
- [ ] Connect GitHub repository → select `main` branch
- [ ] Configure build settings:
  - App location: `SentimentAnalyzer/Frontend/sentiment-analyzer-ui`
  - Output location: `dist/sentiment-analyzer-ui/browser`
  - API location: (empty — backend is separate)
- [ ] Add `staticwebapp.config.json` for route configuration:
  ```json
  {
    "navigationFallback": {
      "rewrite": "/index.html",
      "exclude": ["/assets/*", "/*.ico"]
    },
    "globalHeaders": {
      "X-Content-Type-Options": "nosniff",
      "X-Frame-Options": "DENY",
      "Content-Security-Policy": "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'"
    }
  }
  ```
- [ ] Add `environment.production.ts` with Azure backend URL
- [ ] Verify SPA routing, auto-SSL, and preview environments
- [ ] **Deliverable:** Live Angular app at `https://<name>.azurestaticapps.net`

### Phase 2: Deploy Backend — Azure App Service (Day 2-3)

**Effort:** ~3 hours | **Code changes:** Medium

- [ ] Create Azure App Service (B1 Linux, .NET 10 runtime)
- [ ] Create Azure Key Vault resource
- [ ] Enable Managed Identity on App Service
- [ ] Grant App Service identity `Key Vault Secrets User` role
- [ ] Store secrets in Key Vault:
  - `AgentSystem--Groq--ApiKey`
  - `AgentSystem--Gemini--ApiKey`
  - `OpenAI--ApiKey`
  - `ConnectionStrings--DefaultConnection` (Supabase)
  - `Supabase--Url`, `Supabase--AnonKey`, `Supabase--JwtSecret`
- [ ] Add NuGet packages:
  ```xml
  <PackageReference Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="1.*" />
  <PackageReference Include="Azure.Identity" Version="1.*" />
  <PackageReference Include="Microsoft.ApplicationInsights.AspNetCore" Version="2.*" />
  ```
- [ ] Update `Program.cs`:
  ```csharp
  // Key Vault integration
  if (!builder.Environment.IsDevelopment())
  {
      var keyVaultUri = new Uri($"https://{builder.Configuration["KeyVault:Name"]}.vault.azure.net/");
      builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
  }

  // Application Insights
  builder.Services.AddApplicationInsightsTelemetry();
  ```
- [ ] Update CORS policy:
  ```csharp
  policy.WithOrigins(
      "http://localhost:4200",
      "https://<name>.azurestaticapps.net"
  );
  ```
- [ ] Create GitHub Actions workflow (`.github/workflows/deploy-backend.yml`)
- [ ] Configure App Service deployment credentials in GitHub Secrets
- [ ] **Deliverable:** Live API at `https://<name>.azurewebsites.net`

### Phase 3: Containerize (Week 2)

**Effort:** ~2 hours | **Code changes:** New files only

- [ ] Create `Backend/Dockerfile`:
  ```dockerfile
  # Build stage
  FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
  WORKDIR /src
  COPY . .
  RUN dotnet publish Backend/SentimentAnalyzer.API.csproj -c Release -o /app

  # Runtime stage
  FROM mcr.microsoft.com/dotnet/aspnet:10.0
  WORKDIR /app
  COPY --from=build /app .
  EXPOSE 8080
  ENTRYPOINT ["dotnet", "SentimentAnalyzer.API.dll"]
  ```
- [ ] Create `docker-compose.yml` for local multi-container testing
- [ ] Add `.dockerignore`
- [ ] Update GitHub Actions to build and push container image
- [ ] (Optional) Switch App Service to container deployment
- [ ] **Deliverable:** Docker images, local compose, container-based deployment option

### Phase 4: Harden & Optimize (Week 3+)

**Effort:** Ongoing | **Code changes:** Incremental

- [ ] Add structured health check endpoints (`/health`, `/health/ready`)
- [ ] Configure Application Insights alerts (error rate, response time)
- [ ] Add Azure SQL Free as alternate DB provider:
  ```csharp
  "SqlServer" => options.UseSqlServer(connectionString)
  ```
- [ ] Set up staging deployment slots (preview environments)
- [ ] Add Playwright E2E tests against staging URL in CI pipeline
- [ ] Configure custom domain + SSL certificate (if needed)
- [ ] Add Azure Front Door or CDN for backend caching (if needed)
- [ ] Plan for B1 expiry (12 months) — evaluate:
  - Azure Container Apps (180K vCPU-s/month free, always free)
  - Azure Functions (serverless, 1M executions/month free)
  - Downgrade to F1 tier (60 min CPU/day, limited)

---

## 6. Code Changes Summary

### Files to Modify

| File | Change | Phase |
|---|---|---|
| `Backend/SentimentAnalyzer.API.csproj` | Add Azure NuGet packages | 2 |
| `Backend/Program.cs` | Add Key Vault config + App Insights | 2 |
| `Backend/Program.cs` | Update CORS origins | 2 |
| `Backend/appsettings.json` | Add `KeyVault:Name` setting | 2 |
| `Frontend/src/environments/environment.ts` | Update API URL for production | 1 |

### New Files to Create

| File | Purpose | Phase |
|---|---|---|
| `SentimentAnalyzer/plans/` | This plan document | 0 |
| `Frontend/.../staticwebapp.config.json` | SWA route config | 1 |
| `Frontend/src/environments/environment.production.ts` | Production environment | 1 |
| `.github/workflows/deploy-frontend.yml` | Frontend CI/CD | 1 |
| `.github/workflows/deploy-backend.yml` | Backend CI/CD | 2 |
| `Backend/Dockerfile` | Backend container image | 3 |
| `docker-compose.yml` | Local multi-container dev | 3 |
| `.dockerignore` | Docker build exclusions | 3 |
| `Backend/appsettings.Production.json` | Cloud-specific config | 2 |

### No Changes (Preserved)

| File | Reason |
|---|---|
| v1 API files (`SentimentController.cs`, etc.) | Frozen — backward compatibility |
| `InsuranceAnalysisOrchestrator.cs` | No changes needed — provider chain is config-driven |
| `IAnalysisRepository` / `SqliteAnalysisRepository` | Repository pattern already abstracts DB |
| Agent prompt files (`.claude/agents/*.md`) | Unchanged for cloud |
| Frontend components and services | API URL comes from environment — no logic changes |

---

## 7. Risk Register

| Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|
| B1 free tier expires (12 months) | Medium | Certain | Plan migration to Container Apps or F1 at month 10 |
| Azure SQL cold start (5-10s) | Medium | High | Keep-alive ping or accept first-request delay |
| Free tier rate limits | Low | Low | App is low-traffic; monitor via App Insights |
| Ollama unavailable in cloud | High | Certain | Drop from cloud chain; Groq → Gemini → OpenAI |
| Supabase free tier pausing (inactivity) | Medium | Medium | Keep-alive ping; Azure SQL as fallback |
| GitHub Actions minutes exhausted | Low | Low | 2,000 min/month; optimize with caching |
| App Service ephemeral filesystem | High | Certain | No SQLite in cloud; use Supabase/Azure SQL |

---

## 8. Post-12-Month Strategy (B1 Expiry)

When the Azure App Service B1 free tier expires, evaluate these **always-free** alternatives:

| Option | Free Tier | Tradeoff |
|---|---|---|
| **Azure Container Apps** | 180K vCPU-s, 360K GiB-s/month | Cold starts, consumption-based |
| **Azure Functions** | 1M executions, 400K GB-s/month | Requires refactor to serverless model |
| **App Service F1** | 60 min CPU/day, 1GB storage | Very limited, shared infra |
| **Keep B1 (pay)** | ~$13/month (B1 Linux) | Cheapest paid option |

**Recommendation:** Migrate to **Azure Container Apps** at month 10 — it's always free, supports containers natively, and handles our workload comfortably.

---

## 9. Quick-Start Commands

```bash
# Phase 1: Frontend deployment
az staticwebapp create \
  --name sentiment-analyzer-ui \
  --resource-group sentiment-rg \
  --source https://github.com/<user>/<repo> \
  --branch main \
  --app-location "SentimentAnalyzer/Frontend/sentiment-analyzer-ui" \
  --output-location "dist/sentiment-analyzer-ui/browser" \
  --login-with-github

# Phase 2: Backend deployment
az group create --name sentiment-rg --location eastus
az appservice plan create --name sentiment-plan --resource-group sentiment-rg --sku B1 --is-linux
az webapp create --name sentiment-api --resource-group sentiment-rg --plan sentiment-plan --runtime "DOTNETCORE:10.0"
az keyvault create --name sentiment-kv --resource-group sentiment-rg --location eastus
az webapp identity assign --name sentiment-api --resource-group sentiment-rg
az keyvault set-policy --name sentiment-kv --object-id <managed-identity-id> --secret-permissions get list
```

---

*This plan was collaboratively developed by the CTO, Solution Architect, and Developer agents following the project's established decision authority: Architect proposes, Developer validates feasibility, CTO approves.*
