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

### Sprint 4 Test Targets
| Category | Current | Sprint 4 New | Sprint 4 Total |
|----------|---------|-------------|----------------|
| Backend (xUnit) | 278 | 76+ | 354+ |
| Frontend Unit (Vitest) | 199 | 36+ | 235+ |
| E2E (Playwright) | 263 | 40+ | 303+ |
| **Grand Total** | **740** | **152+** | **892+** |
