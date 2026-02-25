# Insurance AI Operations Hub - Sprint Roadmap

## Vision

Transform the Sentiment Analyzer (v2.0) into a full **Insurance AI Operations Hub** (v3.0) with claims triage, fraud detection, multimodal processing, and operational dashboards — all on free-tier AI providers.

---

## Sprint 1: Infrastructure + New Providers (COMPLETE)

**Goal:** Build the provider infrastructure and multimodal services that everything else depends on.

**Problem solved:** The system had a single static Kernel (one provider at boot, no runtime fallback). We needed runtime resilience, multimodal capabilities, and selective agent activation.

### What Was Built

#### 1. Resilient Kernel Provider (5-Provider Fallback Chain)
- `IResilientKernelProvider` interface in Agents project
- `ResilientKernelProvider` implementation in Backend with exponential backoff cooldown (30s -> 60s -> 120s -> 300s max)
- Fallback order: **Groq -> Mistral -> Gemini -> OpenRouter -> Ollama (local, always available)**
- Backward compatible: `Kernel` singleton still works via `IResilientKernelProvider.GetKernel()`
- Health tracking per provider with `ReportFailure()` and `GetHealthStatus()`

#### 2. Multimodal Services (5 Services, 0 New NuGet Packages)
| Service | Provider | Free Tier | Purpose |
|---------|----------|-----------|---------|
| `DeepgramSpeechToTextService` | Deepgram Nova-2 | $200 credit | Transcribe adjuster voice notes, call recordings |
| `AzureVisionService` | Azure Vision F0 | 5K/month | Analyze claim damage photos (labels, captions, objects) |
| `CloudflareVisionService` | Cloudflare Workers AI | 10K neurons/day | Natural language image analysis for damage assessment |
| `OcrSpaceService` | OCR.space | 500/day | Digitize scanned policy docs and claim forms |
| `HuggingFaceNerService` | HuggingFace (BERT NER) | 300/hour | Extract entities (names, orgs, locations + insurance entities) |

All services use `HttpClient` REST calls with PII redaction on output text.

#### 3. Orchestration Profiles (Selective Agent Activation)
- `OrchestrationProfile` enum: SentimentAnalysis, ClaimsTriage, FraudScoring, DocumentQuery
- `OrchestrationProfileFactory` maps profiles to agent subsets (reduces token usage 50-60%)
- ClaimsTriage: 4 agents, 8 max turns | SentimentAnalysis: 7 agents, 14 max turns

#### 4. Claims Triage + Fraud Detection Agent Prompts
- `ClaimsTriageSpecialist`: Severity (Critical/High/Medium/Low), urgency, claim type, recommended actions, preliminary fraud flags
- `FraudDetectionSpecialist`: Fraud probability scoring (0-100), 5 indicator categories (Timing/Behavioral/Financial/Pattern/Documentation), SIU referral recommendations
- Both have `.md` prompt files + hardcoded fallbacks in `AgentDefinitions.cs`

#### 5. Insurance Entity Extraction (NER Post-Processing)
- Regex patterns for: POLICY_NUMBER, CLAIM_NUMBER, MONEY, DATE, SSN, PHONE, EMAIL
- Supplements BERT NER (PER/ORG/LOC/MISC) with insurance-domain entities
- Deduplication by value+type

#### 6. PII Redaction Across Multimodal Pipeline
- `IPIIRedactor` injected into Deepgram, AzureVision, CloudflareVision, OcrSpace
- Output text redacted before returning to callers
- HuggingFace NER exempt (needs raw text) with audit warning log

#### 7. Expanded Damage Keywords (Vision Services)
- AzureVision: 16 -> 30 keywords
- CloudflareVision: 16 -> 33 damage terms
- Added: vandalism, theft, wind, foundation, glass, shatter, tree, smoke, roof, sinkhole, lightning, explosion, sewage, asbestos, erosion, corrosion, collapse, burst, cave-in, landslide

