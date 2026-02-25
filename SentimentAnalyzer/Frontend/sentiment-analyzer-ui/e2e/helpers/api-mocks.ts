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

  // Claims triage endpoint
  await page.route('**/api/insurance/claims/triage', (route) => {
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_CLAIM_TRIAGE_RESPONSE) });
    }
    return route.continue();
  });

  // Claims upload endpoint
  await page.route('**/api/insurance/claims/upload', (route) => {
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

  // Provider health endpoint
  await page.route('**/api/insurance/health/providers', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_PROVIDER_HEALTH_RESPONSE) })
  );

  // Claims by ID endpoint (must be after more specific claims routes)
  // Use route.fallback() so more-specific handlers (history, triage, upload) get priority
  await page.route('**/api/insurance/claims/*', (route) => {
    const url = route.request().url();
    if (url.includes('/history') || url.includes('/triage') || url.includes('/upload')) {
      return route.fallback();
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_CLAIM_TRIAGE_RESPONSE) });
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
