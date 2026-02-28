# CLAUDE.md - Insurance AI Sentiment Analyzer

## Project Overview
AI-powered sentiment analysis for the insurance industry. Analyzes policyholder communications, claims notes, agent reviews, and regulatory correspondence.

- **v3.0 (Current)**: 5-provider fallback chain, 5 multimodal services, claims triage + fraud detection agents, orchestration profiles, PII redaction. See `SPRINT-ROADMAP.md`.
- **v4.0 (COMPLETE)**: Document Intelligence RAG, CX Copilot (SSE streaming), cross-claim fraud correlation (4-strategy), v1 PII decorator fix, MCP server integration. 18 Angular components, 15 routes, 1053 total tests (461 backend + 235 frontend + 357 E2E).

## Non-Negotiable Rules

### #1 End-to-End Feature Development (MOST IMPORTANT)
**Every feature MUST be built end-to-end in a single pass — NEVER backend-first or frontend-first.**

When building any feature, implement ALL layers together in this order:
1. **C# Model** (`Backend/Models/`) — Define the response shape
2. **Backend Service + Endpoint** — Implement the business logic and API
3. **Backend Tests** (xUnit) — Verify the service works correctly
4. **TypeScript Interface** (`Frontend/.../models/`) — Must match the C# model exactly, property-for-property
5. **Angular Service** (`Frontend/.../services/`) — HTTP client calling the endpoint
6. **Angular Component** — UI rendering with defensive null guards on every field
7. **Frontend Unit Tests** (Vitest) — Component and service tests
8. **E2E Mock Data** (`e2e/fixtures/mock-data.ts`) — Must match the C# model shape exactly
9. **E2E Tests** (Playwright) — Full user journey tests written immediately, not deferred
10. **Navigation Link** — Every new route must have at least one `routerLink` from another component

**Why:** Building layers separately across sprints causes contract mismatches where the backend returns different data than the frontend expects. E2E mocks mask these gaps. This rule prevents that.

**E2E tests are mandatory, not optional.** Every feature must ship with Playwright E2E tests covering:
- Happy path (form fill → submit → verify results render)
- Error states (API 500, 429, empty responses)
- Navigation (links to/from the new feature work)
- Accessibility (axe-core scan on the new page)

This routine applies to ALL sprints uniformly — no exceptions.

### v1 Backward Compatibility
**NEVER** modify these frozen files: `SentimentController.cs`, `SentimentRequest.cs`, `SentimentResponse.cs`, `ISentimentService.cs`, `OpenAISentimentService.cs`

### PII Redaction
Before ANY external AI call, redact SSN, policy numbers, claim numbers, phone, email. See [docs/security.md](docs/security.md).

### Provider Fallback Order
**LLM:** Groq -> Cerebras -> Mistral -> Gemini -> OpenRouter -> OpenAI -> Ollama -> error. Never skip the chain. Managed by `IResilientKernelProvider`.

**OCR (Document Processing, ordered by data safety):** PdfPig (local, no data transfer) -> Azure Document Intelligence (no training on data) -> OCR Space (immediate deletion, GDPR compliant) -> Gemini Vision (last resort — free tier may train on data). Never skip the chain. Managed by `ResilientOcrProvider`. PdfPig handles native/digital PDFs in <50ms with no API calls; Azure/OCR Space/Gemini handle scanned documents.

**NER (Entity Extraction):** HuggingFace BERT (300 req/hr) -> Azure AI Language (5K/month). Never skip the chain. Managed by `ResilientEntityExtractionProvider`.

**STT (Speech-to-Text):** Deepgram ($200 credit) -> Azure AI Speech (5 hrs/month). Never skip the chain. Managed by `ResilientSpeechToTextProvider`.

**Embeddings:** Voyage AI -> Ollama. Managed by `ResilientEmbeddingProvider`.

**Content Safety:** Azure AI Content Safety (5K text + 5K image/month). Screens CX Copilot responses before sending to policyholders. Managed by `IContentSafetyService`.

**Translation:** Azure AI Translator (2M chars/month). Pre-processes non-English claims text. Managed by `ITranslationService`.

## Tech Stack
- **Backend**: .NET 10, C# 13, ASP.NET Core, Semantic Kernel, System.Text.Json, xUnit + Moq
- **Frontend**: Angular 21 (standalone components), TypeScript 5.9 strict, Tailwind CSS, RxJS + Signals, Vitest
- **Database**: SQLite (dev) / Supabase PostgreSQL (prod), Repository pattern
- **E2E**: Playwright 1.58+ with axe-core accessibility
- **MCP Servers**: Playwright MCP (E2E test generation), Stitch MCP (design-to-code). Config: `.mcp.json`

## Code Style

### C#
- File-scoped namespaces, `Async` suffix, XML docs on public members
- `ILogger<T>` in every service, `_camelCase` private fields, `PascalCase` constants
- Constructor injection, thin controllers, business logic in services