### Sprint 1 Stats
- **New files:** 30 (implementations + interfaces + tests + prompt files)
- **Modified files:** 7 (Program.cs, AgentDefinitions, Orchestrator, IAnalysisOrchestrator, AgentRole, appsettings, LlmProviderConfiguration)
- **Tests:** 173 passing (52 original + 121 new)
- **New NuGet packages:** 0
- **API keys configured:** 13 (all in .NET User Secrets)

### Sprint 1 Architecture
```
Angular 21 SPA (Port 4200) — unchanged
    |
.NET 10 Web API (Port 5143)
    |
    ├── v1 API (legacy, frozen)
    ├── v2 Insurance API
    └── Agent Orchestration (Semantic Kernel)
         ├── CTO Agent (orchestrator)
         ├── BA Agent (domain analysis)
         ├── Developer Agent (formatting)
         ├── QA Agent (validation)
         ├── AI Expert Agent (model/cloud/training)
         ├── Architect Agent (storage/perf)
         ├── UX Designer Agent (screens/a11y)
         ├── Claims Triage Agent (NEW)        <-- Sprint 1
         └── Fraud Detection Agent (NEW)      <-- Sprint 1
              |
         IResilientKernelProvider (NEW)        <-- Sprint 1
         ├── Groq (primary)
         ├── Mistral (NEW)
         ├── Gemini
         ├── OpenRouter (NEW)
         └── Ollama (local fallback)
              |
         Multimodal Services (NEW)             <-- Sprint 1
         ├── Deepgram STT
         ├── Azure Vision
         ├── Cloudflare Vision
         ├── OCR.space
         └── HuggingFace NER
              |
         SQLite / Supabase (PostgreSQL)
```

---

## Sprint 2: Claims & Fraud Pipeline + API Endpoints (COMPLETE)

**Goal:** Wire Sprint 1 infrastructure into working claims processing workflows with real API endpoints.

**Problem solved:** All Sprint 1 infrastructure (5-provider fallback, 5 multimodal services, 9 agent definitions, orchestration profiles) was idle — no endpoints consumed the claims/fraud agents or multimodal services. Sprint 2 wired everything into working pipelines.

### What Was Built

#### 1. Database Layer (3 New Tables)
- `ClaimRecord` entity: Id, ClaimText, Severity, Urgency, ClaimType, FraudScore, FraudRiskLevel, Status, TriageJson, FraudAnalysisJson, FraudFlagsJson, CreatedAt
- `ClaimEvidenceRecord` entity: Id, ClaimId (FK), EvidenceType, MimeType, Provider, ProcessedText, DamageIndicatorsJson, CreatedAt
- `ClaimActionRecord` entity: Id, ClaimId (FK), Action, Priority, Reasoning, Status, CreatedAt
- `IClaimsRepository` + `SqliteClaimsRepository` with pagination support (tuple return `(List<ClaimRecord>, int TotalCount)`)
- `PaginatedResponse<T>` generic wrapper with Items, TotalCount, Page, PageSize, TotalPages

#### 2. Profile-Aware Orchestration (Critical Enabler)
- Replaced stub in `InsuranceAnalysisOrchestrator.AnalyzeAsync(text, profile)` with real profile-aware agent selection
- `OrchestrationProfile.ClaimsTriage`: 4 agents (ClaimsTriage, FraudDetection, BA, QA), 8 max turns
- `OrchestrationProfile.FraudScoring`: 3 agents (FraudDetection, ClaimsTriage, QA), 6 max turns
- JSON schema examples injected into agent prompts for consistent output structure
- New parsing logic extracts `claimTriage` and `fraudAnalysis` JSON blocks from agent output
- `ClaimTriageDetail` and `FraudAnalysisDetail` agent output models added to `AgentAnalysisResult`

