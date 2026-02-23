# Insurance AI Operations Hub — Full Team Planning Session

## Meeting: Implementing BA's Insurance AI Opportunity Analysis

> **Date:** 2026-02-21
> **Facilitator:** CTO Agent (Orchestrator)
> **Attendees:** CTO, Solution Architect, AI Expert, Senior Developer, QA Lead, Business Analyst (Insurance), UX Designer
> **Input Documents:**
> - `ai-insurance-opportunities-ba-analysis.md` (BA's 14-dimension opportunity map)
> - `azure-cloud-adoption-plan.md` (Cloud deployment strategy)
> **Constraint:** FREE tools, free-tier APIs, open-source only. Zero licensing cost.
> **Goal:** Finalize a buildable implementation plan for transforming the Sentiment Analyzer into an Insurance AI Operations Hub.

---

# ROUND 1 — Initial Proposals & Positions

## CTO Agent (Orchestrator)

> *"BA's analysis is excellent — 14 AI opportunity areas, but we can't boil the ocean. Here's my framing for this session."*

### Strategic Direction

BA identified that only **7% of carriers have scaled AI beyond pilots** (BCG 2025). The opportunity isn't building the most sophisticated AI — it's building the most **adoptable** one. Our multi-agent Semantic Kernel architecture is our moat.

### My Prioritization (for team debate):

| Priority | Module | Why First |
|----------|--------|-----------|
| **Sprint 1** (Weeks 1-3) | Claims Triage Agent + Fraud Scoring | 14x impact (McKinsey). Extends existing agents. |
| **Sprint 2** (Weeks 4-6) | Document Intelligence (RAG) | Every insurer needs policy Q&A. RAG is proven. |
| **Sprint 3** (Weeks 7-9) | Customer Experience Copilot | Direct extension of our sentiment core. Low effort. |
| **Sprint 4** (Weeks 10-12) | Unified Dashboard Hub | Pull it all together into one Insurance AI Operations Hub. |

### Non-Negotiables:
1. v1 API backward compatibility — FROZEN, never touch
2. PII redaction before ANY external call — extends to all new modules
3. Free-tier only — Groq, Gemini, Ollama, Mistral, OpenRouter. No paid APIs.
4. Ship incrementally — demo-ready at end of each sprint

### Open Questions for Team:
- Architect: Can RAG work within free tier limits?
- AI Expert: What's our updated free model strategy for 2026?
- Developer: How much code refactoring does Claims Triage need?
- QA: What's the test strategy for non-deterministic multi-agent claims analysis?
- BA: Which claims scenarios should we prototype first?
- UX: What does a Claims Triage dashboard look like?

---

## Business Analyst (Insurance Domain)

> *"Let me ground this in insurance operations reality. Not every AI opportunity has equal ROI."*

### Domain Priority Assessment

From my 15 years in insurance operations, here's what carriers actually buy and adopt:

#### Tier 1 — "Must Have" (carriers will pay for this tomorrow)
1. **Claims Triage + FNOL Automation** — Every carrier's #1 pain point. Manual triage takes 2-4 hours per claim. AI can do it in seconds.
2. **Fraud Detection** — $80B annual problem. Even a 5% improvement saves millions. Regulators are pushing for it.
3. **Complaint Escalation** — We already detect this. Scaling it with routing = immediate value.

#### Tier 2 — "Want to Have" (builds competitive moat)
4. **Document Intelligence / Policy RAG** — Underwriters spend 60% of time reading documents. Game-changer.
5. **Customer Experience Copilot** — Reduces call center volume 15-25%. Retention weapon.

#### Tier 3 — "Nice to Have" (requires more data maturity)
6. **Underwriting Risk Scoring** — Needs actuarial data we don't have yet
7. **Predictive Analytics** — Same data dependency

### Claims Triage — What the Agent Needs to Do

```
Input: Free-text claim description + optional metadata
  "Water damage to kitchen ceiling discovered Jan 15.
   Called twice, no response. Policy HO-2024-789456.
   Considering switching to State Farm."

Output:
├── Claim Type: Property — Water Damage
├── Severity: Medium (structural, not catastrophic)
├── Urgency: HIGH (2 contacts, no response = service failure)
├── Fraud Risk: Low (consistent narrative, reasonable timeline)
├── Churn Risk: HIGH ("considering switching" = active shopping)
├── Recommended Actions:
│   ├── 1. Immediate callback from senior adjuster
│   ├── 2. Fast-track inspection scheduling
│   ├── 3. Retention team alert (churn flag)
│   └── 4. Supervisor escalation (missed contacts)
├── Regulatory Flags: None
└── Estimated Reserve Range: $5,000 - $25,000
```

### Insurance-Specific Test Scenarios (for QA)

| Scenario | Expected Behavior |
|----------|-------------------|
| Simple auto claim (fender bender) | Low severity, low fraud, standard triage |
| Catastrophe event (hurricane, flood) | Cat-code triggers, batch processing mode |
| Suspicious fire claim (recent policy change) | High fraud score, SIU referral |
| Complaint with "department of insurance" mention | Priority 1 escalation, regulatory flag |
| Workers' comp with return-to-work dispute | Medical review flag, legal hold advisory |
| Policy Q&A: "Am I covered for mold?" | RAG retrieval from policy document |

### Key Domain Rules for All New Agents:
1. **Lines of Business matter** — Auto, Home, Commercial, Workers' Comp, Life/Health are DIFFERENT domains with different rules
2. **State regulations vary** — CA, FL, NY, TX each have unique requirements
3. **Claim reserves are financial** — any AI-suggested reserve amount MUST have human approval
4. **SIU referrals are legal actions** — false positives have consequences

---

## Solution Architect

> *"The good news: our architecture was designed for this expansion. The challenge: free-tier limits are real constraints."*

### Architecture Extension Plan

```
                    ┌─────────────────────────────────┐
                    │     Angular 21 SPA               │
                    │     Insurance AI Operations Hub   │
                    │     ┌───────────────────────┐    │
                    │     │ Module Router          │    │
                    │     │ /sentiment (existing)  │    │
                    │     │ /claims (NEW)          │    │
                    │     │ /fraud (NEW)           │    │
                    │     │ /documents (NEW)       │    │
                    │     │ /experience (NEW)      │    │
                    │     │ /dashboard (enhanced)  │    │
                    │     └───────────────────────┘    │
                    └──────────┬────────────────────────┘
                               │
                    ┌──────────┴────────────────────────┐
                    │  .NET 10 Web API                   │
                    │  ┌─────────────────────────────┐  │
                    │  │ v1 API (frozen)              │  │
                    │  │ v2 Insurance API (CQRS)      │  │
                    │  │ v2 Claims API (NEW)          │  │
                    │  │ v2 Fraud API (NEW)           │  │
                    │  │ v2 Documents API (NEW)       │  │
                    │  └─────────────────────────────┘  │
                    │  ┌─────────────────────────────┐  │
                    │  │ Semantic Kernel Orchestrator │  │
                    │  │ ┌─────────┐ ┌────────────┐ │  │
                    │  │ │Existing │ │ NEW Agents │ │  │
                    │  │ │6 Agents │ │ Claims     │ │  │
                    │  │ │CTO,BA,  │ │ Fraud      │ │  │
                    │  │ │Dev,QA,  │ │ Document   │ │  │
                    │  │ │Arch,UX  │ │ CX Copilot │ │  │
                    │  │ └─────────┘ └────────────┘ │  │
                    │  └─────────────────────────────┘  │
                    │  ┌─────────────────────────────┐  │
                    │  │ RAG Engine (NEW)             │  │
                    │  │ Embedding: Ollama nomic-embed│  │
                    │  │ Vector DB: ChromaDB (local)  │  │
                    │  │ Retriever: Semantic Kernel   │  │
                    │  └─────────────────────────────┘  │
                    └──────┬───────┬───────┬────────────┘
                           │       │       │
              ┌────────────┘       │       └─────────────┐
              ▼                    ▼                      ▼
  ┌───────────────────┐ ┌──────────────────┐  ┌──────────────────┐
  │ AI Providers      │ │ Storage          │  │ RAG Vector Store │
  │ Groq (primary)    │ │ SQLite (dev)     │  │ ChromaDB (local) │
  │ Gemini (secondary)│ │ Supabase PG (prd)│  │ FREE, unlimited  │
  │ Mistral (NEW!)    │ │                  │  │ runs alongside   │
  │ OpenRouter (NEW!) │ │                  │  │ Ollama locally   │
  │ Ollama (local/PII)│ │                  │  │                  │
  │ OpenAI (legacy)   │ │                  │  │ Embeddings:      │
  └───────────────────┘ └──────────────────┘  │ nomic-embed-text │
                                               │ via Ollama(local)│
                                               └──────────────────┘
```

### Key Architectural Decisions

#### Decision A: RAG Storage — ChromaDB (local, free, unlimited)
- **Why:** Supabase pgvector is unavailable in our region. ChromaDB runs locally alongside Ollama — same local-first pattern.
- **ChromaDB advantages:** Zero cost, unlimited storage (disk-based), runs as lightweight local server, excellent .NET client (`ChromaDB.Client` NuGet).
- ChromaDB supports: cosine similarity, inner product, L2 distance. Full metadata filtering. Collection-based organization.
- Local disk stores unlimited document embeddings (768-dim from nomic-embed-text) — no cloud limits to worry about.
- **Cloud migration path:** When ready for cloud, ChromaDB Cloud has a free tier, or we can switch to Qdrant Cloud (1GB free).

#### Decision B: Embeddings — Ollama nomic-embed-text (local, free, PII-safe)
- **Why:** Runs locally, zero cost, zero PII risk. Outperforms OpenAI ada-002.
- **Fallback:** Gemini embedding API (free tier) for cloud deployment where Ollama isn't available.
- Model size: ~274MB, downloads once.

#### Decision C: New AI Providers — Add Mistral + OpenRouter
- **Mistral free tier:** 1 billion tokens/month. Best free tier in the industry.
- **OpenRouter:** Access to 24+ free models including DeepSeek R1, Llama 3.3 70B.
- Updated fallback chain: `Groq → Mistral → Gemini → OpenRouter → Ollama → OpenAI`

#### Decision D: Module Architecture — Feature Folders (CQRS)
Each new capability gets its own feature folder following existing CQRS pattern:
```
Backend/Features/
  ├── Insurance/          (existing - sentiment analysis)
  │   ├── Commands/AnalyzeInsurance/
  │   └── Queries/GetDashboard/
  ├── Claims/             (NEW)
  │   ├── Commands/TriageClaim/
  │   └── Queries/GetClaimsHistory/
  ├── Fraud/              (NEW)
  │   ├── Commands/ScoreFraud/
  │   └── Queries/GetFraudAlerts/
  └── Documents/          (NEW)
      ├── Commands/IngestDocument/
      └── Queries/QueryDocument/
```

#### Decision E: Shared Agent Pool vs. Dedicated Orchestrators
**Proposal:** One CTO Agent orchestrates ALL modules. Domain agents (Claims, Fraud, Document) are added to the shared pool. CTO routes based on request type.

**Rationale:** Keeps the single-orchestrator pattern. CTO Agent prompt gets updated to understand claim triage, fraud scoring, and document queries in addition to sentiment analysis.

### Free-Tier Budget Tracking

| Provider | Free Limit | Daily Budget | Monthly Budget |
|----------|-----------|-------------|----------------|
| Groq | 14,400 req/day | ~500 analyses | ~15,000 |
| Mistral (NEW) | 1B tokens/month | ~33M tokens/day | 1,000,000,000 |
| Gemini | 60 req/min | ~86,400/day (theoretical) | ~2,592,000 |
| OpenRouter | 50 req/day (free) | 50 | 1,500 |
| Ollama | Unlimited (local) | Unlimited | Unlimited |
| Supabase | 500MB + 50K MAU | N/A | N/A |
| GitHub Actions | 2,000 min/month | ~66 min/day | 2,000 min |

**Total monthly free capacity:** ~15,000+ analyses with fallback chain. More than enough for MVP + demo.

---

## AI Expert Agent

> *"The AI landscape has shifted massively since we started. Here's what's actually free and good in Feb 2026."*

### Updated Free AI Provider Strategy (2026)

#### Tier 1: Primary Providers (Permanent Free)

| Provider | Model | Free Limit | Strengths | Use In Our System |
|----------|-------|-----------|-----------|-------------------|
| **Groq** | Llama 3.3 70B | 14,400 req/day | Fastest inference (300 tok/s) | Real-time sentiment, claims triage |
| **Mistral** | Mistral Large 2 | 1B tokens/month | Highest volume free tier | Bulk analysis, document processing |
| **Gemini** | Gemini 2.0 Flash | 60 req/min | 1M context window, multimodal | Long document analysis, policy RAG |
| **Ollama** | Llama 3.3, Phi-4, Qwen 2.5 | Unlimited | Local, PII-safe | PII processing, embeddings |

#### Tier 2: Supplementary (Free Credits / Limited)

| Provider | Model | Free Limit | Use Case |
|----------|-------|-----------|----------|
| **OpenRouter** | 24+ models (DeepSeek R1, etc.) | 50/day free | A/B testing models, diversity |
| **HuggingFace** | 300+ models | Rate-limited API | Specialized NER, classification |
| **DeepSeek** | DeepSeek R1 | $5 signup credit | Complex reasoning tasks |
| **Cloudflare Workers AI** | 70+ models | 10K tokens/day free | Edge inference |

#### Updated Fallback Chain

```
Primary:    Groq (Llama 3.3 70B)         ← fastest, 14.4K/day
     ↓ 429/500
Secondary:  Mistral (Large 2)             ← NEW, 1B tokens/month
     ↓ 429/500
Tertiary:   Gemini (2.0 Flash)            ← 1M context, multimodal
     ↓ 429/500
Quaternary: OpenRouter (best available)   ← NEW, 24+ free models
     ↓ 429/500
Local:      Ollama (Llama 3.3 / Phi-4)    ← unlimited, PII-safe
     ↓ unavailable
Legacy:     OpenAI (GPT-4o-mini)           ← existing credits
```

### RAG Architecture (for Document Intelligence)

```
Document Upload Flow:
  PDF/DOCX → Text Extraction (free: Apache Tika or pdf.js)
       ↓
  Chunking (512 tokens, 50-token overlap)
       ↓
  Embedding (Ollama nomic-embed-text — FREE, local)
       ↓
  Store in ChromaDB (local, unlimited)
       ↓
  Index with metadata (policy_type, lob, state, effective_date)

Query Flow:
  User question → Embed query (nomic-embed-text)
       ↓
  Semantic search (ChromaDB cosine similarity, top-5)
       ↓
  Rerank (Ollama, optional)
       ↓
  Context + Question → LLM (Groq/Mistral/Gemini)
       ↓
  Answer with source citations
```

### Model Selection per Task

| Task | Best Free Model | Why |
|------|----------------|-----|
| Real-time sentiment | Groq Llama 3.3 70B | Fastest response, good accuracy |
| Claims triage (complex reasoning) | Mistral Large 2 | Best reasoning at scale, 1B free |
| Fraud scoring | Gemini 2.0 Flash | Multimodal (text + image), large context |
| Document RAG answers | Gemini 2.0 Flash | 1M context window — fits entire policies |
| Embeddings | Ollama nomic-embed-text | Free, local, outperforms ada-002 |
| PII-sensitive processing | Ollama Llama 3.3 | Local only, zero data exposure |
| Complex chain-of-thought | DeepSeek R1 (via OpenRouter) | Best free reasoning model |

### Responsible AI Framework (Non-Negotiable)

1. **Bias Monitoring:** Log demographic distribution of fraud flags. Alert if disparity >10%.
2. **Explainability:** Every fraud score and claims triage decision includes a plain-English explanation.
3. **Human-in-the-Loop:** NO autonomous financial decisions. AI recommends, human approves.
4. **Audit Trail:** Every analysis: timestamp, model used, input hash (SHA-256), decision, confidence.
5. **PII Pipeline:** Redact → Analyze → De-redact for display (never store raw PII with AI output).

---

## Senior Developer

> *"I've reviewed the architecture and BA requirements. Here's what's buildable and what's tricky."*

### Implementation Complexity Assessment

| Module | New Files | Modified Files | Effort (days) | Risk |
|--------|-----------|---------------|---------------|------|
| Claims Triage Agent | ~8 | ~4 | 5-7 | Medium |
| Fraud Scoring Agent | ~6 | ~3 | 4-5 | Medium |
| RAG Engine (Documents) | ~12 | ~5 | 8-10 | High |
| CX Copilot Agent | ~5 | ~3 | 3-4 | Low |
| Mistral Provider | ~3 | ~2 | 2 | Low |
| OpenRouter Provider | ~3 | ~2 | 2 | Low |
| Dashboard Hub (frontend) | ~15 | ~6 | 8-10 | Medium |

### Claims Triage Agent — Implementation Sketch

```csharp
// New: Agents/Definitions/AgentRole.cs — add enum values
ClaimsTriage,
FraudDetection,
DocumentIntelligence,
CustomerExperience

// New: Agents/Definitions/AgentDefinitions.cs — add prompts
public static string ClaimsTriagePrompt => """
You are a Claims Triage Specialist with 20 years of insurance claims experience.
Analyze the incoming claim and produce a structured JSON assessment:
{
  "claimType": "Property|Auto|Workers Comp|Liability|Health",
  "severity": "Low|Medium|High|Catastrophic",
  "urgency": "Standard|Elevated|High|Critical",
  "fraudRiskScore": 0-100,
  "churnRiskScore": 0-100,
  "estimatedReserveRange": { "min": number, "max": number },
  "recommendedActions": [...],
  "regulatoryFlags": [...],
  "triageDecision": "Auto-Process|Standard-Review|Senior-Review|SIU-Referral",
  "reasoning": "plain-English explanation"
}
Output ONLY raw JSON, NO markdown code fences.
""";

// New: Backend/Features/Claims/Commands/TriageClaim/
public record TriageClaimCommand(string ClaimText, string? LineOfBusiness) : IRequest<ClaimTriageResponse>;

// New: Backend/Features/Claims/Commands/TriageClaim/TriageClaimHandler.cs
public class TriageClaimHandler : IRequestHandler<TriageClaimCommand, ClaimTriageResponse>
{
    // Reuses: InsuranceAnalysisOrchestrator pattern
    // Reuses: PII redaction pipeline
    // Reuses: Provider fallback chain
    // NEW: Claims-specific agent prompt + response parsing
}
```

### New Provider Implementation Pattern

```csharp
// New: Backend/Services/Providers/MistralProvider.cs
public class MistralProvider : IAIProvider
{
    // Mistral API: https://api.mistral.ai/v1/chat/completions
    // Free tier: 1B tokens/month
    // Model: mistral-large-latest
    // Auth: Bearer token via User Secrets
}

// New: Backend/Services/Providers/OpenRouterProvider.cs
public class OpenRouterProvider : IAIProvider
{
    // OpenRouter API: https://openrouter.ai/api/v1/chat/completions
    // Free models: deepseek/deepseek-r1, meta-llama/llama-3.3-70b
    // Auth: Bearer token + HTTP-Referer header
}
```

### RAG Implementation — Key Technical Decisions

```csharp
// ChromaDB approach (local vector store, runs alongside Ollama)
// 1. Install ChromaDB: pip install chromadb (or Docker: docker run -p 8000:8000 chromadb/chroma)
// 2. Add NuGet package: ChromaDB.Client
// 3. Create collection + store embeddings:

// ChromaDB service:
public class ChromaVectorStore : IVectorStore
{
    private readonly ChromaClient _client;
    private readonly ICollection _policyCollection;

    public async Task StoreChunkAsync(string documentId, string chunkText,
        int chunkIndex, float[] embedding, Dictionary<string, string> metadata)
    {
        await _policyCollection.AddAsync(
            ids: [$"{documentId}_chunk_{chunkIndex}"],
            documents: [chunkText],
            embeddings: [embedding],
            metadatas: [metadata]  // { lob, state, type, page }
        );
    }

    public async Task<List<ChunkResult>> SearchAsync(float[] queryEmbedding,
        int topK = 5, Dictionary<string, string>? filter = null)
    {
        var results = await _policyCollection.QueryAsync(
            queryEmbeddings: [queryEmbedding],
            nResults: topK,
            where: filter  // e.g., { "lob": "homeowner" }
        );
        return MapToChunkResults(results);
    }
}

// 4. Ollama embedding call:
// POST http://localhost:11434/api/embed
// { "model": "nomic-embed-text", "input": "chunk text here" }

// ChromaDB runs on http://localhost:8000 (default)
// Data persists to local disk — unlimited storage, zero cost
```

### Concerns & Risks

1. **RAG complexity is high** — embedding, chunking, retrieval, reranking. Suggest we build a minimal RAG first (no reranking, simple chunking) and iterate.
2. **Multi-agent orchestration scaling** — Adding 4 more agents to AgentGroupChat increases token usage per analysis. Need to use **selective agent activation** — only invoke relevant agents per request type.
3. **ChromaDB local disk usage** — Stores to local disk with no hard limit. At 768-dim embeddings (nomic-embed-text), 100K chunks ≈ 500MB. Trivially expandable.
4. **Ollama embedding latency** — nomic-embed-text is fast (~50ms per chunk) but bulk document ingestion needs async queuing.

---

## QA Lead

> *"Multi-agent systems are inherently non-deterministic. Here's how we test them reliably."*

### Test Strategy for Insurance AI Hub

#### Layer 1: Unit Tests (Deterministic)

| Component | Test Focus | Framework |
|-----------|-----------|-----------|
| PII Redaction (extended) | New patterns: claim#, medical record#, VIN | xUnit + explicit assertions |
| Claims Triage Response Parsing | Two-phase parsing for new JSON schema | xUnit + fixture data |
| Fraud Score Calculation | Threshold-based routing (0-20, 21-50, etc.) | xUnit + boundary tests |
| Provider Fallback Chain | Groq→Mistral→Gemini→OpenRouter→Ollama | xUnit + Moq (mock 429s) |
| RAG Chunking | 512 tokens, 50-token overlap, edge cases | xUnit + real policy text |
| Embedding Storage | ChromaDB CRUD operations | xUnit + ChromaDB test collection |

#### Layer 2: Integration Tests (Semi-Deterministic)

| Test | What It Validates | Approach |
|------|------------------|----------|
| Claims Agent E2E | Full claim text → triage response | Real LLM call, validate JSON schema |
| Fraud Agent E2E | Suspicious claim → high fraud score | Real LLM call, validate score range |
| RAG Pipeline | Upload doc → query → relevant answer | Real embeddings + retrieval, check relevance |
| Provider Failover | Simulate 429 → verify fallback works | Mock primary, real secondary |

#### Layer 3: E2E Tests (Playwright — expanded)

```
e2e/
├── (existing 7 spec files)
├── claims-triage.spec.ts          (NEW)
│   ├── Submit claim text → see triage result
│   ├── Severity badge colors (green/yellow/red)
│   ├── Recommended actions list renders
│   ├── Fraud risk indicator displays
│   └── Error handling (429, 500, 503)
├── fraud-dashboard.spec.ts        (NEW)
│   ├── Fraud alerts list renders
│   ├── Score distribution chart loads
│   ├── Filter by fraud risk level
│   └── SIU referral action button
├── document-rag.spec.ts           (NEW)
│   ├── Upload policy document
│   ├── Ask question → get answer with citations
│   ├── Empty state before first upload
│   └── Error for unsupported file types
└── hub-navigation.spec.ts         (NEW)
    ├── Module switcher navigation
    ├── Mobile hamburger menu with all modules
    └── Breadcrumb trail
```

#### Test Data Requirements (BA must provide)

| Scenario | Required Test Data |
|----------|-------------------|
| Claims triage | 10+ realistic claim descriptions across LOBs |
| Fraud detection | 5 clean claims + 5 suspicious claims (with known fraud patterns) |
| Document RAG | 3 sample policy documents (HO, Auto, Commercial) |
| Complaint escalation | 5 escalation-trigger texts with regulatory language |

#### Quality Gates (blocks merge)

1. All existing v1 regression tests pass (SentimentControllerTests.cs)
2. New unit tests: 80%+ coverage on new code
3. Playwright E2E: all new specs pass on chromium + mobile-chrome
4. Accessibility: axe-core clean on all new screens (excluding known color-contrast)
5. PII redaction: 100% on all new data patterns (claim#, medical record#)
6. No `any` types in new TypeScript code
7. All public C# members have XML documentation

---

## UX Designer Agent

> *"I've studied the BA's requirements, the dashboard design trends, and the design inspiration sources. Here's my vision."*

### Design Inspiration Sources Referenced

- [Muzli Dashboard Design Examples 2026](https://muz.li/blog/best-dashboard-design-examples-inspirations-for-2026/)
- [SaaS Frame Dashboard Gallery (163 examples)](https://www.saasframe.io/categories/dashboard)
- [Dribbble Insurance Dashboard](https://dribbble.com/search/insurance-dashboard)
- [Behance Finance Dashboard](https://www.behance.net/search/projects/finance%20dashboard%20design)
- [Eleken Dashboard Design Examples](https://www.eleken.co/blog-posts/dashboard-design-examples-that-catch-the-eye)
- [Wrappixel Best Dashboard Designs 2026](https://www.wrappixel.com/best-dashboard-designs/)

### Design System: "InsureAI Hub"

#### Visual Identity
- **Primary palette:** Indigo-to-purple gradients (existing brand, per CLAUDE.md)
- **Accent colors:** Emerald (positive/safe), Amber (warning/medium), Rose (negative/high-risk)
- **Neutral:** Slate-50 through Slate-900 (Tailwind defaults)
- **Typography:** Inter (body), JetBrains Mono (data/scores)
- **Icon set:** Iconoir (free, MIT) + custom insurance SVGs from Flaticon

#### Layout Philosophy
Following 2026 trends from the research:
1. **Bento Grid Layout** — modular cards that users can mentally scan
2. **Progressive Disclosure** — summary → detail → deep-dive
3. **Data Storytelling** — AI-generated insight summaries, not just raw numbers
4. **Dark-first Design** — insurance analysts work long hours; dark mode is default

### Screen Designs

#### Screen 1: Hub Home (Module Launcher)

```
┌──────────────────────────────────────────────────────────────────┐
│  InsureAI Hub                              [Theme] [User] [?]   │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Welcome back, Analyst. Here's your AI-powered overview.        │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ ◉ Sentiment  │  │ ⚡ Claims    │  │ 🛡 Fraud     │          │
│  │   Analysis   │  │   Triage     │  │   Detection  │          │
│  │              │  │              │  │              │          │
│  │ Analyze      │  │ Triage new   │  │ Score claims │          │
│  │ policyholder │  │ claims in    │  │ for fraud    │          │
│  │ sentiment    │  │ seconds      │  │ probability  │          │
│  │              │  │              │  │              │          │
│  │ [Launch →]   │  │ [Launch →]   │  │ [Launch →]   │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ 📄 Document  │  │ 💬 Customer  │  │ 📊 Analytics │          │
│  │ Intelligence │  │ Experience   │  │   Dashboard  │          │
│  │              │  │   Copilot    │  │              │          │
│  │ RAG over     │  │ AI-powered   │  │ Cross-module │          │
│  │ policy docs  │  │ CX strategy  │  │ insights     │          │
│  │              │  │              │  │              │          │
│  │ [Launch →]   │  │ [Launch →]   │  │ [Launch →]   │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
│                                                                  │
│  ─── Recent Activity ─────────────────────────────────────────  │
│  │ 2 min ago  │ Claims Triage  │ Auto-Fender bender │ Low  │  │
│  │ 5 min ago  │ Fraud Score    │ Fire claim #4521   │ HIGH │  │
│  │ 12 min ago │ Sentiment      │ Complaint detected │ NEG  │  │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

#### Screen 2: Claims Triage View

```
┌──────────────────────────────────────────────────────────────────┐
│  ← Hub  /  Claims Triage                   [History] [Export]   │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  Describe the claim or paste FNOL text:                 │    │
│  │  ┌─────────────────────────────────────────────────┐    │    │
│  │  │ Water damage to kitchen ceiling discovered...   │    │    │
│  │  │                                                 │    │    │
│  │  └─────────────────────────────────────────────────┘    │    │
│  │  Line of Business: [Property ▼]    [🔍 Triage Claim]   │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ─── Triage Result ───────────────────────────────────────────  │
│                                                                  │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │ MEDIUM   │  │ HIGH     │  │ LOW      │  │ HIGH     │       │
│  │ Severity │  │ Urgency  │  │ Fraud    │  │ Churn    │       │
│  │  ◐       │  │  ●●●     │  │  ○       │  │  ●●●     │       │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘       │
│                                                                  │
│  Claim Type: Property — Water Damage                            │
│  Triage Decision: [SENIOR REVIEW]                               │
│  Estimated Reserve: $5,000 - $25,000                            │
│                                                                  │
│  ─── Recommended Actions ─────────────────────────────────────  │
│  ☑ 1. Immediate callback from senior adjuster                  │
│  ☐ 2. Fast-track inspection scheduling                         │
│  ☐ 3. Retention team alert (churn risk: HIGH)                  │
│  ☐ 4. Supervisor escalation (missed contacts)                  │
│                                                                  │
│  ─── AI Reasoning ────────────────────────────────────────────  │
│  │ "Customer reported 2 contact attempts with no response.     │
│  │  Combined with active competitor shopping ('State Farm'),    │
│  │  this indicates HIGH churn risk. Water damage severity is    │
│  │  MEDIUM (structural but not catastrophic). Recommend         │
│  │  immediate engagement via senior adjuster to retain."        │
│                                                                  │
│  ─── Agent Analysis Trail (expandable) ───────────────────────  │
│  ▶ Claims Agent analyzed (2.3s)                                 │
│  ▶ Fraud Agent scored (1.1s)                                    │
│  ▶ BA Agent reviewed domain rules (1.8s)                        │
│  ▶ QA Agent validated (0.9s)                                    │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

#### Screen 3: Fraud Detection Dashboard

```
┌──────────────────────────────────────────────────────────────────┐
│  ← Hub  /  Fraud Detection                  [Alerts] [Export]   │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌──────────┐ │
│  │   12       │  │    3       │  │   $2.1M    │  │   89%    │ │
│  │  Flagged   │  │  SIU       │  │  At-Risk   │  │ Accuracy │ │
│  │  Today     │  │  Referrals │  │  Amount    │  │  Score   │ │
│  └────────────┘  └────────────┘  └────────────┘  └──────────┘ │
│                                                                  │
│  ┌──────────────────────────┐  ┌──────────────────────────────┐ │
│  │ Fraud Score Distribution │  │ Fraud Trend (30 days)        │ │
│  │                          │  │                              │ │
│  │  ████  Clear (0-20)      │  │      ╭─╮                    │ │
│  │  ██    Low (21-50)       │  │   ╭──╯ ╰──╮    ╭──         │ │
│  │  █     Medium (51-75)    │  │ ──╯        ╰──╮╭╯           │ │
│  │  ▌     High (76-100)     │  │               ╰╯            │ │
│  └──────────────────────────┘  └──────────────────────────────┘ │
│                                                                  │
│  ─── Recent Fraud Alerts ─────────────────────────────────────  │
│  │ Score │ Claim           │ Type    │ Flags        │ Action  │ │
│  │ [92]  │ CLM-2026-4521   │ Fire    │ Recent chg,  │ [SIU→]  │ │
│  │       │                 │         │ timing       │         │ │
│  │ [67]  │ CLM-2026-3892   │ Auto    │ Inconsistent │ [Review] │ │
│  │ [45]  │ CLM-2026-4102   │ WC      │ Frequency    │ [Review] │ │
│  │ [12]  │ CLM-2026-4489   │ Property│ None         │ Auto ✓  │ │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

#### Screen 4: Document Intelligence (RAG)

```
┌──────────────────────────────────────────────────────────────────┐
│  ← Hub  /  Document Intelligence         [Upload] [Library]    │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  Ask about any uploaded policy document:                │    │
│  │  ┌─────────────────────────────────────────────────┐    │    │
│  │  │ Is water damage from a burst pipe covered       │    │    │
│  │  │ under policy HO-2024-789456?                    │    │    │
│  │  └─────────────────────────────────────────────────┘    │    │
│  │  Policy: [HO-2024-789456 ▼]        [🔍 Ask Question]   │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ─── Answer ──────────────────────────────────────────────────  │
│                                                                  │
│  ✅ Yes, water damage from a burst pipe IS covered.             │
│                                                                  │
│  Your HO-3 policy covers "sudden and accidental discharge       │
│  of water" under Section I — Dwelling Coverage. This includes   │
│  burst pipes, but excludes gradual leaks or maintenance         │
│  failures (Section I, Exclusion 2.d).                           │
│                                                                  │
│  Coverage Limit: $350,000 (Dwelling)                            │
│  Deductible: $1,000                                             │
│                                                                  │
│  ─── Sources ─────────────────────────────────────────────────  │
│  📄 Section I, p.12 — Insured Perils (water damage)            │
│  📄 Section I, p.18 — Exclusions (gradual damage)              │
│  📄 Declarations, p.2 — Coverage limits & deductibles           │
│                                                                  │
│  ─── Document Library (3 uploaded) ───────────────────────────  │
│  │ 📄 HO-2024-789456.pdf  │ Homeowner │ 42 pages │ ✓ Indexed│ │
│  │ 📄 AUTO-2024-321.pdf   │ Auto      │ 28 pages │ ✓ Indexed│ │
│  │ 📄 CGL-2024-555.pdf    │ Commercial│ 67 pages │ ✓ Indexed│ │
│  │                                     [📤 Upload New Doc]    │ │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### UX Patterns Across All Screens

| Pattern | Implementation | Why |
|---------|---------------|-----|
| **Skeleton Loaders** | Pulsing card outlines during agent analysis (15-60s) | Eliminates perceived wait, per CLAUDE.md |
| **Phase Progress** | "Claims Agent analyzing... → Fraud Agent scoring... → QA validating..." | User knows what's happening |
| **Bento Cards** | Score badges in color-coded cards with semantic icons | Quick visual scanning |
| **Expandable Trails** | Agent analysis trail collapsed by default, expandable | Power users can inspect, others aren't overwhelmed |
| **Action Checklists** | Recommended actions as interactive checkboxes | Transforms insight into action |
| **Responsive Mobile** | Cards stack vertically, hamburger nav, touch targets 44px | Field adjusters use phones |
| **Dark Mode Default** | CSS variables, 3 themes (dark/light/system) | Analysts work long hours |
| **ARIA Complete** | Every interactive element: role, aria-label, aria-live | WCAG AA compliance |

### Chart Library Recommendation

- **ng2-charts** (Chart.js wrapper) — KPI trend lines, sentiment over time, simple bar charts
- **ngx-echarts** (Apache ECharts) — Fraud distribution heatmaps, claims triage flow diagrams, geographic maps
- Both are **free, MIT licensed**, Angular-native

---

---

# ROUND 2 — Cross-Team Feedback & Challenges

## CTO → All: "Three concerns after Round 1"

### Concern 1: Scope Creep Risk
> BA proposes 8 AI modules. Architect has 6 providers. UX has 6 screens. Developer estimates 35+ days. This is growing.

**CTO Ruling:** We build **3 modules in 12 weeks**, not 6. Priority:
1. Claims Triage (Sprint 1-2)
2. Fraud Scoring (Sprint 2-3) — shares 70% of Claims infrastructure
3. Document Intelligence RAG (Sprint 3-4)

CX Copilot moves to Phase 2 (post-MVP). It's a natural extension once we have claims + fraud data.

### Concern 2: Provider Complexity
> 6 providers in the fallback chain is too many to test, maintain, and debug.

**CTO Ruling (REVISED — 2026-02-21):** Team has now secured API keys for **all providers**. Updated strategy — organize by **capability**, not a single chain:

| Capability | Providers (priority order) | Status |
|-----------|--------------------------|--------|
| **Text LLM** | Groq → Mistral → Gemini → OpenRouter → Ollama | ✅ All keys active |
| **Vision/Multimodal** | Gemini Flash → Cloudflare Workers AI → Azure Vision F0 → Ollama LLaVA | ✅ All keys active |
| **Document OCR** | OCR.space → Azure Vision F0 → Gemini (PDF native) | ✅ All keys active |
| **Speech-to-Text** | Deepgram → Browser SpeechRecognition API | ✅ Deepgram $200 credit |
| **Specialized NLP** | HuggingFace (NER, classification) | ✅ Key active |
| **Embeddings** | Ollama nomic-embed-text (local) | ✅ Installed |

Keep OpenAI as legacy (existing, don't remove). Azure budget alert set at $1 to prevent charges.

### Concern 3: Agent Pool Explosion
> Adding 4 domain agents to a single AgentGroupChat will cause token bloat and slower responses.

**CTO Ruling:** Architect must design **selective agent activation**. Claims request → only activate Claims + Fraud + BA + QA agents. Don't wake CTO/Developer/Architect/UX for a claims triage.

---

## Architect → CTO: "Selective Agent Activation — proposal"

### Dynamic Orchestrator Pattern

```csharp
// Instead of one big AgentGroupChat with all agents,
// create focused orchestration profiles:

public enum OrchestrationProfile
{
    SentimentAnalysis,      // existing: CTO, BA, Dev, QA, Architect, UX
    ClaimsTriage,           // new: Claims, Fraud, BA, QA
    FraudScoring,           // new: Fraud, Claims, BA, QA
    DocumentQuery,          // new: Document, BA, QA
    CustomerExperience      // future: CX, BA, Sentiment, QA
}

// Factory creates the right agent group per profile:
public class OrchestrationProfileFactory
{
    public AgentGroupChat CreateForProfile(OrchestrationProfile profile)
    {
        return profile switch
        {
            OrchestrationProfile.ClaimsTriage => new AgentGroupChat(
                _claimsAgent, _fraudAgent, _baAgent, _qaAgent),
            // ...
        };
    }
}
```

**Benefits:**
- 50-60% fewer tokens per request (4 agents instead of 10)
- Faster response times (fewer agent turns)
- Each profile has its own `AgentSelectionStrategy` and `TerminationStrategy`
- Existing sentiment analysis profile is **unchanged** (backward compatible)

### Architect → Developer: "RAG Simplification"

> Developer flagged RAG as 8-10 days and high risk. Let's simplify.

**Simplified RAG for MVP:**
- No reranking (save for Phase 2)
- No multi-format upload (PDF only for MVP, via Apache PDFBox or PdfPig for .NET)
- Fixed chunking: 512 tokens, 50-token overlap (no configurable strategy)
- ChromaDB only (local, no cloud vector DB dependency)
- Ollama nomic-embed-text only (no embedding model switching)

**Estimated effort after simplification: 5-6 days** (down from 8-10)

---

## BA → Architect + Developer: "Domain validation concerns"

### Insurance-Specific Edge Cases the Team Must Handle

1. **Multi-peril claims:** A single event can trigger multiple coverages (e.g., storm damages roof AND floods basement). Claims agent must identify ALL applicable coverages, not just the first match.

2. **Subrogation potential:** If a third party caused the loss (e.g., negligent contractor), the system must flag subrogation recovery opportunity. This is money back for the insurer.

3. **State-specific regulations:**
   - California: 15-day acknowledgment requirement
   - Florida: Hurricane deductible rules
   - New York: Prompt payment laws
   - The Claims Agent prompt needs state context.

4. **Reserve accuracy disclaimer:** ANY AI-suggested reserve amount MUST display:
   > "AI-estimated range. Subject to adjuster review and approval. Not a binding commitment."

### BA → QA: "Test data I'll provide"

I commit to providing:
- 15 realistic claim scenarios across 5 LOBs (Auto, Home, Commercial, WC, Health)
- 5 known-fraud claim patterns (based on NICB/SIU literature)
- 3 sample policy documents (redacted from real policies, anonymized)
- 10 complaint escalation texts with regulatory trigger words
- All test data will use synthetic PII (HO-0000-TEST01, CLM-0000-TEST01, etc.)

---

## QA → Developer: "Parsing risk for new agents"

> We learned the hard way with sentiment analysis that LLM agents wrap JSON in markdown fences and omit fields. The Claims + Fraud agents will have the SAME problem.

### QA Demands:

1. **Reuse `NormalizeForJsonExtraction()`** — the fence-stripping logic already exists. Every new agent response parser MUST go through it.

2. **Two-phase parsing for ALL new response types** — strict `JsonSerializer.Deserialize<T>()` first, then `JsonDocument` manual extraction as fallback. No exceptions.

3. **Property name case tolerance** — support both `camelCase` and `PascalCase` for every field (LLMs are inconsistent).

4. **Missing field defaults** — If Claims agent omits `regulatoryFlags`, default to empty array, don't throw. Define sensible defaults for every nullable field.

5. **Test with REAL LLM output** — capture 20+ actual responses from Groq/Mistral/Gemini for claims triage and test parsing against all of them. Store as test fixtures.

---

## AI Expert → Architect: "Mistral provider is the biggest free-tier upgrade"

> Mistral's 1 billion tokens/month free tier is transformative. At ~750 tokens per claims analysis, that's **1.3 million free analyses per month**. This alone removes any concern about free-tier limits.

### Updated Token Budget

| Operation | Tokens/Request | Provider | Monthly Free Capacity |
|-----------|---------------|----------|----------------------|
| Claims Triage | ~750 | Mistral | 1,333,333 analyses |
| Fraud Scoring | ~500 | Mistral | 2,000,000 analyses |
| Sentiment Analysis | ~600 | Groq | ~500,000 (14.4K/day) |
| Document RAG Query | ~2,000 | Gemini (1M context) | ~500,000 |
| Embedding Generation | ~200 | Ollama (local) | Unlimited |

**Total monthly capacity: 4M+ free analyses across all modules.** This is enterprise-grade capacity at zero cost.

### AI Expert → CTO: "Recommend DeepSeek R1 for complex reasoning"

For claims involving complex multi-peril, multi-party scenarios, DeepSeek R1 (available free via OpenRouter) has the best reasoning capability. Suggest we add it as a "heavy reasoning" option for claims triage when fraud score > 50 (route complex cases to a better reasoner).

---

## UX → BA + CTO: "Mobile-first concern"

> BA mentioned insurance adjusters use phones in the field. Current design is desktop-first.

### UX Proposal: Mobile-First Claims Triage

For Claims Triage specifically (field adjusters), design **mobile-first**:
- Voice-to-text claim input (browser native `SpeechRecognition` API — free)
- Camera capture for damage photos (future: multimodal analysis)
- Swipe-through result cards instead of desktop layout
- Offline-first capability consideration (service worker cache)

**CTO, should we prioritize mobile UX for claims triage specifically?**

---

---

# ROUND 3 — Final Consensus & Decisions

## CTO Final Rulings

After 2 rounds of discussion, here are the binding decisions:

### Decision 1: Scope — 3 Modules, 12 Weeks

| Sprint | Module | Duration | Agents Activated |
|--------|--------|----------|-----------------|
| Sprint 1 (Weeks 1-4) | Claims Triage | 4 weeks | Claims, Fraud, BA, QA |
| Sprint 2 (Weeks 5-8) | Fraud Detection Dashboard | 4 weeks | Fraud, Claims, BA, QA |
| Sprint 3 (Weeks 9-12) | Document Intelligence (RAG) | 4 weeks | Document, BA, QA |

CX Copilot → Phase 2 (post-MVP).

### Decision 2: Provider Ecosystem — Full Stack Secured (REVISED)

All API keys are now active. Providers organized by capability:

```
TEXT LLM Chain:
  Groq (Llama 3.3 70B) → Mistral (Large 2) → Gemini (2.0 Flash)
    → OpenRouter (DeepSeek R1, 24+ models) → Ollama (local)

VISION / MULTIMODAL Chain:
  Gemini Flash (vision+audio) → Cloudflare Workers AI (Llama 4 Scout, Gemma 3)
    → Azure AI Vision F0 (5K/month) → Ollama LLaVA (local)

DOCUMENT OCR Chain:
  OCR.space (500/day) → Azure AI Vision F0 → Gemini (PDF native)

SPEECH-TO-TEXT:
  Deepgram ($200 credit, no expiry) → Browser SpeechRecognition API (free)

SPECIALIZED NLP:
  HuggingFace (NER, entity extraction, classification)

EMBEDDINGS:
  Ollama nomic-embed-text (local, unlimited)

LEGACY:
  OpenAI (GPT-4o-mini) → existing credits, v1 only
```

Azure budget alert set at **$1** — email notification on any charge. All Azure resources use **F0 (free) SKU only**.

### Decision 3: Selective Agent Activation — APPROVED

Architect's `OrchestrationProfile` pattern is approved. Each module gets a focused agent group. Existing sentiment analysis remains unchanged.

### Decision 4: RAG Simplification — APPROVED

Simplified RAG (no reranking, PDF only, fixed chunking, ChromaDB local, Ollama embeddings). This reduces effort from 10 days to 6 days.

### Decision 5: Mobile-First for Claims — EXPANDED (REVISED)

- Mobile-responsive layout: **YES** (Sprint 1, all screens)
- Voice-to-text input: **YES** — now via **Deepgram** ($200 credit) for server-side + Browser SpeechRecognition for client-side
- Camera capture for damage photos: **YES — MOVED TO SPRINT 1** — Gemini Flash + Cloudflare Workers AI now provide free multimodal vision
- Image analysis of claim damage: **YES** — Azure AI Vision F0 (5K/month free) for structured damage assessment
- Offline-first: DEFERRED (service workers are complex, low ROI for MVP)

### Decision 6: Reserve Amount Disclaimer — MANDATORY

Every AI-suggested financial figure (reserve, settlement, pricing) MUST display the BA-mandated disclaimer. UX: render as a yellow warning banner, not small print.

### Decision 7: Test Data Commitment

BA delivers test fixtures by end of Week 1. QA captures 20+ real LLM responses during Sprint 1 dev for parsing test fixtures. No module ships without full test coverage.

---

## Architect → All: "Final API Contract"

### New Endpoints (v2 Extension)

```
POST /api/insurance/claims/triage
  Request:  { "claimText": string, "lineOfBusiness"?: string, "state"?: string }
  Response: { ...sentimentFields, claimTriage: { claimType, severity, urgency,
              fraudRiskScore, churnRiskScore, estimatedReserve, recommendedActions,
              regulatoryFlags, triageDecision, reasoning } }

POST /api/insurance/fraud/score
  Request:  { "claimText": string, "claimId"?: string, "claimHistory"?: object[] }
  Response: { ...sentimentFields, fraudAssessment: { fraudScore: 0-100,
              riskLevel, indicators[], recommendation, reasoning } }

POST /api/insurance/documents/ingest
  Request:  multipart/form-data { file: PDF, policyType: string, state?: string }
  Response: { documentId, chunks: number, status: "indexed" }

POST /api/insurance/documents/query
  Request:  { "question": string, "documentId"?: string, "policyType"?: string }
  Response: { answer: string, sources: { page, section, relevantText }[],
              confidence: number }

GET  /api/insurance/hub/summary
  Response: { totalAnalyses, claimsTriaged, fraudFlagged, documentsIndexed,
              recentActivity[] }
```

### Response Pattern: Extend, Don't Break

All new responses **extend** the existing `InsuranceAnalysisResponse` pattern — they include the base sentiment fields (`sentiment`, `confidenceScore`, `explanation`, `emotionBreakdown`) PLUS module-specific fields. This means:
- Frontend can reuse existing sentiment display components
- Backward compatibility with any client expecting v2 response shape
- Progressive enhancement: show sentiment first, then domain-specific results

---

## Developer → All: "Final Implementation Breakdown"

### Sprint 1 — Claims Triage + Multimodal (Weeks 1-4)

> **CTO:** "With Deepgram, Cloudflare, and Azure Vision now available, Sprint 1 includes voice input and damage photo analysis — not just text."
> **BA:** "This is game-changing. Field adjusters can now speak a claim AND photograph damage — complete FNOL in 60 seconds."
> **UX:** "Claims Triage screen now has 3 input modes: text, voice, and camera. Progressive enhancement."
> **Architect:** "Provider abstraction pattern handles all of this. Each input mode is a separate service behind an interface."

**Week 1: Infrastructure + New Providers**
- [ ] Add Mistral provider (`MistralProvider.cs` implementing `IAIProvider`)
- [ ] Add OpenRouter provider (`OpenRouterProvider.cs` — access DeepSeek R1 + 24 free models)
- [ ] Add Cloudflare Workers AI provider (`CloudflareAIProvider.cs` — multimodal vision)
- [ ] Add Deepgram service (`DeepgramTranscriptionService.cs` — speech-to-text)
- [ ] Add Azure Vision service (`AzureVisionService.cs` — image analysis, F0 tier)
- [ ] Add OCR.space service (`OcrSpaceService.cs` — document OCR)
- [ ] Add HuggingFace service (`HuggingFaceNlpService.cs` — NER, entity extraction)
- [ ] Update provider fallback chain configuration (capability-based routing)
- [ ] Implement `OrchestrationProfile` factory (selective agent activation)
- [ ] Add `ClaimsTriage` and `FraudDetection` to `AgentRole` enum
- [ ] Store all new API keys in .NET User Secrets
- [ ] BA delivers test fixture data (15 claims, 5 fraud patterns, 10 complaints)

**Week 2: Claims Agent + Multimodal Input**
- [ ] Write Claims Triage agent prompt in `AgentDefinitions.cs`
- [ ] Create `TriageClaimCommand` / `TriageClaimHandler` (CQRS)
- [ ] Create `ClaimTriageResponse` model
- [ ] Implement two-phase response parsing (reuse `NormalizeForJsonExtraction`)
- [ ] Add `ClaimsController` with `POST /api/insurance/claims/triage`
- [ ] **NEW:** Add voice-to-text endpoint using Deepgram API (server-side transcription)
- [ ] **NEW:** Add damage photo analysis endpoint using Gemini Flash vision (primary) → Cloudflare Workers AI (fallback)
- [ ] **NEW:** Add Azure Vision F0 integration for structured image labels (damage type, severity indicators)
- [ ] **NEW:** Add HuggingFace NER for extracting policy#, claim#, names, dates from claim text
- [ ] Write unit tests (parsing, PII redaction, domain rules, provider fallback)

**Week 3: Frontend — Claims Triage Screen (3 Input Modes)**
- [ ] Create `ClaimsTriageComponent` (standalone, Angular signals)
- [ ] **Text input:** Claims textarea with LOB selector
- [ ] **NEW: Voice input:** Microphone button → Deepgram server-side STT (with Browser SpeechRecognition as client-side fallback)
- [ ] **NEW: Photo input:** Camera/upload button → damage photo sent to Gemini Flash vision for AI description
- [ ] Triage result display (severity/urgency/fraud/churn badges in bento cards)
- [ ] Recommended actions checklist (interactive checkboxes)
- [ ] Agent analysis trail (expandable accordion)
- [ ] Skeleton loader with phase progress ("Analyzing photo... → Triaging claim... → Scoring fraud...")
- [ ] Reserve disclaimer banner (yellow warning, BA-mandated)
- [ ] Add route `/claims` to `app.routes.ts`
- [ ] Write Playwright e2e tests (`claims-triage.spec.ts`)

**Week 4: Integration + Polish**
- [ ] QA captures real LLM response fixtures (20+ from Groq/Mistral/Gemini)
- [ ] Integration tests with real providers (text + voice + image paths)
- [ ] Mobile responsive testing (voice + camera = phone-first features)
- [ ] Accessibility audit (axe-core, ARIA for voice/camera buttons)
- [ ] Provider fallback chain E2E test (simulate 429 on each provider)
- [ ] Sprint 1 demo: "Speak a claim, photograph damage, get AI triage in 30 seconds"

### Sprint 2 — Fraud Detection Dashboard + Image Forensics (Weeks 5-8)

> **CTO:** "Fraud detection now has multimodal capability — not just text analysis but image manipulation detection."
> **BA:** "Photo fraud is a $5B+ subset of the $80B problem. Doctored damage photos are the #1 SIU complaint."
> **AI Expert:** "Azure Vision F0 + Gemini Flash can detect image inconsistencies. Cloudflare provides a second opinion."
> **QA:** "Fraud scoring boundary tests are critical — false SIU referrals have legal consequences."
> **UX:** "Fraud dashboard needs clear visual hierarchy: KPI cards → charts → alerts table → drill-down."

**Week 5: Fraud Agent + Image Forensics**
- [ ] Write Fraud Detection agent prompt (text + image signals)
- [ ] Create `ScoreFraudCommand` / `ScoreFraudHandler`
- [ ] Fraud scoring model (0-100 with threshold-based routing)
- [ ] **NEW:** Image analysis signal — Azure Vision F0 extracts image metadata + labels
- [ ] **NEW:** Gemini Flash analyzes damage photo consistency (does narrative match photo?)
- [ ] **NEW:** HuggingFace entity extraction — cross-reference names, dates, addresses across claims
- [ ] Cross-reference with claims triage results
- [ ] Unit tests (boundary values: 0, 20, 21, 50, 51, 75, 76, 100)

**Week 6: Fraud Dashboard Frontend**
- [ ] KPI cards (flagged today, SIU referrals, at-risk amount, accuracy)
- [ ] Fraud score distribution chart (ngx-echarts — horizontal bar chart)
- [ ] Fraud trend line over 30 days (ng2-charts — line chart)
- [ ] Alerts table with severity badges and action buttons (SIU Referral, Review, Auto-Clear)
- [ ] Filter by risk level, LOB, date range
- [ ] **NEW:** Photo evidence thumbnail in fraud alert rows (click to expand + see AI analysis)
- [ ] **NEW:** Audio transcript snippet for voice-submitted claims (Deepgram transcription)

**Week 7: Claims + Fraud Integration + Hub Home**
- [ ] Claims triage auto-triggers fraud scoring (pipeline: text+photo → claims agent → fraud agent)
- [ ] Fraud dashboard pulls from claims history repository
- [ ] SIU referral workflow (flag → notify → track status)
- [ ] **Hub Home screen** with module launcher (Bento grid: Sentiment, Claims, Fraud, Documents, Analytics)
- [ ] Recent Activity feed across all modules
- [ ] **NEW:** Voice-recorded claims show transcript + audio player in history

**Week 8: Sprint 2 Testing + Polish**
- [ ] E2E tests (`fraud-dashboard.spec.ts`, `hub-navigation.spec.ts`)
- [ ] Cross-module integration tests (claim → fraud pipeline)
- [ ] Mobile responsive + accessibility
- [ ] **NEW:** Test multimodal fraud flow (text + photo input → fraud score with image analysis)
- [ ] Sprint 2 demo: "Submit a suspicious fire claim with photos — AI detects inconsistencies and refers to SIU"

### Sprint 3 — Document Intelligence RAG + Voice Q&A (Weeks 9-12)

> **CTO:** "RAG is now more powerful — OCR.space handles scanned docs, Deepgram enables voice Q&A, Gemini provides 1M context."
> **BA:** "Underwriters can now ask questions about policies by VOICE. And we can OCR scanned legacy documents."
> **Architect:** "Triple OCR pipeline: PdfPig for digital PDFs, OCR.space for scanned images, Azure Vision for complex layouts."
> **AI Expert:** "Gemini 2.0 Flash with 1M context can ingest an entire policy book. No chunking needed for small docs."
> **UX:** "Document Q&A gets a chat interface with voice input. Like talking to your policy manual."

**Week 9: RAG Infrastructure + Multi-Source OCR**
- [ ] Install ChromaDB locally (Docker or pip) — runs on `http://localhost:8000`
- [ ] Add `ChromaDB.Client` NuGet package
- [ ] Create `IVectorStore` interface + `ChromaVectorStore` implementation
- [ ] Create `DocumentEmbedding` entity + repository
- [ ] Implement PDF text extraction (PdfPig — .NET native, for digital PDFs)
- [ ] **NEW:** Add OCR.space integration for scanned/image PDFs (500 req/day)
- [ ] **NEW:** Add Azure Vision F0 OCR for complex document layouts (5K/month)
- [ ] **NEW:** Smart OCR routing: digital PDF → PdfPig (free, instant) | scanned PDF → OCR.space → Azure Vision fallback
- [ ] Implement chunking service (512 tokens, 50-token overlap)
- [ ] Ollama nomic-embed-text embedding integration

**Week 10: RAG Pipeline + Voice Q&A**
- [ ] Document ingestion endpoint (`POST /api/insurance/documents/ingest`)
- [ ] Semantic search service (ChromaDB cosine similarity)
- [ ] Document Intelligence agent prompt
- [ ] Query endpoint (`POST /api/insurance/documents/query`)
- [ ] Answer generation with source citations (page, section, relevantText)
- [ ] **NEW:** Voice Q&A — Deepgram transcribes spoken question → RAG pipeline → spoken answer (browser TTS)
- [ ] **NEW:** For small docs (<100 pages), option to use Gemini 1M context directly (skip chunking entirely)
- [ ] **NEW:** HuggingFace NER extracts coverage limits, deductibles, effective dates from policy text

**Week 11: Document Intelligence Frontend**
- [ ] Document upload component (drag-and-drop, PDF only, max 50 pages for MVP)
- [ ] **Chat-style Q&A interface** (message bubbles, typed + voice input)
- [ ] **NEW:** Microphone button for voice questions (Deepgram STT)
- [ ] Source citation display with page references (clickable to show extracted text)
- [ ] Document library view (uploaded docs with indexing status)
- [ ] **NEW:** Scanned document indicator — shows OCR method used (PdfPig/OCR.space/Azure)
- [ ] Skeleton loaders + error states

**Week 12: Final Integration + MVP Ship**
- [ ] Hub summary endpoint (`GET /api/insurance/hub/summary`) — all module stats
- [ ] Hub home screen with all module KPI cards + recent activity
- [ ] Full E2E suite (all new specs: claims, fraud, documents, hub navigation)
- [ ] Performance testing (response times under free-tier loads)
- [ ] Final accessibility audit (WCAG AA on all new screens)
- [ ] **NEW:** Provider usage dashboard — show free-tier consumption across all providers
- [ ] MVP demo / stakeholder presentation: "Complete Insurance AI Operations Hub — voice, photo, text, documents"

---

## QA → All: "Final Test Matrix"

| Test Category | Count | Sprint | Blocks Ship? |
|---------------|-------|--------|-------------|
| v1 Regression (existing) | ~15 | Every sprint | YES |
| Claims Triage Unit Tests | ~25 | Sprint 1 | YES |
| Fraud Scoring Unit Tests | ~20 | Sprint 2 | YES |
| RAG Pipeline Unit Tests | ~15 | Sprint 3 | YES |
| Provider Fallback Tests | ~10 | Sprint 1 | YES |
| Parsing Resilience Tests | ~20 | Sprint 1-3 | YES |
| PII Redaction (extended) | ~10 | Sprint 1 | YES |
| Playwright E2E (new) | ~40 | Sprint 1-3 | YES |
| Accessibility (axe-core) | ~15 | Sprint 1-3 | YES |
| Integration (live LLM) | ~10 | Each sprint | NO (flaky) |
| **Total New Tests** | **~180** | | |

---

---

# FINAL IMPLEMENTATION PLAN

## Insurance AI Operations Hub — MVP Build Plan

### Vision
Transform the Sentiment Analyzer into a **3-module Insurance AI Operations Hub**: Claims Triage + Fraud Detection + Document Intelligence. Free-tier only. 12 weeks.

### Free Tools & Technology Stack (Final)

#### AI Providers — Full Ecosystem (All Keys Active, Zero Cost)

**Text LLM Providers:**

| Provider | Model | Free Tier | Role | Status |
|----------|-------|-----------|------|--------|
| **Groq** | Llama 3.3 70B | 14,400 req/day | Real-time sentiment, fast claims | ✅ Active |
| **Mistral** | Large 2 | 1B tokens/month | Bulk claims + fraud analysis | ✅ Active |
| **Gemini** | 2.0 Flash | 60 req/min, 1M context | Long doc RAG, multimodal vision | ✅ Active |
| **OpenRouter** | 24+ models (DeepSeek R1) | 50 req/day | Complex reasoning, model A/B testing | ✅ Active |
| **Ollama** | Llama 3.2, nomic-embed | Unlimited (local) | PII processing, embeddings | ✅ Installed |
| **OpenAI** | GPT-4o-mini | Existing credits | Legacy v1 only | ✅ Active |

**Multimodal & Vision Providers (NEW):**

| Provider | Capability | Free Tier | Insurance Use Case | Status |
|----------|-----------|-----------|-------------------|--------|
| **Gemini Flash** | Image + audio + video | 1000 req/day | Damage photo analysis, video evidence | ✅ Active |
| **Cloudflare Workers AI** | Vision models (Llama 4 Scout, Gemma 3) | 10K neurons/day | Edge multimodal inference | ✅ Active |
| **Azure AI Vision F0** | OCR, labels, object detection | 5,000 txn/month | Structured damage assessment | ✅ Active |
| **Ollama LLaVA** | Local vision model | Unlimited | PII-safe damage photo analysis | ✅ Available |

**Document & Speech Providers (NEW):**

| Provider | Capability | Free Tier | Insurance Use Case | Status |
|----------|-----------|-----------|-------------------|--------|
| **OCR.space** | Document OCR | 500 req/day | Scanned policy extraction | ✅ Active |
| **Deepgram** | Speech-to-text | $200 credit (no expiry) | Call recording transcription, voice claims | ✅ Active |
| **HuggingFace** | NER, classification, 300+ models | 300 req/hour | Entity extraction, specialized NLP | ✅ Active |

**Azure Protection:** Budget alert set at **$1** — email on any charge. All Azure resources = **F0 (free) SKU only**. F0 blocks at limit (returns 429), never charges overage.

#### Infrastructure (Zero Cost)

| Component | Free Service | Limit |
|-----------|-------------|-------|
| Frontend hosting | Azure Static Web Apps | 100GB BW, forever free |
| Backend hosting | Azure App Service B1 | 750 hrs/mo, 12 months |
| Database | Supabase PostgreSQL | 500MB, forever free |
| Vector DB | ChromaDB (local) | Unlimited (local disk) |
| Secrets | Azure Key Vault | 10K ops/mo, forever free |
| CI/CD | GitHub Actions | 2,000 min/mo, forever free |
| Monitoring | Application Insights | 5GB/mo, forever free |
| Image Analysis | Azure AI Vision F0 | 5K txn/mo, forever free |
| Embeddings | Ollama nomic-embed-text | Unlimited (local) |
| PDF Processing | PdfPig (.NET library) | MIT license, free |
| Document OCR | OCR.space | 500 req/day, free |
| Speech-to-Text | Deepgram | $200 credit, no expiry |
| Edge AI | Cloudflare Workers AI | 10K neurons/day, free |
| Charts | ng2-charts + ngx-echarts | MIT license, free |
| Icons | Iconoir + Flaticon (free) | MIT / free attribution |

**Total monthly cost: $0**

#### Development & Design (Zero Cost)

| Tool | Purpose | Free Tier |
|------|---------|-----------|
| Tailwind CSS 3.4 | Styling | MIT, free |
| Angular Material | UI components | MIT, free |
| ng2-charts (Chart.js) | Simple charts/KPIs | MIT, free |
| ngx-echarts (Apache ECharts) | Complex visualizations | Apache 2.0, free |
| Playwright | E2E testing | MIT, free |
| Vitest | Frontend unit testing | MIT, free |
| xUnit + Moq | Backend testing | MIT, free |

### Design Inspiration References

| Source | What to Study |
|--------|--------------|
| [Muzli Dashboard 2026](https://muz.li/blog/best-dashboard-design-examples-inspirations-for-2026/) | Bento grid layouts, dark-mode KPIs |
| [SaaS Frame (163 dashboards)](https://www.saasframe.io/categories/dashboard) | SaaS analytics patterns |
| [Dribbble Insurance](https://dribbble.com/search/insurance-dashboard) | Insurance-domain specific designs |
| [Behance Finance](https://www.behance.net/search/projects/finance%20dashboard%20design) | Finance/risk data visualization |
| [Eleken Dashboard Examples](https://www.eleken.co/blog-posts/dashboard-design-examples-that-catch-the-eye) | Progressive disclosure patterns |
| [TailAdmin](https://tailadmin.com/blog/saas-dashboard-templates) | Tailwind dashboard component patterns |
| [Wrappixel 2026](https://www.wrappixel.com/best-dashboard-designs/) | Chart + table layout combinations |

### Sprint Schedule

```
Week  1  ──── Sprint 1: Claims Triage ────────────────
Week  2  │ Infrastructure + Mistral provider
Week  3  │ Claims Agent + Frontend screen
Week  4  │ Integration, testing, Sprint 1 demo
         ─────────────────────────────────────────────
Week  5  ──── Sprint 2: Fraud Detection ──────────────
Week  6  │ Fraud Agent + Dashboard frontend
Week  7  │ Claims ↔ Fraud integration + Hub home
Week  8  │ Testing, polish, Sprint 2 demo
         ─────────────────────────────────────────────
Week  9  ──── Sprint 3: Document Intelligence ────────
Week 10  │ RAG pipeline + ingestion
Week 11  │ Document Q&A frontend
Week 12  │ Hub integration, final tests, MVP ship
         ─────────────────────────────────────────────
```

### Success Criteria (MVP — REVISED)

| Metric | Target |
|--------|--------|
| Claims triage accuracy | >85% correct severity/urgency (vs BA expert rating) |
| Fraud scoring precision | >70% true positive rate (no false SIU referrals >10%) |
| RAG answer relevance | >80% of answers cite correct policy section |
| **Voice-to-claim accuracy** | >90% transcription accuracy (Deepgram) |
| **Photo damage analysis** | >80% correct damage type identification (Gemini/Cloudflare) |
| **OCR extraction accuracy** | >95% text extraction from scanned docs (OCR.space/Azure) |
| Response time (claims — text) | <30 seconds with skeleton UI |
| Response time (claims — voice+photo) | <45 seconds (transcription + vision + triage) |
| Response time (RAG query) | <10 seconds (embedding + search + generation) |
| Free-tier utilization | <50% of any provider's monthly limit |
| Azure charges | **$0** (F0 tier only, $1 budget alert active) |
| Test coverage (new code) | >80% backend, >75% frontend |
| Accessibility | WCAG AA on all new screens |
| Mobile responsive | All screens usable on 375px viewport |
| Zero PII leakage | 100% redaction on all external calls |

### Risk Mitigation Plan (REVISED)

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Mistral free tier changes/removes | Low | High | 5 other text LLM providers ready; adapter pattern = swap in 1 day |
| LLM claims triage accuracy <85% | Medium | Medium | Few-shot examples in prompt; BA reviews first 50 analyses |
| ChromaDB server unavailable | Low | Medium | ChromaDB runs locally — restart service. Data persists on disk, no data loss |
| Agent response parsing failures | High | Medium | Two-phase parsing + 20 real-response fixtures (proven pattern) |
| RAG answers are inaccurate | Medium | High | Human review for first 100 queries; confidence score threshold |
| Sprint overrun | Medium | Medium | CX Copilot is Phase 2 buffer; RAG scope already simplified |
| **Deepgram $200 credit exhausted** | Low | Low | Browser SpeechRecognition API as zero-cost fallback |
| **Azure Vision F0 limit (5K/month)** | Low | Low | F0 blocks at limit (429, no charge). Gemini Flash as fallback |
| **OCR.space rate limit (500/day)** | Medium | Low | Azure Vision F0 as fallback. PdfPig handles digital PDFs free |
| **Cloudflare model availability** | Low | Low | Gemini Flash + Azure Vision cover all vision use cases |
| **Azure charges accidentally incur** | Low | Medium | $1 budget alert active. All resources F0 SKU. No VMs created |

### Monthly Free Capacity (All Providers Combined)

| Capability | Monthly Capacity | Providers |
|-----------|-----------------|-----------|
| Text analyses | **4M+** | Groq (450K) + Mistral (1.3M) + Gemini + OpenRouter + Ollama |
| Image/Vision analyses | **35K+** | Gemini (30K) + Azure Vision (5K) + Cloudflare (300K neurons) |
| Document OCR | **20K+** | OCR.space (15K) + Azure Vision (5K) + PdfPig (unlimited) |
| Speech transcription | **~3,300 hours** | Deepgram ($200 credit) + browser fallback |
| Embeddings | **Unlimited** | Ollama nomic-embed-text (local) |
| NER/Entity extraction | **~216K** | HuggingFace (300/hr × 720 hrs) |

### Deliverables per Sprint (REVISED)

| Sprint | Demo Deliverable | Business Value |
|--------|-----------------|----------------|
| Sprint 1 | **Multimodal claims triage:** speak a claim, photograph damage, get AI triage with fraud flags + action items in 30 seconds | "Field adjusters do complete FNOL from their phone — voice + photo + AI" |
| Sprint 2 | **Fraud dashboard with image forensics:** scoring, photo analysis, alerts, SIU routing | "AI detects doctored damage photos + text inconsistencies in the $80B fraud problem" |
| Sprint 3 | **Voice-enabled policy Q&A:** upload scanned/digital docs, ask questions by voice or text, get cited answers | "Underwriters talk to their policy manuals — 60% time saved on document review" |

---

---

## Revision History

| Version | Date | Changes |
|---------|------|---------|
| v1.0 | 2026-02-21 | Initial plan — 4 text providers, text-only claims triage |
| v2.0 | 2026-02-21 | REVISED — Added 7 new providers: Deepgram (STT), Cloudflare Workers AI (multimodal), OpenRouter (24+ models), HuggingFace (NER), OCR.space (OCR), Azure AI Vision F0 (image analysis). Sprint 1 now includes voice input + damage photo analysis. Sprint 2 adds image forensics for fraud. Sprint 3 adds multi-source OCR + voice Q&A. All API keys secured. Azure $1 budget alert active. |
| **v2.1** | **2026-02-23** | **REVISED — Replaced Supabase pgvector with ChromaDB (local) for vector storage. Supabase pgvector unavailable in user's region. ChromaDB runs locally alongside Ollama — same local-first pattern, unlimited storage, zero cost. Updated architecture diagram, RAG pipeline, Developer implementation code, Sprint 3 tasks, infrastructure table, risk mitigation. ChromaDB.Client NuGet + IVectorStore interface pattern.** |

---

*This plan was collaboratively developed across 3 rounds of discussion by all 7 team agents (CTO, BA, Architect, AI Expert, Developer, QA, UX Designer) following the project's established decision authority: BA proposes domain priority, Architect designs technical solution, Developer validates feasibility, QA defines quality gates, AI Expert selects models, UX Designer creates screens, CTO makes final decisions.*

*All provider API keys are active. Azure F0 free tier confirmed ($0 cost, $1 budget alert set). Ready to begin Sprint 1.*
