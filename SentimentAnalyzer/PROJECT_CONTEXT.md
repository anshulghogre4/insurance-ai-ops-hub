# Sentiment Analyzer - Insurance Domain Multi-Agent System

## Project Overview

An AI-powered insurance domain sentiment analysis platform that analyzes policyholder communications to extract sentiment, emotions, purchase intent, customer persona, journey stage, risk indicators, and policy recommendations using a multi-agent AI system.

### Version History
- **v1.0**: General-purpose sentiment analyzer. .NET 10 API + Angular 21 SPA + OpenAI GPT-4o-mini. Single endpoint: `POST /api/sentiment/analyze`.
- **v2.0**: Insurance-domain multi-agent system with free AI providers (Groq, Gemini, Ollama), Semantic Kernel orchestration, CQRS + Minimal API, SQLite/Supabase persistence, PII redaction, and analytics dashboard.
- **v3.0**: Insurance AI Operations Hub. 5-provider resilient fallback chain, 5 multimodal services, claims triage + fraud detection pipeline, interactive landing page, Chart.js dashboard, 13 Angular components across 10 routes, comprehensive E2E test suite.
- **v4.0 (Sprint 4 COMPLETE)**: Document Intelligence RAG (Voyage AI `voyage-finance-2` embeddings + SQLite vector store), Customer Experience Copilot (SSE streaming, dual-pass PII, tone classification, escalation detection), cross-claim fraud correlation (4-strategy: DateProximity/SimilarNarrative/SharedFlags/SameSeverity), v1 PII decorator fix, orchestrator test coverage, per-endpoint rate limiting, MCP server integration, 5 new Angular components (document-upload, document-query, document-result, cx-copilot, fraud-correlation), 3 new services, 4 new E2E spec files. 461 backend + 235 frontend + 357 E2E = **1,053 total tests, 0 failures**.
- **v5.0 (Current — Sprint 5 IN PROGRESS)**: Batch claims CSV upload, CX conversation memory/persistence, hybrid RAG retrieval (BM25 keyword + vector semantic, alpha=0.7/beta=0.3), 4 new embedding providers (Cohere, Gemini, HuggingFace, Jina — 6-provider chain), GitHub Actions CI/CD, 4 new Angular components (batch-upload, breadcrumb, command-palette, toast), breadcrumb navigation, Ctrl+K command palette, toast notification system, parallax landing enhancements. 22 Angular components, 16 routes. ~530 backend + ~443 frontend + ~450 E2E = **~1,423 total tests**.

---

## Architecture

```
Angular 21 SPA (Port 4200)
    |
    ├── /              → Landing Page (public, interactive platform showcase)
    ├── /sentiment     → v1 Sentiment Analyzer (legacy, authGuard)
    ├── /insurance     → v2 Insurance Analyzer (authGuard)
    ├── /dashboard     → Analytics Dashboard (authGuard)
    ├── /claims/triage → Claims Triage (authGuard)
    ├── /claims/history→ Claims History (authGuard)
    ├── /claims/:id    → Claim Detail (authGuard)
    ├── /dashboard/providers → Provider Health Monitor (authGuard)
    ├── /dashboard/fraud     → Fraud Alerts (authGuard)
    ├── /documents/upload    → Document Upload (authGuard) [Sprint 4 Week 4]
    ├── /documents/query     → Document Q&A (authGuard) [Sprint 4 Week 4]
    ├── /documents/:id       → Document Detail (authGuard) [Sprint 4 Week 4]
    ├── /cx/copilot          → CX Copilot Chat (authGuard) [Sprint 4 Week 4]
    ├── /fraud/correlations/:claimId → Fraud Correlations (authGuard) [Sprint 4 Week 4]
    └── /claims/batch        → Batch Claims CSV Upload (authGuard) [Sprint 5]
         |
.NET 10 Web API (Port 5143)
    |
    ├── v1 Controller API:  POST /api/sentiment/analyze (frozen)
    ├── v2 Minimal API + CQRS (MediatR):
    │   ├── POST /api/insurance/analyze        → AnalyzeInsuranceCommand
    │   ├── GET  /api/insurance/dashboard       → GetDashboardQuery
    │   ├── GET  /api/insurance/history         → GetHistoryQuery
    │   ├── GET  /api/insurance/health
    │   ├── POST /api/insurance/claims/triage   → TriageClaimCommand
    │   ├── POST /api/insurance/claims/upload   → UploadClaimEvidenceCommand
    │   ├── GET  /api/insurance/claims/{id}     → GetClaimQuery
    │   ├── GET  /api/insurance/claims/history  → GetClaimsHistoryQuery
    │   ├── POST /api/insurance/fraud/analyze   → AnalyzeFraudCommand
    │   ├── GET  /api/insurance/fraud/score/{id}→ GetFraudScoreQuery
    │   ├── GET  /api/insurance/fraud/alerts    → GetFraudAlertsQuery
    │   ├── GET  /api/insurance/health/providers→ GetProviderHealthQuery
    │   │   --- Sprint 4 (COMPLETE) ---
    │   ├── POST /api/insurance/documents/upload → UploadDocumentCommand
    │   ├── POST /api/insurance/documents/query  → QueryDocumentCommand
    │   ├── GET  /api/insurance/documents/{id}   → GetDocumentQuery
    │   ├── GET  /api/insurance/documents/history → GetDocumentHistoryQuery
    │   ├── DELETE /api/insurance/documents/{id}  → DeleteDocumentCommand
    │   ├── POST /api/insurance/cx/chat          → CX ChatCommand
    │   ├── POST /api/insurance/cx/stream        → CX StreamCommand (SSE)
    │   ├── POST /api/insurance/fraud/correlate  → CorrelateClaimsCommand
    │   ├── GET  /api/insurance/fraud/correlations/{claimId} → GetCorrelationsQuery
    │   ├── PATCH /api/insurance/fraud/correlations/{id}/review → ReviewCorrelationCommand
    │   ├── DELETE /api/insurance/fraud/correlations/{claimId} → DeleteCorrelationsCommand
    │   │   --- Sprint 5 (IN PROGRESS) ---
    │   ├── POST /api/insurance/claims/batch      → BatchClaimUploadCommand (CSV)
    │   ├── POST /api/insurance/documents/synthetic-qa → SyntheticQACommand
    │   └── GET  /api/insurance/cx/history         → GetCxHistoryQuery
    │
    ├── PII Redaction Service (before external AI calls + DB storage)
    ├── Global Exception Handler (IExceptionHandler)
    │
    └── Agent Orchestration (Semantic Kernel AgentGroupChat)
         ├── CTO Agent (orchestrator, synthesizer)
         ├── BA Agent (insurance domain analysis)
         ├── Developer Agent (JSON formatting)
         ├── QA Agent (validation, consistency)
         ├── AI Expert Agent (model/cloud/training)
         ├── Architect Agent (storage, performance)
         ├── UX Designer Agent (screen design, accessibility)
         ├── Claims Triage Agent (severity, urgency, actions)
         ├── Fraud Detection Agent (fraud scoring, SIU referral)
         └── Document Query Agent (RAG-based Q&A) [Sprint 4 Week 2]
              |
         IResilientKernelProvider (7-Provider Fallback)
         ├── Groq (primary - Llama 3.3 70B, 250 req/day free)
         ├── Cerebras (secondary - fast inference)
         ├── Mistral (tertiary - 500K tokens/month free)
         ├── Gemini (quaternary - 60 req/min free)
         ├── OpenRouter ($1 free credit)
         ├── OpenAI (paid fallback)
         └── Ollama (local fallback - unlimited, PII-safe)
              |
         Multimodal Services
         ├── Deepgram STT (speech-to-text)
         ├── Azure Vision (image analysis, primary)
         ├── Cloudflare Vision (image analysis, fallback)
         ├── OCR.space (document OCR)
         └── HuggingFace NER (entity extraction)
              |
         Embedding Services [Sprint 4 Week 2, expanded Sprint 5]
         ├── Voyage AI (voyage-finance-2, 1024-dim, finance-optimized)
         ├── Cohere (embed-english-v3.0) [Sprint 5]
         ├── Gemini (text-embedding-004) [Sprint 5]
         ├── HuggingFace (sentence-transformers) [Sprint 5]
         ├── Jina (jina-embeddings-v3) [Sprint 5]
         └── Ollama nomic-embed-text (local fallback)
              |
         SQLite (development) / Supabase PostgreSQL (production)
              |
         Document Intelligence (RAG) [Sprint 4 Week 2]
         ├── DocumentRecord + DocumentChunkRecord (SQLite vector store)
         ├── Cosine similarity via System.Numerics.Vector SIMD
         └── Insurance-aware chunking (DECLARATIONS/COVERAGE/EXCLUSIONS/CONDITIONS/ENDORSEMENTS)
```