#### 3. Service Facades (3 New Services)
- **ClaimsOrchestrationService**: Claim text → PII redact → orchestrator (ClaimsTriage profile) → extract triage + fraud → save to DB → return response
- **MultimodalEvidenceProcessor**: MIME routing (`image/*` → Azure/Cloudflare Vision, `audio/*` → Deepgram STT, `application/pdf` → OCR.space) + NER on all output + DB persistence
  - Azure → Cloudflare vision fallback via `[FromKeyedServices("CloudflareVision")]` keyed DI
  - Graceful degradation when NER or fallback services fail
- **FraudAnalysisService**: Load claim → orchestrator (FraudScoring profile) → fraud score → auto-flag SIU referral (score > 75) → update DB

#### 4. MediatR Commands & Queries (8 New Handlers)
| Handler | Type | Purpose |
|---------|------|---------|
| `TriageClaimCommand` | Command | Validates text → calls ClaimsOrchestrationService |
| `UploadClaimEvidenceCommand` | Command | Validates file → calls MultimodalEvidenceProcessor |
| `GetClaimQuery` | Query | Loads claim by ID |
| `GetClaimsHistoryQuery` | Query | Loads claims with filters + pagination |
| `AnalyzeFraudCommand` | Command | Calls FraudAnalysisService |
| `GetFraudScoreQuery` | Query | Returns fraud score for a claim |
| `GetFraudAlertsQuery` | Query | Returns claims with FraudScore > 55 |
| `GetProviderHealthQuery` | Query | Returns health of all LLM + multimodal providers |

#### 5. API Endpoints (8 New Endpoints)
| Endpoint | Method | MediatR Handler |
|----------|--------|-----------------|
| `/api/insurance/claims/triage` | POST | TriageClaimCommand |
| `/api/insurance/claims/upload` | POST | UploadClaimEvidenceCommand |
| `/api/insurance/claims/{id}` | GET | GetClaimQuery |
| `/api/insurance/claims/history` | GET | GetClaimsHistoryQuery |
| `/api/insurance/fraud/analyze` | POST | AnalyzeFraudCommand |
| `/api/insurance/fraud/score/{claimId}` | GET | GetFraudScoreQuery |
| `/api/insurance/fraud/alerts` | GET | GetFraudAlertsQuery |
| `/api/insurance/health/providers` | GET | GetProviderHealthQuery |

#### 6. PII Redaction for Claims Pipeline
- `IPIIRedactor` injected into `ClaimsOrchestrationService`
- Claim text PII-redacted before DB persistence (not just before AI calls)
- Text truncated to 5000 chars before redaction + storage

#### 7. Provider Health Monitoring
- LLM providers: status, consecutive failures, cooldown remaining for all 5 providers
- Multimodal services: Deepgram, AzureVision, CloudflareVision, OcrSpace, HuggingFace — configured/unconfigured status via `IConfiguration` key check

#### 8. Tests (57 New Tests, 9 New Test Files)
| Test File | Tests | Coverage |
|-----------|-------|----------|
| ClaimsOrchestrationServiceTests | 10 | Triage, PII redaction, DB persistence, null results, text truncation |
| MultimodalEvidenceProcessorTests | 10 | MIME routing, vision fallback (Azure→Cloudflare), NER integration, graceful degradation |
| FraudAnalysisServiceTests | 6 | Fraud scoring, SIU referral, alert thresholds |
| TriageClaimHandlerTests | 5 | Valid claim, empty text, service errors |
| UploadClaimEvidenceHandlerTests | 5 | Image/audio/PDF upload, unsupported MIME |
| ClaimsRepositoryTests | 6 | Save/retrieve, filter by severity/date, pagination |
| GetClaimHandlerTests | 4 | Found/not-found, history filters, empty results |
| FraudCommandsTests | 4 | Analyze, score, alerts with threshold |
| ProviderHealthTests | 5 | LLM health, multimodal health, unconfigured services |

#### 9. Agent Review (3 Iterations)
- All 9 agents reviewed Sprint 2 implementation across 3 iterations
- Iteration 1: Fixed vision fallback, added pagination wrapper, PII before DB storage, 3 new fallback tests
- Iteration 2: All agents rated 9-9.5/10 — no actionable gaps
- Iteration 3: Added JSON schema examples to agent prompts for better output compliance
- Final scores: All agents 9.5-10/10 satisfied

