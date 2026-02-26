# API Reference

## URL Structure
- v1 (legacy, frozen): `/api/sentiment/{action}`
- v2 (insurance): `/api/insurance/{action}`
- v2 (documents, v4.0 Week 2): `/api/insurance/documents/{action}`
- v2 (CX copilot, v4.0 Week 3): `/api/insurance/cx/{action}`
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

## Sprint 4 Week 3 Endpoints (v4.0 — LIVE)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/insurance/cx/chat` | POST | CX Copilot chat (PII redacted input + output, tone classification, escalation detection) |
| `/api/insurance/cx/stream` | POST | CX Copilot SSE streaming chat |
| `/api/insurance/fraud/correlate` | POST | Trigger cross-claim correlation analysis for a claim |
| `/api/insurance/fraud/correlations/{claimId}` | GET | Get fraud correlations for a claim |
| `/api/insurance/fraud/correlations/{claimId}` | DELETE | Delete fraud correlations for a claim |

## Sprint 4 Week 2 Endpoints (v4.0 — LIVE)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/insurance/documents/upload` | POST | Upload document → OCR → chunk → embed → store |
| `/api/insurance/documents/query` | POST | RAG query → embed → vector search → LLM answer with citations |
| `/api/insurance/documents/{id}` | GET | Get document by ID |
| `/api/insurance/documents/history` | GET | List uploaded documents |

## Sprint 4 Week 4 Frontend-Consumed Endpoints

The following endpoints are consumed by the 5 new Angular components added in Week 4. They map to backend handlers built in Weeks 2-3.

### Document Intelligence Endpoints (document-upload, document-query, document-result components)
| Endpoint | Method | Purpose | Request | Response |
|----------|--------|---------|---------|----------|
| `/api/insurance/documents/upload` | POST | Upload document for RAG indexing | `FormData` (file + documentType) | `{ id, fileName, documentType, chunkCount, createdAt }` |
| `/api/insurance/documents/query` | POST | Query documents via RAG | `{ query, documentId? }` | `{ answer, citations: [{ chunkText, documentName, page, score }] }` |
| `/api/insurance/documents/history` | GET | List uploaded documents | Query: `?page=1&pageSize=10` | `PaginatedResponse<DocumentSummary>` |
| `/api/insurance/documents/{id}` | GET | Get document by ID | Path param | `{ id, fileName, documentType, chunkCount, status, createdAt }` |
| `/api/insurance/documents/{id}` | DELETE | Delete document and its chunks | Path param | `204 No Content` |

### CX Copilot Endpoints (cx-copilot component)
| Endpoint | Method | Purpose | Request | Response |
|----------|--------|---------|---------|----------|
| `/api/insurance/cx/chat` | POST | CX Copilot chat (non-streaming) | `{ message, sessionId? }` | `{ response, tone, escalationRecommended, escalationReason?, llmProvider, elapsedMilliseconds, disclaimer }` |
| `/api/insurance/cx/stream` | POST | CX Copilot SSE streaming chat | `{ message, sessionId? }` | SSE stream: `data: { type:"content", content:"token", metadata? }` |

### Fraud Correlation Endpoints (fraud-correlation component)
| Endpoint | Method | Purpose | Request | Response |
|----------|--------|---------|---------|----------|
| `/api/insurance/fraud/correlate` | POST | Trigger cross-claim correlation | `{ claimId }` | `{ claimId, correlations: [...], count }` |
| `/api/insurance/fraud/correlations/{claimId}` | GET | Get correlations for a claim | Path + query: `?page=1&pageSize=20` | `PaginatedResponse<FraudCorrelation>` |
| `/api/insurance/fraud/correlations/{claimId}` | DELETE | Delete all correlations for a claim | Path param | `204 No Content` |

## HTTP Status Codes
- 200: Successful analysis
- 400: Validation error
- 429: Rate limited (free tier exceeded — per-endpoint limits in Sprint 4: analyze 10/min, triage 5/min, fraud 5/min, doc upload 3/min)
- 500: AI provider failure after all fallbacks
- 503: All providers down
