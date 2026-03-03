# Testing Reference

## Coverage Targets
- Backend C# unit tests: minimum 80% line coverage
- Frontend TypeScript unit tests: minimum 75% line coverage
- All public API endpoints: 100% happy-path + error-path coverage
- Insurance domain logic: 100% (PII redaction, complaint detection)

## Test Patterns
- AAA: Arrange, Act, Assert
- Test names describe behavior: `AnalyzeSentiment_WithClaimDenialText_ReturnsNegativeSentiment`
- Use realistic insurance test data (NEVER "test", "foo", "bar")
- Backend: xUnit + Moq (follow `SentimentControllerTests.cs` pattern)
- Frontend: Vitest (follow existing `.spec.ts` patterns)

## Mandatory Test Categories
1. **v1.0 Regression**: `SentimentControllerTests.cs` - NEVER modify, must always pass
2. **PII Redaction**: Verify policy#, claim#, SSN, names redacted before external calls
3. **Provider Fallback**: Groq -> Mistral -> Gemini -> OpenRouter -> Ollama
4. **Insurance Context**: Correct classification into domain categories
5. **Complaint Detection**: Escalation flags trigger correctly

## Insurance Test Data Examples
```csharp
// GOOD - realistic insurance text
var text = "I reported water damage on Jan 15. It's been 3 weeks with no response. Policy HO-2024-789456.";

// BAD - generic text
var text = "Test text for testing"; // WRONG - never use this
```

## E2E Testing (Playwright)
- Framework: Playwright 1.58+ with `@axe-core/playwright` for accessibility
- Config: `playwright.config.ts` in `Frontend/sentiment-analyzer-ui/`
- Projects: `chromium` (desktop) + `mobile-chrome` (Pixel 5)
- Mock strategy: All API calls mocked via `page.route()` in `e2e/helpers/api-mocks.ts`
- Auth bypass: Uses `e2e` Angular build config (no `fileReplacements` = auth disabled)

### E2E Test Structure
```
e2e/
├── fixtures/mock-data.ts            # Realistic insurance mock API responses
├── helpers/api-mocks.ts             # page.route() interceptors for all endpoints
├── global-setup.ts                  # Cleans old screenshots/reports before each run
├── navigation.spec.ts               # Route navigation, mobile hamburger menu
├── sentiment-analyzer.spec.ts       # v1 sentiment analysis flow
├── insurance-analyzer.spec.ts       # v2 insurance analysis (full flow + error paths)
├── dashboard.spec.ts                # Dashboard metrics, charts, history table
├── login.spec.ts                    # Login/register form UX
├── theme.spec.ts                    # Theme cycling, persistence across routes
├── accessibility.spec.ts            # axe-core WCAG AA scans + ARIA validation
├── claims-triage.spec.ts            # Claims triage form + result display (Sprint 3)
├── claims-detail.spec.ts            # Claim detail view by ID (Sprint 3)
├── claims-history.spec.ts           # Claims history table + filters (Sprint 3)
├── provider-health.spec.ts          # Provider health monitor (Sprint 3)
├── fraud-alerts.spec.ts             # Fraud alerts dashboard (Sprint 3)
├── document-upload.spec.ts          # Document upload + type selector (Sprint 4 Week 4)
├── document-query.spec.ts           # RAG Q&A + source citations (Sprint 4 Week 4)
├── cx-copilot.spec.ts               # SSE streaming chat + typing indicator (Sprint 4 Week 4)
├── fraud-correlation.spec.ts        # Cross-claim correlation + review workflow (Sprint 4 Week 4)
├── batch-upload.spec.ts             # Batch CSV claims upload (Sprint 5)
├── breadcrumbs.spec.ts              # Breadcrumb navigation (Sprint 5)
├── command-palette.spec.ts          # Ctrl+K command palette (Sprint 5)
├── content-safety.spec.ts           # Content safety screening (Sprint 5)
├── cx-copilot-memory.spec.ts        # CX conversation persistence (Sprint 5)
├── fine-tuning-qa.spec.ts           # Synthetic QA for fine-tuning (Sprint 5)
├── micro-interactions.spec.ts       # UI micro-interactions (Sprint 5)
├── parallax-landing.spec.ts         # Parallax landing page effects (Sprint 5)
└── toast.spec.ts                    # Toast notifications (Sprint 5)
```

### E2E Rules
- Screenshots captured on failure only (`screenshot: 'only-on-failure'`)
- Desktop-only tests use `skipOnMobile()` helper
- Mock data MUST match real API contracts exactly
- Test error paths for 429, 500, and 503 status codes
- Accessibility tests exclude `color-contrast` from strict checks (known CSS issue)
- All interactive elements must have proper ARIA attributes

### E2E Commands
```bash
cd SentimentAnalyzer/Frontend/sentiment-analyzer-ui
npm run e2e              # Run all tests headless
npm run e2e:headed       # Run with browser visible
npm run e2e:ui           # Open Playwright UI mode
npm run e2e:report       # View HTML test report
```

