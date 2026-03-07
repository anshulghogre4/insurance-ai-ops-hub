# Insurance AI Operations Hub - Sprint Roadmap

## Vision

Transform the Sentiment Analyzer into a full **Insurance AI Operations Hub** with claims triage, fraud detection, document intelligence RAG, multimodal processing, and operational dashboards — all on free-tier AI providers.

---

## Sprint 1: Infrastructure + New Providers (COMPLETE)

**Goal:** Build provider infrastructure and multimodal services.

**Key Deliverables:**
- `IResilientKernelProvider` — 5-provider LLM fallback chain (Groq → Mistral → Gemini → OpenRouter → Ollama) with exponential backoff cooldown
- 5 multimodal services (Deepgram STT, Azure Vision, Cloudflare Vision, OCR.space, HuggingFace NER) — all raw HttpClient, 0 new NuGet packages
- `OrchestrationProfile` enum for selective agent activation (50-60% token reduction)
- Claims Triage + Fraud Detection agent prompts + insurance entity extraction (NER post-processing)
- PII redaction across entire multimodal pipeline

**Stats:** 30 new files, 7 modified | 173 tests | 0 new NuGet packages | 13 API keys configured

---

## Sprint 2: Claims & Fraud Pipeline + API Endpoints (COMPLETE)

**Goal:** Wire Sprint 1 infrastructure into working API endpoints.

**Key Deliverables:**
- 3 new DB tables (ClaimRecord, ClaimEvidenceRecord, ClaimActionRecord) + repository with pagination
- Profile-aware orchestration (ClaimsTriage: 4 agents/8 turns, FraudScoring: 3 agents/6 turns)
- 3 service facades (ClaimsOrchestration, MultimodalEvidenceProcessor, FraudAnalysis)
- 8 MediatR handlers + 8 API endpoints (triage, upload, claim detail, history, fraud analyze/score/alerts, provider health)
- Provider health monitoring (LLM + multimodal status)

**Stats:** 40 new files, 5 modified | 230 tests (+57) | 8 new API endpoints

---

## Sprint 3: Frontend + Dashboard + E2E + Landing Page (COMPLETE)

**Goal:** Build UI for all backend capabilities + public landing page.

**Key Deliverables:**
- Public landing page at `/` (1,726 lines, 7 interactive sections, IntersectionObserver animations)
- 7 new components: claims-triage, claim-result, evidence-viewer, claims-history, provider-health, fraud-alerts, dashboard charts
- Claims service + TypeScript models (8 HTTP methods)
- ng2-charts + Chart.js dashboard (severity doughnut, persona bar chart)
- 6 new routes (all authGuard except landing), SVG nav icons

**Stats:** ~50 new files, 8 modified | 196 frontend unit tests | 239 E2E tests | 2 new npm packages | 13 components, 11 routes

---

## Sprint 4: Document Intelligence RAG + Technical Debt (COMPLETE)

**Goal:** Fix P0/P1 tech debt, build RAG pipeline, CX Copilot, and cross-claim fraud correlation.

### Week 1: P0/P1 Technical Debt
- 15+ orchestrator unit tests (0% → 60%+ coverage)
- V1 PII fix via `PiiRedactingSentimentService` decorator (v1 files stay frozen)
- Per-endpoint rate limiting (analyze: 10/min, triage: 5/min, fraud: 5/min, doc upload: 3/min)
- Accessibility fixes (contrast, keyboard traps, aria-live)

### Week 2: Document Intelligence RAG
- Voyage AI embeddings (voyage-finance-2, 1024-dim) + Ollama fallback
- `DocumentRecord` + `DocumentChunkRecord` with cosine similarity via System.Numerics.Vector
- Insurance section-aware chunking (DECLARATIONS/COVERAGE/EXCLUSIONS/CONDITIONS/ENDORSEMENTS)
- RAG facade: upload (OCR → chunk → embed → store) + query (embed → vector search → LLM answer with citations)
- 4 document endpoints + DocumentQuery agent

### Week 3: CX Copilot + Fraud Enhancement
- `CustomerExperienceService` with SSE streaming, tone classification, 16-keyword escalation detection
- PII dual-pass redaction (input + output), regulatory disclaimers, `CxInteractionRecord` audit trail
- Cross-claim fraud correlation: 4 strategies (DateProximity, SimilarNarrative, SharedFlags, SameSeverity)
- Claim-type-specific correlation windows, review workflow (Pending/Confirmed/Dismissed)

### Week 4: Frontend + E2E + MCP Integration
- 5 new frontend components (document-upload, document-query, document-result, cx-copilot, fraud-correlation)
- 3 new services + models, 4 new E2E specs
- MCP servers: Playwright MCP (E2E test generation), Stitch MCP (design-to-code)
- 3-iteration adversarial review: 37 issues → all fixed → unanimous APPROVE

**Stats:** 461 backend + 235 frontend + 357 E2E = **1,053 total tests** | 18 components, 15 routes

---