---

## Technology Stack

| Layer | Technology | Notes |
|-------|-----------|-------|
| **Frontend** | Angular 21, TypeScript 5.9, Tailwind CSS 3.4 | Standalone components, signals, Vitest |
| **Backend** | .NET 10, C# 13, ASP.NET Core | Minimal API + Controllers hybrid |
| **CQRS** | MediatR 14.0 | Commands and Queries pattern |
| **Agent System** | Microsoft Semantic Kernel 1.71.0 | AgentGroupChat, custom strategies |
| **AI Providers** | Groq, Cerebras, Mistral, Gemini, OpenRouter, OpenAI, Ollama | 7-provider LLM fallback chain |
| **Database** | EF Core 10 + SQLite / Supabase PostgreSQL | Repository pattern, dual provider |
| **Auth** | Supabase JWT (optional) | JwtBearer middleware |
| **PII Security** | PIIRedactionService | SSN, policy#, claim#, phone, email |
| **Testing** | xUnit + Moq (backend), Vitest (frontend), Playwright (E2E) | ~530 backend, ~443 frontend, ~450 E2E (~1,423 total — Sprint 5 in progress) |

---

## Project Structure

```
SentimentAnalyzer/
├── Backend/
│   ├── Controllers/SentimentController.cs     # v1 (FROZEN - never modify)
│   ├── Endpoints/
│   │   ├── InsuranceEndpoints.cs              # v2 Minimal API + MediatR
│   │   ├── ClaimsEndpoints.cs                 # Claims triage + evidence upload
│   │   ├── BatchClaimEndpoints.cs             # Batch CSV claims upload [Sprint 5]
│   │   ├── FraudEndpoints.cs                  # Fraud analysis + alerts
│   │   └── ProviderHealthEndpoints.cs         # Provider health monitoring
│   ├── Features/
│   │   ├── Insurance/
│   │   │   ├── Commands/AnalyzeInsuranceCommand.cs
│   │   │   └── Queries/ (GetDashboardQuery, GetHistoryQuery)
│   │   ├── Claims/
│   │   │   ├── Commands/ (TriageClaimCommand, UploadClaimEvidenceCommand)
│   │   │   └── Queries/ (GetClaimQuery, GetClaimsHistoryQuery)
│   │   ├── Fraud/
│   │   │   ├── Commands/AnalyzeFraudCommand.cs
│   │   │   └── Queries/ (GetFraudScoreQuery, GetFraudAlertsQuery)
│   │   └── Health/Queries/GetProviderHealthQuery.cs
│   ├── Data/
│   │   ├── InsuranceAnalysisDbContext.cs       # EF Core DbContext (6 DbSets)
│   │   ├── IAnalysisRepository.cs             # Sentiment analysis repository
│   │   ├── SqliteAnalysisRepository.cs        # SQLite implementation
│   │   ├── IClaimsRepository.cs               # Claims domain repository
│   │   ├── SqliteClaimsRepository.cs          # Claims SQLite implementation
│   │   ├── ICxConversationRepository.cs       # CX conversation persistence [Sprint 5]
│   │   ├── SqliteCxConversationRepository.cs  # CX conversation SQLite impl [Sprint 5]
│   │   └── Entities/
│   │       ├── AnalysisRecord.cs              # Sentiment analysis entity
│   │       ├── ClaimRecord.cs                 # Claims triage entity
│   │       ├── ClaimEvidenceRecord.cs         # Multimodal evidence entity
│   │       ├── ClaimActionRecord.cs           # Recommended actions entity
│   │       └── CxConversationRecord.cs        # CX conversation memory entity [Sprint 5]
│   ├── Models/                                 # Request/Response DTOs
│   ├── Services/
│   │   ├── PIIRedactionService.cs             # PII redaction (mandatory)
│   │   ├── Claims/
│   │   │   ├── ClaimsOrchestrationService.cs  # Claims triage facade
│   │   │   ├── MultimodalEvidenceProcessor.cs # MIME routing + NER
│   │   │   ├── BatchClaimService.cs           # Batch CSV claims processing [Sprint 5]
│   │   │   └── IBatchClaimService.cs          # Batch claims interface [Sprint 5]
│   │   ├── Fraud/FraudAnalysisService.cs      # Fraud scoring facade
│   │   ├── Documents/
│   │   │   ├── BM25Scorer.cs                  # BM25 keyword scoring for hybrid retrieval [Sprint 5]
│   │   │   └── HybridRetrievalService.cs      # BM25 + vector semantic retrieval (α=0.7/β=0.3) [Sprint 5]
│   │   ├── Embeddings/
│   │   │   ├── ResilientEmbeddingProvider.cs   # 6-provider embedding fallback chain
│   │   │   ├── CohereEmbeddingService.cs      # Cohere embed-english-v3.0 [Sprint 5]
│   │   │   ├── GeminiEmbeddingService.cs      # Gemini text-embedding-004 [Sprint 5]
│   │   │   ├── HuggingFaceEmbeddingService.cs # HuggingFace sentence-transformers [Sprint 5]
│   │   │   └── JinaEmbeddingService.cs        # Jina embeddings-v3 [Sprint 5]
│   │   ├── ISentimentService.cs               # v1 (frozen)
│   │   └── OpenAISentimentService.cs          # v1 (frozen)
│   ├── Middleware/GlobalExceptionHandler.cs
│   └── Program.cs                              # DI, middleware, endpoints
│
├── Agents/
│   ├── Configuration/                          # Agent + LLM settings
│   ├── Definitions/AgentDefinitions.cs         # System prompts (9 agents)
│   ├── Orchestration/
│   │   ├── InsuranceAnalysisOrchestrator.cs    # Profile-aware AgentGroupChat pipeline
│   │   ├── AgentSelectionStrategy.cs           # Turn-taking strategy
│   │   └── AnalysisTerminationStrategy.cs      # ANALYSIS_COMPLETE signal
│   ├── Plugins/                                # SK plugins
│   └── Models/
│       ├── AgentAnalysisResult.cs              # Agent output (+ ClaimTriage, FraudAnalysis)
│       ├── ClaimTriageDetail.cs                # Claims triage output model
│       └── FraudAnalysisDetail.cs              # Fraud detection output model
│
├── Domain/
│   ├── Enums/                                  # SentimentType, CustomerPersona, etc.
│   └── Models/                                 # Shared domain models
│
├── Frontend/sentiment-analyzer-ui/
│   └── src/app/
│       ├── components/ (22 total)
│       │   ├── landing/                          # Public landing page (parallax enhancements Sprint 5)
│       │   ├── sentiment-analyzer/               # v1 general analyzer (legacy)
│       │   ├── insurance-analyzer/               # v2 insurance analysis UI
│       │   ├── dashboard/                        # Analytics dashboard (Chart.js charts)
│       │   ├── claims-triage/                    # Claims triage form + result display
│       │   ├── claim-result/                     # Claim detail view by ID
│       │   ├── evidence-viewer/                  # Multimodal evidence child component
│       │   ├── claims-history/                   # Filterable/paginated claims table
│       │   ├── provider-health/                  # LLM + multimodal service health monitor
│       │   ├── fraud-alerts/                     # High-risk fraud alert cards
│       │   ├── history-panel/                    # Analysis history panel
│       │   ├── login/                            # Supabase auth login
│       │   ├── nav/                              # Navigation bar (theme toggle, mobile menu)
│       │   ├── document-upload/                  # Document upload with drag-drop + category selector (Sprint 4)
│       │   ├── document-query/                   # RAG Q&A with citations + confidence gauge (Sprint 4)
│       │   ├── document-result/                  # Document detail + chunks browser + inline Q&A (Sprint 4)
│       │   ├── cx-copilot/                       # CX Copilot SSE streaming chat (Sprint 4)
│       │   ├── fraud-correlation/                # Cross-claim fraud correlation + review workflow (Sprint 4)
│       │   ├── batch-upload/                     # Batch CSV claims upload (Sprint 5)
│       │   ├── breadcrumb/                       # Breadcrumb navigation (Sprint 5)
│       │   ├── command-palette/                  # Ctrl+K command palette (Sprint 5)
│       │   └── toast/                            # Toast notification system (Sprint 5)
│       ├── services/ (sentiment, insurance, claims, document, customer-experience, fraud-correlation, auth, theme, analysis-state, breadcrumb, command-registry, scroll, toast)
│       ├── models/ (sentiment.model, insurance.model, claims.model, document.model)
│       ├── guards/ (auth.guard, guest.guard)
│       └── interceptors/ (auth.interceptor, error.interceptor)
│   └── e2e/ (25 spec files, ~450 tests)
│       ├── fixtures/mock-data.ts                 # Realistic insurance mock API responses
│       ├── helpers/api-mocks.ts                  # page.route() interceptors for all endpoints
│       ├── navigation.spec.ts                    # Route navigation, mobile menu
│       ├── sentiment-analyzer.spec.ts            # v1 sentiment analysis flow
│       ├── insurance-analyzer.spec.ts            # v2 insurance analysis
│       ├── dashboard.spec.ts                     # Dashboard metrics, charts
│       ├── login.spec.ts                         # Login/register form UX
│       ├── theme.spec.ts                         # Theme cycling, persistence
│       ├── accessibility.spec.ts                 # axe-core WCAG AA + ARIA (15 routes)
│       ├── claims-triage.spec.ts                 # Claims triage flow + errors
│       ├── claims-detail.spec.ts                 # Claim detail view
│       ├── claims-history.spec.ts                # History table + filters + pagination
│       ├── provider-health.spec.ts               # Provider health cards
│       ├── fraud-alerts.spec.ts                  # Fraud alert cards
│       ├── document-upload.spec.ts               # Document upload + type selector (Sprint 4)
│       ├── document-query.spec.ts                # RAG Q&A + source citations (Sprint 4)
│       ├── cx-copilot.spec.ts                    # SSE streaming chat + escalation (Sprint 4)
│       ├── fraud-correlation.spec.ts             # Cross-claim correlation + review (Sprint 4)
│       ├── batch-upload.spec.ts                  # Batch CSV claims upload (Sprint 5)
│       ├── breadcrumbs.spec.ts                   # Breadcrumb navigation (Sprint 5)
│       ├── command-palette.spec.ts               # Ctrl+K command palette (Sprint 5)
│       ├── cx-copilot-memory.spec.ts             # CX conversation memory (Sprint 5)
│       ├── micro-interactions.spec.ts            # Micro-interactions + animations (Sprint 5)
│       ├── parallax-landing.spec.ts              # Parallax landing page (Sprint 5)
│       └── toast.spec.ts                         # Toast notifications (Sprint 5)
│
├── Tests/ (~530 tests)
│   ├── SentimentControllerTests.cs             # v1 regression (9 tests - FROZEN)
│   ├── InsuranceAnalysisControllerTests.cs     # CQRS handler tests (27 tests)
│   ├── PIIRedactionTests.cs                    # PII redaction tests (11 tests)
│   ├── UnitTest1.cs                            # Placeholder (1 test)
│   ├── OrchestrationProfileFactoryTests.cs     # Profile → agent mapping
│   ├── ProviderConfigurationTests.cs           # LLM provider config
│   ├── ResilientKernelProviderTests.cs         # 5-provider fallback chain
│   ├── HuggingFaceNerServiceTests.cs           # NER entity extraction
│   ├── DeepgramServiceTests.cs                 # Speech-to-text
│   ├── AzureVisionServiceTests.cs              # Azure Vision image analysis
│   ├── CloudflareVisionServiceTests.cs         # Cloudflare Vision fallback
│   ├── OcrSpaceServiceTests.cs                 # OCR document extraction
│   ├── CriticalFixTests.cs                     # Sprint 1 critical fixes
│   ├── FinBertSentimentServiceTests.cs         # FinBERT pre-screening (8 tests)
│   ├── AnalyzeInsurancePreScreenTests.cs       # FinBERT handler integration (6 tests)
│   ├── ClaimsOrchestrationServiceTests.cs      # Claims triage (10 tests)
│   ├── MultimodalEvidenceProcessorTests.cs     # MIME routing + fallback (10 tests)
│   ├── FraudAnalysisServiceTests.cs            # Fraud scoring (6 tests)
│   ├── TriageClaimHandlerTests.cs              # Claims command handler (5 tests)
│   ├── UploadClaimEvidenceHandlerTests.cs      # Evidence upload (5 tests)
│   ├── ClaimsRepositoryTests.cs                # Claims DB persistence (6 tests)
│   ├── GetClaimHandlerTests.cs                 # Claims query handler (4 tests)
│   ├── FraudCommandsTests.cs                   # Fraud commands (4 tests)
│   ├── ProviderHealthTests.cs                  # Provider health (5 tests)
│   ├── BatchClaimServiceTests.cs               # Batch CSV claims processing [Sprint 5]
│   ├── BM25ScorerTests.cs                      # BM25 keyword scoring [Sprint 5]
│   ├── HybridRetrievalServiceTests.cs          # Hybrid BM25+vector retrieval [Sprint 5]
│   ├── CxConversationMemoryTests.cs            # CX conversation persistence [Sprint 5]
│   ├── CohereEmbeddingServiceTests.cs          # Cohere embedding provider [Sprint 5]
│   ├── GeminiEmbeddingServiceTests.cs          # Gemini embedding provider [Sprint 5]
│   ├── HuggingFaceEmbeddingServiceTests.cs     # HuggingFace embedding provider [Sprint 5]
│   └── JinaEmbeddingServiceTests.cs            # Jina embedding provider [Sprint 5]
│
├── .github/
│   └── workflows/ci.yml                        # GitHub Actions CI/CD (3 parallel jobs) [Sprint 5]
├── PROJECT_CONTEXT.md (this file)
├── SPRINT-ROADMAP.md
├── REVIEW.md
├── QA_REPORT.md
└── README.md
```

