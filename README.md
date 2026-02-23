# InsureSense AI - Insurance Domain Sentiment Analyzer

An AI-powered insurance domain sentiment analysis platform that uses a multi-agent system to analyze policyholder communications. Extracts sentiment, emotions, purchase intent, customer persona, journey stage, risk indicators, and policy recommendations. Built with .NET 10 Web API, Angular 21 SPA, and Microsoft Semantic Kernel agent orchestration.

## Features

### v1 (Legacy - General Purpose)
- **Real-time Sentiment Analysis**: Analyze any text and get instant results
- **Emotion Breakdown**: Detailed emotional components (joy, sadness, anger, fear, etc.)
- **Confidence Scores**: Visual indicators showing confidence levels

### v2 (Current - Insurance Domain)
- **Multi-Agent AI Analysis**: 6-agent pipeline (CTO, BA, Developer, QA, Architect, UX Designer) via Semantic Kernel
- **Insurance Context Classification**: Claims, policy servicing, billing, agent interaction, underwriting
- **Purchase Intent Scoring**: 0-100 scale with persona and journey stage detection
- **Risk Indicators**: Churn risk, complaint escalation risk, fraud indicators
- **PII Redaction**: Automatic redaction of SSN, policy numbers, claim numbers, phone, email before external AI calls
- **Analytics Dashboard**: Aggregated metrics, sentiment distribution, persona trends
- **Free AI Providers**: Groq (primary), Gemini (secondary), Ollama (local fallback)
- **Persistent Storage**: SQLite (development) / Supabase PostgreSQL (production)

## Tech Stack