## Sprint 5: UI Revamp + Enhanced RAG + New Providers + UX Polish (COMPLETE)

**Goal:** Enhance backend with batch processing, CX memory, hybrid RAG, 4 embedding providers. Revamp landing page with parallax. Add UX polish + CI/CD.

### Backend Enhancements
- **Batch Claims Processing:** `BatchClaimEndpoints.cs`, `BatchClaimService.cs`, CSV upload API
- **CX Conversation Memory:** `CxConversationRecord` entity, `ICxConversationRepository`, session-based chat history
- **Hybrid RAG:** `BM25Scorer.cs` (Okapi BM25) + `HybridRetrievalService.cs` (BM25 + vector fusion)
- **Synthetic QA:** Generate Q&A pairs from document chunks for model fine-tuning
- **4 New Embedding Providers:** Cohere, Gemini, HuggingFace, Jina (extends `ResilientEmbeddingProvider` chain: Voyage → Cohere → Gemini → HuggingFace → Jina → Ollama)
- **M4A Speech Fix:** Added audio/mp4, audio/x-m4a, audio/m4a, audio/aac, audio/flac support

### Frontend + UX Revamp
- **Parallax Landing Page:** Sticky hero, floating geometric shapes, per-section animations, gradient-morph dividers (~1720 CSS, ~1900 HTML)
- **4 New Components:** batch-upload, breadcrumb, command-palette (Ctrl+K), toast notifications
- **Services:** breadcrumb.service, command-registry.service, scroll.service, toast.service
- **GitHub Actions CI/CD:** `.github/workflows/ci.yml` with 3 parallel jobs

### Sprint 5 Stats

| Metric | Sprint 4 End | Sprint 5 Final |
|--------|-------------|----------------|
| Angular components | 18 | **22** (+4) |
| Routes | 15 | **16** (+1: /claims/batch) |
| Backend tests (xUnit) | 461 | **662** (+201) |
| Frontend unit tests (Vitest) | 235 | **443** (+208) |
| E2E tests (Playwright) | 357 | **~450** (+~93) |
| **Grand Total** | **1,053** | **~1,555** |
| MCP servers | 2 | **4** (+Context7, Sequential Thinking) |
| CI/CD | None | **GitHub Actions** |
| Embedding providers | 2 | **6** (+Cohere, Gemini, HuggingFace, Jina) |

---

### Sprint 5 Hotfixes (Post-Completion)

| # | Issue | Root Cause | Fix |
|---|-------|-----------|-----|
| 5.H1 | SSE stream error: "Synchronous operations are disallowed" | `StreamWriter` with `AutoFlush = true` calls sync `Flush()` on Kestrel response stream | Removed `AutoFlush`, rely on explicit `FlushAsync()` |
| 5.H2 | SSE stream stuck at 92% — final events never sent | Missing `FlushAsync()` after `[DONE]` write (caused by H1 fix) | Added `FlushAsync()` after `[DONE]` in both success and error paths |
| 5.H3 | SQLite: "table DocumentChunks has no column named ChunkLevel" | `EnsureCreated()` never adds columns to existing tables; `ChunkLevel` + `ParentChunkId` missing from auto-migration block | Added `ALTER TABLE` for both columns in startup migration |
| 5.H4 | Slow document upload (37 sequential Content Safety API calls) | `ScreenChunksSafetyAsync` used sequential `foreach` for HTTP calls | Switched to `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 5` |
| 5.H5 | Auto-migration block missing table guards for Sprint 4 tables | `CxConversations`, `CxInteractions`, `FraudCorrelations` had no `CREATE TABLE IF NOT EXISTS` guards | Added `TableExists()` helper + `CREATE TABLE` with indexes for all 3 tables |
| 5.H6 | RAG query returns "no results" despite indexed document | Cross-provider embedding mismatch: document indexed with Cohere (VoyageAI in cooldown), query used VoyageAI (recovered) — different vector spaces = 0 similarity | Added `ResolveQueryEmbeddingServiceAsync` — looks up document's `EmbeddingProvider`, resolves matching keyed service from DI |
| 5.H7 | Retrieval confidence shows 3% on relevant results | Raw RRF scores (~0.001-0.033) displayed as misleading low percentages | **FIXED (Sprint 6 6.4.6):** Normalized RRF scores to 0-1 range (top result = 1.0, others proportional). Applied to fused results + edge cases (vector-only, BM25-only). Updated `HybridRetrievalServiceTests` for normalized assertions. |
| 5.H8 | 13-page PDF shows only 2 pages after upload | **OCR chain gap**: PdfPig fails (scanned/image PDF, < 100 chars), Azure DocIntel F0 succeeds but caps at 2 pages/document, `ResilientOcrProvider` accepted 2-page partial as full success | **FIXED (Sprint 6 6.4.9):** Added Tesseract (local) + Mistral OCR (cloud) to chain. Azure page batching (2 pages/batch). OCR Space 1MB guard. PdfPig Letters fallback for CID/Type3 fonts. New 6-tier chain: PdfPig → Tesseract → Azure → Mistral → OCR Space → Gemini. |