### Sprint 2 Stats
- **New files:** 40 (entities, repository, models, services, handlers, endpoints, tests)
- **Modified files:** 5 (DbContext, Program.cs, AgentSelectionStrategy, InsuranceAnalysisOrchestrator, AgentAnalysisResult)
- **Tests:** 230 passing (173 Sprint 1 + 57 new) — 0 regressions on v1
- **New NuGet packages:** 0
- **New API endpoints:** 8

---

## Sprint 3: Frontend + Dashboard + E2E + Landing Page (COMPLETE)

**Goal:** Build the UI for all Sprint 2 backend capabilities, expand E2E test coverage, and create a public landing page showcasing the platform.

**Problem solved:** All Sprint 2 backend endpoints (claims triage, fraud analysis, evidence upload, provider health) had no frontend UI. The app only had v1 sentiment analyzer, v2 insurance analyzer, and a basic dashboard. Sprint 3 added 8 new Angular components, 6 new routes, Chart.js dashboard charts, comprehensive E2E test suite, and an interactive public landing page.

### What Was Built

#### 1. Public Landing Page (Interactive Platform Showcase)
- **Route:** `/` (public, no auth required) — replaced sentiment analyzer as home page
- **Landing component** (1,726 lines across 3 files: `landing.ts`, `landing.html`, `landing.css`)
- 7 interactive sections:
  1. **Hero**: Animated gradient orbs, version badge, gradient text headline, CTA buttons
  2. **Agent Orchestration**: 9 agent cards with "Run Orchestration" step-by-step animation
  3. **Provider Fallback Chain**: 5 LLM provider cards with "Simulate Failover" animation
  4. **Multimodal Pipeline**: 4 modality tabs (Voice, Image, Document, Entities) with auto-cycling
  5. **Interactive Demo**: Sample claim text → simulated triage result with severity/fraud/actions
  6. **Security & PII**: Before/after PII redaction toggle with auto-cycling examples
  7. **Stats & Tech Grid**: Technology badges across 4 categories (AI/ML, Backend, Frontend, Infrastructure)
- IntersectionObserver for scroll-triggered section reveal animations
- Full 3-theme compatibility (dark/semi-dark/light)
- Responsive design (mobile-first)
- `prefers-reduced-motion` support

#### 2. Claims Triage Component
- **Route:** `/claims/triage` (authGuard)
- Claim text textarea with 10,000 char limit and character counter
- Interaction type dropdown (Complaint/General/Call/Email/Review)
- Quick template buttons (Water damage, Auto accident, Theft, Liability)
- File upload zone with drag-and-drop (images, audio, PDFs)
- Submit with loading state (elapsed timer + agent phase descriptions)
- Inline result display: severity badge, fraud score gauge, recommended actions, fraud flags

#### 3. Claim Result Component
- **Route:** `/claims/:id` (authGuard)
- Full claim detail view loaded by ID from `ActivatedRoute`
- Triage summary card (severity, urgency, claim type, estimated loss range)
- Fraud score gauge (0-100 horizontal bar with color zones)
- Recommended actions list with priority badges and reasoning
- Fraud flags warning badges
- Evidence panel with `evidence-viewer` child components
- "Run Fraud Analysis" action button

#### 4. Evidence Viewer Component (Child)
- Evidence type icon (camera/mic/document)
- Provider badge (Azure Vision, Deepgram, OCR.space, etc.)
- Processed text block (transcript / image description / OCR text)
- Damage indicator chips (colored tags)

#### 5. Claims History Component
- **Route:** `/claims/history` (authGuard)
- Filters bar: severity dropdown, status dropdown, date range inputs
- Responsive results table: #, Date, Preview, Severity, Urgency, Fraud Score, Status
- Colored badges for severity/status/fraud score (green <30, yellow 30-55, orange 55-75, red >75)
- Pagination with page size selector (10/20/50)
- Row click → navigate to `/claims/{id}`