---

## Running the Project

### Prerequisites
- .NET 10 SDK
- Node.js 22+ and npm 11+
- Groq API key (free at console.groq.com)

### Backend
```bash
cd SentimentAnalyzer/Backend
dotnet run    # http://localhost:5143
```

### Frontend
```bash
cd SentimentAnalyzer/Frontend/sentiment-analyzer-ui
npm install
npm start     # http://localhost:4200
```

### Tests
```bash
# Backend
dotnet test SentimentAnalyzer/Tests/SentimentAnalyzer.Tests.csproj

# Frontend
cd SentimentAnalyzer/Frontend/sentiment-analyzer-ui
npm test
```

---

## Configuration

### appsettings.json
- `AgentSystem:Provider` — `Groq` | `Ollama` | `Gemini` | (default: OpenAI)
- `Database:Provider` — `Sqlite` (default) | `PostgreSQL` (Supabase)
- `ConnectionStrings:DefaultConnection` — SQLite file or PostgreSQL connection string
- `Supabase:JwtSecret` — Enables JWT auth when set (leave empty to disable)

### For Supabase (production)
```json
{
  "Database": { "Provider": "PostgreSQL" },
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password"
  },
  "Supabase": {
    "Url": "https://your-project.supabase.co",
    "JwtSecret": "your-jwt-secret"
  }
}
```

---

## API Endpoints

### v1 (frozen)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/sentiment/analyze` | Generic sentiment analysis |
| GET | `/api/sentiment/health` | Health check |

### v2 (Insurance — Sentiment Analysis)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/insurance/analyze` | Multi-agent insurance analysis |
| GET | `/api/insurance/dashboard` | Aggregated metrics + distribution |
| GET | `/api/insurance/history?count=20` | Recent analysis history |
| GET | `/api/insurance/health` | Health check |

### v2 (Claims & Fraud Pipeline — Sprint 2)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/insurance/claims/triage` | Submit claim for AI triage |
| POST | `/api/insurance/claims/upload` | Upload multimodal evidence |
| GET | `/api/insurance/claims/{id}` | Retrieve claim triage result |
| GET | `/api/insurance/claims/history` | List claims with filters + pagination |
| POST | `/api/insurance/fraud/analyze` | Deep fraud analysis on a claim |
| GET | `/api/insurance/fraud/score/{claimId}` | Get fraud score for a claim |
| GET | `/api/insurance/fraud/alerts` | List high-risk fraud alerts |
| GET | `/api/insurance/health/providers` | Provider health monitoring |

### v2 (Document Intelligence — Sprint 4 Week 2 LIVE)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/insurance/documents/upload` | Upload document for RAG indexing (OCR → chunk → embed → store) |
| POST | `/api/insurance/documents/query` | Query documents with natural language (embed → vector search → LLM answer with citations) |
| GET | `/api/insurance/documents/{id}` | Retrieve document metadata + chunks |
| GET | `/api/insurance/documents/history` | List indexed documents with pagination |
| DELETE | `/api/insurance/documents/{id}` | Delete document and its chunks |

### v2 (CX Copilot — Sprint 4 Week 3 LIVE)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/insurance/cx/chat` | CX Copilot chat (non-streaming) |
| POST | `/api/insurance/cx/stream` | CX Copilot SSE streaming chat (PII redacted, tone classification, escalation detection) |