---

## Sprint 6: Azure AI Services + Docker + Production Hardening (IN PROGRESS)

**Goal:** Execute the planned Sprint 4.5 Azure AI integrations (4 services, 2 resilient chains), containerize the backend, and add production readiness features (health checks, App Insights, production config).

**Brainstorming:** 9-agent brainstorming across 3 iterations (unanimous 9/9 APPROVE). All agents agreed Sprint 4.5 items are shovel-ready and Content Safety is a compliance gap for customer-facing CX Copilot.

**Why now:** Content Safety is mandatory for any customer-facing AI (CX Copilot). NER and STT are single-point-of-failure services with no fallback. Docker is prerequisite for cloud deployment.

### Week 1: Content Safety + Language NER (P1) — DONE

| # | Item | Deliverable |
|---|------|-------------|
| 6.1.1 | NuGet Packages | `Azure.AI.ContentSafety` 1.0.0, `Azure.AI.TextAnalytics` 5.3.0 |
| 6.1.2 | Settings Classes | `AzureContentSafetySettings`, `AzureLanguageSettings` in `LlmProviderConfiguration.cs` |
| 6.1.3 | Content Safety Service | `IContentSafetyService` + `AzureContentSafetyService` — text/image moderation, severity threshold >= 2 |
| 6.1.4 | CX Copilot Integration | Optional `IContentSafetyService?` in `CustomerExperienceService` — screen LLM responses before output |
| 6.1.5 | Azure Language NER | `AzureLanguageNerService` using `TextAnalyticsClient` SDK + insurance regex patterns |
| 6.1.6 | Resilient NER Chain | `ResilientEntityExtractionProvider` — HuggingFace (300 req/hr) → Azure Language (5K/mo) |
| 6.1.7 | Backend Tests | Content Safety (6) + Language NER (4) + Resilient NER (4) + Parallelism (6) = 20 tests |
| 6.1.8 | Config | `appsettings.json` sections for Content Safety + Language under `AgentSystem` |

### Week 2: Speech STT + Translator + Docker (P2) — DONE

| # | Item | Deliverable |
|---|------|-------------|
| 6.2.1 | NuGet Package | `Microsoft.CognitiveServices.Speech` 1.42.0 |
| 6.2.2 | Settings Classes | `AzureSpeechSettings`, `AzureTranslatorSettings` in `LlmProviderConfiguration.cs` |
| 6.2.3 | Azure Speech STT | `AzureSpeechToTextService` — transcription with PII redaction on output |
| 6.2.4 | Resilient STT Chain | `ResilientSpeechToTextProvider` — Deepgram ($200 credit) → Azure Speech (5 hrs/mo) |
| 6.2.5 | Translation Service | `ITranslationService` + `AzureTranslatorService` — REST API (HttpClient), 130+ languages |
| 6.2.6 | Backend Tests | Speech STT + Resilient STT + Translator tests |
| 6.2.7 | Dockerfile | Multi-stage .NET 10 build (sdk:10.0 → aspnet:10.0), expose 8080, agent prompts copied |
| 6.2.8 | docker-compose.yml | Backend service, SQLite persistence via volume, healthcheck |
| 6.2.9 | .dockerignore | Excludes bin/, obj/, node_modules/, test-results/, .git/ |

### Week 3: RAG Query Agent Team + Production Hardening — DONE

| # | Item | Deliverable |
|---|------|-------------|
| 6.3.1 | RAG Query Agent Team | 3 pipeline services: **Query Reformulator** (rewrites vague questions, skips short queries), **Answer Evaluator** (checks groundedness + citation quality), **Cross-Doc Reasoner** (synthesizes across multiple documents, detects conflicts). DI-registered, 10 unit tests. |
| 6.3.2 | EF Core Migrations | Deferred — current SQLite auto-migration block is robust with idempotent ALTER/CREATE. EF migrations add complexity without benefit for SQLite dev mode. Will implement when PostgreSQL production deployment requires it. |
| 6.3.3 | Health Endpoints | `GET /health` (liveness), `GET /health/ready` (readiness — DB connectivity, LLM provider count, multimodal service status). E2E mocks added. |
| 6.3.4 | Production Config | `appsettings.Production.json` — cloud provider chain (no Ollama), PostgreSQL, reduced max turns, App Insights connection string placeholder |
| 6.3.5 | App Insights Code | `Microsoft.ApplicationInsights.AspNetCore` 2.22.0 NuGet, conditional registration when connection string configured |
| 6.3.6 | Startup Validation | Enhanced: Azure AI services with checkmark/cross per service, embedding provider chain, OCR chain logged at startup |
| 6.3.7 | Frontend Badges | Content safety "Screened" badge on CX Copilot assistant messages, `contentSafetyScreened` field on response model + SSE metadata |
| 6.3.8 | Accessibility Audit | Command palette test fix (empty state outside `[role="listbox"]`). E2E axe-core scans passing. |
| 6.3.9 | E2E Tests | Health endpoint mocks (`/health`, `/health/ready`), document query result model updated with RAG agent fields |
| 6.3.10 | Documentation | Sprint roadmap updated. CLAUDE.md provider chains current. |

