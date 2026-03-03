# Design Patterns Reference

> Decided by: Solution Architect proposed, Developer validated feasibility, CTO approved.
> Principle: Every pattern must have a concrete use case in BOTH backend and frontend.

## Pattern Decision Matrix

| Situation | Pattern |
|-----------|---------|
| Multiple interchangeable implementations | **Strategy** |
| Separating reads from writes | **CQRS** |
| Sequential processing with fallback | **Chain of Responsibility** |
| Simplifying a complex subsystem | **Facade** |
| Reacting to state/data changes | **Observer** (Signals or RxJS) |
| Abstracting data storage/retrieval | **Repository** |
| Converting between incompatible interfaces | **Adapter** |
| Extending frozen/legacy code without modification | **Decorator** |
| Grounding LLM responses in document content | **RAG** |
| Combining keyword + semantic search | **Hybrid Retrieval** |
| Need a new pattern not listed here? | Discuss with Architect first, CTO approves |

## 1. Strategy Pattern
Swap algorithms/behaviors at runtime without modifying client code.

| Stack | Use Case | Implementation |
|-------|----------|----------------|
| Backend | AI provider selection | `IAIProvider` with `GroqProvider`, `GeminiProvider`, `OllamaProvider` |
| Backend | Agent selection & termination | `AgentSelectionStrategy`, `AnalysisTerminationStrategy` |
| Frontend | Auth strategy | `AuthService.authEnabled()` signal toggles strategy |
| Frontend | Theme/styling strategy | `ThemeService` for dark/light mode |

**Rule:** 2+ interchangeable behaviors behind an interface = Strategy. Register via DI, never `if/else` chains.

## 2. CQRS
Separate write operations (commands) from read operations (queries).

| Stack | Commands (Writes) | Queries (Reads) |
|-------|-------------------|-----------------|
| Backend | `AnalyzeInsuranceCommand` -> `AnalyzeInsuranceHandler` (MediatR) | `GetDashboardQuery`, `GetHistoryQuery` -> handlers |
| Frontend | `insuranceService.analyzeInsurance()` (POST) | `insuranceService.getDashboard()`, `getHistory()` (GET) |

**Rules:**
- Backend: ALL v2 endpoints use MediatR commands/queries in `Backend/Features/{Domain}/`
- Frontend: Services separate mutation methods from read methods
- v1 endpoints (frozen) are exempt

## 3. Chain of Responsibility
Sequential processing with potential early termination.

| Stack | Use Case | Chain |
|-------|----------|-------|
| Backend | AI provider fallback | Groq -> Mistral -> Gemini -> OpenRouter -> Ollama |
| Backend | ASP.NET middleware | Request -> Auth -> Exception Handler -> CORS -> Endpoint |
| Backend | PII redaction pipeline | SSN -> Policy# -> Claim# -> Email -> Phone |
| Frontend | HTTP interceptor chain | `authInterceptor` -> future interceptors |

**Rule:** Provider fallback MUST follow: Groq -> Mistral -> Gemini -> OpenRouter -> Ollama -> error. Never skip.

## 4. Facade Pattern
Simplified interface to a complex subsystem.

| Stack | Facade | Hides |
|-------|--------|-------|
| Backend | `InsuranceAnalysisOrchestrator` | Multi-agent system, Semantic Kernel, strategies |
| Backend | `PIIRedactionService` | 5+ regex patterns, order-dependent pipeline |
| Frontend | `InsuranceService` | HTTP calls, request/response mapping, errors |
| Frontend | `AuthService` | Supabase client, session, token refresh, signals |

**Rule:** Controllers/components NEVER interact directly with complex subsystems. If >2 injected services for one action, consider a facade.

## 5. Observer Pattern (Reactive)
State changes notify all dependents automatically.

| Stack | Implementation | Use Case |
|-------|---------------|----------|
| Backend | `AgentGroupChat.InvokeAsync` | Agents observe previous messages and react |
| Frontend | **Angular Signals** | Component state: `isLoading`, `result`, `error` |
| Frontend | **RxJS Observables** | HTTP responses, auth state, cross-component events |

**Rules:**
- Component state: Use **Angular Signals** (preferred for new code)
- Async data streams: Use **RxJS Observables** with `takeUntilDestroyed()`
- Never mix: no Observables for simple state, no signals for HTTP streams

## 6. Repository Pattern
Abstract data access behind a clean interface.

| Stack | Interface | Implementations |
|-------|-----------|-----------------|
| Backend | `IAnalysisRepository` | `SqliteAnalysisRepository` (dev), PostgreSQL (prod) |
| Frontend | Angular services | Abstract HTTP calls behind typed methods |

**Rules:**
- Backend: ALL DB access through repository interfaces. No direct `DbContext` in controllers/handlers/agents.
- Frontend: ALL API access through Angular services. No direct `HttpClient` in components.

## 7. Adapter Pattern
Convert one interface into another that clients expect.

| Stack | Adapter | From | To |
|-------|---------|------|-----|
| Backend | `AnalyzeInsuranceHandler.MapToResponse()` | `AgentAnalysisResult` | `InsuranceAnalysisResponse` |
| Backend | AI provider abstraction | Native responses | Semantic Kernel `ChatMessageContent` |
| Frontend | Component data mapping | API response | View models |

**Rule:** When integrating external systems, ALWAYS use an adapter. Never leak external shapes into domain models.

## 8. Decorator Pattern (v4.0)
Add behavior dynamically without modifying source.

| Stack | Decorator | Wraps | Adds |
|-------|-----------|-------|------|
| Backend | `PiiRedactingSentimentService` | `OpenAISentimentService` (frozen) | PII redaction |

**Rule:** Use Decorator for extending frozen/legacy code. Register in DI so consumers get decorator transparently.

## 9. RAG Pattern (v4.0 Planned)
Ground LLM responses in factual document content.

| Phase | Step | Implementation |
|-------|------|----------------|
| Ingest | Upload → OCR/Vision → Chunk → Embed → Store | `DocumentIntelligenceService.UploadAsync()` |
| Query | Question → Embed → Vector Search → Context → LLM → Citations | `DocumentIntelligenceService.QueryAsync()` |

**Chunking:** Split by insurance section headers, then sentence boundaries with 64-token overlap. Target: 512 tokens. PII redacted before embedding.

**Vector search:** SQLite stores embeddings as JSON blob (`float[1024]`). Cosine similarity via `System.Numerics.Vector<float>` SIMD. Top-5 chunks per query. Production: Supabase pgvector.

**Rule:** Always include source citations. Never let LLM generate claims not grounded in retrieved chunks.

## 10. Hybrid Retrieval Pattern (v5.0)
Combine BM25 keyword scoring with vector semantic search for better document retrieval.

| Phase | Step | Implementation |
|-------|------|----------------|
| Keyword | BM25 scoring | `BM25Scorer` — Okapi BM25 with IDF weighting on document chunks |
| Semantic | Vector search | `ResilientEmbeddingProvider` — 6-provider embedding chain |
| Fusion | Score normalization + ranking | `HybridRetrievalService` — weighted combination (α=0.7 semantic, β=0.3 keyword) |

**Rule:** Hybrid retrieval always outperforms pure vector search for insurance documents with domain-specific terminology. BM25 catches exact policy numbers and claim IDs that semantic search may miss.
