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
| 5.H7 | Retrieval confidence shows 3% on relevant results | Cohere asymmetric embeddings (`search_document` vs `search_query`) produce naturally lower raw cosine scores than symmetric models | Deferred to Sprint 6: per-provider confidence normalization |
| 5.H8 | 13-page PDF shows only 2 pages after upload | **OCR chain gap**: PdfPig fails (scanned/image PDF, < 100 chars), Azure DocIntel F0 succeeds but caps at 2 pages/document, `ResilientOcrProvider` accepted 2-page partial as full success | Added partial extraction detection: compare Azure page count vs PdfPig's detected page count, fall through to next provider when `azurePages < expectedPages`. Updated `MaxPagesPerDocument` from 20 to 50. Enhanced PdfPig logging with first-200-char preview. |

---

## Sprint 6: Azure AI Services + Docker + Production Hardening (PLANNED)

**Goal:** Execute the planned Sprint 4.5 Azure AI integrations (4 services, 2 resilient chains), containerize the backend, and add production readiness features (health checks, App Insights, production config).

**Brainstorming:** 9-agent brainstorming across 3 iterations (unanimous 9/9 APPROVE). All agents agreed Sprint 4.5 items are shovel-ready and Content Safety is a compliance gap for customer-facing CX Copilot.

**Why now:** Content Safety is mandatory for any customer-facing AI (CX Copilot). NER and STT are single-point-of-failure services with no fallback. Docker is prerequisite for cloud deployment.

### Week 1: Content Safety + Language NER (P1)

| # | Item | Deliverable |
|---|------|-------------|
| 6.1.1 | NuGet Packages | `Azure.AI.ContentSafety` 1.0.0, verify `Azure.AI.TextAnalytics` already present |
| 6.1.2 | Settings Classes | `AzureContentSafetySettings`, `AzureLanguageSettings` in `LlmProviderConfiguration.cs` |
| 6.1.3 | Content Safety Service | `IContentSafetyService` + `AzureContentSafetyService` — text/image moderation, prompt shields, groundedness detection |
| 6.1.4 | CX Copilot Integration | Optional `IContentSafetyService?` in `CustomerExperienceService` — screen LLM responses before output |
| 6.1.5 | Azure Language NER | `AzureLanguageNerService` using `TextAnalyticsClient` SDK, maps Azure entity categories → our format (Person→PERSON, Organization→ORGANIZATION) + insurance regex patterns |
| 6.1.6 | Resilient NER Chain | `ResilientEntityExtractionProvider` — HuggingFace (300 req/hr) → Azure Language (5K/mo), exponential backoff (30s→300s cap) |
| 6.1.7 | Backend Tests | ~14 tests: Content Safety (6) + Language NER (4) + Resilient NER (4) |
| 6.1.8 | Config | `appsettings.json` sections for Content Safety + Language under `AgentSystem` |

**Gate:** Content Safety screens CX Copilot output. NER resolves to `ResilientEntityExtractionProvider`. 14 tests pass.

### Week 2: Speech STT + Translator + Docker (P2)