## MCP-Driven Test Generation (Sprint 4 Week 4)

### Playwright MCP Server
The Playwright MCP server (`@playwright/mcp@latest`) enables AI-assisted E2E test generation:

- **Browser Session Recording**: Navigate the app in a headless browser via MCP, record interactions
- **Auto-Generated Specs**: Convert recorded browser sessions into Playwright test specs
- **Exploratory Testing**: Let Claude Code explore the UI and identify untested paths
- **Visual Regression**: Capture screenshots for baseline comparison

### Workflow
1. Playwright MCP launches headless browser pointing at `localhost:4200`
2. Claude Code navigates through app flows (claims triage, fraud alerts, etc.)
3. Interactions are captured and converted to `.spec.ts` files
4. Generated specs follow existing patterns in `e2e/` directory
5. Mock data added to `e2e/fixtures/mock-data.ts` for new endpoints

### Sprint 4 Test Counts (COMPLETE)
| Category | Sprint 3 End | Sprint 4 Week 3 | Sprint 4 Week 4 | Sprint 5 (est.) | Status |
|----------|-------------|----------------|----------------|----------------|--------|
| Backend (xUnit) | 246 | **461** | **461** | **~530** | +~69 new tests (Sprint 5) |
| Frontend Unit (Vitest) | 199 | 199 | **235** | **~443** | +~208 new tests (Sprint 5) |
| E2E (Playwright) | 263 | 263 | **357** | **~450** | +~93 new tests (Sprint 5) |
| **Grand Total** | **708** | **923** | **1,053** | **~1,423** | **0 failures** |

### Week 4 New Test Files
| Test File | Type | Tests | Coverage |
|-----------|------|-------|----------|
| document.service.spec.ts | Unit | ~8 | Document upload, query, get, history, delete HTTP methods |
| customer-experience.service.spec.ts | Unit | ~6 | SSE connection, chat messages, session management, error handling |
| fraud-correlation.service.spec.ts | Unit | ~4 | Correlate, get correlations, review status, delete |
| document-upload.spec.ts (component) | Unit | ~5 | Form, file input, type selector, submit, loading state |
| document-query.spec.ts (component) | Unit | ~4 | Query input, submit, citations display, empty state |
| document-result.spec.ts (component) | Unit | ~3 | Document details, chunk count, metadata |
| cx-copilot.spec.ts (component) | Unit | ~4 | Message input, streaming indicator, history, error state |
| fraud-correlation.spec.ts (component) | Unit | ~4 | Correlation cards, strategy badges, review actions, empty state |
| document-upload.spec.ts (E2E) | E2E | ~10 | Drag-drop, type selector, upload flow, error states |
| document-query.spec.ts (E2E) | E2E | ~10 | RAG Q&A, source citations, document selector, error states |
| cx-copilot.spec.ts (E2E) | E2E | ~10 | SSE streaming chat, typing indicator, message history, escalation |
| fraud-correlation.spec.ts (E2E) | E2E | ~8 | Linked claims, correlation indicators, review workflow, empty state |
| accessibility.spec.ts (updated) | E2E | +4 | axe-core scans for 4 new routes (15 total routes scanned) |

### Sprint 5 New Test Files
| Test File | Type | Tests | Coverage |
|-----------|------|-------|----------|
| BM25ScorerTests.cs | Backend | ~6 | BM25 keyword scoring, IDF calculation, term frequency |
| BatchClaimServiceTests.cs | Backend | ~8 | Batch CSV parsing, validation, bulk triage |
| CohereEmbeddingServiceTests.cs | Backend | ~6 | Cohere API calls, error handling, dimension validation |
| CxConversationMemoryTests.cs | Backend | ~8 | Conversation save/load, session management, history |
| GeminiEmbeddingServiceTests.cs | Backend | ~6 | Gemini embedding API, error handling |
| HuggingFaceEmbeddingServiceTests.cs | Backend | ~6 | HuggingFace sentence-transformers, error handling |
| HybridRetrievalServiceTests.cs | Backend | ~8 | BM25+vector fusion, score normalization, ranking |
| JinaEmbeddingServiceTests.cs | Backend | ~6 | Jina API calls, error handling |
| SyntheticQAServiceTests.cs | Backend | ~6 | QA pair generation from chunks, quality scoring |

### SSE Mock Pattern (CX Copilot E2E)
CX Copilot E2E tests mock SSE streaming using Playwright's `page.route()` with a custom `ReadableStream` response:
```typescript
await page.route('**/api/insurance/cx/stream', route => {
  const encoder = new TextEncoder();
  const body = ['data: {"token":"Hello"}\n\n', 'data: {"token":" there","done":true}\n\n']
    .map(chunk => encoder.encode(chunk));
  route.fulfill({
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
    body: Buffer.concat(body)
  });
});
```
