# Architecture Reference

## System Architecture
```
Angular 21 SPA (Port 4200)
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
         ├── Claims Triage Agent (v3.0)
         ├── Fraud Detection Agent (v3.0)
         └── Document Query Agent (v4.0 - RAG Q&A)
              |
         IResilientKernelProvider (v3.0 - runtime fallback, 7 providers)
         ├── Groq (primary, fast)
         ├── Cerebras (secondary, GPT-OSS 120B)
         ├── Mistral (tertiary)
         ├── Gemini (quaternary)
         ├── OpenRouter (quinary)
         ├── OpenAI (legacy v1)
         └── Ollama (local, PII-safe, always-on fallback)
              |
         Multimodal Services (v3.0)
         ├── Deepgram (STT - voice notes/calls)
         ├── Azure Vision (image analysis - primary)
         ├── Cloudflare Vision (image analysis - secondary)
         ├── OCR.space (document OCR)
         └── HuggingFace (NER - entity extraction)
              |
         ResilientEmbeddingProvider (v5.0 - 6-provider chain)
         ├── Voyage AI (voyage-finance-2, 1024-dim)
         ├── Cohere (embed-english-v3.0)
         ├── Gemini (text-embedding-004)
         ├── HuggingFace (sentence-transformers)
         ├── Jina (embeddings-v3)
         └── Ollama nomic-embed-text (local fallback)
              |
         Sprint 5 Features
         ├── Hybrid RAG (BM25 + Vector retrieval)
         ├── CX Conversation Memory (persistent chat history)
         ├── Batch Claims (CSV upload processing)
         └── Synthetic QA (auto-generated Q&A from document chunks)
              |
         SQLite / Supabase (PostgreSQL)
              |
         MCP Servers (v5.0)
         ├── Playwright MCP (E2E test generation from browser)
         ├── Context7 MCP (up-to-date library documentation)
         └── Sequential Thinking MCP (structured reasoning)
```

## MCP Server Integration (Sprint 5)

### Active MCP Servers
Configured in `.mcp.json` at project root:

| MCP Server | Package | Transport | Purpose |
|-----------|---------|-----------|---------|
| **Playwright** | `@playwright/mcp@latest` | stdio (headless) | Browser automation, E2E test generation from live browser sessions, exploratory testing |
| **Context7** | `@upstash/context7-mcp@latest` | stdio | Up-to-date documentation for .NET 10, Angular 21, Semantic Kernel, and other libraries |
| **Sequential Thinking** | `@anthropic/mcp-sequential-thinking` | stdio | Structured multi-step reasoning for architecture decisions and complex problem solving |

### How MCP Servers Integrate
```
Claude Code CLI
    |
    ├── Playwright MCP Server (stdio)
    │   ├── Navigate to localhost:4200
    │   ├── Record browser interactions
    │   ├── Generate Playwright test specs
    │   └── Capture screenshots for visual regression
    |
    ├── Context7 MCP Server (stdio)
    │   ├── Resolve library IDs for any package
    │   ├── Query up-to-date docs with code examples
    │   └── Inform Claude with current API surfaces
    |
    └── Sequential Thinking MCP Server (stdio)
        ├── Break down complex architecture decisions
        ├── Multi-step reasoning with revision support
        └── Hypothesis generation and verification
```

### Future MCP Ecosystem (Planned)
| MCP Server | Category | Purpose |
|-----------|----------|---------|
| Supabase | Database | Schema management, query testing, migrations |
| GitHub | DevOps | PR management, automated code review, issues |
| Sentry | Monitoring | Error tracking across 7 LLM + 5 multimodal providers |
| Grafana | Observability | Provider health dashboards |
| Snyk | Security | Dependency vulnerability scanning |
| Upstash | Caching | Redis rate limiting + analysis caching |
| Tavily | Research | Insurance domain research |

## Sprint 4 Week 4 Frontend Architecture

### Document Intelligence RAG Frontend Flow
```
User uploads document (document-upload component)
  -> POST /api/insurance/documents/upload (FormData: file + type)
  -> Backend: OCR (OCR.space) -> chunk (section-aware) -> embed (Voyage AI) -> store (SQLite)
  -> Upload result displayed (document-result component)

User queries document (document-query component)
  -> POST /api/insurance/documents/query { query, documentId? }
  -> Backend: embed query -> vector search top-5 chunks -> LLM answer with citations
  -> Answer + source citations rendered inline
```

### SSE Streaming Pattern (CX Copilot)
The CX Copilot component uses raw `fetch()` + `ReadableStream` for POST-based SSE, because Angular's `HttpClient` does not support streaming responses from POST requests.