### Backend
- .NET 10 Web API (C# 13, `net10.0`)
- Microsoft Semantic Kernel 1.71.0 (Agent orchestration)
- MediatR 14.0 (CQRS pattern)
- Entity Framework Core 10 (SQLite / PostgreSQL)
- ASP.NET Core Minimal API + Controllers hybrid

### Frontend
- Angular 21.1.0 (standalone components, signals)
- TypeScript 5.9.2 (strict mode)
- Tailwind CSS 3.4.17
- Vitest 4.0.8 (testing)

### AI Providers (Priority Order)
1. **Groq** - Llama 3.3 70B, fastest inference (250 req/day free)
2. **Gemini** - gemini-2.5-flash, best quality (60 req/min free)
3. **Ollama** - llama3.2, local inference (unlimited, PII-safe)
4. **OpenAI** - gpt-4o-mini, legacy v1 provider

### Testing
- Backend: xUnit 2.9.3 + Moq 4.20.72 (48 tests across 4 files)
- Frontend: Vitest 4.0.8 via Angular CLI (126 tests across 14 spec files)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/) and npm 11+
- At least one AI provider API key:
  - Groq API Key (free at [console.groq.com](https://console.groq.com)) -- recommended
  - Gemini API Key (free at [aistudio.google.com](https://aistudio.google.com))
  - Ollama installed locally (free at [ollama.com](https://ollama.com))
  - OpenAI API Key (for v1 legacy only)

## Setup Instructions

### 1. Clone the Repository

```bash
cd SentimentAnalyzer
```

### 2. Backend Setup

```bash
cd Backend
```

#### Configure API Keys

Create or edit `appsettings.Development.json` with your provider keys:

```json
{
  "OpenAI": {
    "ApiKey": "your-openai-api-key-here",
    "Model": "gpt-4o-mini"
  },
  "AgentSystem": {
    "Provider": "Groq",
    "Groq": {
      "ApiKey": "your-groq-api-key-here",
      "Model": "llama-3.3-70b-versatile",
      "Endpoint": "https://api.groq.com/openai/v1"
    },
    "Gemini": {
      "ApiKey": "your-gemini-api-key-here",
      "Model": "gemini-2.5-flash",
      "Endpoint": "https://generativelanguage.googleapis.com/v1beta/openai/"
    },
    "Ollama": {
      "Model": "llama3.2",
      "Endpoint": "http://localhost:11434/v1"
    }
  },
  "Database": {
    "Provider": "Sqlite"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=insurance_analysis.db"
  }
}
```

**Important**: Never commit `appsettings.Development.json` to version control (it is gitignored).

#### Install Dependencies and Run

```bash
dotnet restore
dotnet run
```

The API will start at `http://localhost:5143`

### 3. Frontend Setup

Open a new terminal:

```bash
cd Frontend/sentiment-analyzer-ui
npm install
npm start
```

The app will open at `http://localhost:4200`

### 4. Run Tests

```bash
# Backend tests
cd SentimentAnalyzer/Tests
dotnet test

# Frontend tests (must use Angular CLI, not direct vitest)
cd SentimentAnalyzer/Frontend/sentiment-analyzer-ui
npx ng test --watch=false
```

## Usage

1. Navigate to `http://localhost:4200` in your browser
2. Use the **navigation bar** to switch between:
   - **Home** (`/`) - v1 general sentiment analyzer
   - **Insurance Analyzer** (`/insurance`) - v2 multi-agent insurance analysis
   - **Dashboard** (`/dashboard`) - analytics and metrics
   - **Login** (`/login`) - Supabase authentication (optional)
3. On the Insurance Analyzer page:
   - Enter policyholder text or use a sample template
   - Select the interaction type (General, Email, Call, Chat, Review, Complaint)
   - Click **Analyze** to run the multi-agent pipeline
   - View sentiment, emotions, purchase intent, persona, risk indicators, and recommendations

## API Endpoints

### v1 (Legacy - Frozen)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/sentiment/analyze` | General sentiment analysis |
| GET | `/api/sentiment/health` | Health check |

**v1 Request:**
```json
{
  "text": "Your text here"
}
```

**v1 Response:**
```json
{
  "sentiment": "Positive",
  "confidenceScore": 0.95,
  "explanation": "The text expresses strong positive emotions...",
  "emotionBreakdown": {
    "joy": 0.8,
    "excitement": 0.6,
    "satisfaction": 0.7
  }
}
```

### v2 (Insurance Domain)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/insurance/analyze` | Multi-agent insurance sentiment analysis |
| GET | `/api/insurance/dashboard` | Aggregated metrics + sentiment distribution |
| GET | `/api/insurance/history?count=20` | Recent analysis history |
| GET | `/api/insurance/health` | Health check |

**v2 Request:**
```json
{
  "text": "I reported water damage on Jan 15. It has been 3 weeks with no response. Policy HO-2024-789456.",
  "interactionType": "Complaint"
}
```

**v2 Response:**
```json
{
  "sentiment": "Negative",
  "confidenceScore": 0.92,
  "explanation": "Customer is expressing frustration with claim processing delays...",
  "emotionBreakdown": { "frustration": 0.85, "anger": 0.70 },
  "insuranceAnalysis": {
    "purchaseIntentScore": 15,
    "customerPersona": "ClaimFrustrated",
    "journeyStage": "ActiveClaim",
    "riskIndicators": {
      "churnRisk": "High",
      "complaintEscalationRisk": "High",
      "fraudIndicators": "None"
    },
    "policyRecommendations": [
      { "product": "Claims Fast-Track", "reasoning": "Expedited claims processing to reduce churn" }
    ],
    "interactionType": "Complaint",
    "keyTopics": ["claim delay", "water damage", "no response"]
  },
  "quality": {
    "isValid": true,
    "qualityScore": 92,
    "issues": [],
    "suggestions": [],
    "warnings": []
  }
}
```

## Project Structure

```
SentimentAnalyzer/
├── Backend/
│   ├── Controllers/SentimentController.cs     # v1 API (FROZEN - never modify)
│   ├── Endpoints/InsuranceEndpoints.cs        # v2 Minimal API + MediatR
│   ├── Features/Insurance/
│   │   ├── Commands/AnalyzeInsuranceCommand.cs # CQRS command + handler + MapToResponse() + MapQuality()
│   │   └── Queries/
│   │       ├── GetDashboardQuery.cs           # Dashboard metrics query
│   │       └── GetHistoryQuery.cs             # Analysis history query
│   ├── Data/
│   │   ├── InsuranceAnalysisDbContext.cs       # EF Core DbContext
│   │   ├── IAnalysisRepository.cs             # Repository interface
│   │   ├── SqliteAnalysisRepository.cs        # SQLite implementation
│   │   └── Entities/AnalysisRecord.cs         # DB entity
│   ├── Models/
│   │   ├── SentimentRequest.cs                # v1 (frozen)
│   │   ├── SentimentResponse.cs               # v1 (frozen)
│   │   └── InsuranceAnalysisResponse.cs       # v2 (QualityDetail + QualityIssueDetail)
│   ├── Services/
│   │   ├── PIIRedactionService.cs             # PII redaction (source-generated regex)
│   │   ├── ISentimentService.cs               # v1 (frozen)
│   │   └── OpenAISentimentService.cs          # v1 (frozen)
│   ├── Middleware/GlobalExceptionHandler.cs    # IExceptionHandler
│   └── Program.cs                             # DI, middleware, endpoint registration
│
├── Agents/
│   ├── Configuration/                         # AgentSystemSettings, AgentConfiguration
│   ├── Definitions/
│   │   ├── AgentDefinitions.cs                # System prompts (6 agents)
│   │   └── AgentRole.cs                       # Agent role enum (6 roles)
│   ├── Orchestration/
│   │   ├── InsuranceAnalysisOrchestrator.cs   # AgentGroupChat pipeline
│   │   ├── AgentSelectionStrategy.cs          # Deterministic turn-taking
│   │   └── AnalysisTerminationStrategy.cs     # ANALYSIS_COMPLETE detection
│   ├── Plugins/                               # Semantic Kernel plugins
│   └── Models/AgentAnalysisResult.cs          # Agent output (QualityMetadata + QualityIssue)
│
├── Domain/
│   ├── Enums/                                 # SentimentType, CustomerPersona, InteractionType, etc.
│   └── Models/                                # Shared domain models
│
├── Frontend/sentiment-analyzer-ui/
│   └── src/app/
│       ├── components/
│       │   ├── sentiment-analyzer/            # v1 general analyzer (legacy)
│       │   ├── insurance-analyzer/            # v2 insurance analysis UI (signals, timer, phases)
│       │   ├── dashboard/                     # Analytics dashboard (charts, metrics)
│       │   ├── login/                         # Supabase auth login
│       │   └── nav/                           # Navigation bar (theme toggle, mobile menu)
│       ├── services/
│       │   ├── sentiment.service.ts           # v1 HTTP client
│       │   ├── insurance.service.ts           # v2 API client (inject() pattern)
│       │   ├── auth.service.ts                # Supabase auth (signals)
│       │   └── theme.service.ts               # Theme switching (dark/semi-dark/light)
│       ├── models/
│       │   ├── sentiment.model.ts             # v1 interfaces
│       │   └── insurance.model.ts             # v2 interfaces (QualityDetail, QualityIssue, 14+ types)
│       ├── guards/
│       │   ├── auth.guard.ts                  # Route protection (CanActivateFn)
│       │   └── guest.guard.ts                 # Guest-only routes
│       └── interceptors/
│           ├── auth.interceptor.ts            # JWT header injection
│           └── error.interceptor.ts           # 401/403 redirect handling
│
├── Tests/
│   ├── SentimentControllerTests.cs            # v1 regression (9 tests - FROZEN)
│   ├── InsuranceAnalysisControllerTests.cs    # CQRS handler tests (27 tests incl. 7 MapQuality)
│   ├── PIIRedactionTests.cs                   # PII redaction tests (11 tests)
│   └── UnitTest1.cs                           # Placeholder (1 test)
│
├── PROJECT_CONTEXT.md
├── REVIEW.md
├── QA_REPORT.md
└── README.md (this file)
```

## Configuration

### AI Provider Selection

Set `AgentSystem:Provider` in `appsettings.json` to switch providers:
- `"Groq"` (default) - Fastest, recommended for development
- `"Gemini"` - Higher quality analysis
- `"Ollama"` - Local inference, no API key needed, PII-safe
- `"OpenAI"` - Legacy, uses existing credits

### Database Provider

Set `Database:Provider` in `appsettings.json`:
- `"Sqlite"` (default) - Local file-based, zero setup
- `"PostgreSQL"` - Supabase cloud (500MB free tier)

### Supabase (Production)
```json
{
  "Database": { "Provider": "PostgreSQL" },
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password"
  },
  "Supabase": {
    "Url": "https://your-project.supabase.co",
    "JwtSecret": "your-jwt-secret"
  }
}
```

### Frontend API URL

The v2 Insurance service uses environment configuration. Update `Frontend/sentiment-analyzer-ui/src/environments/environment.ts`:

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5143'
};
```

## Troubleshooting

### CORS Issues
CORS is configured in `Program.cs` to allow `http://localhost:4200`. If using a different frontend port, update the CORS policy in `Backend/Program.cs`.

### AI Provider Errors
- **Groq 429**: Free tier limit reached (250 req/day). Switch to Gemini or Ollama in `appsettings.json`.
- **Gemini 429**: Rate limit (60 req/min). Add delay between requests or switch provider.
- **Ollama connection refused**: Ensure Ollama is running (`ollama serve`) on port 11434.
- **OpenAI errors**: Verify API key and credit balance.

### Frontend Test Runner
Tests **must** be run via Angular CLI, not directly with Vitest:
```bash
# Correct
npx ng test --watch=false

# Incorrect - will fail with "describe is not defined"
npx vitest run
```

### Port Already in Use
- Backend: Change port in `Backend/Properties/launchSettings.json`
- Frontend: Run with custom port: `ng serve --port 4300`
- Ollama: Default port 11434

### Backend Build While Running
Backend DLLs are locked while the API server is running. Stop the server before rebuilding.

## Ports

| Service | Port |
|---------|------|
| Backend API | `http://localhost:5143` |
| Frontend Dev | `http://localhost:4200` |
| Ollama (if used) | `http://localhost:11434` |

## License

This project is licensed under the MIT License.

## Contributing

Contributions welcome! Please open an issue or submit a pull request.

## Contact

For questions or feedback, please open an issue in the repository.
