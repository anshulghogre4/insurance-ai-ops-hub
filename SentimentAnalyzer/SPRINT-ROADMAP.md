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
| 6.4.12 | Provider Health UI Polish | ✅ **DONE** — Fixed accordion backdrop-filter blur artifact (`.no-backdrop-blur` class with `!important`), text-left alignment on accordion headers, refresh button restyled with hover/active scale effects + disabled state. Gemini/HuggingFace embedding health now falls back to shared LLM API keys (`\|\|` logic). |
| 6.4.13 | Native Select Dropdown Theme | ✅ **DONE** — Added `color-scheme: dark` to `.theme-dark`/`.theme-semi-dark` and `color-scheme: light` to `.theme-light` in `styles.css` so native `<select>`/`<option>` elements match the app theme. |
| 6.4.14 | E2E Test Fixes | ✅ **DONE** — Fixed document-library badge tests (scoped to `.grid` to avoid matching hidden `<option>` elements), fixed document-query filter dropdown count (5 options: "All documents" + 4 items). 666 E2E tests passing. |

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
| Frontend unit tests | 443 | **462** (+19: document library, provider health) |
| E2E tests | ~450 | **~666** (+~216: document library, document query, provider health, batch upload) |
| **Grand Total** | **~1,555** | **~1,832** |
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
| **Sprint 6** | Azure AI + Docker + RAG Agents + Hardening | **COMPLETE** | 6 Azure AI services, 2 resilient chains, RAG query pipeline, Docker, health endpoints, provider health UI, document library, ~1,832 total tests |
| **Sprint 7** | Real-Time Analytics + Cloud Deploy + Advanced Fraud | **PLANNED** | SignalR dashboards, VPS+Cloudflare deployment (~$4.51/mo), anomaly detection, Resend email alerts, ~1,954 projected tests |

### Deferred to Sprint 8+
- Azure AI Search or self-hosted Meilisearch (replace in-memory vector search)
- Custom domain + SSL certificate (Caddy auto-HTTPS ready)
- Multi-tenant support
- Webhook integrations for external claims systems

---

## Sprint 7: Real-Time Analytics + Cloud Deployment + Advanced Fraud (IN PROGRESS)

**Goal:** Add real-time dashboards via SignalR, deploy to production via VPS (Hetzner/Netcup ~$4.51/mo) + Cloudflare Pages ($0) + PostgreSQL+pgvector, implement advanced fraud detection with anomaly patterns, and add email alerts via Resend (3K/mo free).

**Prerequisites:** Sprint 6 COMPLETE (Azure AI services, Docker, health endpoints, ~1,832 tests passing).

**Starting State:** 22 components, 17 routes, 8 endpoint groups, 704 backend + 462 frontend + ~666 E2E = ~1,832 total tests.

**Architecture Decision — Auth + Database Split:**
Supabase Auth (free, 50K MAU) is kept for authentication — it's frontend-only via `@supabase/supabase-js` and independent of the app database. App data moves to self-hosted PostgreSQL+pgvector on the VPS. Zero code changes to `AuthService`, guards, or login component. The `authEnabled` computed signal checks `supabaseUrl`/`supabaseAnonKey` environment vars — when empty, auth is disabled (E2E/local dev mode).

```
Supabase (FREE - auth only)          VPS (~$4.51/mo)
├── User sign-up/sign-in             ├── .NET Backend (Docker)
├── JWT tokens                       ├── PostgreSQL 16 + pgvector (app data)
├── Email verification               └── Caddy (reverse proxy, auto HTTPS)
└── 50K monthly active users
                                     Cloudflare Pages ($0)
                                     └── Angular frontend (CDN)
```

---

### Week 1: SignalR Real-Time Dashboards (COMPLETE)

#### 7.1.1 — SignalR Infrastructure + NuGet

| Layer | Detail |
|-------|--------|
| **NuGet** | `Microsoft.AspNetCore.SignalR` (included in ASP.NET Core — no new package) |
| **npm** | `@microsoft/signalr` ^8.0.0 |
| **Config** | Add `SignalR` section to `appsettings.json`: `KeepAliveIntervalSeconds: 15`, `ClientTimeoutSeconds: 30`, `MaximumReceiveMessageSize: 65536` |
| **Program.cs** | `builder.Services.AddSignalR()`, `app.MapHub<ClaimsHub>("/hubs/claims")`, `app.MapHub<ProviderHealthHub>("/hubs/provider-health")`, `app.MapHub<AnalyticsHub>("/hubs/analytics")` |
| **CORS** | Add SignalR origins to existing CORS policy (WebSocket + SSE transports) |

#### 7.1.2 — Claims Hub

| Layer | Detail |
|-------|--------|
| **File** | `Backend/Hubs/ClaimsHub.cs` |
| **Interface** | `IClaimsHubClient`: `ClaimTriaged(ClaimTriagedEvent)`, `ClaimStatusChanged(ClaimStatusEvent)`, `FraudAlertRaised(FraudAlertEvent)` |
| **Groups** | `JoinSeverityGroup(string severity)` — subscribe to Critical/High/Medium/Low groups |
| **Event models** | `ClaimTriagedEvent { ClaimId, Severity, PersonaType, TriagedAt }`, `ClaimStatusEvent { ClaimId, OldStatus, NewStatus }`, `FraudAlertEvent { ClaimId, FraudScore, Flags[], DetectedAt }` |
| **Integration** | Inject `IHubContext<ClaimsHub>` into `ClaimsOrchestrationService` + `FraudAnalysisService` — broadcast after triage/fraud operations |
| **Tests** | 4 tests: broadcast on triage, severity group filtering, status change event, fraud alert event |

#### 7.1.3 — Provider Health Hub

| Layer | Detail |
|-------|--------|
| **File** | `Backend/Hubs/ProviderHealthHub.cs` |
| **Interface** | `IProviderHealthHubClient`: `ProviderStatusChanged(ProviderStatusEvent)`, `HealthSnapshot(HealthSnapshotEvent)` |
| **Background service** | `ProviderHealthBroadcaster : BackgroundService` — polls `IResilientKernelProvider.GetHealthStatus()` every 30s, broadcasts only on state change (diff detection) |
| **Event models** | `ProviderStatusEvent { ProviderName, OldStatus, NewStatus, CooldownSeconds, ChangedAt }`, `HealthSnapshotEvent { Providers[], CheckedAt }` |
| **Integration** | `ResilientKernelProvider` raises status change → broadcaster pushes to all connected clients |
| **Tests** | 3 tests: periodic broadcast, state-change-only filtering, cooldown event |

#### 7.1.4 — Analytics Hub

| Layer | Detail |
|-------|--------|
| **File** | `Backend/Hubs/AnalyticsHub.cs` |
| **Interface** | `IAnalyticsHubClient`: `MetricsUpdate(AnalyticsMetrics)` |
| **Background service** | `AnalyticsAggregator : BackgroundService` — rolling 1-hour window with 10s granularity. Tracks: claims processed/hour, avg triage time (ms), fraud detection rate (%), provider response times (ms), document queries/hour |
| **In-memory store** | `ConcurrentDictionary<string, RollingCounter>` — thread-safe counters per metric. 360 buckets (1hr ÷ 10s) |
| **Event model** | `AnalyticsMetrics { ClaimsPerHour, AvgTriageMs, FraudDetectionRate, ProviderResponseMs, DocQueriesPerHour, WindowStart, WindowEnd }` |
| **Tests** | 3 tests: rolling window calculation, counter thread safety, metrics aggregation |

