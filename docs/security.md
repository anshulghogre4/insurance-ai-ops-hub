# Security Reference

## PII Redaction (Non-Negotiable)
Before ANY external AI API call, redact:
- SSN: `\d{3}-\d{2}-\d{4}` -> `[SSN-REDACTED]`
- Policy numbers: `[A-Z]{2,3}-\d{4,10}` -> `[POLICY-REDACTED]`
- Claim numbers: `CLM-\d{4}-\d{4,8}` -> `[CLAIM-REDACTED]`
- Phone numbers -> `[PHONE-REDACTED]`
- Email addresses -> `[EMAIL-REDACTED]`

## API Key & Secrets Management

### Tiered Secrets Strategy
| Tier | Environment | Method |
|------|-------------|--------|
| Tier 1 | Local Dev | .NET User Secrets (`dotnet user-secrets set`) |
| Tier 2 | CI/CD | Environment variables (`AgentSystem__Groq__ApiKey`) |
| Tier 3 | Production | Azure Key Vault / AWS Secrets Manager / Supabase Vault |

### Backend Rules
- **User Secrets enabled** via `<UserSecretsId>` in `.csproj`
- `appsettings.json`: empty placeholder keys only (committed)
- `appsettings.Development.json`: logging overrides only (gitignored) - NEVER store keys
- Startup validation in `Program.cs` checks all provider keys at boot
- NEVER log API key values - validate presence only with `string.IsNullOrWhiteSpace()`

### Frontend Rules
- All services import from `environment.ts` (NOT `environment.development.ts`)
- Angular CLI `fileReplacements` swaps environments during `ng serve`
- `environment.ts` (committed): production config with empty keys
- `environment.development.ts` (gitignored): local dev config

### Required Secrets (by provider)
```bash
# LLM Providers
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

# CI/CD Environment Variables
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

### NEVER Commit
- Real API keys in any file
- `appsettings.Development.json` with secrets
- `environment.development.ts` with real Supabase URLs/keys
- `.env` files

### Logging Rules
- NEVER log raw input text
- Log only: input hash (SHA-256), sentiment result, provider used, processing time
- Use `ILogger<T>` at appropriate levels (Info for flow, Warning for retries, Error for exceptions)

### Insurance Domain Rules
1. **PII First**: Redact PII before ANY external AI provider call
2. **Audit Trail**: Log every analysis (timestamp, provider, input hash, result)
3. **Complaint Detection**: Flag texts with confidence >0.8 negative + keywords
4. **Insurance Context**: Classify every analysis (claims, policy servicing, billing, agent interaction, underwriting)
5. **No Training Data Leakage**: Use API modes that do not train on input

### CX Copilot PII Rules (Sprint 4 Week 3)
- PII redacted on **both input and output** (dual-pass redaction)
- `CxInteractionRecord` stores SHA-256 hash of messages — never raw text
- Escalation detection uses 16 keywords ("attorney", "department of insurance", "file complaint", etc.) + LLM-tagged escalation flags
- Regulatory disclaimer enforcement on all CX responses
- Audit trail: every interaction logged with timestamp, tone classification, escalation status

## MCP Server Security
- MCP servers run locally via stdio — no external network exposure
- Playwright MCP runs headless (no visible browser window) — configured with `--headless` flag
- Stitch MCP uses proxy transport — network calls go to Google Stitch AI (design generation only, no PII)
- `.mcp.json` is committed to repo (contains no secrets, only package names and flags)
- MCP servers do NOT have access to .NET User Secrets or API keys
- Never pass PII-containing text through MCP tool calls — redact first

## CI/CD Security (Sprint 5)
- GitHub Actions secrets stored in repository settings (never in workflow files)
- CI/CD workflow `.github/workflows/ci.yml` uses `secrets.GROQ_API_KEY` etc.
- No real API keys needed for test runs — all backend tests use Moq, frontend tests use mocks
- Environment files: `environment.development.ts` is gitignored, never committed

### Embedding Service Secrets (Sprint 4-5)
```bash
# Voyage AI (primary embeddings)
dotnet user-secrets set "AgentSystem:Voyage:ApiKey" "your-key"

# Cohere (Sprint 5 - embed-english-v3.0)
dotnet user-secrets set "AgentSystem:Cohere:ApiKey" "your-key"

# Gemini (Sprint 5 - text-embedding-004, uses same Gemini API key)
# Uses AgentSystem:Gemini:ApiKey (already configured)

# HuggingFace (Sprint 5 - sentence-transformers, uses same HF key)
# Uses AgentSystem:HuggingFace:ApiKey (already configured)

# Jina (Sprint 5 - embeddings-v3)
dotnet user-secrets set "AgentSystem:Jina:ApiKey" "your-key"

# CI/CD
AgentSystem__Voyage__ApiKey=your-key
AgentSystem__Cohere__ApiKey=your-key
AgentSystem__Jina__ApiKey=your-key
```
