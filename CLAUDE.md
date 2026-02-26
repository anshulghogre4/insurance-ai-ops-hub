# CLAUDE.md - Insurance AI Sentiment Analyzer

## Project Overview
AI-powered sentiment analysis for the insurance industry. Analyzes policyholder communications, claims notes, agent reviews, and regulatory correspondence.

- **v3.0 (Current)**: 5-provider fallback chain, 5 multimodal services, claims triage + fraud detection agents, orchestration profiles, PII redaction. See `SPRINT-ROADMAP.md`.
- **v4.0 (COMPLETE)**: Document Intelligence RAG, CX Copilot (SSE streaming), cross-claim fraud correlation (4-strategy), v1 PII decorator fix, MCP server integration. 18 Angular components, 15 routes, 1053 total tests (461 backend + 235 frontend + 357 E2E).

## Non-Negotiable Rules

### v1 Backward Compatibility
**NEVER** modify these frozen files: `SentimentController.cs`, `SentimentRequest.cs`, `SentimentResponse.cs`, `ISentimentService.cs`, `OpenAISentimentService.cs`

### PII Redaction
Before ANY external AI call, redact SSN, policy numbers, claim numbers, phone, email. See [docs/security.md](docs/security.md).

### Provider Fallback Order
Groq -> Mistral -> Gemini -> OpenRouter -> Ollama -> error. Never skip the chain. Managed by `IResilientKernelProvider`.

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

## Detailed Reference Docs
- [docs/architecture.md](docs/architecture.md) — Agent system, orchestration flows, providers, multimodal services, MCP servers
- [docs/design-patterns.md](docs/design-patterns.md) — All 9 design patterns with decision matrix
- [docs/testing.md](docs/testing.md) — Coverage targets, test categories, E2E structure, MCP test generation
- [docs/security.md](docs/security.md) — PII redaction, secrets management, required env vars
- [docs/api-reference.md](docs/api-reference.md) — URL structure, response envelope, HTTP status codes
- [docs/pitfalls.md](docs/pitfalls.md) — Common problems and their solutions