### v2 (Fraud Correlation — Sprint 4 Week 3 LIVE)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/insurance/fraud/correlate` | Trigger cross-claim correlation analysis |
| GET | `/api/insurance/fraud/correlations/{claimId}` | Get correlations for a claim (4-strategy: DateProximity, SimilarNarrative, SharedFlags, SameSeverity) |
| PATCH | `/api/insurance/fraud/correlations/{id}/review` | Review correlation (Confirm/Dismiss with reason) |
| DELETE | `/api/insurance/fraud/correlations/{claimId}` | Delete all correlations for a claim |

### v2 (Batch Claims + CX Memory + Synthetic QA — Sprint 5 IN PROGRESS)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/insurance/claims/batch` | Batch CSV claims upload (parse → triage → store) |
| POST | `/api/insurance/documents/synthetic-qa` | Generate synthetic Q&A pairs from indexed documents |
| GET | `/api/insurance/cx/history` | Retrieve CX conversation history (persisted memory) |

---

## v1 Frozen Files (NEVER modify)

- `SentimentController.cs`, `SentimentRequest.cs`, `SentimentResponse.cs`
- `ISentimentService.cs`, `OpenAISentimentService.cs`
- `SentimentControllerTests.cs`
- `sentiment.model.ts`, `sentiment.service.ts`

---

---

# CHANGELOG

All sessions, reviews, decisions, and changes are logged here in reverse chronological order.

---

## [2026-02-28] Sprint 5: Hybrid RAG + Batch Claims + CX Memory + UX Enhancements (IN PROGRESS)

### What Was Built

**Backend Features:**
- **Batch Claims CSV Upload**: `BatchClaimService` + `IBatchClaimService` + `BatchClaimEndpoints` — parse CSV, validate rows, triage each claim, store results
- **CX Conversation Memory**: `CxConversationRecord` entity + `ICxConversationRepository` + `SqliteCxConversationRepository` — persist chat history across sessions
- **Hybrid RAG Retrieval**: `BM25Scorer` (Okapi BM25 keyword scoring) + `HybridRetrievalService` — combines BM25 keyword search (beta=0.3) with vector semantic search (alpha=0.7) for improved document retrieval accuracy
- **4 New Embedding Providers**: Cohere (`embed-english-v3.0`), Gemini (`text-embedding-004`), HuggingFace (`sentence-transformers`), Jina (`jina-embeddings-v3`) — 6-provider chain: Voyage AI -> Cohere -> Gemini -> HuggingFace -> Jina -> Ollama
- **GitHub Actions CI/CD**: `.github/workflows/ci.yml` with 3 parallel jobs (backend-tests, frontend-unit-tests, e2e-tests)

**Frontend Features (4 new components, 4 new services):**
- **batch-upload**: Batch CSV claims upload component with file validation, progress tracking, result summary
- **breadcrumb**: Dynamic breadcrumb navigation component + `breadcrumb.service.ts` — route-aware hierarchy
- **command-palette**: Ctrl+K command palette component + `command-registry.service.ts` — global action search
- **toast**: Toast notification system component + `toast.service.ts` — signal-based success/error/warning/info toasts
- **scroll.service.ts**: Scroll position tracking and smooth navigation
- **Parallax landing enhancements**: Improved parallax effects on landing page hero and sections
- **Landing page spec**: Unit test coverage for landing component