#### 6. Provider Health Monitor
- **Route:** `/dashboard/providers` (authGuard)
- LLM providers card grid: 5 providers with status dots (green/yellow/red), consecutive failures, cooldown timer
- Fallback chain visualization (Groq → Mistral → Gemini → OpenRouter → Ollama)
- Multimodal services card grid: 6 services with configured/unconfigured indicators
- Auto-refresh every 30 seconds via `interval(30000).pipe(takeUntilDestroyed())`

#### 7. Fraud Alerts Component
- **Route:** `/dashboard/fraud` (authGuard)
- Alert cards sorted by fraud score (highest first)
- Fraud score gauge, risk level badge, SIU referral indicator
- Fraud indicator category chips (Timing/Behavioral/Financial/Pattern/Documentation)
- "View Claim" and "Run Deep Analysis" action buttons
- Empty state message when no alerts

#### 8. Dashboard Charts (ng2-charts + Chart.js)
- Severity distribution doughnut chart (Critical/High/Medium/Low)
- Customer persona horizontal bar chart (replacing CSS-only bars)
- Quick links cards row: Claims Triage, Claims History, Provider Health, Fraud Alerts

#### 9. Navigation + Route Updates
- Sentiment Analyzer moved from `/` to `/sentiment` (authGuard)
- 6 new routes added (all with authGuard except landing page)
- Desktop nav: added "Claims" section (Triage + History) and expanded "Dashboard" (Providers + Fraud)
- Mobile nav: same links in mobile drawer
- SVG icons for all new nav items

#### 10. Claims Service + TypeScript Models
- `claims.model.ts`: ClaimTriageRequest/Response, FraudAnalysisResponse, ProviderHealthResponse, PaginatedResponse<T>, etc.
- `claims.service.ts`: 8 HTTP methods mapping to all Sprint 2 API endpoints

#### 11. Frontend Unit Tests (34 new, 196 total)
| Test File | Tests | Coverage |
|-----------|-------|----------|
| claims.service.spec.ts | ~10 | All 8 HTTP methods + FormData + errors |
| claims-triage.spec.ts | ~6 | Form, validation, submit, loading, file, error |
| claim-result.spec.ts | ~5 | Data load, badges, gauge, evidence, not-found |
| claims-history.spec.ts | ~5 | Table, filters, pagination, row click, empty |
| provider-health.spec.ts | ~4 | Provider cards, status, multimodal, refresh |
| fraud-alerts.spec.ts | ~4 | Alert cards, fraud score, SIU, empty state |

#### 12. E2E Tests (Playwright — 101 new, 239 total passing)
| E2E Spec File | Tests | Coverage |
|---------------|-------|----------|
| claims-triage.spec.ts | ~10 | Form, templates, submit, result, errors, Ctrl+Enter, mobile |
| claims-detail.spec.ts | ~6 | Load by ID, badges, gauge, evidence, actions, back nav |
| claims-history.spec.ts | ~8 | Table, filters, pagination, row click, empty, refresh |
| provider-health.spec.ts | ~6 | LLM cards, status, fallback chain, multimodal, refresh |
| fraud-alerts.spec.ts | ~6 | Alert cards, fraud score, SIU, category chips, empty |
| accessibility.spec.ts | ~15 | WCAG AA axe-core on all 9 routes + ARIA + form labels + progress bars |
| navigation.spec.ts (updated) | +2 | Landing page at root + sentiment at /sentiment |
| sentiment-analyzer.spec.ts (updated) | — | Route changed to /sentiment |

#### 13. BA Validation (3 Iterations)
- Iteration 1 (B+): 12 issues found, all High/Medium fixed
- Iteration 2 (A-): 6 remaining items, all Low/Informational
- Iteration 3 (A): SHIP approved, 0 blocking issues, 6 deferred to Sprint 4

