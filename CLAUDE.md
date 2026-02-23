# CLAUDE.md - AI Assistant Guidelines for Insurance Domain Sentiment Analyzer

## 1. Project Overview & Architecture

### What This Project Is
An AI-powered sentiment analysis platform specialized for the insurance industry. It analyzes policyholder communications, claims notes, agent reviews, and regulatory correspondence to extract sentiment, emotions, insurance-specific context, purchase intent, and complaint escalation signals.

### Version History
- **v1.0 (Baseline)**: General-purpose sentiment analyzer. .NET 10 API + Angular 21 SPA + OpenAI GPT-4o-mini. Single endpoint: `POST /api/sentiment/analyze`.
- **v2.0**: Insurance-domain multi-agent system with free AI providers (Groq, Gemini, Ollama), Semantic Kernel orchestration, persistent storage, insurance context classification, and role-based dashboards.
- **v3.0 (Current)**: Insurance AI Operations Hub. 5-provider resilient fallback chain (Groq/Mistral/Gemini/OpenRouter/Ollama), 5 multimodal services (STT/Vision x2/OCR/NER), claims triage + fraud detection agents, orchestration profiles, PII redaction across multimodal pipeline. See `SPRINT-ROADMAP.md` for sprint details.

### Architecture
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
         └── Fraud Detection Agent (v3.0)
              |
         IResilientKernelProvider (v3.0 - runtime fallback)
         ├── Groq (primary, fast)
         ├── Mistral (v3.0)
         ├── Gemini (quality)
         ├── OpenRouter (v3.0)
         ├── Ollama (local, PII-safe, always-on fallback)
         └── OpenAI (legacy v1)
              |
         Multimodal Services (v3.0)
         ├── Deepgram (STT - voice notes/calls)
         ├── Azure Vision (image analysis - primary)
         ├── Cloudflare Vision (image analysis - secondary)
         ├── OCR.space (document OCR)
         └── HuggingFace (NER - entity extraction)
              |
         SQLite / Supabase (PostgreSQL)
