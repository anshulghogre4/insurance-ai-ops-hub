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

## Sprint 2: Claims & Fraud Pipeline + API Endpoints (PLANNED)

**Goal:** Wire Sprint 1 infrastructure into working claims processing workflows with real API endpoints.

### Week 1: Claims Triage Pipeline

#### New API Endpoints
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `POST /api/insurance/claims/triage` | POST | Submit claim text for triage assessment |
| `POST /api/insurance/claims/upload` | POST | Upload multimodal evidence (photo/audio/document) |
| `GET /api/insurance/claims/{id}` | GET | Retrieve claim triage result |
| `GET /api/insurance/claims/history` | GET | List recent claims with filters |

#### Backend Implementation
1. **MediatR Commands/Queries** (CQRS pattern):
   - `TriageClaimCommand` -> `TriageClaimHandler` (orchestrates ClaimsTriage + FraudDetection agents)
   - `UploadClaimEvidenceCommand` -> handler (routes to STT/Vision/OCR based on MIME type)
   - `GetClaimQuery` / `GetClaimsHistoryQuery` -> handlers
2. **ClaimsOrchestrationService** (Facade):
   - Accepts claim text + optional multimodal evidence
   - Runs OrchestrationProfile.ClaimsTriage (4 agents, 8 max turns)
   - Chains: OCR/STT -> NER -> ClaimsTriage -> FraudDetection -> QA
   - Returns unified `ClaimTriageResult` with severity, urgency, fraud score, actions
3. **Multimodal Evidence Processor**:
   - Route `image/*` -> AzureVision (primary) or CloudflareVision (fallback via keyed services)
   - Route `audio/*` -> Deepgram STT -> then feed transcript to triage
   - Route `application/pdf` -> OcrSpace -> then feed text to triage
   - Route text -> HuggingFace NER -> extract entities -> enrich triage context
4. **Database Schema** (new tables):
   - `Claims` table: Id, Text, Severity, Urgency, FraudScore, Status, CreatedAt
   - `ClaimEvidence` table: ClaimId, EvidenceType, Provider, ProcessedText, DamageIndicators
   - `ClaimActions` table: ClaimId, Action, Priority, Status, AssignedTo

#### Tests
- Command handler tests (mock orchestrator)
- Multimodal routing tests (MIME type -> correct service)
- Claim storage/retrieval tests
- Integration tests for full triage pipeline

### Week 2: Fraud Detection Pipeline

#### New API Endpoints
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `POST /api/insurance/fraud/analyze` | POST | Deep fraud analysis on a claim |
| `GET /api/insurance/fraud/score/{claimId}` | GET | Get fraud score for a claim |
| `GET /api/insurance/fraud/alerts` | GET | List high-risk fraud alerts |

#### Backend Implementation
1. **FraudAnalysisService** (Facade):
   - Runs OrchestrationProfile.FraudScoring (FraudDetection + ClaimsTriage + BA + QA)
   - Cross-references NER entities against claim history patterns
   - Produces fraud probability score (0-100) with categorized indicators
2. **Fraud Alert System**:
   - Auto-flag claims with fraud score > 55 (Medium+)
   - SIU referral queue for scores > 75 (High/VeryHigh)
   - Dashboard-ready alert feed
3. **Provider Health Endpoint**:
   - `GET /api/insurance/health/providers` -> returns health of all 5 LLM providers + 5 multimodal services
   - Cooldown status, consecutive failures, last success timestamp

#### Tests
- Fraud scoring tests with various claim scenarios
- Alert threshold tests
- Provider health endpoint tests

### Sprint 2 Deliverables
- 6 new API endpoints (claims triage + fraud + health)
- 3 new database tables
- Full claims processing pipeline (text + multimodal -> triage -> fraud -> storage)
- Provider health monitoring
- ~40 new tests

---

## Sprint 3: Frontend + Dashboard + E2E (PLANNED)

