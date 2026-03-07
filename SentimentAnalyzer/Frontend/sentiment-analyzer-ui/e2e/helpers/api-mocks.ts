import { Page } from '@playwright/test';
import {
  MOCK_INSURANCE_ANALYSIS_RESPONSE,
  MOCK_SENTIMENT_V1_RESPONSE,
  MOCK_DASHBOARD_RESPONSE,
  MOCK_HISTORY_RESPONSE,
  MOCK_CLAIM_TRIAGE_RESPONSE,
  MOCK_CLAIMS_HISTORY_RESPONSE,
  MOCK_FRAUD_ALERTS_RESPONSE,
  MOCK_PROVIDER_HEALTH_RESPONSE,
  MOCK_EVIDENCE_RESPONSE,
  MOCK_FRAUD_ANALYSIS_RESPONSE,
  MOCK_DOCUMENT_UPLOAD_RESULT,
  MOCK_DOCUMENT_QUERY_RESULT,
  MOCK_DOCUMENT_DETAIL,
  MOCK_DOCUMENT_HISTORY_RESPONSE,
  MOCK_UPLOAD_PROGRESS_SSE,
  MOCK_CX_CHAT_RESPONSE,
  MOCK_CX_STREAM_EVENTS,
  MOCK_CX_SESSION_RESPONSE,
  MOCK_CX_SESSION_HISTORY_RESPONSE,
  MOCK_CORRELATE_RESULT,
  MOCK_CORRELATIONS_PAGINATED,
  MOCK_BATCH_CLAIM_UPLOAD_RESULT,
  MOCK_QA_PAIRS,
  MOCK_EXTENDED_PROVIDER_HEALTH,
} from '../fixtures/mock-data';