**Gate:** Docker container builds. RAG query pipeline services tested (10 tests). Health endpoints return 200. App Insights configured. All tests pass.

### Week 4: Bug Regression Tests + UX Polish

| # | Item | Deliverable |
|---|------|-------------|
| 6.4.1 | SSE Streaming Tests | ✅ **DONE** — 5 tests: AutoFlush disabled, [DONE] on success/error paths, SSE separators, cancellation |
| 6.4.2 | Migration Tests | ✅ **DONE** — 7 tests: ALTER TABLE idempotency, ChunkLevel/ParentChunkId (5.H3 regression), CREATE TABLE IF NOT EXISTS, indexes, foreign keys, full migration x2 |
| 6.4.3 | Embedding Provider Consistency Tests | ✅ **DONE** — 7 tests: keyed service resolution, Resilient() wrapper stripping, dimension mismatch, cosine similarity, unknown provider, all 6 registered |
| 6.4.4 | Content Safety Parallelism Tests | ✅ **DONE** — 6 tests: Interlocked thread safety, individual failure isolation, cancellation propagation, MaxDegreeOfParallelism, empty chunks, safety flags |
| 6.4.5 | Document Upload Sub-Loaders | ✅ **DONE** — Vertical timeline with phase nodes, per-phase sub-progress bars, ETA display, phase duration tracking |
| 6.4.6 | Confidence Score Normalization | ✅ **DONE** — RRF scores normalized to 0-1 (top=1.0, proportional). `NormalizeScores()` helper for edge cases. Updated `DocumentModels.Confidence` XML docs. 3 `HybridRetrievalServiceTests` updated. |
| 6.4.7 | Provider Health UI Revamp | ✅ **DONE** — Backend `/providers/extended` endpoint + frontend 7-section collapsible UI with chain visualizations, free tier limits, abbreviation subtitles |
| 6.4.8 | Card Hover Effects Polish | ✅ **DONE** — Added `focus-visible` states matching hover for `.glass-card`, `.glass-card-static`, `.metric-card`. Outline + box-shadow for keyboard accessibility. |
| 6.4.9 | OCR Chain Overhaul — 2 New Providers + Hardening | ✅ **DONE** — Added `TesseractOcrService` (local, TesseractOCR 5.5.1) + `MistralOcrService` (cloud, reuses Mistral API key). Azure page batching (2 pages/batch via `AnalyzeDocumentOptions.Pages`). OCR Space 1MB guard. PdfPig Letters fallback for CID/Type3 fonts. `ResilientOcrProvider` rewritten for 6-tier chain. 9 tests in `ResilientOcrProviderTests`. |
| 6.4.10 | Document Library Page | ✅ **DONE** — `/documents` route with card grid, category filter, sort toggle, pagination, nav links. 14 unit + 10 E2E tests |
| 6.4.11 | Navigation Cross-Linking Gaps | ✅ **DONE** — Added "View Correlations" link on `claim-result` when fraud flags present (`[routerLink]="'/fraud/correlations/' + c.claimId"`). Remaining items (Document Library page, nav links) deferred to 6.4.10. |

### New Files (Sprint 6)

| # | File | Week |
|---|------|------|
| 1 | `Backend/Services/Multimodal/IContentSafetyService.cs` | 1 |
| 2 | `Backend/Services/Multimodal/AzureContentSafetyService.cs` | 1 |
| 3 | `Backend/Services/Multimodal/AzureLanguageNerService.cs` | 1 |
| 4 | `Backend/Services/Multimodal/ResilientEntityExtractionProvider.cs` | 1 |
| 5 | `Backend/Services/Multimodal/AzureSpeechToTextService.cs` | 2 |
| 6 | `Backend/Services/Multimodal/ResilientSpeechToTextProvider.cs` | 2 |
| 7 | `Backend/Services/Multimodal/ITranslationService.cs` | 2 |
| 8 | `Backend/Services/Multimodal/AzureTranslatorService.cs` | 2 |
| 9 | `Tests/AzureContentSafetyServiceTests.cs` | 1 |
| 10 | `Tests/AzureLanguageNerServiceTests.cs` | 1 |
| 11 | `Tests/ResilientEntityExtractionProviderTests.cs` | 1 |
| 12 | `Tests/AzureSpeechToTextServiceTests.cs` | 2 |
| 13 | `Tests/ResilientSpeechToTextProviderTests.cs` | 2 |
| 14 | `Tests/AzureTranslatorServiceTests.cs` | 2 |
| 15 | `Backend/Dockerfile` | 2 |
| 16 | `docker-compose.yml` | 2 |
| 17 | `.dockerignore` | 2 |
| 18 | `Backend/appsettings.Production.json` | 3 |
| 19 | `Agents/RAGQuery/ProviderRouterAgent.cs` | 3 |
| 20 | `Agents/RAGQuery/QueryReformulatorAgent.cs` | 3 |
| 21 | `Agents/RAGQuery/AnswerEvaluatorAgent.cs` | 3 |
| 22 | `Agents/RAGQuery/CrossDocReasonerAgent.cs` | 3 |
| 23 | `Tests/SseStreamingTests.cs` | 4 |
| 24 | `Tests/SqliteMigrationTests.cs` | 4 |
| 25 | `Tests/EmbeddingProviderConsistencyTests.cs` | 4 |
| 26 | `Tests/ContentSafetyParallelismTests.cs` | 4 |