```typescript
// Pattern: POST-based SSE with ReadableStream (not EventSource, which is GET-only)
const response = await fetch('/api/insurance/cx/stream', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ message, sessionId })
});
const reader = response.body!.getReader();
const decoder = new TextDecoder();
// Read SSE chunks: "data: {json}\n\n" format
while (true) {
  const { done, value } = await reader.read();
  if (done) break;
  // Parse SSE lines, update Angular Signal for reactive UI
}
```

Key design choices:
- Angular Signals for real-time message state (not RxJS Subject)
- Typing indicator shown while streaming, hidden on completion
- Dual-pass PII redaction: input redacted before sending, output redacted by backend before streaming
- Session ID persisted for conversation continuity

### Fraud Correlation UI
The fraud-correlation component uses a split-card design:
- **Left panel**: Source claim summary (severity, type, fraud score)
- **Right panel**: Correlated claims list with 4-strategy badges (DateProximity, SimilarNarrative, SharedFlags, SameSeverity)
- **Review workflow**: Each correlation has Pending/Confirmed/Dismissed status with PATCH action buttons
- **Confidence gauge**: Horizontal bar showing correlation confidence (0-100) with color zones

### Angular Application Totals (v5.0)
- **Components**: 22 (was 18 after Sprint 4)
- **Routes**: 16 (was 15 after Sprint 4)
- **Services**: claims, document, customer-experience, fraud-correlation, sentiment, insurance, auth, theme, breadcrumb, command-registry, scroll, toast
- **Model files**: claims.model.ts, document.model.ts, batch-claim models

---

## Agent Orchestration Flows

### Default Sentiment Analysis Flow
```
User Input -> CTO Agent (decomposes task)
  -> BA Agent (insurance domain analysis)
  -> Developer Agent (format response)
  -> QA Agent (validate consistency)
  -> AI Expert Agent (model evaluation, training recommendations, responsible AI)
  -> UX Designer Agent (screens, a11y, design system)
  -> Architect Agent (storage/perf advice)
  -> CTO Agent (synthesize final output) -> "ANALYSIS_COMPLETE"
```

### Claims Triage Flow (v3.0 - OrchestrationProfile.ClaimsTriage)
```
-> Claims Triage Agent (severity, urgency, actions)
-> Fraud Detection Agent (fraud scoring, SIU referral)
-> BA Agent (domain validation)
-> QA Agent (quality check)
```

### Document Query Flow (v4.0 planned)
```
-> Document Query Agent (RAG context + question → answer with citations)
-> BA Agent (domain validation)
-> Developer Agent (format response)
-> QA Agent (verify citations, no-hallucination check)
```

### Customer Experience Flow (v4.0 — Sprint 4 Week 3 COMPLETE)
```
User message -> PII redact input -> CustomerExperienceService
  -> Orchestrator (CustomerExperience profile)
     -> BA Agent (domain context)
     -> Developer Agent (response formatting)
     -> UX Designer Agent (tone, empathy, clarity)
     -> QA Agent (quality check)
  -> Tone classification + escalation detection (16 keywords + LLM tags)
  -> PII redact output -> SSE streaming to frontend
  -> CxInteractionRecord audit trail (SHA-256 message hash, never raw PII)
```

## Orchestration Profiles (v3.0+)
Selective agent activation reduces token usage 50-60% for specialized workflows:
- **SentimentAnalysis**: All 7 original agents, 14 max turns (default)
- **ClaimsTriage**: ClaimsTriage + FraudDetection + BA + QA, 8 max turns
- **FraudScoring**: FraudDetection + ClaimsTriage + BA + QA, 8 max turns
- **DocumentQuery**: DocumentQuery + BA + Developer + QA, 6 max turns (v4.0)
- **CustomerExperience**: BA + Developer + UX + QA, 8 max turns (v4.0 — LIVE)

Managed by `IOrchestrationProfileFactory` / `OrchestrationProfileFactory`.

## Agent Decision Authority
- **CTO**: Priority, scope, conflict resolution (final say)
- **BA**: Insurance domain correctness, business rules
- **Architect**: Technical design, DB schema, API contracts
- **Developer**: Implementation approach within Architect's design
- **QA**: Quality - can block releases with justified concerns
- **AI Expert**: Model selection, cloud adoption, responsible AI governance
- **UX Designer**: Screen layouts, design system, accessibility
- **Claims Triage** (v3.0): Claim severity, urgency, action recommendations
- **Fraud Detection** (v3.0): Fraud probability, indicator categorization, SIU referral
- **Document Query** (v4.0): RAG-based Q&A, source citations, no-hallucination guardrails
- **Customer Experience** (v4.0 Week 3): CX Copilot SSE chat, tone classification, escalation detection, regulatory disclaimers
- **Fraud Correlation** (v4.0 Week 3): Cross-claim 4-strategy detection (DateProximity, SimilarNarrative, SharedFlags, SameSeverity), claim-type-specific windows, review workflow

