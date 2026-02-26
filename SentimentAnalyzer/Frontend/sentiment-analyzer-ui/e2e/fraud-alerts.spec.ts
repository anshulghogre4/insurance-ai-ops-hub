import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';

test.describe('Fraud Alerts', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/dashboard/fraud');
  });

  test('should show page header with alert count', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Fraud Alerts');
    await expect(page.getByText('2 alerts')).toBeVisible({ timeout: 10_000 });
  });

  test('should display 2 alert cards with fraud scores', async ({ page }) => {
    // Wait for cards to render
    await expect(page.getByText('Claim #201')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Claim #202')).toBeVisible();

    // Fraud scores should be visible
    await expect(page.getByText('92').first()).toBeVisible();
    await expect(page.getByText('72').first()).toBeVisible();
  });

  test('should show SIU Referral indicator for high fraud score', async ({ page }) => {
    // Claim 201 has fraudScore 92 (>= 75), so SIU Referral should be visible
    await expect(page.getByText('Claim #201')).toBeVisible({ timeout: 10_000 });
    // Use exact match to avoid also matching "SIU Referrals" in summary stats
    await expect(page.getByText('SIU Referral', { exact: true })).toBeVisible();
  });

  test('should show summary stats', async ({ page }) => {
    await expect(page.getByText('Claim #201')).toBeVisible({ timeout: 10_000 });

    // Summary stats: Critical Risk, High Risk, Avg Score, SIU Referrals
    await expect(page.getByText('Critical Risk')).toBeVisible();
    await expect(page.getByText('High Risk').first()).toBeVisible();
    await expect(page.getByText('Avg Score')).toBeVisible();
    await expect(page.getByText('SIU Referrals')).toBeVisible();

    // Count values: 1 critical (92 >= 85), 1 high (72 >= 55 && < 75), avg 82
    const criticalStat = page.locator('.glass-card-static').filter({ hasText: 'Critical Risk' });
    await expect(criticalStat.locator('.text-2xl')).toContainText('1');

    const highStat = page.locator('.glass-card-static').filter({ hasText: 'High Risk' });
    await expect(highStat.locator('.text-2xl')).toContainText('1');

    const avgStat = page.locator('.glass-card-static').filter({ hasText: 'Avg Score' });
    await expect(avgStat.locator('.text-2xl')).toContainText('82');
  });

  test('should show empty state when no fraud alerts', async ({ page }) => {
    // Re-mock with empty response
    await page.route('**/api/insurance/fraud/alerts*', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) })
    );
    await page.goto('/dashboard/fraud');

    await expect(page.getByText('All Clear')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('No fraud alerts detected')).toBeVisible();
    await expect(page.getByText('Submit a Claim')).toBeVisible();
  });

  test('should show fraud flags on alert cards', async ({ page }) => {
    await expect(page.getByText('Claim #201')).toBeVisible({ timeout: 10_000 });

    // Claim 201 flags
    await expect(page.getByText('Timing anomaly').first()).toBeVisible();
    await expect(page.getByText('Financial motive').first()).toBeVisible();
  });

  test('should have refresh button', async ({ page }) => {
    await expect(page.getByLabel('Refresh fraud alerts')).toBeVisible();
  });

  test('should show error banner on 500 server error', async ({ page }) => {
    await page.route('**/api/insurance/fraud/alerts*', route =>
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Internal server error' }),
      })
    );
    await page.goto('/dashboard/fraud');

    await expect(page.getByText('Failed to load fraud alerts. Please try again.')).toBeVisible({ timeout: 10_000 });
  });

  test('should show error banner on 429 rate limit', async ({ page }) => {
    await page.route('**/api/insurance/fraud/alerts*', route =>
      route.fulfill({
        status: 429,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Rate limit exceeded. Free tier quota reached.' }),
      })
    );
    await page.goto('/dashboard/fraud');

    await expect(page.getByText('Failed to load fraud alerts. Please try again.')).toBeVisible({ timeout: 10_000 });
  });
});