### Sprint 3 Stats
- **New files:** ~50 (8 components × 3 files each + models + service + 6 unit specs + 5 e2e specs + mock data)
- **Modified files:** 8 (app.routes.ts, nav.ts, dashboard.ts, api-mocks.ts, mock-data.ts, navigation.spec.ts, sentiment-analyzer.spec.ts, accessibility.spec.ts)
- **Backend tests:** 246 (unchanged — 0 backend changes in Sprint 3)
- **Frontend unit tests:** 196 passing (20 spec files — was 126)
- **E2E tests:** 239 passing, 9 skipped (12 spec files — was 7 spec files, ~138 tests)
- **New npm packages:** 2 (ng2-charts, chart.js)
- **New routes:** 6 (/sentiment, /claims/triage, /claims/history, /claims/:id, /dashboard/providers, /dashboard/fraud)
- **Angular components:** 13 total (was 6)

---

## Sprint 4: Document Intelligence RAG + Technical Debt (PLANNED)

**Goal:** Fix critical P0/P1 technical debt, then build Document Intelligence RAG foundation for insurance policy/claims document understanding, Customer Experience Copilot, and cross-claim fraud correlation.

**Brainstorming:** 9-agent brainstorming across 3 iterations (unanimous APPROVE). See `REVIEW.md` Session #6 for full details.

### Week 1: P0/P1 Technical Debt (MUST-HAVE)

| # | Item | Priority | Owner | Deliverable |
|---|------|----------|-------|-------------|
| 1.1 | Orchestrator Unit Tests | P0 | Developer + QA | 15+ tests for `InsuranceAnalysisOrchestrator.cs` (0% → 60%+ coverage) |
| 1.2 | V1 PII Fix (Decorator) | P0 | Developer + Architect | `PiiRedactingSentimentService` wrapping `ISentimentService` (v1 files remain frozen) |
| 1.3 | PII Regression Tests | P0 | QA | 5 tests asserting zero PII patterns in DB across v1 and v2 pipelines |
| 1.4 | Per-Endpoint Rate Limiting | P1 | Architect | analyze: 10/min, triage: 5/min, fraud: 5/min, doc upload: 3/min |
| 1.5 | Accessibility Fixes | P1 | UX Designer | Fix `--text-muted`/`--text-secondary` contrast, keyboard trap prevention, `aria-live` regions |

**Gate:** 15+ orchestrator tests pass, PII regression tests pass, 0 test regressions.

### Week 2: Document Intelligence RAG Foundation (MUST-HAVE)

| # | Item | Deliverable |
|---|------|-------------|
| 2.1 | Voyage AI Embedding Service | `IEmbeddingService` + `VoyageEmbeddingService` (voyage-finance-2, 1024-dim) + `OllamaEmbeddingService` fallback, 8 tests |
| 2.2 | RAG Database Schema | `DocumentRecord` + `DocumentChunkRecord` entities, `IDocumentRepository` + `SqliteDocumentRepository` with cosine similarity via `System.Numerics.Vector`, 6 tests |
| 2.3 | Document Chunking | Insurance section-aware chunking (DECLARATIONS/COVERAGE/EXCLUSIONS/CONDITIONS/ENDORSEMENTS) + sentence-boundary splitting with 64-token overlap, 512-token target, 5 tests |
| 2.4 | Document Intelligence Service | RAG Facade: upload (OCR → chunk → embed → store) + query (embed → vector search top-5 → LLM answer with citations), 10 tests |
| 2.5 | Document API Endpoints | 4 MediatR handlers + 4 endpoints: POST upload, POST query, GET by ID, GET history, 8 tests |
| 2.6 | DocumentQuery Agent | Agent prompt + `AgentRole.DocumentQuery` + orchestrator + selection strategy integration |

**Gate:** RAG pipeline operational (upload + query), 32+ new backend tests, Voyage AI validated.