### TypeScript
- `strict: true`, no `any`, interfaces for shapes, `inject()` for new components
- kebab-case files, PascalCase classes, no `I` prefix on interfaces
- Angular Signals for state, RxJS for async streams

### CSS
- Tailwind utility classes only, indigo-to-purple gradients, hover/focus states required

## File Organization
- Controllers: `Backend/Controllers/`, Models: `Backend/Models/`, Services: `Backend/Services/{domain}/`
- Agents: `Agents/` class library, Components: `Frontend/.../components/{name}/`
- C# PascalCase filenames, TS kebab-case with type suffix, one class per file

## Testing
- Use realistic insurance test data (NEVER "test", "foo", "bar")
- Backend: xUnit + Moq, Frontend: Vitest, E2E: Playwright
- Full details: [docs/testing.md](docs/testing.md)

## Git Conventions
- Format: `<type>(<scope>): <description>` (types: feat/fix/refactor/test/docs/chore)
- Branches: `feature/insurance-{desc}`, `fix/{desc}`, `agent/{name}/{task-id}`
- Never commit: `appsettings.Development.json`, `.env`, `node_modules/`, `bin/`, `obj/`, `e2e/test-results/`

## Common Commands
```bash
# Backend
cd SentimentAnalyzer/Backend && dotnet build && dotnet run  # Port 5143

# Tests
cd SentimentAnalyzer/Tests && dotnet test

# Frontend
cd SentimentAnalyzer/Frontend/sentiment-analyzer-ui
npm install && npm start   # Port 4200
npm test                   # Vitest
npm run e2e                # Playwright
```

## MCP Servers (Sprint 4 Week 4+)
Active in `.mcp.json`:
- **Playwright MCP** (`@playwright/mcp`) — Headless browser automation, E2E test generation from browser sessions
- **Stitch MCP** (`@_davideast/stitch-mcp`) — Google Stitch AI design-to-code pipeline for UI components

Sprint 5 planned: Supabase, GitHub, Context7, Sequential Thinking, Sentry, Grafana, Snyk, Upstash, Tavily. See [docs/architecture.md](docs/architecture.md).

## Quality Checklist (Mandatory for All Agents)

### Backend-Frontend Contract Validation
- When building/changing a backend endpoint return type, verify the corresponding frontend TypeScript interface matches the C# model property-for-property
- When building frontend components, verify E2E mock data shapes in `e2e/fixtures/mock-data.ts` match actual backend C# response models in `Backend/Models/`
- When changing E2E mocks, verify the mock values are consistent with any display transformation utils (e.g., `getEffectiveFraudScore()` floors)

### Defensive Template Rendering
- **EVERY** API property access in Angular templates MUST have a fallback:
  - Strings: `{{ value || 'N/A' }}`
  - Dates: `{{ value ? formatDate(value) : 'Date unavailable' }}`
  - Objects: `{{ obj?.property }}` (optional chaining)
  - Arrays: `@if (arr && arr.length > 0)` before `@for`
- Never trust that the backend will return all optional fields populated
- Use `?? ''` for string concatenation in TypeScript to prevent "undefined" literals

### Navigation Completeness
- Every route in `app.routes.ts` MUST have at least one `routerLink` pointing to it from another component
- After adding a new route, verify navigation exists: `grep -r "routerLink.*your-route" src/app/components/`
- Related features must cross-link (e.g., fraud alerts → correlations, claims → fraud analysis)

### E2E Mock Consistency
- Mock route patterns MUST use trailing `*` wildcard to match query parameters: `**/api/endpoint*` not `**/api/endpoint`
- When nav uses dropdown buttons, scope page-level button queries: `page.getByRole('main').getByRole('button', { name: 'X' })`
- After adding display transformation utilities, update mock values to be post-transformation consistent

### UX Consistency Rules
- All dates formatted via `formatDate()` or Angular `DatePipe` — never raw ISO strings
- All fraud scores on 0-100 scale (correlation match scores labeled "Match Score" to distinguish)
- Error banners: rose/red for failures, amber/yellow for partial success/warnings
- Backend `errorMessage` fields must always be surfaced to the user when non-null

## Detailed Reference Docs
- [docs/architecture.md](docs/architecture.md) — Agent system, orchestration flows, providers, multimodal services, MCP servers
- [docs/design-patterns.md](docs/design-patterns.md) — All 9 design patterns with decision matrix
- [docs/testing.md](docs/testing.md) — Coverage targets, test categories, E2E structure, MCP test generation
- [docs/security.md](docs/security.md) — PII redaction, secrets management, required env vars
- [docs/api-reference.md](docs/api-reference.md) — URL structure, response envelope, HTTP status codes
- [docs/pitfalls.md](docs/pitfalls.md) — Common problems and their solutions
