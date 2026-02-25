# API Reference

## URL Structure
- v1 (legacy, frozen): `/api/sentiment/{action}`
- v2 (insurance): `/api/insurance/{action}`
- v2 (documents, v4.0 planned): `/api/insurance/documents/{action}`
- v2 (CX copilot, v4.0 planned): `/api/insurance/cx/{action}`
- Health: `/api/sentiment/health` (v1), `/api/insurance/health` (v2)

## v2 Response Envelope
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
    "warnings": ["[warning] sentiment: Confidence below threshold"]
  }
}
```

## Sprint 2 Claims & Fraud Endpoints
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/insurance/claims/triage` | POST | Triage a claim (text → agents → severity/fraud/actions) |
| `/api/insurance/claims/upload` | POST | Upload claim evidence (image/audio/PDF → multimodal processing) |
| `/api/insurance/claims/{id}` | GET | Get claim by ID |
| `/api/insurance/claims/history` | GET | List claims with filters + pagination |
| `/api/insurance/fraud/analyze` | POST | Run fraud analysis on existing claim |
| `/api/insurance/fraud/score/{claimId}` | GET | Get fraud score for a claim |
| `/api/insurance/fraud/alerts` | GET | Get claims with fraud score > 55 |
| `/api/insurance/health/providers` | GET | LLM + multimodal provider health status |

## Sprint 4 Planned Endpoints (v4.0)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/insurance/documents/upload` | POST | Upload document → OCR → chunk → embed → store |
| `/api/insurance/documents/query` | POST | RAG query → embed → vector search → LLM answer with citations |
| `/api/insurance/documents/{id}` | GET | Get document by ID |
| `/api/insurance/documents/history` | GET | List uploaded documents |
| `/api/insurance/cx/chat` | POST | CX Copilot chat (SSE streaming response) |
| `/api/insurance/fraud/correlations` | GET | Cross-claim fraud correlation results |

## HTTP Status Codes
- 200: Successful analysis
- 400: Validation error
- 429: Rate limited (free tier exceeded — per-endpoint limits in Sprint 4: analyze 10/min, triage 5/min, fraud 5/min, doc upload 3/min)
- 500: AI provider failure after all fallbacks
- 503: All providers down