#### 7.1.5 — Angular SignalR Service

| Layer | Detail |
|-------|--------|
| **File** | `Frontend/.../services/signalr.service.ts` |
| **Spec** | `Frontend/.../services/signalr.service.spec.ts` |
| **DI** | `providedIn: 'root'`, injectable via `inject(SignalRService)` |
| **Signals** | `connectionState: signal<'connected' \| 'reconnecting' \| 'disconnected'>`, `isConnected: computed(() => connectionState() === 'connected')` |
| **Methods** | `connect()`, `disconnect()`, `on<T>(hubName, eventName): Observable<T>`, `joinGroup(hubName, group)`, `leaveGroup(hubName, group)` |
| **Reconnect** | Exponential backoff: 0s → 2s → 10s → 30s → 60s (max). Auto-reconnect on disconnect |
| **Hub connections** | Lazy-create per hub URL (`/hubs/claims`, `/hubs/provider-health`, `/hubs/analytics`). Shared connection pool |
| **Tests** | 6 tests: connect/disconnect, reconnect backoff, event subscription, group join/leave, connection state signal, error handling |

#### 7.1.6 — Live Dashboard Component (Full Stack)

| Layer | Detail |
|-------|--------|
| **C# Model** | `AnalyticsMetrics` (reuse from 7.1.4 hub event model) |
| **TS Interface** | `Frontend/.../models/analytics.ts`: `AnalyticsMetrics`, `ClaimTriagedEvent`, `FraudAlertEvent`, `ProviderStatusEvent` |
| **Component** | `Frontend/.../components/live-dashboard/live-dashboard.ts` |
| **Route** | `{ path: 'dashboard/live', component: LiveDashboardComponent, canActivate: [authGuard], data: { breadcrumb: 'Live' } }` |
| **UI sections** | (1) **Claims velocity** — Chart.js line chart (claims/hour, 1hr window, auto-scroll), (2) **Provider status grid** — cards with live pulse dot (green/amber/red), cooldown timer, (3) **Fraud alert feed** — auto-scrolling list of recent fraud events with severity badges, (4) **Key metrics** — 4 metric cards (Claims/hr, Avg triage, Fraud rate, Doc queries) with animated counters |
| **Signals** | `metrics = signal<AnalyticsMetrics \| null>(null)`, `recentFraudAlerts = signal<FraudAlertEvent[]>([])`, `providerStatuses = signal<ProviderStatusEvent[]>([])` |
| **Subscriptions** | On init: subscribe to all 3 hubs via `SignalRService.on()`. Cleanup in `ngOnDestroy` |
| **Fallback** | If SignalR disconnected, show "Live updates paused" banner with reconnect button. Fall back to polling `/api/insurance/health/providers/extended` every 30s |
| **Unit tests** | 8 tests: render metrics cards, chart updates on signal, fraud feed auto-scroll, provider status pulse, disconnected banner, reconnect button, empty state, cleanup on destroy |
| **E2E tests** | 5 tests: page loads with metrics, provider cards visible, fraud feed renders, navigation from dashboard, accessibility scan |

#### 7.1.7 — Notification Bell (Full Stack)

| Layer | Detail |
|-------|--------|
| **C# Model** | `NotificationEvent { Id, Type, Title, Message, Severity, CreatedAt, IsRead }` |
| **TS Interface** | `Frontend/.../models/notification.ts`: `NotificationEvent`, `NotificationType` enum |
| **Service** | `Frontend/.../services/notification.service.ts` — manages notification store (signal array), unread count (computed), `markAsRead(id)`, `clearAll()`, persist to `localStorage` (max 50) |
| **Component** | Embedded in `nav.ts` — bell icon SVG, unread count badge (red circle), click toggles dropdown panel |
| **Dropdown** | Max 10 recent notifications, grouped by type (Claims/Fraud/Provider), timestamp relative ("2m ago"), click navigates to detail page (`/claims/:id`, `/dashboard/providers`, etc.) |
| **Integration** | `NotificationService` subscribes to all 3 SignalR hubs, creates `NotificationEvent` for each event |
| **Unit tests** | 6 tests: unread count, mark as read, clear all, localStorage persistence, max 50 cap, notification creation from hub events |
| **E2E tests** | 4 tests: bell visible in nav, unread badge count, dropdown opens/closes, notification click navigates |

#### 7.1.8 — Week 1 Backend Tests Summary

| Test File | Count | Coverage |
|-----------|-------|----------|
| `Tests/ClaimsHubTests.cs` | 4 | Broadcast, groups, status change, fraud alert |
| `Tests/ProviderHealthHubTests.cs` | 3 | Periodic broadcast, state diff, cooldown |
| `Tests/AnalyticsHubTests.cs` | 3 | Rolling window, thread safety, aggregation |
| `signalr.service.spec.ts` | 6 | Connect, reconnect, events, groups, state, errors |
| `live-dashboard.spec.ts` | 8 | Metrics, chart, feed, status, banner, empty, cleanup |
| `notification.service.spec.ts` | 6 | Count, read, clear, persist, cap, creation |
| **Week 1 Total** | **30** | |

**Week 1 Gate:** PASSED. SignalR hubs broadcast on claim/fraud/provider events. Live dashboard updates without page refresh. Notification bell shows real-time alerts. 30+ tests pass.

**Week 1 Bug Fixes (QA iterations):**
- `AnalyticsAggregator.cs`: RollingCounter `Snapshot()` method — snapshot-then-compute in `ExecuteAsync` prevents stale data
- `DocumentIntelligenceService.cs`: Added `_analyticsAggregator?.RecordDocQuery()` to track document queries
- `signalr.service.ts`: Deferred handler registration in `connect()`, Subject completion in `disconnect()`
- `live-dashboard.ts`: `Promise.allSettled` for graceful multi-hub disconnect, per-hub cleanup in `ngOnDestroy`
- `live-dashboard.html`: `aria-hidden` on provider status dots for accessibility
- `e2e/live-dashboard.spec.ts`: Rewritten with SignalR negotiate mocks, assertive test structure
- `Tests/AnalyticsHubTests.cs`: 2 new tests — RollingCounter Snapshot + ExpiredEntries

---

### Week 2: Cloud Deployment — VPS + Cloudflare Pages ($4-5/mo) (IN PROGRESS)

**Architecture Decision:** After researching free-forever options, we chose a VPS + free services hybrid over Azure App Service B1 ($13/mo). This gives us always-on hosting with no cold starts, self-hosted PostgreSQL+pgvector with no storage caps, and unlimited SSE connections — all for ~$4.51/mo total.

```
VPS (~$4.51/mo)                        Cloudflare Pages ($0)
├── Docker Compose                    └── Angular frontend (CDN-distributed)
│   ├── .NET Backend (port 8080)
│   ├── PostgreSQL + pgvector
│   └── Caddy (reverse proxy + auto HTTPS)
│
GitHub Actions ($0) → Build → Push to ghcr.io → SSH deploy to VPS
```