## Agent Output Parsing (Critical)
LLM agents produce non-deterministic output. The orchestrator uses resilient two-phase parsing:

1. **Normalization**: Strip markdown code fences before extraction
2. **JSON Extraction**: String-literal-aware brace counting (tracks `inString` and `escape` state)
3. **Phase 1 - Strict**: `JsonSerializer.Deserialize<AgentAnalysisResult>()`
4. **Phase 2 - Manual**: `JsonDocument` field extraction with both camelCase and PascalCase support
5. **Diagnostics**: Log JSON length + SHA-256 hash (NEVER raw JSON content)

## Agent Prompt Engineering Rules
- ALL agent prompts instruct "output ONLY raw JSON, NO markdown code fences"
- Developer agent prompt MUST include the `quality` field in its JSON schema
- CTO prompt must say "merge QA output into the quality field"
- Prompts loaded from `.claude/agents/*.md` with hardcoded fallbacks in `AgentDefinitions.cs` - keep both in sync

## Escalation Rules
- Agent blocked >1 iteration -> escalate to CTO
- Developer vs Architect disagreement -> Architect decides, CTO tiebreaker
- QA critical bug -> goes to Developer AND CTO simultaneously
- BA identifies regulatory gap -> immediately Priority 1
- AI Expert flags responsible AI concern -> goes to CTO AND BA simultaneously

## Free AI Providers — LLM Fallback Chain
1. **Groq** - Primary, fastest (Llama 3.3 70B, 250 req/day free)
2. **Cerebras** - Secondary (GPT-OSS 120B, fast inference)
3. **Mistral** - Tertiary (Mistral Large, 500K tokens/month free)
4. **Gemini** - Quaternary (60 req/min free)
5. **OpenRouter** - Quinary ($1 free credit)
6. **OpenAI** - Legacy v1.0 provider only
7. **Ollama** - Local fallback, always available (unlimited, PII-safe)

Managed by `IResilientKernelProvider` with exponential backoff cooldown (30s/60s/120s/300s max).

## Multimodal Services (v3.0)
| Service | Provider | Free Tier | Use Case |
|---------|----------|-----------|----------|
| STT | Deepgram Nova-2 | $200 credit | Adjuster voice notes, call recordings |
| Vision (primary) | Azure Vision F0 | 5K/month | Claim damage photo analysis |
| Vision (secondary) | Cloudflare Workers AI | 10K neurons/day | Natural language image analysis |
| OCR | OCR.space | 500/day | Scanned policy docs, claim forms |
| NER | HuggingFace (BERT NER) | 300/hour | Entity extraction + insurance regex |

All use `HttpClient` REST — 0 new NuGet packages. PII redacted on output text.

## Embedding Services (v5.0 — 6-Provider Chain)
Managed by `ResilientEmbeddingProvider` with automatic fallback.

| Service | Provider | Free Tier | Use Case |
|---------|----------|-----------|----------|
| Embeddings (primary) | Voyage AI (`voyage-finance-2`) | 50M tokens | Finance-optimized 1024-dim |
| Embeddings (secondary) | Cohere (`embed-english-v3.0`) | 1000 req/min | General-purpose, high quality |
| Embeddings (tertiary) | Gemini (`text-embedding-004`) | 1500 req/min | Google ecosystem integration |
| Embeddings (quaternary) | HuggingFace (`sentence-transformers`) | 300 req/hr | Open-source transformer models |
| Embeddings (quinary) | Jina (`embeddings-v3`) | 10M tokens | Multi-language, flexible dimensions |
| Embeddings (fallback) | Ollama (`nomic-embed-text`) | Unlimited (local) | Local fallback, PII-safe |

## Adding a New Agent
1. Add system prompt to `Agents/Definitions/AgentDefinitions.cs`
2. Add role to `Agents/Definitions/AgentRole.cs` enum
3. Update `InsuranceAnalysisOrchestrator.cs` to include agent
4. Update `AgentSelectionStrategy.cs` if speaking order changes
5. Add unit tests in `Tests/AgentTests/`

## Adding a New API Endpoint
1. Add request/response models to `Backend/Models/`
2. Add service interface + implementation to `Backend/Services/`
3. Add controller method to appropriate controller
4. Register service in `Program.cs`
5. Add controller tests to `Tests/`
6. Add frontend model, service, and component
7. Update `app.routes.ts`