```

### Backward Compatibility (Non-Negotiable)
The v1.0 API contract MUST remain unchanged:
- Endpoint: `POST /api/sentiment/analyze`
- Request: `{ "text": string }`
- Response: `{ "sentiment": string, "confidenceScore": number, "explanation": string, "emotionBreakdown": { [key: string]: number } }`
- **NEVER** modify: `SentimentController.cs`, `SentimentRequest.cs`, `SentimentResponse.cs`, `ISentimentService.cs`, `OpenAISentimentService.cs`

---

## 2. Tech Stack & Conventions

### Backend (.NET 10)
- ASP.NET Core 10 Web API (minimal hosting in Program.cs)
- C# 13 with nullable reference types, implicit usings
- Target: `net10.0`
- DI: Constructor injection, register via interfaces
- Serialization: System.Text.Json (not Newtonsoft for new code)
- Agent Framework: Microsoft Semantic Kernel (AgentGroupChat)
- Testing: xUnit 2.9.3 + Moq 4.20.72

### Frontend (Angular 21)
- Standalone components (NO NgModules)
- TypeScript 5.9.2 with `strict: true`
- Tailwind CSS 3.4.17 (utility-first)
- RxJS for HTTP, Angular signals for new component state
- Testing: Vitest 4.0.8
- Package manager: npm 11.6.2

### Free AI Providers — LLM Fallback Chain (v3.0)
1. **Groq** - Primary, fastest inference (Llama 3.3 70B, 250 req/day free)
2. **Mistral** - Secondary, strong reasoning (Mistral Large, 500K tokens/month free)
3. **Gemini** - Tertiary, best quality (60 req/min free)
4. **OpenRouter** - Quaternary, multi-model gateway ($1 free credit)
5. **Ollama** - Local fallback, always available (unlimited, PII-safe)
6. **OpenAI** - Legacy v1.0 provider only (existing credits)

Managed by `IResilientKernelProvider` with exponential backoff cooldown (30s/60s/120s/300s max).

### Multimodal Services (v3.0)
| Service | Provider | Free Tier | Use Case |
|---------|----------|-----------|----------|
| STT | Deepgram Nova-2 | $200 credit | Adjuster voice notes, call recordings |
| Vision (primary) | Azure Vision F0 | 5K/month | Claim damage photo analysis |
| Vision (secondary) | Cloudflare Workers AI | 10K neurons/day | Natural language image analysis |
| OCR | OCR.space | 500/day | Scanned policy docs, claim forms |
| NER | HuggingFace (BERT NER) | 300/hour | Entity extraction + insurance regex |

All use `HttpClient` REST — 0 new NuGet packages. PII redacted on output text.

### Database
- **SQLite** via EF Core (local development)
- **Supabase PostgreSQL** (cloud, 500MB free tier)
- Repository pattern for all data access

---

## 3. Code Style Rules

### C#
- File-scoped namespaces: `namespace SentimentAnalyzer.API.Services;`
- Async methods end with `Async` suffix
- XML documentation on ALL public types and members
- Use `ILogger<T>` injected via constructor in every service/controller
- Null handling: `??`, `?.`, `ArgumentNullException` with `nameof()`
- Use `var` when type is obvious from right-hand side
- Private fields: `_camelCase` with underscore prefix
- Constants: `PascalCase`
- Keep controllers thin - business logic in services

### TypeScript
- Strict mode always (`strict: true`)
- Interfaces for object shapes (not classes)
- Explicit return types on public methods
- No `any` type - use `unknown` + type guards
- File naming: kebab-case (`insurance-sentiment.service.ts`)
- Class naming: PascalCase (`InsuranceSentimentService`)
- No `I` prefix on TypeScript interfaces
- Use `inject()` function for new components (not constructor injection)
- Prefer Angular signals over manual `ChangeDetectorRef` for new code

### CSS/Styling
- Tailwind CSS utility classes only (no custom CSS unless Tailwind can't express it)
- Follow existing color scheme: indigo-to-purple gradients
- All interactive elements must have hover/focus states
- Responsive breakpoints: sm, md, lg, xl

---

## 4. File Organization Rules

### Where New Files Go
- **New controllers**: `Backend/Controllers/`
- **New models**: `Backend/Models/` (use subfolders for domains if needed)
- **New services**: `Backend/Services/` (subfolder by domain)
- **Agent code**: `Agents/` class library (separate from Backend)
- **Shared domain models**: `Domain/` class library
- **New Angular components**: `Frontend/.../components/{component-name}/`
- **New Angular services**: `Frontend/.../services/`
- **New Angular models**: `Frontend/.../models/`

### File Naming
- C#: PascalCase matching class name (`InsuranceAnalysisService.cs`)
- TypeScript: kebab-case with type suffix (`insurance-analysis.service.ts`)
- Tests: Same name + `.Tests` (C#) or `.spec` (TS) suffix
- One component per file, one class per file

### Adding a New Agent
1. Add system prompt to `Agents/Definitions/AgentDefinitions.cs`
2. Add role to `Agents/Definitions/AgentRole.cs` enum
3. Update `InsuranceAnalysisOrchestrator.cs` to include agent
4. Update `AgentSelectionStrategy.cs` if speaking order changes
5. Add unit tests in `Tests/AgentTests/`

### Adding a New API Endpoint
1. Add request/response models to `Backend/Models/`
2. Add service interface + implementation to `Backend/Services/`
3. Add controller method to appropriate controller
4. Register service in `Program.cs`
5. Add controller tests to `Tests/`
6. Add frontend model, service, and component
7. Update `app.routes.ts`

---

## 5. Agent System Guidelines

### Agent Orchestration Flow
```
User Input -> CTO Agent (decomposes task)
  -> BA Agent (insurance domain analysis)
  -> Developer Agent (format response)
  -> QA Agent (validate consistency)
  -> AI Expert Agent (model evaluation, training recommendations, responsible AI)
  -> UX Designer Agent (screens, a11y, design system)
  -> Architect Agent (storage/perf advice)
  -> CTO Agent (synthesize final output) -> "ANALYSIS_COMPLETE"

Claims Triage Flow (v3.0 - OrchestrationProfile.ClaimsTriage):
  -> Claims Triage Agent (severity, urgency, actions)
  -> Fraud Detection Agent (fraud scoring, SIU referral)
  -> BA Agent (domain validation)
  -> QA Agent (quality check)