#### 7.2.1 — Cloudflare Pages (Frontend)

| Layer | Detail |
|-------|--------|
| **Platform** | Cloudflare Pages — unlimited bandwidth, 500 builds/mo, global CDN |
| **Build** | `ng build --configuration production` → `dist/sentiment-analyzer-ui/browser/` output |
| **Config** | `_redirects` file for SPA routing: `/* /index.html 200` |
| **Headers** | `_headers` file: CSP, X-Frame-Options, HSTS, X-Content-Type-Options |
| **Environment** | `environment.prod.ts`: `apiUrl` points to VPS backend URL (`https://api.yourdomain.com`), SSE endpoints on same origin |
| **GitHub Actions** | `deploy-frontend` job: checkout → setup Node 22 → `npm ci` → `ng build` → deploy via `cloudflare/wrangler-action@v3` |
| **Advantages over Azure SWA** | Unlimited bandwidth (vs 100GB/mo), faster global CDN, simpler config |

#### 7.2.2 — VPS Backend Deployment (Docker Compose)

| Layer | Detail |
|-------|--------|
| **VPS provider** | Hetzner CX22 (~€4.15/mo) or Netcup RS 1000 (~€4/mo). 2 vCPU, 4GB RAM, 40-80GB NVMe |
| **Docker Compose** | 3 services: (1) `backend` — .NET 10 from Sprint 6 Dockerfile, (2) `postgres` — PostgreSQL 16 + pgvector extension, (3) `caddy` — reverse proxy with automatic HTTPS (Let's Encrypt) |
| **Container registry** | GitHub Container Registry (ghcr.io) — free for public images |
| **Health check** | Caddy proxies `/health` and `/health/ready` (Sprint 6 liveness/readiness endpoints) |
| **Volumes** | `postgres-data` (persistent DB), `caddy-data` (TLS certs), `caddy-config` |
| **Startup** | `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://+:8080` |
| **No cold starts** | Always-on VPS — no sleep/wake cycles like serverless or free-tier PaaS |

#### 7.2.3 — Secrets Management (dotenv + Azure Key Vault Backup)

| Layer | Detail |
|-------|--------|
| **Primary** | `.env` file on VPS — loaded by Docker Compose `env_file: .env`. All 20+ API keys live here |
| **No NuGet** | Zero runtime dependency on Azure — app starts instantly with local env vars only |
| **Secrets stored** | 20+ API keys in `.env`: Groq, Cerebras, Mistral, Gemini, OpenRouter, OpenAI, Deepgram, Azure (Vision, DocIntel, ContentSafety, Language, Speech, Translator), Voyage, Cohere, HuggingFace, Jina, OcrSpace, Supabase, Resend, Sentry DSN |
| **Permissions** | `chmod 600 .env`, owned by deploy user only. Never committed to git (`.gitignore`) |
| **Backup** | Azure Key Vault (Free tier) as **disaster recovery only** — secrets copied there manually via Azure Portal/CLI. No runtime integration, no NuGet packages. If VPS dies, secrets are recoverable from Key Vault |
| **Local dev** | Unchanged — continues using `dotnet user-secrets` or local `.env` |
| **Rotation** | Update `.env` on VPS → `docker compose up -d` to reload. Mirror changes to Key Vault manually for backup |

#### 7.2.4 — GitHub Actions CD Pipeline

| Layer | Detail |
|-------|--------|
| **File** | `.github/workflows/cd.yml` (separate from existing `ci.yml`) |
| **Trigger** | Push to `main` branch only (not PRs) |
| **Jobs** | (1) `build-and-test` — run existing CI, (2) `deploy-frontend` — build Angular + deploy to Cloudflare Pages via `wrangler-action`, (3) `deploy-backend` — build Docker image + push to ghcr.io + SSH into VPS + `docker compose pull && docker compose up -d` |
| **Secrets** | `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`, `VPS_HOST`, `VPS_SSH_KEY`, `GHCR_TOKEN` |
| **Environments** | GitHub Environments: `production` with required reviewers (optional) |
| **Dependency** | `deploy-*` jobs depend on `build-and-test` passing |

#### 7.2.5 — CORS + Caddy Configuration

| Layer | Detail |
|-------|--------|
| **Caddyfile** | Reverse proxy `api.yourdomain.com` → `backend:8080`, auto HTTPS via Let's Encrypt, gzip compression |
| **Production** | `appsettings.Production.json`: `AllowedOrigins: ["https://yourdomain.pages.dev", "https://yourdomain.com"]` |
| **Development** | `appsettings.Development.json`: `AllowedOrigins: ["http://localhost:4200"]` |
| **Program.cs** | Named CORS policy `"InsuranceHub"` with `WithOrigins()`, `AllowCredentials()` (required for SSE), `WithHeaders("*")`, `WithMethods("GET","POST","PUT","DELETE")` |
| **SSE** | CORS must allow credentials for SSE streaming (CX Copilot, document processing) |

#### 7.2.6 — Monitoring (Grafana Cloud + Sentry)

| Layer | Detail |
|-------|--------|
| **Grafana Cloud** | Free tier: 50GB logs/mo, 10K metrics series, 50GB traces. Replaces Azure App Insights |
| **Sentry** | Free tier: 5K errors/mo, performance monitoring. `Sentry.AspNetCore` NuGet for .NET |
| **Custom events** | Sentry breadcrumbs: `LlmProviderUsed { provider, latencyMs }`, `ClaimTriaged { severity, elapsedMs }`, `FraudDetected { score, flagCount }`, `DocumentQueried { confidence, provider }` |
| **Dashboard** | Grafana Cloud dashboard: request rate, error rate, provider health, custom metrics |
| **NuGet swap** | Remove `Microsoft.ApplicationInsights.AspNetCore` → add `Sentry.AspNetCore` (conditional on `Sentry:Dsn` being set) |

#### 7.2.7 — SSL + Custom Domain

| Layer | Detail |
|-------|--------|
| **Backend** | Caddy auto-provisions Let's Encrypt TLS certificates — zero config HTTPS |
| **Frontend** | Cloudflare Pages provides free SSL on `*.pages.dev` + custom domains with Cloudflare DNS |
| **DNS** | CNAME: `app.yourdomain.com` → `<project>.pages.dev`, A record: `api.yourdomain.com` → VPS IP |
| **Cost** | $0 — Let's Encrypt + Cloudflare SSL are both free |

#### 7.2.8 — Post-Deploy Smoke Tests

| Layer | Detail |
|-------|--------|
| **Script** | `scripts/smoke-test.sh` — bash script run after CD deploy |
| **Checks** | (1) `GET /health` returns 200, (2) `GET /health/ready` returns 200 with `isReady: true`, (3) `GET /api/insurance/health/providers` returns provider list, (4) Frontend root URL returns 200 with `<app-root>`, (5) SSE endpoint returns 200 |
| **GitHub Actions** | Add smoke test step after deploy-backend, fail pipeline if any check fails |
| **Tests** | 2 tests: smoke test script validates endpoints, handles timeout gracefully |

#### 7.2.9 — PostgreSQL Migration (SQLite → PostgreSQL+pgvector)

| Layer | Detail |
|-------|--------|
| **Docker image** | `pgvector/pgvector:pg16` — PostgreSQL 16 with pgvector extension pre-installed |
| **Connection string** | `Host=postgres;Database=insurancehub;Username=app;Password=${POSTGRES_PASSWORD}` |
| **EF Core** | Add `Npgsql.EntityFrameworkCore.PostgreSQL` + `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` NuGet |
| **Migration** | Auto-migration block adapts to PostgreSQL syntax. Vector columns use `vector(1024)` type natively |
| **Dev mode** | SQLite remains for local development (`appsettings.Development.json`). PostgreSQL for production only |
| **Advantage** | Native pgvector replaces in-memory cosine similarity — faster RAG queries at scale |

#### Week 2 Tests Summary

| Test File | Count | Coverage |
|-----------|-------|----------|
| `Tests/EnvConfigTests.cs` | 2 | Load + fallback |
| `Tests/SmokeTestScriptTests.cs` | 2 | Validate + timeout |
| **Week 2 Total** | **4** | |

**Week 2 Progress:**
- [x] `docker-compose.yml` — 3 services (backend, postgres+pgvector, caddy), healthchecks, env_file
- [x] `Caddyfile` — Reverse proxy `api1.anshulghogre.co.in` → `backend:8080`, auto HTTPS, security headers, gzip
- [x] `.github/workflows/cd.yml` — CD pipeline: CI gate → parallel frontend/backend deploy → smoke tests
- [x] `.github/workflows/ci.yml` — Added `workflow_call` trigger for CD reuse
- [x] `Backend/Program.cs` — Sentry swap (`UseSentry` replaces App Insights), config-driven CORS from `AllowedOrigins` array
- [x] `Backend/SentimentAnalyzer.API.csproj` — Swapped `Microsoft.ApplicationInsights.AspNetCore` for `Sentry.AspNetCore` v5.6.0
- [x] `Backend/Dockerfile` — Added curl for healthcheck
- [x] `appsettings.Production.json` — `AllowedOrigins` array, `Database.Provider: PostgreSQL`
- [x] `environment.ts` — Production apiUrl: `https://api1.anshulghogre.co.in`
- [x] `environment.development.ts` — Tracked in git (public Supabase keys only)
- [x] `_redirects` + `_headers` — SPA routing + security headers for Cloudflare Pages
- [x] `.env.example` — Template with 25+ secret keys (DB, LLM, embedding, multimodal, Azure, Cloudflare AI, auth, monitoring)
- [x] `scripts/smoke-test.sh` — Post-deploy health checks (liveness, readiness, providers, frontend)
- [x] `Tests/EnvConfigTests.cs` + `Tests/SmokeTestScriptTests.cs` — 4 deployment tests
- [x] VPS provisioned (Hetzner CX22), Docker installed, deploy user created
- [x] PostgreSQL running on VPS (`docker compose up -d postgres`)
- [x] Cloudflare DNS active, `api1` A record (DNS only), `app1` CNAME (Cloudflare Pages)
- [x] Cloudflare Pages project created, custom domain `app1.anshulghogre.co.in` added
- [x] GitHub Secrets configured: `VPS_HOST`, `VPS_SSH_KEY`, `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`
- [x] `.env` file on VPS with API keys, `chmod 600`
- [x] Deployment files copied to VPS: `docker-compose.yml`, `Caddyfile`, `smoke-test.sh`
- [ ] First successful CD pipeline run (frontend + backend deploy)
- [ ] Caddy auto-HTTPS working
- [ ] Smoke tests green
- [ ] Sentry DSN configured (deferred — sign up later)
- [ ] Grafana Cloud setup (deferred)

**Week 2 Gate:** Frontend accessible via Cloudflare Pages URL. Backend API responds on VPS (Docker Compose). Secrets managed via env file. CD pipeline deploys on push to main. Sentry + Grafana collecting telemetry. Caddy auto-HTTPS working. PostgreSQL+pgvector running. Smoke tests green. **Total cost: ~$4.51/mo.**

---

### Week 3: Advanced Fraud Detection + Email Alerts (P2)

#### 7.3.1 — Anomaly Detection Service

| Layer | Detail |
|-------|--------|
| **Interface** | `Backend/Services/Fraud/IAnomalyDetectionService.cs` — `DetectAnomaliesAsync(ClaimRecord claim): Task<AnomalyResult>` |
| **Implementation** | `Backend/Services/Fraud/StatisticalAnomalyService.cs` |
| **Algorithm** | Z-score based: (1) Query 90-day rolling baseline per claim category from DB, (2) Calculate mean + stddev for claim amount, (3) Flag if Z > 2.0 (configurable threshold), (4) Also check filing frequency (claims per claimant in 30 days) |
| **Model** | `AnomalyResult { IsAnomaly, ZScore, Metric, BaselineMean, BaselineStdDev, Threshold, Explanation }` |
| **DB query** | `IClaimRepository.GetClaimsByCategory(category, dateRange)` — new repository method |
| **DI** | `services.AddScoped<IAnomalyDetectionService, StatisticalAnomalyService>()` |
| **Tests** | 6 tests: normal claim (no anomaly), high-amount anomaly (Z>2), frequency anomaly, empty baseline (first claim), configurable threshold, edge case (stddev=0) |

#### 7.3.2 — Fraud Pattern Engine

| Layer | Detail |
|-------|--------|
| **File** | `Backend/Services/Fraud/FraudPatternEngine.cs` |
| **Interface** | `IFraudPatternEngine` — `AnalyzePatternsAsync(ClaimRecord): Task<List<FraudPattern>>` |
| **Patterns** | 4 new detection strategies (extends Sprint 4's 4 correlation strategies): |
| | (1) **Velocity Check** — same claimant filing >3 claims in 30 days. Query: `GetClaimsByClaimant(name, last30Days)` |
| | (2) **Amount Escalation** — progressive claim value increases across consecutive claims. Detect: each claim >= 1.5x previous |
| | (3) **Geographic Clustering** — multiple claims from same postal code/region within 60 days. Query: `GetClaimsByLocation(postalCode, last60Days)` |
| | (4) **Holiday Proximity** — claims filed within 48hrs of US federal holidays or weekends. Hardcoded holiday list + `DayOfWeek` check |
| **Model** | `FraudPattern { PatternType, Severity, Description, RelatedClaimIds[], Confidence, DetectedAt }` |
| **Integration** | Called from `FraudAnalysisService` alongside existing correlation strategies |
| **Tests** | 8 tests: velocity detected, velocity normal, escalation detected, escalation flat, geographic cluster, geographic spread, holiday match, weekday normal |

#### 7.3.3 — Email Alert Service (Resend)

| Layer | Detail |
|-------|--------|
| **NuGet** | `Resend` (official .NET SDK) — lightweight REST-based email API |
| **Interface** | `Backend/Services/Notifications/IEmailAlertService.cs` — `SendAlertAsync(AlertEmail): Task<bool>` |
| **Implementation** | `Backend/Services/Notifications/ResendEmailService.cs` — uses `ResendClient` SDK |
| **Config** | `AgentSystem:Resend: { ApiKey, SenderAddress }` in `appsettings.json` |
| **Templates** | 3 HTML email templates (embedded resources): (1) `fraud-alert.html` — fraud score, flags, deep link to `/claims/:id`, (2) `critical-claim.html` — severity, persona, triage summary, (3) `provider-down.html` — provider name, cooldown duration, failover chain |
| **Model** | `AlertEmail { To[], Subject, TemplateName, TemplateData (Dictionary), Priority }` |
| **Free tier** | 3,000 emails/month (100/day). Log send count, skip if exhausted (graceful degradation) |
| **Why Resend over Azure Communication Email** | Better DX (simpler SDK), more generous free tier (3K/mo vs 100/day with complex setup), modern REST API, no Azure resource provisioning needed |
| **Tests** | 4 tests: send success, template rendering, daily limit skip, API key missing (graceful) |

#### 7.3.4 — Alert Rules Engine

| Layer | Detail |
|-------|--------|
| **File** | `Backend/Services/Notifications/AlertRulesEngine.cs` |
| **Interface** | `IAlertRulesEngine` — `EvaluateAsync(AlertContext): Task`, `GetRulesAsync(): Task<List<AlertRule>>`, `CreateRuleAsync(AlertRule)`, `UpdateRuleAsync(AlertRule)`, `DeleteRuleAsync(int id)` |
| **DB entity** | `Backend/Data/Entities/AlertRuleRecord.cs` — `Id, Name, RuleType (Severity/FraudScore/ProviderDown/AnomalyDetected), Threshold, EmailRecipients (JSON), IsEnabled, CreatedAt, UpdatedAt` |
| **Rule types** | (1) `SeverityThreshold` — trigger on claims with severity >= threshold (Critical, High), (2) `FraudScoreThreshold` — trigger when fraud score >= N (0-100), (3) `ProviderDown` — trigger when provider enters cooldown, (4) `AnomalyDetected` — trigger when anomaly Z-score exceeds threshold |
| **Evaluation** | Called from service facades after operations. Matches context against enabled rules → calls `IEmailAlertService` for matches |
| **API endpoints** | `Backend/Endpoints/AlertEndpoints.cs`: `GET /api/insurance/alerts/rules`, `POST /api/insurance/alerts/rules`, `PUT /api/insurance/alerts/rules/{id}`, `DELETE /api/insurance/alerts/rules/{id}`, `POST /api/insurance/alerts/test` (send test email) |
| **Tests** | 5 tests: severity rule match, fraud score rule match, disabled rule skip, CRUD operations, test email endpoint |

#### 7.3.5 — Alert Management UI (Full Stack)

| Layer | Detail |
|-------|--------|
| **C# Model** | `AlertRuleResponse { Id, Name, RuleType, Threshold, EmailRecipients[], IsEnabled, CreatedAt }` |
| **TS Interface** | `Frontend/.../models/alert.ts`: `AlertRule`, `AlertRuleType` enum, `CreateAlertRuleRequest` |
| **Service** | `Frontend/.../services/alert.service.ts` — `getRules()`, `createRule()`, `updateRule()`, `deleteRule()`, `sendTestAlert()` |
| **Component** | `Frontend/.../components/alert-management/alert-management.ts` |
| **Route** | `{ path: 'alerts/manage', component: AlertManagementComponent, canActivate: [authGuard], data: { breadcrumb: 'Alert Rules' } }` |
| **UI** | (1) Rules table — name, type badge, threshold, recipients, enabled toggle, edit/delete buttons, (2) Create/edit modal — form with rule type dropdown, threshold input, email chips input, (3) Test alert button — sends test email, shows success/failure toast, (4) Empty state — "No alert rules configured" with create CTA |
| **Unit tests** | 8 tests: rules list renders, create rule form, edit rule, delete rule confirmation, toggle enabled, test alert button, empty state, form validation |
| **E2E tests** | 6 tests: page loads with rules, create rule flow, edit rule, delete rule, toggle enabled, test alert button |

#### 7.3.6 — Fraud Analytics Dashboard (Full Stack)

| Layer | Detail |
|-------|--------|
| **C# Model** | `FraudAnalyticsResponse { DetectionRateOverTime[], PatternDistribution[], TopPatterns[], AnomalyHeatmap[], TotalClaimsAnalyzed, TotalFraudDetected, AvgFraudScore }` |
| **Endpoint** | `GET /api/insurance/fraud/analytics?days=30` in `FraudEndpoints.cs` |
| **TS Interface** | `Frontend/.../models/fraud-analytics.ts` |
| **Service** | Add `getAnalytics(days)` to existing `fraud-correlation.service.ts` |
| **Component** | `Frontend/.../components/fraud-analytics/fraud-analytics.ts` |
| **Route** | `{ path: 'fraud/analytics', component: FraudAnalyticsComponent, canActivate: [authGuard], data: { breadcrumb: 'Fraud Analytics' } }` |
| **Charts** | (1) **Detection rate** — Chart.js line chart (fraud detections/day over 30 days), (2) **Pattern distribution** — doughnut chart (velocity/escalation/geographic/holiday/correlation), (3) **Top patterns** — table with pattern type, count, avg confidence, example claim IDs, (4) **Anomaly heatmap** — bar chart grouped by claim category (Policy/Claim/Endorsement/Correspondence) with anomaly count |
| **Date range** | Dropdown: 7 days / 30 days / 90 days — re-fetches on change |
| **Unit tests** | 8 tests: charts render, date range change, empty state, metric cards, pattern table, loading state, error state, responsive layout |
| **E2E tests** | 5 tests: page loads with charts, date range filter, pattern table visible, navigation from fraud alerts, accessibility scan |

#### 7.3.7 — Week 3 Backend Tests Summary

| Test File | Count | Coverage |
|-----------|-------|----------|
| `Tests/AnomalyDetectionTests.cs` | 6 | Z-score, frequency, baseline, threshold, edge cases |
| `Tests/FraudPatternEngineTests.cs` | 8 | All 4 patterns × detected + normal |
| `Tests/EmailAlertServiceTests.cs` | 4 | Send, template, limit, missing config |
| `Tests/AlertRulesEngineTests.cs` | 5 | Match, skip, CRUD, test email |
| `alert.service.spec.ts` | 4 | getRules, create, update, delete |
| `alert-management.spec.ts` | 8 | List, create, edit, delete, toggle, test, empty, validation |
| `fraud-analytics.spec.ts` | 8 | Charts, date range, empty, metrics, table, loading, error, responsive |
| **Week 3 Total** | **43** | |

#### 7.3.8 — Week 3 E2E Tests Summary

| Spec File | Count | Scenarios |
|-----------|-------|-----------|
| `e2e/alert-management.spec.ts` | 6 | Page load, create, edit, delete, toggle, test alert |
| `e2e/fraud-analytics.spec.ts` | 5 | Page load, date filter, pattern table, navigation, a11y |
| **Week 3 E2E Total** | **11** | |

**Week 3 Gate:** Anomaly detection flags statistical outliers. 4 fraud patterns detect velocity/escalation/geographic/holiday. Email alerts sent for critical events. Alert rules configurable via CRUD UI. 54 new tests pass (43 unit + 11 E2E).

---

### Week 4: Polish + Documentation + Performance (P3)

#### 7.4.1 — Performance Profiling + Optimization

| Layer | Detail |
|-------|--------|
| **Hot paths** | (1) RAG query — profile embedding generation + vector search + LLM answer, (2) Batch claims — profile CSV parse + sequential triage, (3) SignalR broadcast — measure message rate under load |
| **Caching** | `IMemoryCache` for document list (5-min TTL), embedding lookup memoization (by content hash), provider health snapshot (30s TTL) |
| **Optimizations** | Response compression middleware for JSON payloads >1KB, `AsNoTracking()` on read-only DB queries, connection pooling for SQLite |
| **Benchmarks** | Log baseline metrics → optimize → log improved metrics. Target: RAG query <3s, batch claim <500ms/item |

#### 7.4.2 — Rate Limit Dashboard (Full Stack)

| Layer | Detail |
|-------|--------|
| **C# Model** | `RateLimitStatus { ProviderName, ServiceType, CurrentUsage, Limit, Unit, UsagePercent, ResetAt }` |
| **Endpoint** | `GET /api/insurance/health/rate-limits` in `ProviderHealthEndpoints.cs` |
| **TS Interface** | `Frontend/.../models/rate-limit.ts` |
| **Component** | `Frontend/.../components/rate-limits/rate-limits.ts` |
| **Route** | `{ path: 'dashboard/rate-limits', component: RateLimitsComponent, canActivate: [authGuard], data: { breadcrumb: 'Rate Limits' } }` |
| **UI** | Grouped by service type (LLM/Embedding/OCR/NER/STT/Safety/Translation). Each row: provider name, progress bar (green <50%, yellow 50-80%, red >80%), usage text ("142/250 req"), reset time |
| **Auto-refresh** | Poll every 60s or via SignalR `ProviderHealthHub` if connected |
| **Unit tests** | 6 tests: render progress bars, color coding, grouping, auto-refresh, empty state, responsive |
| **E2E tests** | 3 tests: page loads, progress bars visible, navigation from provider health |

#### 7.4.3 — API Documentation (Swagger/OpenAPI)

| Layer | Detail |
|-------|--------|
| **NuGet** | `Swashbuckle.AspNetCore` 7.2.0 |
| **Config** | `builder.Services.AddEndpointsApiExplorer()` + `builder.Services.AddSwaggerGen()` — XML comment integration via `IncludeXmlComments()` |
| **Availability** | Dev only: `if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }` |
| **URL** | `http://localhost:5143/swagger` |
| **Grouping** | Tag groups: Sentiment, Insurance, Claims, Fraud, Documents, CX Copilot, Provider Health, Alerts |
| **Tests** | 1 test: Swagger endpoint returns valid OpenAPI JSON |

#### 7.4.4 — User Preferences Component (Full Stack)

| Layer | Detail |
|-------|--------|
| **Component** | `Frontend/.../components/user-preferences/user-preferences.ts` |
| **Route** | `{ path: 'preferences', component: UserPreferencesComponent, canActivate: [authGuard], data: { breadcrumb: 'Preferences' } }` |
| **Sections** | (1) **Theme** — light/dark/semi-dark radio group (integrates with existing `ThemeService`), (2) **Notifications** — toggle email alerts, toggle browser notifications, severity filter checkboxes, (3) **Dashboard** — default date range (7/30/90 days), auto-refresh interval, (4) **Provider override** — optional preferred LLM provider dropdown (skip chain, go direct) |
| **Persistence** | `localStorage` key `user-preferences`. Load on app init, apply theme + settings |
| **Unit tests** | 6 tests: theme switch, notification toggles, save to localStorage, load from localStorage, provider override, default values |
| **E2E tests** | 3 tests: page loads, theme switch persists, navigation from nav menu |

#### 7.4.5 — Accessibility Audit v2

| Layer | Detail |
|-------|--------|
| **Scope** | All ~27 routes (22 existing + 5 new) |
| **Focus areas** | (1) ARIA live regions for SignalR real-time updates (`aria-live="polite"` on notification bell, live dashboard metrics), (2) Screen reader announcements for toast notifications, (3) Keyboard navigation through notification dropdown, (4) Focus management on modal open/close in alert management |
| **Tools** | axe-core via Playwright `@axe-core/playwright` in E2E specs |
| **Tests** | Add `axe` scan to each new E2E spec (already counted in per-component E2E tests above) |

#### 7.4.6 — Documentation Update

| File | Updates |
|------|---------|
| `CLAUDE.md` | Add SignalR hubs, alert endpoints, new routes, fraud patterns, deployment info |
| `docs/architecture.md` | SignalR architecture, VPS deployment diagram, anomaly detection flow |
| `docs/api-reference.md` | 5 new endpoints (alert CRUD + test, fraud analytics, rate limits), SignalR hub contracts |
| `docs/testing.md` | Updated test counts, new test categories (hub tests, anomaly tests) |
| `docs/security.md` | Secrets management (dotenv/Infisical), CORS policy, Caddy HTTPS |
| `SPRINT-ROADMAP.md` | Sprint 7 status → COMPLETE, stats table, Sprint 8 planning |

#### 7.4.7 — E2E Full Regression

| Layer | Detail |
|-------|--------|
| **Scope** | Run all E2E specs across all routes |
| **Fix** | Fix any broken tests from new features (SignalR mock setup, new nav links) |
| **Mock updates** | Add SignalR mock (stub `HubConnectionBuilder`), alert rules mock data, fraud analytics mock data to `e2e/fixtures/mock-data.ts` |
| **Target** | 0 failures across ~700+ E2E tests |

#### 7.4.8 — Sprint 7 Retrospective

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Angular components | 22 | 28 | +6 |
| Routes | 17 | 22 | +5 |
| Backend tests | 704 | ~750 | +~46 |
| Frontend unit tests | 462 | ~506 | +~44 |
| E2E tests | ~666 | ~698 | +~32 |
| **Grand Total** | **~1,832** | **~1,954** | **+~122** |

#### Week 4 Tests Summary

| Test File | Count | Coverage |
|-----------|-------|----------|
| `rate-limits.spec.ts` | 6 | Progress bars, colors, grouping, refresh, empty, responsive |
| `user-preferences.spec.ts` | 6 | Theme, notifications, save, load, provider, defaults |
| `Tests/SwaggerTests.cs` | 1 | OpenAPI JSON valid |
| `e2e/rate-limits.spec.ts` | 3 | Page load, bars, navigation |
| `e2e/user-preferences.spec.ts` | 3 | Page load, theme persist, navigation |
| **Week 4 Total** | **19** | |

**Week 4 Gate:** All routes accessible. Swagger docs live at `/swagger`. axe-core clean on all routes. Full E2E regression green. Documentation current. Performance profiled + optimized.

---

### New Files (Sprint 7) — Complete List

| # | File | Week | Type |
|---|------|------|------|
| 1 | `Backend/Hubs/ClaimsHub.cs` | 1 | SignalR Hub |
| 2 | `Backend/Hubs/ProviderHealthHub.cs` | 1 | SignalR Hub |
| 3 | `Backend/Hubs/AnalyticsHub.cs` | 1 | SignalR Hub |
| 4 | `Backend/Services/Notifications/ProviderHealthBroadcaster.cs` | 1 | BackgroundService |
| 5 | `Backend/Services/Notifications/AnalyticsAggregator.cs` | 1 | BackgroundService |
| 6 | `Frontend/.../services/signalr.service.ts` | 1 | Angular Service |
| 7 | `Frontend/.../services/signalr.service.spec.ts` | 1 | Test |
| 8 | `Frontend/.../services/notification.service.ts` | 1 | Angular Service |
| 9 | `Frontend/.../services/notification.service.spec.ts` | 1 | Test |
| 10 | `Frontend/.../models/analytics.ts` | 1 | TS Interface |
| 11 | `Frontend/.../models/notification.ts` | 1 | TS Interface |
| 12 | `Frontend/.../components/live-dashboard/live-dashboard.ts` | 1 | Component |
| 13 | `Frontend/.../components/live-dashboard/live-dashboard.spec.ts` | 1 | Test |
| 14 | `Tests/ClaimsHubTests.cs` | 1 | xUnit Test |
| 15 | `Tests/ProviderHealthHubTests.cs` | 1 | xUnit Test |
| 16 | `Tests/AnalyticsHubTests.cs` | 1 | xUnit Test |
| 17 | `docker-compose.prod.yml` | 2 | Production Docker Compose (backend + postgres + caddy) |
| 18 | `Caddyfile` | 2 | Reverse proxy config (auto HTTPS) |
| 19 | `.github/workflows/cd.yml` | 2 | CD Pipeline |
| 20 | `scripts/smoke-test.sh` | 2 | Deploy Script |
| 21 | `Frontend/.../environment.prod.ts` | 2 | Config |
| 22 | `Frontend/_redirects` | 2 | Cloudflare Pages SPA routing |
| 23 | `Frontend/_headers` | 2 | Cloudflare Pages security headers |
| 24 | `Tests/EnvConfigTests.cs` | 2 | xUnit Test |
| 25 | `Backend/Services/Fraud/IAnomalyDetectionService.cs` | 3 | Interface |
| 26 | `Backend/Services/Fraud/StatisticalAnomalyService.cs` | 3 | Service |
| 27 | `Backend/Services/Fraud/FraudPatternEngine.cs` | 3 | Service |
| 28 | `Backend/Services/Notifications/IEmailAlertService.cs` | 3 | Interface |
| 29 | `Backend/Services/Notifications/ResendEmailService.cs` | 3 | Service |
| 30 | `Backend/Services/Notifications/AlertRulesEngine.cs` | 3 | Service |
| 31 | `Backend/Endpoints/AlertEndpoints.cs` | 3 | API Endpoints |
| 32 | `Backend/Data/Entities/AlertRuleRecord.cs` | 3 | DB Entity |
| 33 | `Frontend/.../models/alert.ts` | 3 | TS Interface |
| 34 | `Frontend/.../models/fraud-analytics.ts` | 3 | TS Interface |
| 35 | `Frontend/.../models/rate-limit.ts` | 4 | TS Interface |
| 36 | `Frontend/.../services/alert.service.ts` | 3 | Angular Service |
| 37 | `Frontend/.../services/alert.service.spec.ts` | 3 | Test |
| 38 | `Frontend/.../components/alert-management/alert-management.ts` | 3 | Component |
| 39 | `Frontend/.../components/alert-management/alert-management.spec.ts` | 3 | Test |
| 40 | `Frontend/.../components/fraud-analytics/fraud-analytics.ts` | 3 | Component |
| 41 | `Frontend/.../components/fraud-analytics/fraud-analytics.spec.ts` | 3 | Test |
| 42 | `Frontend/.../components/rate-limits/rate-limits.ts` | 4 | Component |
| 43 | `Frontend/.../components/rate-limits/rate-limits.spec.ts` | 4 | Test |
| 44 | `Frontend/.../components/user-preferences/user-preferences.ts` | 4 | Component |
| 45 | `Frontend/.../components/user-preferences/user-preferences.spec.ts` | 4 | Test |
| 46 | `Tests/AnomalyDetectionTests.cs` | 3 | xUnit Test |
| 47 | `Tests/FraudPatternEngineTests.cs` | 3 | xUnit Test |
| 48 | `Tests/EmailAlertServiceTests.cs` | 3 | xUnit Test |
| 49 | `Tests/AlertRulesEngineTests.cs` | 3 | xUnit Test |
| 50 | `e2e/alert-management.spec.ts` | 3 | E2E Test |
| 51 | `e2e/fraud-analytics.spec.ts` | 3 | E2E Test |
| 52 | `e2e/rate-limits.spec.ts` | 4 | E2E Test |
| 53 | `e2e/user-preferences.spec.ts` | 4 | E2E Test |
| 54 | `e2e/live-dashboard.spec.ts` | 1 | E2E Test |
| 55 | `e2e/notification-bell.spec.ts` | 1 | E2E Test |

### Modified Files (Sprint 7)

| # | File | Change |
|---|------|--------|
| 1 | `Backend/SentimentAnalyzer.API.csproj` | +4 NuGet: `Resend`, `Sentry.AspNetCore`, `Swashbuckle.AspNetCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` |
| 2 | `Backend/Program.cs` | SignalR registration, hub mappings, Sentry config, Swagger, CORS update, response compression, memory cache, PostgreSQL conditional |
| 3 | `Backend/appsettings.json` | `SignalR`, `Resend`, `Sentry`, `Swagger` config sections |
| 4 | `Backend/appsettings.Production.json` | `AllowedOrigins`, Sentry DSN, PostgreSQL connection string |
| 5 | `Backend/Services/ClaimsProcessing/ClaimsOrchestrationService.cs` | Inject `IHubContext<ClaimsHub>`, broadcast after triage |
| 6 | `Backend/Services/Fraud/FraudAnalysisService.cs` | Inject `IHubContext<ClaimsHub>`, broadcast fraud alerts. Call `FraudPatternEngine` + `AnomalyDetectionService` |
| 7 | `Backend/Endpoints/FraudEndpoints.cs` | Add `GET /api/insurance/fraud/analytics` |
| 8 | `Backend/Endpoints/ProviderHealthEndpoints.cs` | Add `GET /api/insurance/health/rate-limits` |
| 9 | `Backend/Data/InsuranceDbContext.cs` | Add `DbSet<AlertRuleRecord>`, migration block |
| 10 | `Frontend/.../app.routes.ts` | +5 routes: live dashboard, alert management, fraud analytics, rate limits, preferences |
| 11 | `Frontend/.../components/nav/nav.ts` | Notification bell integration, new nav links (Live, Alerts, Analytics, Rate Limits, Preferences) |
| 12 | `Frontend/.../services/fraud-correlation.service.ts` | Add `getAnalytics(days)` method |
| 13 | `Frontend/package.json` | +1 npm: `@microsoft/signalr` |
| 14 | `docker-compose.yml` | Updated for production: add postgres + caddy services |
| 15 | `e2e/fixtures/mock-data.ts` | Alert rules, fraud analytics, rate limits, SignalR stubs mock data |
| 16 | `e2e/helpers/api-mocks.ts` | Mock routes for alert, fraud analytics, rate limit endpoints |
| 17 | `CLAUDE.md` | SignalR, alerts, deployment, new routes |
| 18 | `docs/architecture.md` | SignalR diagram, VPS deployment, anomaly detection |
| 19 | `docs/api-reference.md` | 5 new endpoints + hub contracts |
| 20 | `docs/testing.md` | Updated counts |
| 21 | `docs/security.md` | Secrets management, CORS, Caddy HTTPS |

### Sprint 7 Stats (Projected)

| Metric | Sprint 6 Final | Sprint 7 Projected | Delta |
|--------|---------------|-------------------|-------|
| Angular components | 22 | **28** | +6 (live-dashboard, notification-bell*, alert-management, fraud-analytics, rate-limits, user-preferences) |
| Routes | 17 | **22** | +5 (/dashboard/live, /alerts/manage, /fraud/analytics, /dashboard/rate-limits, /preferences) |
| Backend tests (xUnit) | 704 | **~750** | +~46 (hubs + anomaly + fraud patterns + email + alert rules + Key Vault + Swagger) |
| Frontend unit tests | 462 | **~506** | +~44 (SignalR service + notification service + 4 new components) |
| E2E tests | ~666 | **~698** | +~32 (live dashboard + notifications + alerts + analytics + rate limits + preferences) |
| **Grand Total** | **~1,832** | **~1,954** | **+~122** |
| SignalR hubs | 0 | **3** | Claims, ProviderHealth, Analytics |
| Background services | 0 | **2** | ProviderHealthBroadcaster, AnalyticsAggregator |
| Cloud deployment | None | **VPS (Hetzner/Netcup) + Cloudflare Pages** | CD pipeline on main push, ~$4.51/mo total |
| Fraud detection strategies | 4 (correlation) | **8** | +velocity, amount escalation, geographic, holiday proximity |
| Email alerts | None | **Resend** | 3K emails/mo free |
| API documentation | None | **Swagger/OpenAPI** | Dev-only at /swagger |
| New NuGet packages | 0 | **4** | Resend, Sentry.AspNetCore, Swashbuckle, Npgsql.EFCore.PostgreSQL |
| New npm packages | 0 | **1** | @microsoft/signalr |

*\*notification-bell is embedded in nav.ts, not a separate routed component*

### Sprint 7 Implementation Order (End-to-End per CLAUDE.md Rule #1)

| Order | Item | Depends On | Week |
|-------|------|-----------|------|
| 1 | SignalR infrastructure (Program.cs + npm) | — | 1 |
| 2 | Claims Hub (C# → TS interface → integrate) | 1 | 1 |
| 3 | Provider Health Hub + Broadcaster | 1 | 1 |
| 4 | Analytics Hub + Aggregator | 1 | 1 |
| 5 | Angular SignalR Service | 1 | 1 |
| 6 | Live Dashboard (model → endpoint → TS → component → tests → E2E) | 2,3,4,5 | 1 |
| 7 | Notification Service + Bell | 2,3,4,5 | 1 |
| 8 | Cloudflare Pages + VPS Docker Compose deploy | — | 2 |
| 9 | Secrets (.env) + PostgreSQL migration | 8 | 2 |
| 10 | CD pipeline (GitHub Actions → ghcr.io → SSH deploy) | 8,9 | 2 |
| 11 | CORS + Caddy + Sentry/Grafana wire-up | 8 | 2 |
| 12 | Smoke tests | 8,10 | 2 |
| 13 | Anomaly Detection (C# service → tests) | — | 3 |
| 14 | Fraud Pattern Engine (C# service → tests) | — | 3 |
| 15 | Email Alert Service (C# → config → tests) | — | 3 |
| 16 | Alert Rules Engine (C# → DB entity → endpoints → tests) | 15 | 3 |
| 17 | Alert Management UI (TS model → service → component → E2E) | 16 | 3 |
| 18 | Fraud Analytics (endpoint → TS → component → E2E) | 13,14 | 3 |
| 19 | Rate Limit Dashboard (endpoint → TS → component → E2E) | — | 4 |
| 20 | User Preferences (component → localStorage → E2E) | — | 4 |
| 21 | Swagger/OpenAPI | — | 4 |
| 22 | Performance profiling + caching | 6,18 | 4 |
| 23 | Accessibility audit v2 | 6,7,17,18,19,20 | 4 |
| 24 | Documentation + E2E regression | All | 4 |

### Sprint 7 Risk Register

| Risk | Severity | Mitigation |
|------|----------|-----------|
| VPS provider downtime | Low | Choose reputable provider (Hetzner 99.9% SLA). Docker Compose restart policies. Health check alerts via Sentry |
| VPS disk space exhaustion | Low | PostgreSQL WAL archiving + log rotation. Monitor via Grafana Cloud. 40-80GB NVMe is generous for this workload |
| Resend email deliverability | Low | Verify sender domain (SPF/DKIM), use templates, test with internal addresses first |
| SignalR self-hosted connection limits | Low | Self-hosted SignalR has no artificial limits — bounded only by VPS RAM (4GB handles 1000s of connections) |
| D3.js bundle size for fraud network graph | Low | Use Chart.js scatter plot as simpler alternative, lazy-load if needed |
| Caddy TLS cert renewal failure | Low | Caddy auto-renews Let's Encrypt certs 30 days before expiry. Sentry alert on renewal failure |
| SignalR WebSocket blocked by corporate firewall | Medium | SignalR auto-negotiates transport: WebSocket → SSE → Long Polling. No action needed |
| `@microsoft/signalr` version compatibility with .NET 10 | Low | Use matching major version. Test negotiate endpoint during Week 1 |
| PostgreSQL migration data loss | Medium | Keep SQLite for local dev. Production migration is fresh (no existing prod data). Test migration script in staging |

---

## Sprint 6 Week 4: Complete

All Week 4 items are done. See individual item rows in Week 4 table above for details.

---

### Deferred to Sprint 8+
- Azure AI Search (replace in-memory vector search) — or self-hosted Meilisearch on VPS
- Custom domain + SSL certificate (Caddy auto-HTTPS ready, just add domain)
- Multi-tenant support
- Webhook integrations for external claims systems
- Auth provider migration (Clerk/Auth0/Firebase if Supabase free tier exceeded)

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
| Supabase Auth | 50K MAU (auth only, no DB) | **Sprint 7** — kept from existing setup |
| Resend | 3K emails/mo (100/day) | **Sprint 7** |
| Cloudflare Pages | Unlimited bandwidth, 500 builds/mo | **Sprint 7** |
| VPS (Hetzner CX22 + IPv4) | **$4.09/mo** (2 vCPU, 4GB RAM, 40GB NVMe) | **Sprint 7** — only paid service |
| Domain (.co.in) | **$5/yr** (~$0.42/mo) | **Sprint 7** — via Porkbun/Namecheap + Cloudflare DNS |
| Sentry | 5K errors/mo, performance | **Sprint 7** |
| Grafana Cloud | 50GB logs, 10K metrics/mo | **Sprint 7** |
| Infisical | 5 users, E2E encrypted | **Sprint 7** (optional) |
| ghcr.io | Free for public images | **Sprint 7** |

---

*Last updated: 2026-03-08 | Sprint 6 COMPLETE. Sprint 7 PLANNED with VPS+Cloudflare deployment (~$4.51/mo), Resend email, Sentry+Grafana monitoring, PostgreSQL+pgvector. 704 backend + 462 frontend + ~666 E2E = ~1,832 total tests passing.*