### Modified Files (Sprint 6)

| # | File | Change |
|---|------|--------|
| 1 | `Agents/Configuration/LlmProviderConfiguration.cs` | 4 settings classes + 4 properties on `AgentSystemSettings` |
| 2 | `Backend/appsettings.json` | 4 config sections under `AgentSystem` |
| 3 | `Backend/SentimentAnalyzer.API.csproj` | 3 NuGet packages (ContentSafety, Speech, AppInsights) |
| 4 | `Backend/Program.cs` | DI registrations (keyed services + resilient providers) + health checks + App Insights + startup validation |
| 5 | `Backend/Services/CustomerExperience/CustomerExperienceService.cs` | Optional content safety screening integration |
| 6 | Frontend CX Copilot component | Content safety badge + language badge |
| 7 | `CLAUDE.md` | Updated provider chains |
| 8 | All docs/*.md files | Sprint 6 content |

### New Resilient Provider Chains (Sprint 6)

```
NER:  HuggingFace BERT (300 req/hr) → Azure AI Language (5K records/mo)
STT:  Deepgram Nova-2 ($200 credit)  → Azure AI Speech (5 hrs/mo)
```

Both follow the `ResilientOcrProvider` pattern: exponential backoff cooldown (30s→60s→120s→240s→300s cap), thread-safe with `lock`, keyed DI services.

### Sprint 6 Stats (Current)

| Metric | Sprint 5 End | Sprint 6 Current |
|--------|-------------|-----------------|
| Backend tests (xUnit) | 662 | **704** (+42: Week 4 regression + Week 1-3 services + extended health) |
| Frontend unit tests | 443 | **443** (command palette fix) |
| E2E tests | ~450 | **~450** (health endpoint mocks added) |
| **Grand Total** | **~1,555** | **~1,597** |
| New NuGet packages | — | **3** (ContentSafety, Speech, AppInsights) |
| New resilient chains | — | **2** (NER: HuggingFace→Azure, STT: Deepgram→Azure) |
| Azure AI services integrated | 2 (Vision, DocIntel) | **6** (+ContentSafety, Language, Speech, Translator) |
| RAG query pipeline | None | **3 services** (QueryReformulator, AnswerEvaluator, CrossDocReasoner) |
| Docker | None | **Dockerfile + docker-compose + .dockerignore** |
| Health endpoints | None | **GET /health + GET /health/ready** |
| Production config | None | **appsettings.Production.json + App Insights** |

### Sprint 6 Risk Register

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Speech SDK may not support .NET 10 | Medium | Use REST API fallback (same HttpClient pattern as Deepgram) |
| Content Safety F0 5K text limit hit | Low | Log usage counter, graceful bypass when exhausted |
| Language F0 5K records shared across features | Medium | Budget: ~2,500 docs/month with dual-feature calls, log usage |
| Docker image size bloat | Low | Multi-stage build, .dockerignore, minimal aspnet runtime image |
| App Insights exceeds 5GB/month | Medium | Set daily cap to 0.16 GB/day, enable adaptive sampling |

---

## Summary Timeline

| Sprint | Focus | Status | Key Deliverable |
|--------|-------|--------|-----------------|
| **Sprint 1** | Infrastructure + Providers | **COMPLETE** | 5-provider LLM fallback, 5 multimodal services, 9 agents |
| **Sprint 2** | Claims & Fraud Pipeline | **COMPLETE** | 8 API endpoints, 3 DB tables, claims triage, fraud scoring |
| **Sprint 3** | Frontend + Dashboard + E2E | **COMPLETE** | 8 components, landing page, Chart.js dashboard, 239 E2E tests |
| **Sprint 4** | Document Intelligence RAG | **COMPLETE** | RAG pipeline, CX Copilot (SSE), fraud correlation, 1,053 tests |
| **Sprint 5** | UI Revamp + Enhanced RAG | **COMPLETE** | Parallax landing, batch claims, hybrid RAG, 4 embedding providers, ~1,555 tests |
| **Sprint 6** | Azure AI + Docker + RAG Agents + Hardening | **COMPLETE** (Weeks 1-4 ✅) | 6 Azure AI services, 2 resilient chains, RAG query pipeline, Docker, health endpoints, provider health UI, document library, 704 backend + 462 frontend tests |
| **Sprint 7** | Real-Time Analytics + Cloud Deploy + Advanced Fraud | **PLANNED** | SignalR dashboards, Azure deployment, anomaly detection, email alerts, ~1,850 tests |

### Deferred to Sprint 8+
- Azure AI Search (replace in-memory vector search)
- Custom domain + SSL certificate
- Azure SQL Free as alternate DB provider
- Multi-tenant support
- Webhook integrations for external claims systems

---

## Sprint 7: Real-Time Analytics + Cloud Deployment + Advanced Fraud (PLANNED)

**Goal:** Add real-time dashboards via SignalR, deploy to Azure cloud (Static Web Apps + App Service), implement advanced fraud detection with anomaly patterns, and add email/notification alerts for critical events.

**Prerequisites:** Sprint 6 Weeks 1-3 must be complete (Azure AI services, Docker, health endpoints, EF migrations).

### Week 1: SignalR Real-Time Dashboards (P1)

| # | Item | Deliverable |
|---|------|-------------|
| 7.1.1 | NuGet Package | `Microsoft.AspNetCore.SignalR` (included in ASP.NET Core) — no new package needed |
| 7.1.2 | Claims Hub | `ClaimsHub.cs` — real-time push for new claims, status changes, triage completions. Groups by severity (Critical/High subscribers get instant alerts). Methods: `ClaimTriaged`, `ClaimStatusChanged`, `FraudAlertRaised` |
| 7.1.3 | Provider Health Hub | `ProviderHealthHub.cs` — real-time provider status updates. Push cooldown state changes, failover events, usage counter ticks. Auto-broadcast every 30s or on state change |
| 7.1.4 | Analytics Hub | `AnalyticsHub.cs` — live dashboard metrics. Claims processed/hour, avg triage time, fraud detection rate, provider response times. Rolling 1-hour window with 10s granularity |
| 7.1.5 | Angular SignalR Service | `signalr.service.ts` — `@microsoft/signalr` npm package, auto-reconnect with exponential backoff, connection state signal, typed event handlers |
| 7.1.6 | Live Dashboard Component | `live-dashboard` component — real-time charts (claims/hour line chart, provider status cards with live pulse indicators, fraud alert feed with auto-scroll) |
| 7.1.7 | Notification Bell | Global notification bell in nav bar — unread count badge, dropdown with recent alerts (claims triaged, fraud detected, provider failovers), mark-as-read, link to relevant detail pages |
| 7.1.8 | Backend Tests | ~12 tests: Hub unit tests (message broadcasting, group management), SignalR service integration |

**Gate:** SignalR hubs broadcast on claim/fraud/provider events. Live dashboard updates without page refresh. Notification bell shows real-time alerts. 12 tests pass.

### Week 2: Azure Cloud Deployment (P1)

| # | Item | Deliverable |
|---|------|-------------|
| 7.2.1 | Azure Static Web Apps | Frontend deployment — Angular build output to Azure SWA, `staticwebapp.config.json` for SPA routing, environment-based API proxy |
| 7.2.2 | Azure App Service B1 | Backend deployment — Docker container from Sprint 6, App Service plan B1 ($13/mo or free trial), health check path `/health` |
| 7.2.3 | Azure Key Vault | Replace user-secrets with Key Vault references. `Azure.Extensions.AspNetCore.Configuration.Secrets` NuGet. All 20+ API keys moved to vault. Managed identity access |
| 7.2.4 | GitHub Actions CD | Extend CI pipeline with deploy jobs: `deploy-frontend` (SWA), `deploy-backend` (App Service). Environment secrets for Azure credentials. Deploy on `main` push only |
| 7.2.5 | CORS Configuration | Production CORS policy: allow SWA domain only. Dev: localhost:4200. `appsettings.Production.json` CORS origins |
| 7.2.6 | Azure Application Insights | Wire up App Insights from Sprint 6 config to actual Azure resource. Dashboard with request metrics, failure rates, dependency tracking, custom events for LLM provider usage |
| 7.2.7 | SSL + Custom Domain | Optional: custom domain on SWA + App Service. Azure-managed SSL certificates |
| 7.2.8 | Smoke Tests | Post-deploy health check script: verify `/health`, `/health/ready`, sample API calls, frontend loads |

**Gate:** Frontend accessible via Azure SWA URL. Backend API responds on App Service. Key Vault provides all secrets. CD pipeline deploys on push to main. App Insights collecting telemetry.

### Week 3: Advanced Fraud Detection + Email Alerts (P2)

| # | Item | Deliverable |
|---|------|-------------|
| 7.3.1 | Anomaly Detection Service | `IAnomalyDetectionService` + `StatisticalAnomalyService` — Z-score based anomaly detection on claim patterns ($ amount, frequency, timing). Rolling 90-day baseline per claim category. No Azure dependency (statistical approach). Flag claims >2σ from mean |
| 7.3.2 | Fraud Pattern Engine | `FraudPatternEngine.cs` — extends Sprint 4 correlation with temporal patterns: (1) **Velocity check** — same claimant filing >3 claims in 30 days, (2) **Amount escalation** — progressive claim value increases, (3) **Geographic clustering** — multiple claims from same location, (4) **Holiday proximity** — claims filed within 48hrs of holidays/weekends |
| 7.3.3 | Email Alert Service | `IEmailAlertService` + `AzureCommunicationEmailService` — Azure Communication Services Email (free: 100 emails/day). Templates: fraud alert, critical claim, provider down. HTML email with deep links to app |
| 7.3.4 | Alert Rules Engine | `AlertRulesEngine.cs` — configurable rules: severity threshold triggers, fraud score triggers, provider cooldown alerts. Rules stored in DB (`AlertRuleRecord`). Enable/disable per rule |
| 7.3.5 | Alert Management UI | `alert-management` component — CRUD for alert rules (severity, threshold, email recipients). Test alert button. Alert history log with delivery status |
| 7.3.6 | Fraud Analytics Dashboard | `fraud-analytics` component — charts: fraud detection rate over time, correlation network graph (D3.js or Chart.js scatter), top fraud patterns, anomaly heatmap by category |
| 7.3.7 | Backend Tests | ~18 tests: Anomaly detection (6), Fraud patterns (6), Email service (3), Alert rules (3) |
| 7.3.8 | E2E Tests | ~12 tests: Alert management CRUD, fraud analytics charts render, email test button |

**Gate:** Anomaly detection flags statistical outliers. Fraud patterns detect velocity/escalation. Email alerts sent for critical events. Alert rules configurable via UI. 30 tests pass.

### Week 4: Polish + Documentation + Performance (P3)

| # | Item | Deliverable |
|---|------|-------------|
| 7.4.1 | Performance Profiling | Profile hot paths: RAG query latency, batch claims throughput, SignalR message rate. Optimize: add response caching for document list, chunk embedding lookup memoization |
| 7.4.2 | Rate Limit Dashboard | `rate-limits` component — visual display of all free-tier usage vs limits. Progress bars with color coding (green <50%, yellow 50-80%, red >80%). Auto-refresh every 60s via SignalR |
| 7.4.3 | API Documentation | Swagger/OpenAPI via `Swashbuckle.AspNetCore` — auto-generated API docs with XML comment integration. Available at `/swagger` in dev mode only |
| 7.4.4 | User Preferences | `user-preferences` component — theme (light/dark), default LLM provider override, notification preferences, dashboard layout persistence (localStorage) |
| 7.4.5 | Accessibility Audit v2 | Full axe-core sweep on all ~24 routes including new SignalR components. Fix ARIA live regions for real-time updates. Screen reader announcements for notifications |
| 7.4.6 | Documentation Update | Update all docs: CLAUDE.md (new services), architecture.md (SignalR, deployment), api-reference.md (new endpoints), testing.md (updated counts), security.md (Key Vault) |
| 7.4.7 | E2E Full Regression | Complete E2E regression run across all routes. Fix any broken tests from new features |
| 7.4.8 | Sprint 7 Retrospective | Performance benchmarks, deployment checklist, free-tier budget audit |

**Gate:** All routes accessible. Swagger docs live. axe-core clean. Full E2E regression green. Documentation current.

### New Files (Sprint 7)

| # | File | Week |
|---|------|------|
| 1 | `Backend/Hubs/ClaimsHub.cs` | 1 |
| 2 | `Backend/Hubs/ProviderHealthHub.cs` | 1 |
| 3 | `Backend/Hubs/AnalyticsHub.cs` | 1 |
| 4 | `Frontend/.../services/signalr.service.ts` | 1 |
| 5 | `Frontend/.../components/live-dashboard/` | 1 |
| 6 | `Frontend/.../components/notification-bell/` | 1 |
| 7 | `staticwebapp.config.json` | 2 |
| 8 | `.github/workflows/cd.yml` | 2 |
| 9 | `Backend/Services/Fraud/IAnomalyDetectionService.cs` | 3 |
| 10 | `Backend/Services/Fraud/StatisticalAnomalyService.cs` | 3 |
| 11 | `Backend/Services/Fraud/FraudPatternEngine.cs` | 3 |
| 12 | `Backend/Services/Notifications/IEmailAlertService.cs` | 3 |
| 13 | `Backend/Services/Notifications/AzureCommunicationEmailService.cs` | 3 |
| 14 | `Backend/Services/Notifications/AlertRulesEngine.cs` | 3 |
| 15 | `Backend/Data/Entities/AlertRuleRecord.cs` | 3 |
| 16 | `Frontend/.../components/alert-management/` | 3 |
| 17 | `Frontend/.../components/fraud-analytics/` | 3 |
| 18 | `Frontend/.../components/rate-limits/` | 4 |
| 19 | `Frontend/.../components/user-preferences/` | 4 |
| 20 | `Tests/ClaimsHubTests.cs` | 1 |
| 21 | `Tests/AnomalyDetectionTests.cs` | 3 |
| 22 | `Tests/FraudPatternEngineTests.cs` | 3 |
| 23 | `Tests/EmailAlertServiceTests.cs` | 3 |
| 24 | `Tests/AlertRulesEngineTests.cs` | 3 |

### Sprint 7 Stats (Projected)

| Metric | Sprint 6 End (Projected) | Sprint 7 Projected |
|--------|-------------------------|-------------------|
| Angular components | 22 | **~28** (+6: live-dashboard, notification-bell, alert-management, fraud-analytics, rate-limits, user-preferences) |
| Routes | 16 | **~21** (+5: /live, /alerts/manage, /fraud/analytics, /rate-limits, /preferences) |
| Backend tests (xUnit) | ~725 | **~767** (+~42: hubs + anomaly + fraud patterns + email + alert rules) |
| Frontend unit tests | ~465 | **~505** (+~40: new components + SignalR service) |
| E2E tests | ~475 | **~500** (+~25: real-time + alert management + fraud analytics) |
| **Grand Total** | **~1,665** | **~1,772** |
| SignalR hubs | None | **3** (Claims, ProviderHealth, Analytics) |
| Cloud deployment | None | **Azure SWA + App Service** |
| Fraud detection strategies | 4 | **8** (+velocity, amount escalation, geographic, holiday proximity) |
| Email alerts | None | **Azure Communication Services** |
| API documentation | None | **Swagger/OpenAPI** |

### Sprint 7 Risk Register

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Azure App Service B1 cost ($13/mo) | Low | Use free trial 30 days, then evaluate. Can downgrade to F1 (limited) |
| Azure Communication Services email deliverability | Low | Verify sender domain, use templates, test with internal addresses first |
| SignalR connection limits on free tier | Medium | Use Azure SignalR Service free tier (20 concurrent, 20K msgs/day) if needed |
| D3.js bundle size for fraud network graph | Low | Use Chart.js scatter plot as simpler alternative, lazy-load D3 if needed |
| Key Vault access latency | Low | Cache secrets with 5-min TTL via `AzureKeyVaultConfigurationOptions.ReloadInterval` |

---

## Sprint 6 Week 4: Complete

All Week 4 items are done. See individual item rows in Week 4 table above for details.

---

### Deferred to Sprint 8+
- Azure AI Search (replace in-memory vector search)
- Custom domain + SSL certificate
- Azure SQL Free as alternate DB provider
- Multi-tenant support
- Webhook integrations for external claims systems

---

## Free Tier Budget

| Provider | Free Tier | Status |
|----------|-----------|--------|
| Groq | 250 req/day | Primary LLM |
| Cerebras | 30 req/min | LLM fallback |
| Mistral | 500K tokens/month | LLM fallback |
| Gemini | 60 req/min | LLM fallback |
| OpenRouter | $1 free credit | LLM fallback |
| OpenAI | Pay-per-token | LLM fallback |
| Ollama | Unlimited (local) | LLM last resort |
| Deepgram | $200 credit | Primary STT |
| Azure Vision F0 | 5K/month | Primary vision |
| Cloudflare Vision | 10K neurons/day | Vision fallback |
| OCR.space | 500/day | OCR tier 3 |
| Azure Doc Intelligence F0 | 500 pages/month | OCR tier 2 |
| HuggingFace | 300/hour | Primary NER |
| Voyage AI | 50M tokens | Primary embeddings |
| Cohere | Trial tokens | Embedding fallback |
| Gemini Embeddings | Free with API | Embedding fallback |
| Mistral OCR | Free tier (data trains) | **Sprint 6** OCR Tier 2b |
| Tesseract (local) | Unlimited (open-source) | **Sprint 6** OCR Tier 1b |
| HuggingFace Embeddings | 300/hour | Embedding fallback |
| Jina AI | 1M tokens | Embedding fallback |
| Azure AI Content Safety F0 | 5K text + 5K img/mo | **Sprint 6** |
| Azure AI Language F0 | 5K records/mo | **Sprint 6** |
| Azure AI Speech F0 | 5 hrs STT/mo | **Sprint 6** |
| Azure AI Translator F0 | 2M chars/mo | **Sprint 6** |
| Azure Communication Email | 100 emails/day | **Sprint 7** |
| Azure SignalR Service Free | 20 concurrent, 20K msgs/day | **Sprint 7** (if needed) |
| Azure Static Web Apps Free | 100 GB bandwidth/mo | **Sprint 7** |
| Azure App Service B1 | $13/mo (30-day free trial) | **Sprint 7** |
| Azure Key Vault | 10K transactions/mo free | **Sprint 7** |

---

*Last updated: 2026-03-07 | Sprint 6 COMPLETE (Weeks 1-4). All items done: Upload Sub-Loaders, Provider Health UI Revamp (7 collapsible sections), Document Library Page. 704 backend + 462 frontend tests passing. 3 adversarial review iterations (QA/UX/BA).*
