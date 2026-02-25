import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';
import { MOCK_CLAIMS_HISTORY_RESPONSE } from './fixtures/mock-data';

test.describe('Claims History', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/claims/history');
  });

  test('should show page header with total claims count', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Claims History');
    await expect(page.getByText('3 total claims')).toBeVisible();
  });

  test('should display table with 3 mock claims', async ({ page }) => {
    // Wait for table to render
    const table = page.locator('table[role="table"]');
    await expect(table).toBeVisible({ timeout: 10_000 });

    // Verify all 3 claim IDs are visible
    await expect(page.getByText('#101')).toBeVisible();
    await expect(page.getByText('#102')).toBeVisible();
    await expect(page.getByText('#103')).toBeVisible();
  });

  test('should display severity badges correctly', async ({ page }) => {
    const table = page.locator('table[role="table"]');
    await expect(table).toBeVisible({ timeout: 10_000 });

    // Severity values from mock data - scope to table body to avoid matching filter dropdown <option> elements
    const tbody = page.locator('tbody');
    await expect(tbody.getByText('High', { exact: true }).first()).toBeVisible();
    await expect(tbody.getByText('Low', { exact: true }).first()).toBeVisible();
    await expect(tbody.getByText('Critical', { exact: true }).first()).toBeVisible();
  });

  test('should show filter dropdowns', async ({ page }) => {
    await expect(page.getByLabel('Filter by severity')).toBeVisible();
    await expect(page.getByLabel('Filter by status')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Apply filters' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Clear filters' })).toBeVisible();
  });

  test('should show claim type and fraud score in table rows', async ({ page }) => {
    const table = page.locator('table[role="table"]');
    await expect(table).toBeVisible({ timeout: 10_000 });

    // Fraud scores
    await expect(page.getByText('42').first()).toBeVisible();
    await expect(page.getByText('12').first()).toBeVisible();
    await expect(page.getByText('78').first()).toBeVisible();
  });

  test('should show empty state when no claims returned', async ({ page }) => {
    // Re-mock with empty response
    await page.route('**/api/insurance/claims/history*', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 })
      })
    );
    await page.goto('/claims/history');

    await expect(page.getByText('No claims found')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Submit a claim for AI-powered triage')).toBeVisible();
    await expect(page.getByText('Start Triage')).toBeVisible();
  });

  test('should have New Triage and Refresh buttons in header', async ({ page }) => {
    await expect(page.getByLabel('New triage')).toBeVisible();
    await expect(page.getByLabel('Refresh claims')).toBeVisible();
  });

  test('should show error on 500 server error', async ({ page }) => {
    await page.route('**/api/insurance/claims/history*', route =>
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Internal server error' }),
      })
    );
    await page.goto('/claims/history');

    await expect(page.getByRole('alert')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Failed to load claims history')).toBeVisible();
    await expect(page.getByLabel('Retry loading claims')).toBeVisible();
  });

  test('should show error on 429 rate limit', async ({ page }) => {
    await page.route('**/api/insurance/claims/history*', route =>
      route.fulfill({
        status: 429,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Rate limit exceeded. Free tier quota reached.' }),
      })
    );
    await page.goto('/claims/history');

    await expect(page.getByRole('alert')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Failed to load claims history')).toBeVisible();
  });
});