| # | Item | Deliverable |
|---|------|-------------|
| 6.2.1 | NuGet Package | `Microsoft.CognitiveServices.Speech` 1.42.0 (or REST API fallback if SDK doesn't support .NET 10) |
| 6.2.2 | Settings Classes | `AzureSpeechSettings`, `AzureTranslatorSettings` in `LlmProviderConfiguration.cs` |
| 6.2.3 | Azure Speech STT | `AzureSpeechToTextService` — transcription with PII redaction on output |
| 6.2.4 | Resilient STT Chain | `ResilientSpeechToTextProvider` — Deepgram ($200 credit) → Azure Speech (5 hrs/mo), exponential backoff |
| 6.2.5 | Translation Service | `ITranslationService` + `AzureTranslatorService` — REST API (HttpClient), PII redaction before send, 130+ languages |
| 6.2.6 | Backend Tests | ~17 tests: Speech STT (5) + Resilient STT (4) + Translator (8) |
| 6.2.7 | Dockerfile | Multi-stage .NET 10 build (mcr.microsoft.com/dotnet/sdk:10.0 → aspnet:10.0), expose 8080 |
| 6.2.8 | docker-compose.yml | Backend service + environment variables for local testing |
| 6.2.9 | .dockerignore | Exclude bin/, obj/, node_modules/, test-results/, .git/ |

**Gate:** STT resolves to `ResilientSpeechToTextProvider`. Translation works for Spanish/French claims. Docker builds and runs locally. 17 tests pass.

### Week 3: RAG Query Agent Team + EF Migrations + Production Hardening

| # | Item | Deliverable |
|---|------|-------------|
| 6.3.1 | RAG Query Agent Team | Multi-agent RAG query pipeline: **Provider Router** (matches query embedding to document index provider), **Query Reformulator** (rewrites vague questions for better retrieval), **Answer Evaluator** (checks citation quality + confidence), **Cross-Doc Reasoner** (synthesizes across multiple documents) |
| 6.3.2 | EF Core Migrations | Replace `EnsureCreated()` + manual `ALTER TABLE` block with proper `dotnet ef migrations`. Initial migration from current schema, auto-apply on startup. Eliminates missing-column bugs permanently. |
| 6.3.3 | Health Endpoints | `GET /health` (liveness), `GET /health/ready` (readiness — checks DB + at least 1 LLM provider + multimodal services) |
| 6.3.4 | Production Config | `appsettings.Production.json` — cloud provider chain (no Ollama), Azure connection strings, daily cap settings |
| 6.3.5 | App Insights Code | `builder.Services.AddApplicationInsightsTelemetry()` + `Microsoft.ApplicationInsights.AspNetCore` NuGet |
| 6.3.6 | Startup Validation | Log all Azure AI service status at startup: `Azure AI: Vision ✓, DocIntel ✓, Language ✓, ContentSafety ✓, Translator ✓, Speech ✓` |
| 6.3.7 | Frontend Badges | Content safety indicator on CX Copilot responses ("Screened ✓"), language auto-detect badge |
| 6.3.8 | Accessibility Audit | Fresh axe-core sweep on all 16+ routes, fix any new contrast/ARIA issues from Sprint 5 |
| 6.3.9 | E2E Tests | New specs for content safety flows, translation UI, health endpoint checks |
| 6.3.10 | Documentation | Update all MD files (CLAUDE.md, architecture.md, api-reference.md, testing.md, security.md, pitfalls.md) |

**Gate:** Docker container builds + runs. RAG agent team passes multi-provider query tests. Health endpoints return 200. App Insights configured. Axe-core clean. All tests pass.

### Week 4: Bug Regression Tests + UX Polish

| # | Item | Deliverable |
|---|------|-------------|
| 6.4.1 | SSE Streaming Tests | xUnit tests for `StreamUploadDocumentAsync`: verify `FlushAsync` on all paths, no sync I/O, `[DONE]` marker delivery |
| 6.4.2 | Migration Tests | Integration tests for SQLite auto-migration: verify all `ALTER TABLE` and `CREATE TABLE` operations are idempotent |
| 6.4.3 | Embedding Provider Consistency Tests | Tests for cross-provider query mismatch detection, `ResolveQueryEmbeddingServiceAsync` provider matching, dimension validation |
| 6.4.4 | Content Safety Parallelism Tests | Tests for `Parallel.ForEachAsync` safety screening: thread safety of `Interlocked` counters, cancellation token propagation |
| 6.4.5 | Document Upload Sub-Loaders | UX enhancement: per-phase sub-progress indicators for heavy steps (OCR, Embedding, Safety), animated step transitions, estimated time remaining |
| 6.4.6 | Confidence Score Normalization | Per-provider score normalization for RAG retrieval confidence: Cohere asymmetric (0.02-0.08 raw → 0-100% display), VoyageAI asymmetric (0.15-0.40 raw → 0-100%), symmetric models (0.5-1.0 raw → 0-100%). Provider-specific min/max calibration stored in config. |
| 6.4.7 | Provider Health UI Revamp | Update `provider-health` component to display ALL providers: **LLM** (Groq, Cerebras, Mistral, Gemini, OpenRouter, OpenAI, Ollama), **Embeddings** (VoyageAI, Cohere, Gemini, HuggingFace, Jina, Ollama), **OCR** (PdfPig, Azure DocIntel, OCR Space, Gemini Vision), **NER** (HuggingFace BERT, Azure Language), **STT** (Deepgram, Azure Speech), **Content Safety** (Azure AI), **Translation** (Azure Translator). Group by service category with collapsible sections, real-time health pings, cooldown timers for providers in backoff, and usage counters vs free-tier limits. New `GET /api/provider-health/extended` endpoint returning all provider statuses. |
| 6.4.8 | Card Hover Effects Polish | Fix imperfect hover effects on card headers across all dashboard/list components (provider-health, claims-history, fraud-alerts, dashboard). Standardize hover states: smooth `transition: all 0.2s ease`, consistent `transform: translateY(-2px)`, subtle `box-shadow` elevation, header gradient shift on hover. Ensure focus-visible states match for keyboard accessibility. |
| 6.4.9 | OCR Chain Overhaul — 2 New Providers + Hardening | **5.H8 root cause analysis + research revealed the OCR chain needs 2 new providers and 3 fixes:** **NEW PROVIDERS:** (1) **Mistral OCR** (Tier 2b): Reuses existing Mistral API key from LLM chain. 1,000 pages/doc, 50MB max. Best-in-class accuracy. Free tier available (data used for training, same as Gemini Vision). REST API: `POST https://api.mistral.ai/v1/ocr`. Insert after Azure DocIntel in chain. (2) **Tesseract OCR** (Tier 1b, local): `TesseractOCR` NuGet (5.5.1), 100% local like PdfPig. Handles scanned/image PDFs that PdfPig can't — converts pages to images then OCR. No page limits, no API calls, no data transfer. Insert after PdfPig as local scanned-doc fallback. **FIXES:** (3) **Azure DocIntel page batching**: Use SDK `pages` parameter to batch 2 pages at a time, stitch text. (4) **OCR Space 1MB size guard**: Pre-check file size, skip gracefully. (5) **PdfPig `page.Letters` fallback**: Try Letters collection for CID/Type3 font PDFs. **New chain:** PdfPig → Tesseract (local) → Azure DocIntel (batched) → Mistral OCR → OCR Space → Gemini Vision. |
| 6.4.10 | Document Library Page | New `/documents` route listing all indexed documents (card grid with filename, category, page count, chunk count, embedding provider, upload date). Links to `/documents/:id` detail and `/documents/query?documentId=X`. Filters by category, sort by date. Accessible from nav sidebar + document-upload success state. Fixes orphaned `/documents/:id` route with no browsable entry point. |
| 6.4.11 | Navigation Cross-Linking Gaps | **Audit findings:** (1) `/documents/:id` only reachable after upload — add Document Library as browsable entry point. (2) `/fraud/correlations/:claimId` only reachable from fraud-alerts — add "View Correlations" link on `claim-result` page when fraud flags are present. (3) `/claims/:id` has no direct nav — OK since it's a detail page, but `claims-history` should show clickable claim cards (not just router.navigate). (4) Add nav link for new `/documents` library page in Workspace dropdown + sidebar. |

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

### Sprint 6 Stats (Projected)

| Metric | Sprint 5 End | Sprint 6 Projected |
|--------|-------------|-------------------|
| Backend tests (xUnit) | 662 | **~725** (+63: Azure services + regression) |
| Frontend unit tests | 443 | **~465** (+~22: provider health revamp + hover fixes) |
| E2E tests | ~450 | **~475** (+~25: provider health + content safety flows) |
| **Grand Total** | **~1,555** | **~1,665** |
| New NuGet packages | — | **3** (ContentSafety, Speech, AppInsights) |
| New resilient chains | — | **2** (NER, STT) |
| Azure AI services integrated | 2 (Vision, DocIntel) | **6** (+ContentSafety, Language, Speech, Translator) |
| RAG agent team | None | **4 agents** (ProviderRouter, QueryReformulator, AnswerEvaluator, CrossDocReasoner) |
| Docker | None | **Dockerfile + docker-compose** |
| Health endpoints | None | **/health + /health/ready** |
| DB migrations | Manual ALTER TABLE | **EF Core Migrations** |

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
| **Sprint 6** | Azure AI + Docker + RAG Agents + Hardening | **PLANNED** | 4 Azure AI services, 2 resilient chains, RAG query agent team, EF migrations, Docker, regression tests, ~1,650 tests |

### Deferred to Sprint 7+
- Azure Anomaly Detector integration (fraud pattern detection)
- Azure Communication Services (email alerts for fraud/claims)
- Azure AI Search (replace in-memory vector search)
- Cloud deployment: Azure Static Web Apps (frontend) + App Service B1 (backend)
- Azure Key Vault integration (cloud secrets management)
- Custom domain + SSL certificate
- Azure SQL Free as alternate DB provider

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

---

*Last updated: 2026-03-01 | Sprint 6 planned via 9-agent brainstorming (3 iterations, unanimous 9/9 APPROVE)*
