import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';

test.describe('Fraud Correlations', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/fraud/correlations/101');
  });

  test('should show page header with claim ID', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Fraud Correlations');
    await expect(page.getByText('Claim #101')).toBeVisible();
  });

  test('should show summary statistics', async ({ page }) => {
    // Wait for correlations to load
    await expect(page.getByText('Total Correlations')).toBeVisible({ timeout: 10_000 });

    // 2 total correlations
    const totalCard = page.locator('.glass-card-static').filter({ hasText: 'Total Correlations' });
    await expect(totalCard.locator('.text-2xl')).toContainText('2');

    // Avg score: (78 + 62) / 2 = 70
    const avgCard = page.locator('.glass-card-static').filter({ hasText: 'Avg Match Score' });
    await expect(avgCard.locator('.text-2xl')).toContainText('70');

    // Pending count: 2
    const pendingCard = page.locator('.glass-card-static').filter({ hasText: 'Pending Review' });
    await expect(pendingCard.locator('.text-2xl')).toContainText('2');
  });

  test('should show correlation cards', async ({ page }) => {
    await expect(page.getByText('Correlation #1001')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Correlation #1002')).toBeVisible();
  });

  test('should show strategy badges', async ({ page }) => {
    await expect(page.getByText('Correlation #1001')).toBeVisible({ timeout: 10_000 });

    // Correlation 1001: DateProximity + SharedFlags
    await expect(page.getByText('DateProximity').first()).toBeVisible();
    await expect(page.getByText('SharedFlags').first()).toBeVisible();

    // Correlation 1002: SimilarNarrative + SameSeverity
    await expect(page.getByText('SimilarNarrative').first()).toBeVisible();
    await expect(page.getByText('SameSeverity').first()).toBeVisible();
  });

  test('should show score gauge on cards', async ({ page }) => {
    await expect(page.getByText('Correlation #1001')).toBeVisible({ timeout: 10_000 });

    // Score 78 (0.78 * 100) and 62 (0.62 * 100)
    await expect(page.getByText('78').first()).toBeVisible();
    await expect(page.getByText('62').first()).toBeVisible();
  });

  test('should show source and correlated claim split', async ({ page }) => {
    await expect(page.getByText('Correlation #1001')).toBeVisible({ timeout: 10_000 });

    // Source Claim
    await expect(page.getByText('Source Claim').first()).toBeVisible();
    await expect(page.getByText('#101').first()).toBeVisible();

    // Correlated Claim
    await expect(page.getByText('Correlated Claim').first()).toBeVisible();
    await expect(page.getByText('#205').first()).toBeVisible();
  });

  test('should show status badges', async ({ page }) => {
    await expect(page.getByText('Correlation #1001')).toBeVisible({ timeout: 10_000 });

    // Both correlations are Pending
    const pendingBadges = page.getByText('Pending', { exact: true });
    await expect(pendingBadges.first()).toBeVisible();
  });

  test('should have confirm button on pending correlations', async ({ page }) => {
    await expect(page.getByText('Correlation #1001')).toBeVisible({ timeout: 10_000 });

    await expect(page.getByLabel('Confirm as fraud').first()).toBeVisible();
    await expect(page.getByText('Confirm Fraud').first()).toBeVisible();
  });

  test('should have dismiss button on pending correlations', async ({ page }) => {
    await expect(page.getByText('Correlation #1001')).toBeVisible({ timeout: 10_000 });

    await expect(page.getByLabel('Dismiss correlation').first()).toBeVisible();
    await expect(page.getByText('Dismiss').first()).toBeVisible();
  });

  test('should show status filter tabs', async ({ page }) => {
    await expect(page.getByLabel('Filter by All status')).toBeVisible();
    await expect(page.getByLabel('Filter by Pending status')).toBeVisible();
    await expect(page.getByLabel('Filter by Confirmed status')).toBeVisible();
    await expect(page.getByLabel('Filter by Dismissed status')).toBeVisible();
  });

  test('should have Run New Analysis button', async ({ page }) => {
    const analysisBtn = page.getByLabel('Run new correlation analysis');
    await expect(analysisBtn).toBeVisible();
    await expect(analysisBtn).toBeEnabled();
  });

  test('should show empty state when no correlations', async ({ page }) => {
    await page.route('**/api/insurance/fraud/correlations/*', (route) => {
      const url = route.request().url();
      if (url.includes('/review')) return route.fallback();
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }),
      });
    });
    await page.goto('/fraud/correlations/101');

    await expect(page.getByText('No correlations found')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Run a new analysis to detect cross-claim fraud patterns')).toBeVisible();
  });
});