/** Set up all API mock routes so e2e tests don't need a running backend. */
export async function mockAllApis(page: Page): Promise<void> {
  // Insurance analysis endpoint
  await page.route('**/api/insurance/analyze', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_INSURANCE_ANALYSIS_RESPONSE),
      });
    }
    return route.continue();
  });

  // Sentiment v1 endpoint
  await page.route('**/api/sentiment/analyze', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_SENTIMENT_V1_RESPONSE),
      });
    }
    return route.continue();
  });

  // Dashboard endpoint
  await page.route('**/api/insurance/dashboard', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_DASHBOARD_RESPONSE),
    }),
  );

  // History endpoint
  await page.route('**/api/insurance/history*', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_HISTORY_RESPONSE),
    }),
  );

  // Health endpoints
  await page.route('**/api/sentiment/health', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ status: 'Healthy' }),
    }),
  );
  await page.route('**/api/insurance/health', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ status: 'Healthy' }),
    }),
  );

  // Liveness + readiness probes (Sprint 6)
  await page.route('**/health/ready', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        status: 'Ready',
        timestamp: new Date().toISOString(),
        checks: {
          database: 'Connected',
          llmProviders: ['Groq', 'Ollama'],
          llmProviderCount: 2,
          multimodal: { ner: 'HuggingFace', contentSafety: 'AzureContentSafety', stt: 'Deepgram', translation: 'AzureTranslator', ocr: 'PdfPig' }
        }
      }),
    }),
  );
  await page.route('**/health', (route) => {
    const url = route.request().url();
    if (url.includes('/health/ready') || url.includes('/health/providers')) {
      return route.fallback();
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ status: 'Healthy', timestamp: new Date().toISOString() }),
    });
  });

  // Claims triage endpoint
  await page.route('**/api/insurance/claims/triage', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_CLAIM_TRIAGE_RESPONSE) });
    }
    return route.continue();
  });

  // Batch claims CSV upload endpoint
  await page.route('**/api/insurance/claims/batch*', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_BATCH_CLAIM_UPLOAD_RESULT) });
    }
    return route.continue();
  });

  // Claims upload endpoint
  await page.route('**/api/insurance/claims/upload*', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_EVIDENCE_RESPONSE) });
    }
    return route.continue();
  });

  // Claims history endpoint
  await page.route('**/api/insurance/claims/history*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_CLAIMS_HISTORY_RESPONSE) })
  );

  // Fraud analyze endpoint
  await page.route('**/api/insurance/fraud/analyze', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_FRAUD_ANALYSIS_RESPONSE) });
    }
    return route.continue();
  });

  // Fraud alerts endpoint
  await page.route('**/api/insurance/fraud/alerts*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_FRAUD_ALERTS_RESPONSE) })
  );

  // Fraud score endpoint
  await page.route('**/api/insurance/fraud/score/*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_FRAUD_ANALYSIS_RESPONSE) })
  );

  // Extended provider health endpoint (must be before /providers to avoid route conflict)
  await page.route('**/api/insurance/health/providers/extended*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_EXTENDED_PROVIDER_HEALTH) })
  );

  // Provider health endpoint (legacy)
  await page.route('**/api/insurance/health/providers', (route) => {
    const url = route.request().url();
    if (url.includes('/extended')) {
      return route.fallback();
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_PROVIDER_HEALTH_RESPONSE) });
  });

  // Claims by ID endpoint (must be after more specific claims routes)
  // Use route.fallback() so more-specific handlers (history, triage, upload) get priority
  await page.route('**/api/insurance/claims/*', (route) => {
    const url = route.request().url();
    if (url.includes('/history') || url.includes('/triage') || url.includes('/upload') || url.includes('/batch')) {
      return route.fallback();
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_CLAIM_TRIAGE_RESPONSE) });
  });

  // ===================== Document Intelligence RAG =====================

  // SSE streaming upload (must be before the regular upload route — more specific first)
  await page.route('**/api/insurance/documents/upload/stream*', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
          'Connection': 'keep-alive'
        },
        body: MOCK_UPLOAD_PROGRESS_SSE
      });
    }
    return route.continue();
  });

  // Document upload endpoint (include query params like ?category=Policy)
  await page.route('**/api/insurance/documents/upload*', (route) => {
    const url = route.request().url();
    // Let the more specific /upload/stream route handle streaming requests
    if (url.includes('/upload/stream')) {
      return route.fallback();
    }
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_DOCUMENT_UPLOAD_RESULT) });
    }
    return route.continue();
  });

  // Document query endpoint
  await page.route('**/api/insurance/documents/query', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_DOCUMENT_QUERY_RESULT) });
    }
    return route.continue();
  });

  // Document history endpoint
  await page.route('**/api/insurance/documents/history*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_DOCUMENT_HISTORY_RESPONSE) })
  );

  // Document Q&A generation endpoint (POST)
  await page.route('**/api/insurance/documents/*/generate-qa*', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_QA_PAIRS) });
    }
    return route.continue();
  });

  // Document Q&A pairs retrieval endpoint (GET)
  await page.route('**/api/insurance/documents/*/qa-pairs*', (route) => {
    if (route.request().method() === 'GET') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_QA_PAIRS) });
    }
    return route.continue();
  });

  // Document by ID endpoint (must be after upload/query/history/generate-qa/qa-pairs)
  await page.route('**/api/insurance/documents/*', (route) => {
    const url = route.request().url();
    if (url.includes('/upload') || url.includes('/query') || url.includes('/history') || url.includes('/generate-qa') || url.includes('/qa-pairs')) {
      return route.fallback();
    }
    if (route.request().method() === 'DELETE') {
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_DOCUMENT_DETAIL) });
  });

  // ===================== Customer Experience Copilot =====================

  // CX chat endpoint
  await page.route('**/api/insurance/cx/chat', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_CX_CHAT_RESPONSE) });
    }
    return route.continue();
  });

  // CX stream endpoint (SSE)
  await page.route('**/api/insurance/cx/stream', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache', 'Connection': 'keep-alive' },
        body: MOCK_CX_STREAM_EVENTS
      });
    }
    return route.continue();
  });

  // CX session creation endpoint
  await page.route('**/api/insurance/cx/sessions', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_CX_SESSION_RESPONSE) });
    }
    return route.continue();
  });

  // CX session history endpoint (must be after /sessions POST)
  await page.route('**/api/insurance/cx/sessions/*/history', (route) => {
    if (route.request().method() === 'GET') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_CX_SESSION_HISTORY_RESPONSE) });
    }
    return route.continue();
  });

  // ===================== Fraud Correlation =====================

  // Fraud correlate endpoint
  await page.route('**/api/insurance/fraud/correlate', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_CORRELATE_RESULT) });
    }
    return route.continue();
  });

  // Fraud correlations by claimId (GET + DELETE)
  await page.route('**/api/insurance/fraud/correlations/*/review', (route) => {
    if (route.request().method() === 'PATCH') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ id: 1001, status: 'Confirmed', message: 'Correlation reviewed successfully' }) });
    }
    return route.continue();
  });

  await page.route('**/api/insurance/fraud/correlations/*', (route) => {
    const url = route.request().url();
    if (url.includes('/review')) {
      return route.fallback();
    }
    if (route.request().method() === 'DELETE') {
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_CORRELATIONS_PAGINATED) });
  });
}

/** Mock API to return an error for a specific endpoint. */
export async function mockApiError(page: Page, urlPattern: string, status = 500): Promise<void> {
  await page.route(urlPattern, (route) =>
    route.fulfill({
      status,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Service temporarily unavailable. All AI providers are down.' }),
    }),
  );
}