```

### Orchestration Profiles (v3.0)
Selective agent activation reduces token usage 50-60% for specialized workflows:
- **SentimentAnalysis**: All 7 original agents, 14 max turns (default)
- **ClaimsTriage**: ClaimsTriage + FraudDetection + BA + QA, 8 max turns
- **FraudScoring**: FraudDetection + ClaimsTriage + BA + QA, 8 max turns
- **DocumentQuery**: BA + Developer + QA, 6 max turns

Managed by `IOrchestrationProfileFactory` / `OrchestrationProfileFactory`.

### Agent Decision Authority
- **CTO**: Priority, scope, conflict resolution (final say)
- **BA**: Insurance domain correctness, business rules
- **Architect**: Technical design, DB schema, API contracts
- **Developer**: Implementation approach within Architect's design
- **QA**: Quality - can block releases with justified concerns
- **AI Expert**: Model selection, cloud adoption strategy, training pipelines, responsible AI governance, provider optimization
- **UX Designer**: Screen layouts, design system compliance, accessibility, UX gap identification
- **Claims Triage** (v3.0): Claim severity assessment, urgency classification, action recommendations
- **Fraud Detection** (v3.0): Fraud probability scoring, indicator categorization, SIU referral decisions

### Agent Output Parsing (Critical)
LLM agents produce non-deterministic output. The orchestrator (`InsuranceAnalysisOrchestrator.cs`) uses resilient two-phase parsing:

1. **Normalization**: Strip markdown code fences (```` ```json ... ``` ````) before extraction. LLMs wrap JSON in fences despite prompts saying not to.
2. **JSON Extraction**: String-literal-aware brace counting (tracks `inString` and `escape` state) to handle `{`/`}` inside JSON string values.
3. **Phase 1 - Strict**: `JsonSerializer.Deserialize<AgentAnalysisResult>()` - try clean deserialization first.
4. **Phase 2 - Manual**: If strict fails, use `JsonDocument` to extract fields individually with both `camelCase` and `PascalCase` property name support.
5. **Diagnostics**: Log JSON length + SHA-256 hash (NEVER raw JSON content) for debugging parse failures.

### Agent Prompt Engineering Rules
- ALL agent prompts instruct "output ONLY raw JSON, NO markdown code fences"
- Developer agent prompt MUST include the `quality` field in its JSON schema (omitting it was root cause of initial parsing failures)
- CTO prompt must say "merge QA output into the quality field"
- Prompts are loaded from `.claude/agents/*.md` with hardcoded fallbacks in `AgentDefinitions.cs` - keep both in sync

### Escalation Rules
- Agent blocked >1 iteration -> escalate to CTO
- Developer vs Architect disagreement -> Architect decides, CTO tiebreaker
- QA critical bug -> goes to Developer AND CTO simultaneously
- BA identifies regulatory gap -> immediately Priority 1
- AI Expert flags responsible AI concern -> goes to CTO AND BA simultaneously
- AI Expert vs Architect on cloud/infra -> Architect decides, CTO tiebreaker

### Insurance Domain Rules (All Agents)
1. **PII First**: Redact PII before ANY external AI provider call
2. **Audit Trail**: Log every analysis (timestamp, provider, input hash, result)
3. **Complaint Detection**: Flag texts with confidence >0.8 negative + keywords ("file complaint", "department of insurance", "attorney")
4. **Insurance Context**: Classify every analysis (claims, policy servicing, billing, agent interaction, underwriting)
5. **No Training Data Leakage**: Use API modes that do not train on input

---

## 6. Testing Requirements

### Coverage Targets
- Backend C# unit tests: minimum 80% line coverage
- Frontend TypeScript unit tests: minimum 75% line coverage
- All public API endpoints: 100% happy-path + error-path coverage
- Insurance domain logic: 100% (PII redaction, complaint detection)

### Test Patterns
- AAA: Arrange, Act, Assert
- Test names describe behavior: `AnalyzeSentiment_WithClaimDenialText_ReturnsNegativeSentiment`
- Use realistic insurance test data (NEVER "test", "foo", "bar")
- Backend: xUnit + Moq (follow `SentimentControllerTests.cs` pattern)
- Frontend: Vitest (follow existing `.spec.ts` patterns)

### Mandatory Test Categories
1. **v1.0 Regression**: `SentimentControllerTests.cs` - NEVER modify, must always pass
2. **PII Redaction**: Verify policy#, claim#, SSN, names redacted before external calls
3. **Provider Fallback**: Groq down -> Mistral -> Gemini -> OpenRouter -> Ollama (via `IResilientKernelProvider`)
4. **Insurance Context**: Correct classification into domain categories
5. **Complaint Detection**: Escalation flags trigger correctly

### E2E Testing (Playwright)
- Framework: Playwright 1.58+ with `@axe-core/playwright` for accessibility
- Config: `playwright.config.ts` in `Frontend/sentiment-analyzer-ui/`
- Test files: `e2e/` directory (7 spec files, ~138 tests)
- Projects: `chromium` (desktop) + `mobile-chrome` (Pixel 5)
- Mock strategy: All API calls mocked via `page.route()` in `e2e/helpers/api-mocks.ts`
- Auth bypass: Uses `e2e` Angular build configuration (no `fileReplacements` = `environment.ts` with empty Supabase keys = auth disabled)

#### E2E Test Structure
```
e2e/
├── fixtures/mock-data.ts       # Realistic insurance mock API responses
├── helpers/api-mocks.ts        # page.route() interceptors for all endpoints
├── global-setup.ts             # Cleans old screenshots/reports before each run
├── navigation.spec.ts          # Route navigation, mobile hamburger menu
├── sentiment-analyzer.spec.ts  # v1 sentiment analysis flow
├── insurance-analyzer.spec.ts  # v2 insurance analysis (full flow + error paths)
├── dashboard.spec.ts           # Dashboard metrics, charts, history table
├── login.spec.ts               # Login/register form UX
├── theme.spec.ts               # Theme cycling, persistence across routes
└── accessibility.spec.ts       # axe-core WCAG AA scans + ARIA validation
```

#### E2E Test Rules
- Screenshots captured on failure only (`screenshot: 'only-on-failure'`)
- Old screenshots/reports auto-cleaned before each run (via `global-setup.ts`)
- Desktop-only tests must skip on mobile viewports using `skipOnMobile()` helper
- Mock data MUST match real API contracts (types, field names, nested structures)
- Test error paths for 429, 500, and 503 status codes (free-tier rate limits)
- Accessibility tests exclude `color-contrast` from strict checks (known CSS issue, logged as informational audit)
- All interactive elements must have proper ARIA attributes (`aria-label`, `role`, `aria-live`)

#### E2E Commands
```bash
cd SentimentAnalyzer/Frontend/sentiment-analyzer-ui
npm run e2e              # Run all tests headless
npm run e2e:headed       # Run with browser visible
npm run e2e:ui           # Open Playwright UI mode
npm run e2e:report       # View HTML test report
```

### Insurance Test Data Examples
```csharp
// GOOD - realistic insurance text
var text = "I reported water damage on Jan 15. It's been 3 weeks with no response. Policy HO-2024-789456.";

// BAD - generic text
var text = "Test text for testing"; // WRONG - never use this
```

---

## 7. Git Conventions

### Commit Messages
```
<type>(<scope>): <description>
```
- Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`
- Scopes: `api`, `frontend`, `insurance`, `agents`, `provider`, `db`
- Example: `feat(insurance): add complaint escalation detection service`

### Branch Naming
- Features: `feature/insurance-{description}`
- Bugfixes: `fix/{description}`
- Agent work: `agent/{agent-name}/{task-id}`

### Never Commit
- `appsettings.Development.json` (contains API keys)
- `.env`, `*.local`
- `node_modules/`, `bin/`, `obj/`
- `e2e/test-results/`, `e2e/report/` (Playwright artifacts)

---

## 8. Design Patterns (Architect + CTO + Developer Agreed)

> Decided by: Solution Architect proposed, Developer validated feasibility, CTO approved.
> Principle: Every pattern must have a concrete use case in BOTH backend and frontend. No theoretical patterns.

### 8.1 Strategy Pattern
**Purpose:** Swap algorithms/behaviors at runtime without modifying the client code.

| Stack | Use Case | Implementation |
|-------|----------|----------------|
| **Backend** | AI provider selection (Groq/Gemini/Ollama) | `IAIProvider` interface with `GroqProvider`, `GeminiProvider`, `OllamaProvider` implementations. Selected via config/fallback logic. |
| **Backend** | Agent selection & termination | `AgentSelectionStrategy`, `AnalysisTerminationStrategy` in `Agents/Orchestration/` |
| **Frontend** | Auth strategy (Supabase vs dev-mode bypass) | `AuthService.authEnabled()` signal toggles strategy. Guards adapt behavior based on strategy. |
| **Frontend** | Theme/styling strategy | `ThemeService` - strategy for dark/light mode without conditional sprawl |

**Rule:** When you have 2+ interchangeable behaviors behind an interface, use Strategy. Register implementations via DI, never hard-code selection with `if/else` chains.

### 8.2 CQRS (Command Query Responsibility Segregation)
**Purpose:** Separate write operations (commands) from read operations (queries) for clarity and scalability.

| Stack | Commands (Writes) | Queries (Reads) |
|-------|-------------------|-----------------|
| **Backend** | `AnalyzeInsuranceCommand` -> `AnalyzeInsuranceHandler` (via MediatR) | `GetDashboardQuery`, `GetHistoryQuery` -> handlers |
| **Frontend** | `insuranceService.analyzeInsurance()` (POST) | `insuranceService.getDashboard()`, `getHistory()` (GET) |

**Rule:**
- Backend: ALL v2 endpoints use MediatR commands/queries in `Backend/Features/{Domain}/`. One handler per command/query.
- Frontend: Services separate mutation methods (POST/PUT/DELETE) from read methods (GET). Never mix side-effects into read calls.
- v1 endpoints (frozen) are exempt from CQRS - they use the legacy service pattern.

### 8.3 Chain of Responsibility
**Purpose:** Pass a request through a chain of handlers, each deciding whether to process or pass it along.

| Stack | Use Case | Chain |
|-------|----------|-------|
| **Backend** | AI provider fallback | Groq -> Mistral -> Gemini -> OpenRouter -> Ollama (via `IResilientKernelProvider`, exponential backoff cooldown) |
| **Backend** | ASP.NET middleware pipeline | Request -> Auth -> Exception Handler -> CORS -> Endpoint |
| **Backend** | PII redaction pipeline | SSN -> Policy# -> Claim# -> Email -> Phone (each regex processes and passes text forward) |
| **Frontend** | HTTP interceptor chain | `authInterceptor` -> (future: retry interceptor, logging interceptor) |

**Rule:** When a request needs sequential processing with potential early termination, use Chain of Responsibility. Provider fallback MUST follow: Groq -> Mistral -> Gemini -> OpenRouter -> Ollama -> error. Never skip the chain.

### 8.4 Facade Pattern
**Purpose:** Provide a simplified interface to a complex subsystem.

| Stack | Facade | Complex Subsystem Hidden |
|-------|--------|--------------------------|
| **Backend** | `InsuranceAnalysisOrchestrator` | Multi-agent system (CTO, BA, Dev, QA, Architect, UX agents), Semantic Kernel `AgentGroupChat`, selection/termination strategies |
| **Backend** | `PIIRedactionService` | 5+ regex patterns, order-dependent redaction pipeline |
| **Frontend** | `InsuranceService` | HTTP calls, request/response mapping, error normalization |
| **Frontend** | `AuthService` | Supabase client, session management, token refresh, state signals |

**Rule:** Controllers/components should NEVER interact directly with complex subsystems. Always go through a facade service. If a component needs >2 injected services to complete one action, consider a facade.

### 8.5 Observer Pattern (Reactive)
**Purpose:** When state changes, notify all dependents automatically.

| Stack | Implementation | Use Case |
|-------|---------------|----------|
| **Backend** | Event-driven agent conversation (`AgentGroupChat.InvokeAsync`) | Agents observe previous messages and react. CTO observes `ANALYSIS_COMPLETE` signal. |
| **Backend** | `ILogger<T>` pipeline | Observers (console, file, external) subscribe to log events |
| **Frontend** | **Angular Signals** (`signal()`, `computed()`) | Component state reactivity. `isLoading`, `result`, `error` signals auto-update templates. |
| **Frontend** | **RxJS Observables** | HTTP responses, auth state changes (`onAuthStateChange`), cross-component communication |

**Rule:**
- Frontend component state: Use **Angular Signals** (preferred for new code).
- Frontend async data streams (HTTP, WebSocket, events): Use **RxJS Observables** with `takeUntilDestroyed()`.
- Never mix: don't use Observables for simple component state, don't use signals for HTTP streams.

### 8.6 Repository Pattern
**Purpose:** Abstract data access behind a clean interface, making storage swappable.

| Stack | Interface | Implementations |
|-------|-----------|-----------------|
| **Backend** | `IAnalysisRepository` | `SqliteAnalysisRepository` (dev), PostgreSQL repository (prod via Supabase) |
| **Frontend** | Angular services (`InsuranceService`, `SentimentService`) | Act as repositories - abstract HTTP calls behind typed methods |

**Rule:**
- Backend: ALL database access goes through `IAnalysisRepository`. No direct `DbContext` usage in controllers, handlers, or agents.
- Frontend: ALL API access goes through Angular services. No direct `HttpClient` calls in components.
- Test both layers with mocks/stubs (Moq for C#, `HttpTestingController` for Angular).

### 8.7 Adapter Pattern
**Purpose:** Convert one interface into another that clients expect.

| Stack | Adapter | Adapts From | Adapts To |
|-------|---------|-------------|-----------|
| **Backend** | `AnalyzeInsuranceHandler.MapToResponse()` | `AgentAnalysisResult` (raw multi-agent output) | `InsuranceAnalysisResponse` (API contract) |
| **Backend** | AI provider abstraction | Groq/Gemini/Ollama native responses | Unified Semantic Kernel `ChatMessageContent` |
| **Backend** | `InsuranceAnalysisResponse` inheriting v1 fields | v2 insurance analysis | v1-compatible response shape (backward compat) |
| **Frontend** | Component data mapping | `InsuranceAnalysisResponse` (API shape) | Component-friendly view models (chart data, display strings) |

**Rule:** When integrating external systems (AI providers, Supabase, third-party APIs), ALWAYS use an adapter. Never leak external data shapes into your domain models.

### Pattern Decision Matrix

Use this to decide which pattern applies:

| Situation | Pattern |
|-----------|---------|
| Multiple interchangeable implementations | **Strategy** |
| Separating reads from writes | **CQRS** |
| Sequential processing with fallback | **Chain of Responsibility** |
| Simplifying a complex subsystem | **Facade** |
| Reacting to state/data changes | **Observer** (Signals or RxJS) |
| Abstracting data storage/retrieval | **Repository** |
| Converting between incompatible interfaces | **Adapter** |
| Need a new pattern not listed here? | Discuss with Architect first, CTO approves |

---

## 9. API Endpoint Patterns

### URL Structure
- v1 (legacy, frozen): `/api/sentiment/{action}`
- v2 (insurance): `/api/insurance/{action}`
- Health: `/api/sentiment/health` (v1), `/api/insurance/health` (v2)

### v2 Response Envelope
```json
{
  "sentiment": "Negative",
  "confidenceScore": 0.92,
  "explanation": "...",
  "emotionBreakdown": { "frustration": 0.85, "anger": 0.70 },
  "insuranceAnalysis": {
    "purchaseIntentScore": 15,
    "customerPersona": "Claim-Frustrated",
    "journeyStage": "Active-Claim",
    "riskIndicators": {
      "churnRisk": "High",
      "complaintEscalationRisk": "High",
      "fraudIndicators": "None"
    },
    "policyRecommendations": [...],
    "interactionType": "claims",
    "keyTopics": ["claim delay", "switching providers"]
  },
  "quality": {
    "isValid": true,
    "qualityScore": 92,
    "issues": [
      { "severity": "warning", "field": "sentiment", "message": "Confidence below threshold" }
    ],
    "suggestions": ["Add customer ID for personalized recommendations"],
    "warnings": ["[warning] sentiment: Confidence below threshold", "Add customer ID for personalized recommendations"]
  }
}
```

### HTTP Status Codes
- 200: Successful analysis
- 400: Validation error
- 429: Rate limited (free tier exceeded)
- 500: AI provider failure after all fallbacks
- 503: All providers down

---

## 10. Security Rules

### PII Redaction (Non-Negotiable)
Before ANY external AI API call, redact:
- SSN: `\d{3}-\d{2}-\d{4}` -> `[SSN-REDACTED]`
- Policy numbers: `[A-Z]{2,3}-\d{4,10}` -> `[POLICY-REDACTED]`
- Claim numbers: `CLM-\d{4}-\d{4,8}` -> `[CLAIM-REDACTED]`
- Phone numbers -> `[PHONE-REDACTED]`
- Email addresses -> `[EMAIL-REDACTED]`

### API Key & Secrets Management (Architect + Developer Agreed)

#### Tiered Secrets Strategy
| Tier | Environment | Method |
|------|-------------|--------|
| **Tier 1** | Local Dev | .NET User Secrets (`dotnet user-secrets set "AgentSystem:Groq:ApiKey" "key"`) - stored in `%APPDATA%`, never in project tree |
| **Tier 2** | CI/CD | Environment variables (`AgentSystem__Groq__ApiKey`) - set in GitHub Actions / Azure DevOps |
| **Tier 3** | Production | Azure Key Vault / AWS Secrets Manager / Supabase Vault - fetched at runtime |

#### Backend Rules
- **User Secrets enabled** via `<UserSecretsId>` in `.csproj` - use for local dev API keys
- `appsettings.json`: empty placeholder keys only (committed) - contains structure, not values
- `appsettings.Development.json`: logging overrides only (gitignored) - NEVER store keys here
- `.env.example`: template documenting all required env vars (committed) - real `.env` is gitignored
- Startup validation in `Program.cs` checks all provider keys at boot and throws with `dotnet user-secrets` instructions
- NEVER log API key values - validate presence only with `string.IsNullOrWhiteSpace()`

#### Frontend Rules
- All services import from `environment.ts` (NOT `environment.development.ts`)
- Angular CLI `fileReplacements` in `angular.json` swaps `environment.ts` with `environment.development.ts` during `ng serve`
- `environment.ts` (committed): production config with empty keys
- `environment.development.ts` (gitignored): local dev config - copy from `environment.development.ts.example`
- `environment.development.ts.example` (committed): template with placeholder values
- Supabase anon keys are technically public but still should not be committed with real project URLs

#### Required Secrets (by provider)
```bash
# .NET User Secrets (recommended for local dev)
# LLM Providers (fallback chain)
dotnet user-secrets set "AgentSystem:Groq:ApiKey" "your-key"
dotnet user-secrets set "AgentSystem:Mistral:ApiKey" "your-key"
dotnet user-secrets set "AgentSystem:Gemini:ApiKey" "your-key"
dotnet user-secrets set "AgentSystem:OpenRouter:ApiKey" "your-key"
dotnet user-secrets set "OpenAI:ApiKey" "your-key"

# Multimodal Services (v3.0)
dotnet user-secrets set "AgentSystem:Deepgram:ApiKey" "your-key"
dotnet user-secrets set "AgentSystem:AzureVision:ApiKey" "your-key"
dotnet user-secrets set "AgentSystem:AzureVision:Endpoint" "https://your-resource.cognitiveservices.azure.com"
dotnet user-secrets set "AgentSystem:Cloudflare:ApiKey" "your-key"
dotnet user-secrets set "AgentSystem:Cloudflare:AccountId" "your-account-id"
dotnet user-secrets set "AgentSystem:OcrSpace:ApiKey" "your-key"
dotnet user-secrets set "AgentSystem:HuggingFace:ApiKey" "your-key"

# Environment Variables (CI/CD)
AgentSystem__Groq__ApiKey=your-key
AgentSystem__Gemini__ApiKey=your-key
AgentSystem__Mistral__ApiKey=your-key
AgentSystem__OpenRouter__ApiKey=your-key
OpenAI__ApiKey=your-key
AgentSystem__Deepgram__ApiKey=your-key
AgentSystem__AzureVision__ApiKey=your-key
AgentSystem__AzureVision__Endpoint=https://your-resource.cognitiveservices.azure.com
AgentSystem__Cloudflare__ApiKey=your-key
AgentSystem__Cloudflare__AccountId=your-account-id
AgentSystem__OcrSpace__ApiKey=your-key
AgentSystem__HuggingFace__ApiKey=your-key
```

#### NEVER Commit
- Real API keys in any file
- `appsettings.Development.json` with secrets
- `environment.development.ts` with real Supabase URLs/keys
- `.env` files

### Logging
- NEVER log raw input text
- Log only: input hash (SHA-256), sentiment result, provider used, processing time
- Use `ILogger<T>` at appropriate levels (Info for flow, Warning for retries, Error for exceptions)

---

## 11. Common Pitfalls & Solutions

| Pitfall | Solution |
|---------|----------|
| Breaking v1.0 API | NEVER modify frozen v1 files. Create NEW classes for v2. Run regression tests every build. |
| Angular change detection not firing | Use Angular signals for new components. Use `async` pipe in templates. Only use `ChangeDetectorRef` if signals aren't viable. |
| Free tier rate limits (429 errors) | `IResilientKernelProvider` handles automatically: Groq -> Mistral -> Gemini -> OpenRouter -> Ollama. Exponential backoff cooldown per provider (30s/60s/120s/300s max). Cache identical analyses. |
| PII leaking to external AI | Run PIIRedactionService BEFORE any external call. Unit test redaction. Enforce in middleware. |
| Insurance sentiment misclassification | Include insurance context in AI prompts. Use insurance emotion taxonomy. Provide few-shot examples. |
| Supabase free tier pausing | Implement SQLite fallback. Add keep-alive ping. Use Polly retry policies. |
| Ollama unavailable in CI | Mock via IAIProvider interface. Integration tests with live providers run locally only. |
| Multiple agents modifying same file | CTO assigns file ownership. Use subfolders by domain. Sequence conflicting tasks. |
| Test data with real PII | ALL test data must be synthetic (e.g., "HO-0000-TEST01", "Jane Testpolicyholder"). |
| Agent JSON wrapped in markdown fences | Orchestrator's `NormalizeForJsonExtraction()` strips fences. Agent prompts also say "no fences". Always handle both. |
| Agent output missing fields | Two-phase parsing: strict deserialization first, manual `JsonDocument` extraction as fallback. Support both camelCase and PascalCase. |
| Long analysis wait (15-60s) | Show elapsed timer + phase descriptions ("Business Analyst reviewing...", "QA validating..."). Use skeleton cards for layout stability. |
| No empty state before first analysis | Always show a "Ready to Analyze" card with 7 dimension badges before first submission. |
| Missing ARIA/accessibility | All interactive elements need `role`, `aria-live`, `aria-label`. Progress bars need `role="progressbar"` with `aria-valuenow/min/max`. |
| Raw PII in diagnostic logs | Use SHA-256 hashing (`ComputeSha256()`) for diagnostic logging. NEVER log raw JSON from agent output. |
| E2e tests failing with `ERR_EMPTY_RESPONSE` | Port 4200 conflict from leftover node processes. Kill with `taskkill /F /IM node.exe` (Windows) or `pkill -f "ng serve"` (Linux). |
| E2e desktop nav tests fail on mobile | Desktop nav links (`hidden md:flex`) are invisible on mobile. Use `skipOnMobile()` helper in navigation tests. |
| E2e mock data type mismatch | Mock data in `e2e/fixtures/mock-data.ts` MUST match TypeScript interface types exactly (e.g., `id: number` not `id: string`). |
| E2e accessibility color-contrast failures | Known CSS issue in dark themes. Excluded from strict axe-core tests. Logged as informational audit. Fix CSS `--text-muted`/`--text-secondary` variables. |
| Angular dev server overwhelmed by Playwright workers | Limit `workers: 3` in `playwright.config.ts`. Use `retries: 1` locally. |

---

## Common Commands

### Backend
```bash
cd SentimentAnalyzer/Backend
dotnet restore      # Restore packages
dotnet build        # Build project
dotnet run          # Run on http://localhost:5143
```

### Tests
```bash
cd SentimentAnalyzer/Tests
dotnet test         # Run all tests
```

### Frontend
```bash
cd SentimentAnalyzer/Frontend/sentiment-analyzer-ui
npm install         # Install dependencies
npm start           # Serve on http://localhost:4200
npm run build       # Production build
npm test            # Run Vitest
npm run e2e         # Run Playwright e2e tests
npm run e2e:headed  # Run e2e with browser visible
npm run e2e:ui      # Open Playwright UI mode
npm run e2e:report  # View HTML test report
```

### Ports
- Backend API: `http://localhost:5143`
- Frontend Dev: `http://localhost:4200`
- Ollama (if used): `http://localhost:11434`