### Week 3: CX Copilot + Fraud Enhancement (SHOULD-HAVE)

| # | Item | Deliverable |
|---|------|-------------|
| 3.1 | Customer Experience Copilot | `CustomerExperienceService` with SSE streaming, `CustomerExperience` orchestration profile, chat endpoint, 6 tests |
| 3.2 | Cross-Claim Fraud Correlation | Pattern matching (address, phone, date overlap 90d, narrative similarity >0.92), 2+ indicators to flag, `FraudCorrelationRecord`, 8 tests |
| 3.3 | Related Claims Context in Triage | Query related claims before triage, inject context into agent prompt |

**Gate:** CX copilot endpoint working, fraud correlation returning results, 20+ new tests.

### Week 4: Frontend + E2E + MCP Integration + Documentation (SHOULD-HAVE)

| # | Item | Deliverable |
|---|------|-------------|
| 4.1 | Frontend Components (5 new) | document-upload, document-query, document-result, cx-copilot, fraud-correlation |
| 4.2 | E2E Tests (4 new specs) | document-upload, document-query, cx-copilot, fraud-correlation (36+ tests) |
| 4.3 | MCP Server Integration | Playwright MCP (E2E test generation from browser sessions, exploratory testing) + Stitch MCP (design-to-code pipeline for Sprint 5 UI revamp) |
| 4.4 | Documentation Updates | All MD files updated with Sprint 4 content |

**Gate:** All frontend components rendered, 40+ E2E tests pass, MCP servers operational, grand total 892+ tests.

### Sprint 4 Test Targets

| Category | Current | Sprint 4 New | Sprint 4 Total |
|----------|---------|-------------|----------------|
| Backend (xUnit) | 278 | 76+ | 354+ |
| Frontend Unit (Vitest) | 199 | 36+ | 235+ |
| E2E (Playwright) | 263 | 40+ | 303+ |
| **Grand Total** | **740** | **152+** | **892+** |

### Sprint 4 Risk Register

| Risk | Mitigation |
|------|-----------|
| SQLite vector search too slow (>1K chunks) | Cap dev at 500 chunks; production uses Supabase pgvector |
| Voyage AI free tier exhaustion (50M tokens) | Ollama `nomic-embed-text` fallback; incremental indexing not bulk |
| SSE streaming browser compat issues | Standard `EventSource` API; fallback to polling |
| Cross-claim correlation false positives | Require 2+ indicators; narrative similarity threshold 0.92 |
| Scope creep into Week 3-4 | CTO scope lock Day 1; Week 3-4 items are SHOULD-HAVE, can defer to Sprint 5 |

---

## Summary Timeline

| Sprint | Focus | Status | Key Deliverable |
|--------|-------|--------|-----------------|
| **Sprint 1** | Infrastructure + Providers | **COMPLETE** | 5-provider fallback, 5 multimodal services, 9 agents, 173 tests |
| **Sprint 2** | Claims & Fraud Pipeline | **COMPLETE** | 8 API endpoints, 3 DB tables, claims triage, fraud scoring, provider health, 246 tests |
| **Sprint 3** | Frontend + Dashboard + E2E + Landing | **COMPLETE** | 8 new components, 6 new routes, landing page, Chart.js dashboard, 196 unit tests, 239 E2E tests |
| **Sprint 4** | Document Intelligence RAG + Tech Debt | **PLANNED** | RAG pipeline, CX Copilot, fraud correlation, PII fixes, rate limiting, 152+ new tests |

## Free Tier Budget

