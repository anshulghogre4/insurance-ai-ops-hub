# Common Pitfalls & Solutions

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
| Long analysis wait (15-60s) | Show elapsed timer + phase descriptions. Use skeleton cards for layout stability. |
| No empty state before first analysis | Always show a "Ready to Analyze" card with 7 dimension badges before first submission. |
| Missing ARIA/accessibility | All interactive elements need `role`, `aria-live`, `aria-label`. Progress bars need `role="progressbar"`. |
| Raw PII in diagnostic logs | Use SHA-256 hashing. NEVER log raw JSON from agent output. |
| E2e tests failing with `ERR_EMPTY_RESPONSE` | Port 4200 conflict. Kill with `taskkill /F /IM node.exe` (Windows). |
| E2e desktop nav tests fail on mobile | Use `skipOnMobile()` helper in navigation tests. |
| E2e mock data type mismatch | Mock data MUST match TypeScript interface types exactly. |
| E2e accessibility color-contrast failures | Known CSS issue. Excluded from strict axe-core tests. |
| Angular dev server overwhelmed by Playwright | Limit `workers: 3` in `playwright.config.ts`. |
| MCP server hangs on direct run | MCP servers are long-running stdio processes. Never run directly — use Playwright MCP via Claude Code or test with `timeout 5s`. |
| Stitch MCP proxy connection failure | Ensure `@_davideast/stitch-mcp` is installed. Run `npx -y @_davideast/stitch-mcp proxy` to verify. Check network access to Google Stitch API. |
| Playwright MCP can't reach localhost:4200 | Start Angular dev server first (`npm start`). Kill stale node processes (`taskkill /F /IM node.exe`). |
| MCP-generated tests have wrong mock data | Generated specs must be reviewed — update mock data in `e2e/fixtures/mock-data.ts` to match real API contracts. |
| SQLite vector search too slow (>1K chunks) | Cap dev at 500 chunks; production uses Supabase pgvector. |
| Voyage AI free tier exhaustion (50M tokens) | Ollama `nomic-embed-text` fallback; use incremental indexing, not bulk. |
| SSE streaming browser compat issues | Use standard `EventSource` API; fallback to polling. |
| Cross-claim correlation false positives | Require 2+ indicators; narrative similarity threshold 0.92. |