**Goal:** Build the UI for all Sprint 2 backend capabilities and expand E2E test coverage.

### Week 1: Claims Triage UI

#### New Angular Components
| Component | Route | Purpose |
|-----------|-------|---------|
| `claims-triage` | `/claims/triage` | Submit claims with text + file upload |
| `claim-result` | `/claims/:id` | View triage result (severity, urgency, fraud, actions) |
| `claims-history` | `/claims/history` | Searchable/filterable claims list |
| `evidence-viewer` | (child component) | Display processed evidence (image annotations, transcripts, OCR text) |

#### Implementation
1. **Claims Triage Form**:
   - Text area for claim description
   - Drag-and-drop file upload (images, audio, PDFs)
   - MIME type detection -> show preview (image thumbnail, audio player, PDF icon)
   - Submit button with loading state (elapsed timer, phase descriptions)
   - Real-time triage result display
2. **Triage Result Card**:
   - Severity badge (Critical=red, High=orange, Medium=yellow, Low=green)
   - Urgency timeline indicator
   - Fraud risk gauge (0-100 with color zones)
   - Recommended actions checklist
   - Evidence panel (processed images, transcripts, OCR text)
3. **Claims History Table**:
   - Sortable columns: Date, Severity, Urgency, Fraud Score, Status
   - Filters: severity, urgency, date range, fraud risk level
   - Click row -> navigate to detail view
4. **Navigation Update**:
   - Add "Claims" section to sidebar nav
   - Sub-routes: Triage, History

### Week 2: Dashboard Expansion + Provider Health

#### New Angular Components
| Component | Route | Purpose |
|-----------|-------|---------|
| `provider-health` | `/dashboard/providers` | Real-time health of all 10 services |
| `claims-dashboard` | `/dashboard/claims` | Claims metrics (volume, severity distribution, avg fraud score) |
| `fraud-alerts` | `/dashboard/fraud` | Active fraud alerts with SIU referral status |

#### Implementation
1. **Provider Health Monitor**:
   - Card per provider showing: status (green/yellow/red), consecutive failures, cooldown timer
   - LLM providers section (5 providers with fallback chain visualization)
   - Multimodal services section (5 services)
   - Auto-refresh every 30 seconds
2. **Claims Dashboard**:
   - Claims volume chart (daily/weekly/monthly)
   - Severity distribution pie chart
   - Average fraud score trend line
   - Top damage types bar chart (from vision services)
   - Recent claims activity feed
3. **Fraud Alerts Panel**:
   - Active alerts sorted by fraud score (highest first)
   - SIU referral queue with accept/dismiss actions
   - Fraud indicator category breakdown chart

#### E2E Test Expansion
- Claims triage flow (submit text -> view result)
- Claims upload flow (attach image -> processing indicator -> result with damage indicators)
- Claims history filtering and pagination
- Provider health dashboard rendering
- Fraud alerts interaction
- Accessibility scans on all new components
- Mobile responsive tests for claims forms

### Sprint 3 Deliverables
- 7 new Angular components
- 3 new routes (/claims/*, /dashboard/providers, /dashboard/fraud)
- Updated navigation with Claims section
- Provider health real-time monitoring
- Claims metrics dashboard
- Fraud alerts panel
- ~50 new E2E tests
- WCAG AA accessibility on all new components

---

## Summary Timeline

| Sprint | Focus | Status | Key Deliverable |
|--------|-------|--------|-----------------|
| **Sprint 1** | Infrastructure + Providers | **COMPLETE** | 5-provider fallback, 5 multimodal services, 9 agents, 173 tests |
| **Sprint 2** | Claims & Fraud Pipeline | **PLANNED** | 6 API endpoints, claims DB, fraud scoring, provider health |
| **Sprint 3** | Frontend + Dashboard | **PLANNED** | 7 components, claims UI, dashboards, ~50 E2E tests |

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