| Provider | Free Tier | Sprint 1 Usage | Sprint 2-3 Projected |
|----------|-----------|----------------|----------------------|
| Groq | 250 req/day | Primary LLM | ~100 req/day (claims + fraud) |
| Mistral | 500K tokens/month | Fallback | ~50K tokens/month |
| Gemini | 60 req/min | Fallback | ~20 req/day |
| OpenRouter | $1 free credit | Fallback | Minimal |
| Ollama | Unlimited (local) | Last resort | PII-sensitive analysis |
| Deepgram | $200 credit | Ready | ~50 transcriptions/day |
| Azure Vision | 5K/month | Ready | ~200 images/day |
| Cloudflare Vision | 10K neurons/day | Ready (secondary) | Fallback for Azure |
| OCR.space | 500/day | Ready | ~100 documents/day |
| HuggingFace | 300/hour | Ready | ~50 NER calls/day |
| Voyage AI | 50M tokens (free) | Sprint 4 planned | Document embeddings (voyage-finance-2, 1024-dim) |

---

## Sprint 5: UI Revamp + MCP Ecosystem + Observability (PLANNED)

**Goal:** Full UI/UX revamp using Google Stitch AI design-to-code pipeline, expand MCP server ecosystem for god-tier developer experience, and add production observability.

### Week 1: UI/UX Revamp with Google Stitch

| # | Item | Deliverable |
|---|------|-------------|
| 5.1 | Stitch Design Generation | Generate new UI designs for all 13 components using Google Stitch AI from text prompts |
| 5.2 | Landing Page Revamp | Redesign landing page with Stitch-generated layouts, convert to Angular 21 + Tailwind |
| 5.3 | Claims Workflow Revamp | Redesign claims triage, history, detail, and fraud alerts with modern insurance UX patterns |
| 5.4 | Dashboard Revamp | Redesign dashboard with improved data visualization, provider health, and KPI cards |
| 5.5 | Design System Extraction | Extract consistent design tokens (colors, spacing, typography) from Stitch outputs |

### Week 2: MCP Ecosystem Expansion

| # | Item | Deliverable |
|---|------|-------------|
| 5.6 | Supabase MCP | Direct database schema management, query testing, migration validation |
| 5.7 | GitHub MCP | PR management, automated code review, issue tracking integration |
| 5.8 | Context7 MCP | Up-to-date docs for .NET 10, Angular 21, Semantic Kernel, Playwright |
| 5.9 | Sequential Thinking MCP | Structured reasoning for multi-agent architecture decisions |
| 5.10 | Sentry MCP | Production error tracking across 5 LLM providers and 5 multimodal services |

### Week 3: Observability + Security

| # | Item | Deliverable |
|---|------|-------------|
| 5.11 | Grafana Dashboards | Provider health monitoring, agent orchestration metrics, rate limit tracking |
| 5.12 | Snyk Security Scanning | Dependency vulnerability scanning for .NET + npm packages |
| 5.13 | Docker Containerization | Multi-stage Dockerfiles for backend + frontend + Ollama sidecar |
| 5.14 | Upstash Redis Caching | Cache identical analyses, per-endpoint rate limiting (from v4.0 design) |

### Week 4: Polish + Testing + Documentation

| # | Item | Deliverable |
|---|------|-------------|
| 5.15 | Playwright MCP Test Generation | Auto-generate E2E specs from browser sessions for new UI |
| 5.16 | Accessibility Audit | Full WCAG AA re-audit on revamped UI with axe-core |
| 5.17 | Performance Optimization | Lighthouse audits, bundle size analysis, lazy loading |
| 5.18 | Documentation | Updated all MD files, API docs, architecture diagrams |

### MCP Server Stack (Sprint 5)

| MCP Server | Category | Purpose |
|-----------|----------|---------|
| Playwright | Testing | Browser automation, E2E test generation |
| Stitch | Design | AI UI design → Angular code pipeline |
| Supabase | Database | Schema management, query testing |
| GitHub | DevOps | PR management, code review |
| Context7 | Docs | Latest library documentation |
| Sequential Thinking | Reasoning | Architecture decision support |
| Sentry | Monitoring | Error tracking + AI root cause |
| Grafana | Observability | Provider health dashboards |
| Snyk | Security | Dependency vulnerability scanning |
| Docker | Containers | Production containerization |
| Upstash | Caching | Redis rate limiting + caching |
| Tavily | Research | Insurance domain research |
