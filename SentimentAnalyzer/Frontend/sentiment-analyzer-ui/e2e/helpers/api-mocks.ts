import { Page } from '@playwright/test';
import {
  MOCK_INSURANCE_ANALYSIS_RESPONSE,
  MOCK_SENTIMENT_V1_RESPONSE,
  MOCK_DASHBOARD_RESPONSE,
  MOCK_HISTORY_RESPONSE,
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