**New Endpoints (3):**
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/insurance/claims/batch` | Batch CSV claims upload |
| POST | `/api/insurance/documents/synthetic-qa` | Synthetic QA generation from indexed documents |
| GET | `/api/insurance/cx/history` | CX conversation history retrieval |

**New E2E Spec Files (7):**
- `batch-upload.spec.ts`, `breadcrumbs.spec.ts`, `command-palette.spec.ts`, `cx-copilot-memory.spec.ts`, `micro-interactions.spec.ts`, `parallax-landing.spec.ts`, `toast.spec.ts`

**New Backend Test Files (8):**
- `BatchClaimServiceTests.cs`, `BM25ScorerTests.cs`, `HybridRetrievalServiceTests.cs`, `CxConversationMemoryTests.cs`, `CohereEmbeddingServiceTests.cs`, `GeminiEmbeddingServiceTests.cs`, `HuggingFaceEmbeddingServiceTests.cs`, `JinaEmbeddingServiceTests.cs`

### Architecture Changes
- **LLM Providers**: 5 -> 7 (added Cerebras, OpenAI to chain: Groq -> Cerebras -> Mistral -> Gemini -> OpenRouter -> OpenAI -> Ollama)
- **Embedding Providers**: 2 -> 6 (added Cohere, Gemini, HuggingFace, Jina)
- **Angular Components**: 18 -> 22
- **Routes**: 15 -> 16
- **E2E Spec Files**: 16 -> 25 (including landing page spec)

### Test Counts (Sprint 5 — In Progress)
| Suite | Count | Status |
|-------|-------|--------|
| Backend (xUnit) | ~530 | IN PROGRESS |
| Frontend (Vitest) | ~443 | IN PROGRESS |
| E2E (Playwright) | ~450 | IN PROGRESS |
| **Total** | **~1,423** | **IN PROGRESS** |

---

## [2026-02-26] Sprint 4 Week 4: Frontend + E2E + MCP + Documentation (COMPLETE)

### What Was Built

**5 new Angular components, 3 new services, 1 model file, 4 E2E spec files, 36 unit tests, 94 E2E tests:**

#### New Components (5)
- **document-upload**: Drag-and-drop file upload with category selector (Policy/Claim/Endorsement/Correspondence/Other), multi-phase loading animation, 5MB/PDF/PNG/JPEG/TIFF validation, result card with actions
- **document-query**: RAG Q&A textarea with optional document filter, confidence gauge (green/yellow/red), expandable citation accordion with similarity bars, LLM provider + elapsed time
- **document-result**: Document detail view by ID, metadata header with category badges, chunks browser with expandable content, inline Q&A scoped to document, delete with confirmation modal
- **cx-copilot**: Chat interface with SSE streaming (POST-based ReadableStream, not EventSource), user messages right/AI left, tone badges (Professional/Empathetic/Urgent/Informational), escalation pulsing badge, optional claim context, Ctrl+Enter send, disclaimer footer
- **fraud-correlation**: Correlation cards with split-card design (source vs correlated claim), 4 strategy badges (DateProximity/SimilarNarrative/SharedFlags/SameSeverity), score gauge, status filter tabs, review workflow (Confirm/Dismiss with reason modal), "Run New Analysis" button

#### New Services (3)
- **document.service.ts**: uploadDocument, queryDocuments, getDocumentById, getDocumentHistory, deleteDocument
- **customer-experience.service.ts**: chat (POST), streamChat (POST → SSE via raw fetch + ReadableStream)
- **fraud-correlation.service.ts**: correlate, getCorrelations, reviewCorrelation, deleteCorrelations

#### Routes + Navigation
- 5 new routes: `/documents/upload`, `/documents/query`, `/documents/:id`, `/cx/copilot`, `/fraud/correlations/:claimId`
- Nav updated: "Documents" dropdown (Upload + Query), "CX Copilot" link, mobile drawer mirrored
- Total: 15 routes (was 10), 18 components (was 13)

#### E2E Tests (4 new spec files)
- `document-upload.spec.ts`, `document-query.spec.ts`, `cx-copilot.spec.ts`, `fraud-correlation.spec.ts`
- `accessibility.spec.ts` updated: 15 routes scanned (was 10)
- SSE mock pattern: Playwright `page.route()` with `text/event-stream` content type + pre-composed event chunks

#### 3-Iteration Adversarial Review
- **Iteration 1**: 12 issues found (3 Critical, 5 High, 4 Medium) — all Critical+High fixed
- **Iteration 2**: Remaining High fixes + 4 additional issues resolved
- **Iteration 3**: Final review found 12 polish items (1 High, 6 Medium, 5 Low) — top 4 fixed, shipped

Key fixes: `@for` track expressions use incremental counter (not Date), SSE complete handler assigns unique msg ID, `filteredCorrelations` converted to `computed()` signal, per-correlation `reviewingId` signal (not global boolean), reactive `route.params` Observable, nav outside-click close, escape key for modals, NaN guard on route params, dynamic reviewer from AuthService

### Test Counts (Post-Sprint 4 Week 4)
| Suite | Count | Status |
|-------|-------|--------|
| Backend (xUnit) | 461 | ALL PASS |
| Frontend (Vitest) | 235 | ALL PASS |
| E2E (Playwright) | 357 | ALL PASS (9 skipped) |
| **Total** | **1,053** | **0 failures** |

### Files Changed
- **21 new files** (1 model, 3 services, 5 components, 8 unit specs, 4 E2E specs)
- **8 modified files** (routes, nav, mock-data, api-mocks, accessibility.spec, SPRINT-ROADMAP, CLAUDE.md, docs)

---

## [2026-02-25] Sprint 4 Weeks 1-3: Document Intelligence RAG + CX Copilot + Fraud Correlation (COMPLETE)

### Sprint 4 Brainstorming (9-Agent, 3 Iterations — Unanimous APPROVE)

All 9 agents brainstormed Sprint 4 scope across 3 iterations. Final consensus:

**Week 1 — P0/P1 Technical Debt (MUST-HAVE) — COMPLETE:**
- Orchestrator unit tests (0% → 60%+ coverage) — 15+ tests for `InsuranceAnalysisOrchestrator.cs`
- V1 PII fix via decorator pattern (`PiiRedactingSentimentService` wrapping `ISentimentService`)
- PII regression tests (5 tests querying DB for leaked patterns)
- Per-endpoint rate limiting (analyze: 10/min, triage: 5/min, fraud: 5/min)
- Accessibility fixes (color contrast, keyboard traps, `aria-live` regions)

**Week 2 — Document Intelligence RAG Foundation (MUST-HAVE) — COMPLETE:**
- Voyage AI embedding service (`voyage-finance-2`, 1024-dim) + Ollama fallback
- RAG database schema: `DocumentRecord` + `DocumentChunkRecord` + `SqliteDocumentRepository`
- Insurance-aware document chunking (section headers + sentence-boundary splitting)
- Document Intelligence facade service (upload → OCR → chunk → embed → store; query → embed → search → LLM)
- 5 API endpoints + MediatR handlers
- `DocumentQuery` agent prompt + orchestration profile

**Week 3 — CX Copilot + Fraud Correlation (SHOULD-HAVE) — COMPLETE:**
- Customer Experience Copilot with SSE streaming and `CustomerExperience` orchestration profile
- Cross-claim fraud correlation (4-strategy: DateProximity, SimilarNarrative, SharedFlags, SameSeverity)
- Claim-type-specific windows (Auto 90d, Property/Liability 180d, WorkersComp 365d)
- Review workflow (Pending/Confirmed/Dismissed)

**Week 4 — Frontend + E2E + Documentation (SHOULD-HAVE) — COMPLETE:**
- 5 new Angular components, 3 services, 4 E2E spec files
- All MD files updated, MCP servers configured

**Test Targets:** 740 → 1,053 (313 new tests across 20+ files) — EXCEEDED

---

## [2026-02-24] Sprint 3: Frontend + Dashboard + E2E + Landing Page

### What Was Done

**Full frontend buildout wiring all Sprint 2 backend capabilities + interactive public landing page:**

#### Landing Page (Public Platform Showcase)
- New `LandingComponent` (1,726 lines) at root `/` — public, no auth required
- 7 interactive sections: Hero, Agent Orchestration (9 agents), Provider Fallback Chain (5 LLM), Multimodal Pipeline (4 tabs), Interactive Demo, PII Security, Stats & Tech Grid
- IntersectionObserver scroll-triggered animations, 3-theme compatibility, `prefers-reduced-motion` support
- Sentiment Analyzer moved from `/` to `/sentiment` (authGuard)

#### New Angular Components (7 new + landing)
- `claims-triage`: Submit claims with text + file upload, inline triage result display
- `claim-result`: Full claim detail view by ID (severity, fraud gauge, actions, evidence)
- `evidence-viewer`: Child component for multimodal evidence (image/audio/PDF)
- `claims-history`: Filterable/paginated claims table with severity/status/date filters
- `provider-health`: LLM + multimodal service health monitor with auto-refresh
- `fraud-alerts`: High-risk fraud alert cards with SIU referral indicators
- `landing`: Interactive platform showcase (described above)

#### Dashboard Expansion (Chart.js)
- Severity distribution doughnut chart (ng2-charts)
- Customer persona horizontal bar chart
- Quick links cards row (Claims Triage, History, Provider Health, Fraud Alerts)

#### Navigation + Routes
- 6 new routes: `/sentiment`, `/claims/triage`, `/claims/history`, `/claims/:id`, `/dashboard/providers`, `/dashboard/fraud`
- Desktop + mobile nav updated with Claims section and expanded Dashboard sub-links
- 10 total routes (was 4)

#### Claims Service + Models
- `claims.model.ts`: Full TypeScript interfaces matching all Sprint 2 backend response models
- `claims.service.ts`: 8 HTTP methods mapping to all Sprint 2 API endpoints

#### E2E Test Suite Expansion (Playwright)
- 5 new E2E spec files: claims-triage, claims-detail, claims-history, provider-health, fraud-alerts
- Updated: accessibility.spec.ts (all 9 routes), navigation.spec.ts (landing + sentiment split), sentiment-analyzer.spec.ts (route change)
- Extended mock data and API interceptors for all new endpoints

#### BA Validation (3 Iterations)
- Iteration 1 (B+): 12 issues found, all High/Medium fixed
- Iteration 2 (A-): 6 remaining, all Low/Informational
- Iteration 3 (A): SHIP approved, 0 blocking issues

### Test Counts (Post-Sprint 3)
- Backend: **246 tests** across 24 files — 0 regressions (0 backend changes)
- Frontend unit: **196 tests** across 20 spec files (was 126 across 14)
- E2E: **239 passed**, 9 skipped across 12 spec files (was ~138 across 7)
- New npm packages: 2 (ng2-charts, chart.js)

### Files Changed
- **~50 new files** (8 components × 3 files + models + service + 6 unit specs + 5 e2e specs + mock data)
- **8 modified files** (routes, nav, dashboard, api-mocks, mock-data, navigation/sentiment/accessibility specs)

---

## [2026-02-23] Sprint 2: Claims & Fraud Pipeline + API Endpoints

### What Was Done

**Full claims processing pipeline wired end-to-end:**

#### Database Layer
- 3 new EF Core entities: `ClaimRecord`, `ClaimEvidenceRecord`, `ClaimActionRecord`
- `IClaimsRepository` + `SqliteClaimsRepository` with pagination support
- `PaginatedResponse<T>` generic wrapper (Items, TotalCount, Page, PageSize, TotalPages)

#### Profile-Aware Orchestration
- Replaced stub in `InsuranceAnalysisOrchestrator.AnalyzeAsync(text, profile)` with real profile-aware agent selection
- ClaimsTriage profile: 4 agents, 8 max turns | FraudScoring: 3 agents, 6 max turns
- JSON schema examples in `BuildProfileUserMessage` for consistent agent output
- New parsing for `claimTriage` and `fraudAnalysis` JSON blocks in agent result

#### Service Facades
- `ClaimsOrchestrationService`: text → PII redact → orchestrate → save → respond
- `MultimodalEvidenceProcessor`: MIME routing (image/audio/pdf → Vision/STT/OCR) + NER + vision fallback (Azure → Cloudflare)
- `FraudAnalysisService`: fraud scoring → SIU referral (score > 75) → alert flagging (score > 55)

#### 8 New MediatR Handlers
- Claims: TriageClaimCommand, UploadClaimEvidenceCommand, GetClaimQuery, GetClaimsHistoryQuery
- Fraud: AnalyzeFraudCommand, GetFraudScoreQuery, GetFraudAlertsQuery
- Health: GetProviderHealthQuery (LLM + multimodal service health)

#### 8 New API Endpoints
- Claims: POST triage, POST upload, GET by ID, GET history (with filters + pagination)
- Fraud: POST analyze, GET score, GET alerts
- Health: GET providers (LLM + multimodal)

#### Security
- PII redaction before DB storage in claims pipeline (not just before AI calls)
- Text truncation to 5000 chars before redaction + persistence

#### Agent Review (3 Iterations)
- All 9 agents reviewed implementation across 3 iterations
- Iteration 1: Vision fallback, pagination, PII before DB, fallback tests
- Iteration 2: All agents 9-9.5/10 — no actionable gaps
- Iteration 3: JSON schema in agent prompts for better compliance
- Final: All agents 9.5-10/10 satisfied

### Test Counts (Post-Update)
- Backend: **230 tests** across 22 files (173 Sprint 1 + 57 Sprint 2) — 0 regressions
- Frontend: **126 tests** across 14 spec files — all passing
- New test files: 9 (ClaimsOrchestrationService, MultimodalEvidence, FraudAnalysis, TriageHandler, UploadHandler, ClaimsRepository, GetClaimHandler, FraudCommands, ProviderHealth)

### Files Changed
- **40 new files** (entities, repository, models, services, handlers, endpoints, tests)
- **5 modified files** (DbContext, Program.cs, AgentSelectionStrategy, InsuranceAnalysisOrchestrator, AgentAnalysisResult)

---

## [2026-02-18] Full 6-Agent Collaboration Cycle + Quality Model Alignment

### What Was Done

**Round 1 — Blocking Fixes (All 6 agents reviewed, identified issues, and fixes implemented):**
- Quality model aligned across 3 layers: Agent `QualityMetadata` → API `QualityDetail` → Frontend `QualityDetail`
- Added `MapQuality()` adapter method to `AnalyzeInsuranceCommand.cs` (Issues, Suggestions, backward-compat Warnings)
- Added `QualityIssueDetail` to backend and `QualityIssue` interface to frontend
- API keys removed from `appsettings.json` (replaced with empty strings)
- Timer memory leak fixed in `InsuranceAnalyzerComponent` (`OnDestroy` + `stopElapsedTimer()`)
- All frontend services switched to `inject()` DI pattern
- PII redactor null warning added to `InsuranceAnalysisOrchestrator`
- Error recovery (Retry button) added to UI error state
- Always-visible recommendations section with empty state message
- ARIA accessibility: descriptive `aria-label` on sentiment badge and risk indicators
- Structured quality issues display with severity badges (error/warning/info)

**Round 2 — All agents re-reviewed and approved:**
- CTO: 10/10, BA: 9/10, Developer: 8/10, QA: 7/10, Architect: 8/10, UX: 9/10

**Post-Review:**
- 7 new `MapQuality` unit tests added (issues+suggestions, issues-only, suggestions-only, null quality, failed quality, empty quality, null lists)
- Design Patterns section (Section 8) added to CLAUDE.md — 7 patterns with Pattern Decision Matrix
- UX Designer Agent added to CLAUDE.md architecture and decision authority
- All MD files updated to reflect current state

### Test Counts (Post-Update)
- Backend: **48 tests** (SentimentControllerTests: 9, InsuranceAnalysisControllerTests: 27, PIIRedactionTests: 11, UnitTest1: 1)
- Frontend: **126 tests** across 14 spec files — all passing
- Both builds clean: backend 0 errors, frontend 575.34 kB

---

## [2026-02-17] CTO & Solution Architect Review Session

### What Was Done
- Launched 3 parallel review agents: Architecture & Agent System, API Design & Contracts, Frontend Architecture
- All 3 agents completed full codebase audit and reported findings to CTO
- CTO & Solution Architect synthesized consolidated review with prioritized action plan
- Validated all findings against actual source code

### Validated Findings

#### CRITICAL (P0)

| # | Issue | Location | Status |
|---|-------|----------|--------|
| 1 | **v1 controller sends raw text to OpenAI — no PII redaction** | `Backend/Controllers/SentimentController.cs:36` | OPEN |
| 2 | ~~**API keys in appsettings.Development.json**~~ (gitignored but keys should be rotated) | `Backend/appsettings.json` | **RESOLVED Feb 18** — Keys removed (empty strings) |

#### HIGH (P1)

| # | Issue | Location | Status |
|---|-------|----------|--------|
| 3 | **DB column InputText maxlength 2000, API allows 10,000** — silent data loss | `Backend/Data/Entities/AnalysisRecord.cs:14` vs `Backend/Endpoints/InsuranceEndpoints.cs:61` | OPEN |
| 4 | **Frontend v1 component: memory leak + manual ChangeDetectorRef** | `Frontend/.../sentiment-analyzer/sentiment-analyzer.ts:21,34,41` | OPEN (v2 InsuranceAnalyzer timer leak fixed Feb 18; v1 still uses ChangeDetectorRef) |
| 5 | **No rate limiting middleware** — free tier APIs unprotected | `Backend/Program.cs` — absent | OPEN |
| 6 | **GlobalExceptionHandler missing HttpRequestException** — AI provider failures unhandled | `Backend/Middleware/GlobalExceptionHandler.cs:27-36` | OPEN |

#### MEDIUM (P2)

| # | Issue | Location | Status |
|---|-------|----------|--------|
| 7 | **Health checks return static "healthy"** — no dependency verification | `Backend/Endpoints/InsuranceEndpoints.cs:30-36` | OPEN |
| 8 | **GetTopPersonasAsync queries DB twice** — inefficient | `Backend/Data/SqliteAnalysisRepository.cs:73,87` | OPEN |
| 9 | **No audit trail middleware** — CLAUDE.md requires request logging | `Backend/Program.cs` — absent | OPEN |
| 10 | **SentimentService hardcodes localhost URL** | `Frontend/.../services/sentiment.service.ts` | OPEN |

#### Overblown (Corrected)

| Finding | Reality |
|---------|---------|
| "API keys committed to git" | `.gitignore` correctly excludes `appsettings.Development.json` — not in version control |
| "Architect agent output ignored" | Output feeds into agent chat context for CTO to synthesize — by design |

### Approved Action Plan

#### Phase 1: Security Hardening (P0)
- [ ] 1.1 Add PII redaction to v1 controller (`_piiRedactor.Redact()` before OpenAI call)
- [x] 1.2 ~~Rotate exposed OpenAI + Groq API keys~~ — RESOLVED Feb 18: Keys removed from appsettings.json (empty strings)
- [ ] 1.3 Add `HttpRequestException` to `GlobalExceptionHandler` (return 502/503)

#### Phase 2: Data Integrity Fixes (P1)
- [ ] 2.1 Increase `AnalysisRecord.InputText` from 2000 to 10000 chars
- [ ] 2.2 Increase `Explanation` to 2000, `PolicyRecommendationsJson` to 5000
- [ ] 2.3 Recreate SQLite DB after schema changes

#### Phase 3: Operational Resilience (P1)
- [ ] 3.1 Add ASP.NET `RateLimiter` middleware (fixed window)
- [ ] 3.2 Real health checks — verify DB + AI provider
- [ ] 3.3 Optimize `GetTopPersonasAsync` to single query

#### Phase 4: Frontend Cleanup (P2)
- [ ] 4.1 Refactor v1 `SentimentAnalyzer` to signals + `inject()` + `takeUntilDestroyed()`
- [ ] 4.2 Remove `ChangeDetectorRef` and `console.log` debug statements
- [ ] 4.3 Move `SentimentService` API URL to environment config

#### Phase 5: Observability (P2)
- [ ] 5.1 Add request audit logging middleware (input SHA-256, provider, timestamp)
- [ ] 5.2 Log PII redaction events (count of redacted items per request)

---

---

# AGENT REVIEW REPORTS

Full reports from each review agent, preserved for reference.

---

## Agent 1: Architecture & Agent System Review

**Scope:** Overall project structure, backend architecture, agent system, database layer, AI providers, security, testing, configuration.

### Project Structure — Grade: A
- Clean separation: Backend, Agents, Domain, Frontend, Tests
- v1 API properly frozen, v2 follows industry patterns
- File-scoped namespaces, modern C# 13 patterns

### Backend Architecture — Grade: A-
- Program.cs: Clean DI, dual DB support, conditional JWT auth, CORS, auto-migration
- v1 SentimentController: Proper validation (empty text, 5000 char limit), error handling
- v2 InsuranceEndpoints: RouteGroupBuilder pattern, MediatR delegation, conditional auth
- CQRS Handlers: AnalyzeInsuranceCommand maps agent result to API response, persists to DB (non-blocking on failure)

### Agent System — Grade: A+
**9-Agent Pipeline:**
1. **CTO Agent** — Coordinates pipeline, ensures ANALYSIS_COMPLETE signal, synthesizes output
2. **BA Agent** — Sentiment + confidence, purchase intent (0-100), persona classification (6 types), journey stage (6 stages), risk indicators, emotion breakdown (8 types), policy recommendations
3. **Developer Agent** — Formats to strict JSON schema, validates field ranges, backward compat
4. **QA Agent** — Field completeness, range validation, logical consistency, domain rules, partial auth detection
5. **AI Expert Agent** — Model evaluation, training recommendations, responsible AI governance
6. **Architect Agent** — Storage recommendations, workflow triggers, dashboard metric updates
7. **UX Designer Agent** — Screen design, accessibility (WCAG 2.1 AA), design system governance, UX gap identification
8. **Claims Triage Agent** — Severity/urgency assessment, claim type classification, estimated loss range, recommended actions, preliminary fraud flags
9. **Fraud Detection Agent** — Fraud probability scoring (0-100), 5 indicator categories, SIU referral recommendations

**Orchestrator (InsuranceAnalysisOrchestrator):**
- Profile-aware agent selection (ClaimsTriage=4 agents/8 turns, FraudScoring=3 agents/6 turns, SentimentAnalysis=7 agents/14 turns)
- Automatic PII redaction before external calls
- 60s timeout with cancellation token
- Fallback to single-agent on multi-agent failure
- JSON extraction with brace-counting + validation
- JSON schema examples injected into agent prompts for consistent output
- Parses `claimTriage` and `fraudAnalysis` JSON blocks from agent output
- Terminates on "ANALYSIS_COMPLETE" or max turns per profile
- Deterministic speaking order via AgentSelectionStrategy

### Database Layer — Grade: B
- EF Core with SQLite/PostgreSQL dual provider
- Indexes on: CreatedAt, Sentiment, CustomerPersona, CustomerId, InteractionType
- Repository pattern with IAnalysisRepository
- **Issue:** InputText truncated to 2000 (API allows 10,000)
- **Issue:** JSON fields stored as strings (no query-ability)
- **Issue:** No soft deletes, no audit trail

### AI Provider Abstraction — Grade: A
- All providers use `AddOpenAIChatCompletion()` — clean abstraction
- Provider switch: Groq → Gemini → Ollama → OpenAI (fallback)
- Configuration-driven via `AgentSystem:Provider`

### Security — Grade: B-
- PIIRedactionService: Source-generated regex, 5 patterns (SSN, claim#, policy#, email, phone)
- 11 dedicated PII tests
- **GAP:** v1 controller does NOT call PII redaction
- **GAP:** No PII redaction middleware (only called explicitly in orchestrator)

### Testing — Grade: B+
- Backend: 48 tests across 4 files — SentimentControllerTests (9 tests, frozen), InsuranceAnalysisControllerTests (27 tests incl. 7 MapQuality), PIIRedactionTests (11 tests), UnitTest1 (1 test)
- Frontend: 126 tests across 14 spec files (incl. theme.service, error.interceptor, ux)
- Good AAA pattern, realistic insurance test data
- Estimated ~85% backend, ~80% frontend coverage
- **Gap:** Agent Orchestrator and Agent Strategies still at 0% coverage

---

## Agent 2: API Design & Contracts Review

**Scope:** Controllers, endpoints, models, Program.cs middleware pipeline, validation, rate limiting, health checks.

### v1/v2 Separation — EXCELLENT
- v1 runs on `/api/sentiment/*` via controllers
- v2 runs on `/api/insurance/*` via minimal APIs
- No shared routes, no contract breaks
- Both APIs coexist cleanly

### v2 Endpoint Validation — GOOD
- POST `/api/insurance/analyze`: Empty text, 10K char limit, InteractionType whitelist
- GET `/api/insurance/history`: Count parameter (clamped 1-100 in handler)
- GET `/api/insurance/health`: AllowAnonymous
- Conditional auth on all non-health endpoints

### Models & Serialization
- v1: Simple `SentimentRequest`/`SentimentResponse` — stable
- v2: 14+ interfaces with full type coverage — `InsuranceAnalysisResponse`, `InsuranceAnalysisDetail`, `RiskIndicatorDetail`, `QualityDetail`, `DashboardData`, `AnalysisHistoryItem`
- Uses System.Text.Json (no explicit `[JsonPropertyName]` — relies on implicit camelCase)

### Middleware Pipeline (Program.cs)
```
OpenApi (dev) → ExceptionHandler → HTTPS (prod) → CORS → Auth (conditional) → Authorization → Controllers (v1) → InsuranceEndpoints (v2)
```
- Global exception handler covers: UnauthorizedAccessException, SecurityTokenExpiredException, OperationCanceledException, ArgumentException, InvalidOperationException
- **Missing:** HttpRequestException (AI provider failures), rate limiting, audit logging

### Issues Found
1. Inconsistent text length limits: v1=5000, v2=10000 (no documented reason)
2. InteractionType whitelist hardcoded in InsuranceEndpoints.cs — duplicates Domain/Enums
3. Dashboard models missing XML documentation
4. No Content-Type validation (framework handles implicitly)
5. No API versioning headers (`api-version`)

---

## Agent 3: Frontend Architecture Review

**Scope:** Angular config, routing, components, services, guards, interceptors, models, tests, Tailwind CSS.

### Configuration — EXCELLENT
- Angular 21.1.0 with all strict TypeScript flags enabled
- `strict: true`, `strictTemplates: true`, `strictInjectionParameters: true`
- Tailwind 3.4.17 with custom fade-in/slide-up animations
- Vitest 4.0.8 for testing
- Production budgets: 550KB initial, 1MB max

### Routing — EXCELLENT (Updated Sprint 3)
- 10 routes: landing (public), login (guest), sentiment (guarded), insurance (guarded), dashboard (guarded), claims/triage, claims/history, claims/:id, dashboard/providers, dashboard/fraud (all guarded), wildcard redirect
- Functional auth guard (CanActivateFn) — modern pattern
- Functional HTTP interceptor — adds JWT conditionally

### Component Quality

| Component | Pattern | Grade |
|-----------|---------|-------|
| **InsuranceAnalyzerComponent** | Signals + inject() + takeUntilDestroyed | A+ |
| **DashboardComponent** | Signals + takeUntilDestroyed + OnInit | A+ |
| **LoginComponent** | Signals + inject() + async/await | A |
| **NavComponent** | inject() + @if control flow | A |
| **SentimentAnalyzerComponent** | Manual ChangeDetectorRef, no cleanup | C- |

### Services — GOOD
- `InsuranceService`: Environment config, 3 endpoints, strong typing
- `AuthService`: Signals for state, computed `isAuthenticated`, Supabase integration, graceful degradation without config
- `SentimentService`: Hardcoded localhost URL (inconsistent with InsuranceService)

### Type Safety — EXCELLENT
- 14+ TypeScript interfaces with no `any` types
- Full response typing for all API contracts
- Strict mode catches type errors at compile time

### Tailwind CSS — EXCELLENT
- All styling via utility classes, zero custom CSS
- Responsive: grid-cols-1 / md:grid-cols-2 / lg:grid-cols-4
- Consistent indigo-to-purple gradient theme
- Hover/focus states on all interactive elements

### Test Coverage — GOOD (improved Feb 18)
- 14 spec files, 126 tests total
- Vitest with AAA pattern
- HttpClientTestingModule for service tests
- Mock services with `vi.fn()` for component tests
- New coverage: theme.service.spec.ts (9 tests), error.interceptor.spec.ts (5 tests), ux.spec.ts (30 tests)
- **Gap:** Nav component untested, some error paths uncovered

### Anti-Patterns Found
1. `SentimentAnalyzerComponent`: Memory leak (no subscription cleanup), manual `cdr.detectChanges()`, `console.log` debug statements
2. `SentimentService`: Hardcoded API URL (should use environment config)

---

---

## Agent 4: Business Analyst (BA) Review

**Scope:** Insurance domain correctness, business rules, customer classification accuracy, regulatory compliance.

### Insurance Domain Coverage — Grade: A
The BA agent system prompt correctly covers all required insurance domain dimensions:

| Dimension | Implementation | Status |
|-----------|---------------|--------|
| **Sentiment Classification** | Positive/Negative/Neutral/Mixed | Implemented |
| **Confidence Scoring** | 0.0–1.0 range | Implemented, QA validates range |
| **Purchase Intent** | 0–100 score | Implemented |
| **Customer Persona** | 6 types: Price-Sensitive, Loyal-Advocate, Claim-Frustrated, New-Shopper, Policy-Upgrader, At-Risk | Implemented |
| **Journey Stage** | 6 stages: Awareness, Quote-Shopping, New-Policy, Mid-Term, Renewal, Active-Claim | Implemented |
| **Risk Indicators** | Churn risk, complaint escalation, fraud indicators | Implemented |
| **Emotion Taxonomy** | 8 insurance-relevant emotions (frustration, trust, anxiety, anger, satisfaction, confusion, urgency, relief) | Implemented in BA prompt |
| **Interaction Type** | General, Email, Call, Chat, Review, Complaint | Implemented with validation |
| **Policy Recommendations** | Dynamic based on analysis | Implemented |
| **Key Topics** | Extracted from text | Implemented |

### Business Rule Compliance

**Complaint Detection:**
- BA prompt instructs: Flag texts with >0.8 negative confidence + keywords ("file complaint", "department of insurance", "attorney")
- `ComplaintEscalationRisk` field returned in response
- **Gap:** No automated alerting/notification when high-risk complaint detected — only stored in DB

**PII Handling (Business Rule):**
- v2 path: PII redacted before external AI calls — COMPLIANT
- v1 path: PII NOT redacted — NON-COMPLIANT
- **Risk:** Regulatory exposure if policyholder PII sent to OpenAI unredacted

**Audit Trail (Regulatory Requirement):**
- Every analysis persisted to DB with timestamp, sentiment, persona, interaction type
- **Gap:** No input text hash logged (CLAUDE.md requires SHA-256 hash for traceability without storing raw text)
- **Gap:** No provider name logged per analysis

### Domain Accuracy Concerns
1. **Emotion taxonomy not enforced** — BA prompt lists 8 emotions but AI models can return arbitrary emotion keys. No validation on returned keys.
2. **Persona classification not validated** — Model could return a persona not in the 6 defined types. Developer agent formats but doesn't constrain to enum values.
3. **Journey stage same issue** — No enum enforcement at API response level.
4. **InteractionType duplication** — Defined in `Domain/Enums/` AND hardcoded in `InsuranceEndpoints.cs` string array. Should use single source of truth.

### BA Recommendations
1. Add complaint escalation alerting (webhook, email, or dashboard highlight)
2. Enforce persona/journey/emotion enums in Developer agent + QA agent validation
3. Add regulatory audit fields: input hash (SHA-256), provider name, processing time
4. Unify InteractionType to use Domain enum everywhere

---

## Agent 5: Developer Review

**Scope:** Code quality, patterns, implementation correctness, maintainability.

### Backend Code Quality — Grade: A-

**What's Done Well:**
- File-scoped namespaces throughout (`namespace X;`)
- Async methods properly suffixed with `Async`
- `ILogger<T>` injected in every service/controller
- Null handling: `??`, `?.` used consistently
- Private fields: `_camelCase` convention followed
- DI: All constructor injection via interfaces
- Source-generated regex in PIIRedactionService (performant)
- CQRS commands/queries as records (immutable, clean)

**Issues Found:**

| File | Issue | Severity |
|------|-------|----------|
| `SentimentController.cs:35` | Logs raw text preview (`request.Text[..50]`) — should log hash only | Medium |
| `SentimentController.cs:36` | Passes raw text to `AnalyzeSentimentAsync` without PII redaction | Critical |
| `AnalyzeInsuranceCommand.cs` | 19-line response mapping with null-coalescing chains — consider mapper | Low |
| `AnalyzeInsuranceCommand.cs` | DB persistence failure silently swallowed (logged as warning) | Medium |
| `SqliteAnalysisRepository.cs:87` | Second DB query in `GetTopPersonasAsync` — use total from first query | Medium |
| `GlobalExceptionHandler.cs` | No `HttpRequestException` case — AI provider connection failures return 500 instead of 502 | Medium |
| `InsuranceEndpoints.cs:10-11` | `ValidInteractionTypes` hardcoded — should reference Domain enum | Low |
| `InsuranceAnalysisOrchestrator.cs` | JSON extraction via brace-counting (~25 lines) — fragile | Medium |

### Frontend Code Quality — Grade: B+

**What's Done Well:**
- v2 components (InsuranceAnalyzer, Dashboard, Login) use modern Angular patterns
- Angular signals for reactive state
- `inject()` function for DI (not constructor injection)
- `takeUntilDestroyed()` for subscription cleanup
- Functional guards and interceptors (not class-based)
- TypeScript strict mode with no `any` types
- 14+ well-typed interfaces

**Issues Found:**

| File | Issue | Severity |
|------|-------|----------|
| `sentiment-analyzer.ts:21` | Uses `ChangeDetectorRef` — anti-pattern in Angular 21 | High |
| `sentiment-analyzer.ts:34` | Observable subscription without `takeUntilDestroyed()` — memory leak | High |
| `sentiment-analyzer.ts:36,44` | `console.log` debug statements left in production code | Medium |
| `sentiment-analyzer.ts:14-17` | Property-based state instead of signals | Medium |
| `sentiment.service.ts` | Hardcoded `http://localhost:5143` — doesn't use environment config | Medium |

### Developer Recommendations
1. Fix v1 component to match v2 patterns (signals, inject, takeUntilDestroyed)
2. Remove all `console.log` debug statements
3. Add `HttpRequestException` → 502 in GlobalExceptionHandler
4. Consider AutoMapper/Mapster for AnalyzeInsuranceCommand response mapping
5. Strengthen JSON extraction with schema validation library

---

## Agent 6: QA / Tester Review

**Scope:** Test coverage, test quality, validation gaps, quality gates.

### Test Coverage Summary

*Updated Feb 18, 2026 to reflect MapQuality tests, new frontend spec files, and corrected counts.*

| Area | Tests | Coverage Est. | Grade |
|------|-------|--------------|-------|
| **v1 Controller** | 9 tests (6 Facts + 1 Theory x3, frozen) | 100% of controller | A |
| **PII Redaction** | 11 tests | 100% of patterns | A+ |
| **CQRS Handlers** | 27 tests (17 AnalyzeInsurance + 1 Dashboard + 3 History + 7 MapQuality) | ~90% of handlers | A- |
| **Frontend Components** | 82 tests (6 spec files incl. ux.spec.ts) | ~80% | B+ |
| **Frontend Services** | 28 tests (4 spec files incl. theme.service) | ~85% | A- |
| **Frontend Guards/Interceptors** | 16 tests (4 spec files incl. error.interceptor, guest.guard) | ~90% | A |
| **Agent Orchestrator** | 0 tests | 0% | F |
| **Agent Strategies** | 0 tests | 0% | F |

### What's Tested Well
- v1 SentimentController: Happy path, empty text, whitespace, length limits, exceptions, health, various text inputs
- PIIRedactionService: All 5 patterns individually, combined PII, null/empty edge cases
- CQRS Handlers: Full coverage including MapQuality mapping (null, empty, issues-only, suggestions-only, combined), DB persistence, error handling, interaction type parsing
- Frontend components: Initialization, input validation, loading states, error handling, API calls, theme switching, UX integration (30 tests)
- Auth guard + Guest guard: Auth enabled/disabled, authenticated/unauthenticated, guest-only routes
- Auth interceptor + Error interceptor: Token injection, no-auth passthrough, 401/403 redirect handling

### Critical Testing Gaps

1. **Agent Orchestrator — ZERO TESTS**
   - `InsuranceAnalysisOrchestrator.AnalyzeAsync()` — the core business logic — has no unit tests
   - Fallback from multi-agent to single-agent untested
   - PII redaction call within orchestrator untested
   - Timeout behavior untested
   - JSON extraction logic untested

2. **Agent Strategies — ZERO TESTS**
   - `AgentSelectionStrategy` speaking order untested
   - `AnalysisTerminationStrategy` ANALYSIS_COMPLETE detection untested

3. **Provider Fallback — NOT TESTED**
   - No tests for Groq → Gemini → Ollama fallback chain
   - No tests for 429 rate limit handling

4. **Integration Tests — NONE**
   - No end-to-end tests: HTTP request → controller → orchestrator → mock AI → response
   - No database integration tests (repository + real SQLite)

5. **Frontend Missing Tests** (partially addressed Feb 18)
   - Nav component: No spec file -- still missing
   - Error page/404: No route exists, no tests
   - HTTP timeout scenarios untested
   - ~~Dashboard empty-state untested~~ — addressed in ux.spec.ts (30 tests including dashboard states)

### Test Quality Assessment
- AAA pattern (Arrange, Act, Assert): Followed consistently
- Test naming: Descriptive behavior names (`AnalyzeSentiment_WithClaimDenialText_ReturnsNegativeSentiment`)
- Test data: Realistic insurance text used (not "foo", "bar")
- Mocking: Moq for backend, `vi.fn()` for frontend — correctly applied
- Assertions: Single assertion per test in most cases

### QA Verdict (Updated Feb 18)
**CONDITIONAL PASS** -- improved from prior FAIL verdict. 2 of 4 original blockers resolved:
- ~~Agent output parsing broken~~ -- RESOLVED: Quality model alignment + MapQuality adapter (7 new tests)
- ~~Frontend timer memory leak~~ -- RESOLVED: InsuranceAnalyzerComponent ngOnDestroy cleanup

**Still blocks production:**
1. Agent orchestrator has unit tests (mock Kernel, verify PII redaction called, verify fallback) -- 0% coverage
2. Provider fallback chain has integration tests -- not implemented
3. v1 PII redaction gap is fixed (security blocker) -- OPEN
4. PII stored unredacted in database by AnalyzeInsuranceCommand -- OPEN (new P0 finding)

### QA Recommendations
1. Add `InsuranceAnalysisOrchestratorTests.cs` with mocked Kernel — minimum 10 tests
2. Add `AgentSelectionStrategyTests.cs` and `AnalysisTerminationStrategyTests.cs`
3. Add integration test project with real SQLite for repository tests
4. Add frontend Nav component spec
5. Add frontend empty-state and error-state tests for Dashboard
6. Set up CI pipeline test gate: all tests must pass before merge

---

---

**Last Updated**: February 24, 2026
**Current Version**: 3.0 (Sprint 3 Complete — Insurance AI Operations Hub)
**Next Review**: After Sprint 4 (TBD — brainstormed in separate session)
