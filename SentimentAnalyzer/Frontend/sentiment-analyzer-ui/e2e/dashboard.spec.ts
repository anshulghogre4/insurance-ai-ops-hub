import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';

test.describe('Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/dashboard');
  });

  test('should show dashboard header', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Analytics Dashboard');
    await expect(page.getByText('Insurance sentiment trends')).toBeVisible();
  });

  test('should show refresh button', async ({ page }) => {
    await expect(page.getByRole('button', { name: 'Refresh' })).toBeVisible();
  });

  test('should display 4 metric cards', async ({ page }) => {
    await expect(page.getByText('Total Analyses')).toBeVisible();
    await expect(page.getByText('Avg Purchase Intent')).toBeVisible();
    await expect(page.getByText('Avg Sentiment')).toBeVisible();
    await expect(page.getByText('High Risk Alerts')).toBeVisible();
  });

  test('should show metric values from mock data', async ({ page }) => {
    // Wait for loading to complete - metric cards should appear
    const totalCard = page.locator('.metric-card').filter({ hasText: 'Total Analyses' });
    await expect(totalCard).toBeVisible();
    await expect(totalCard.locator('.text-2xl')).toContainText('42');

    const riskCard = page.locator('.metric-card').filter({ hasText: 'High Risk Alerts' });
    await expect(riskCard.locator('.text-2xl')).toContainText('7');
  });

  test('should show sentiment distribution chart', async ({ page }) => {
    await expect(page.getByText('Sentiment Distribution')).toBeVisible();
    // Check that distribution labels are present
    const chart = page.locator('text=Sentiment Distribution >> ..');
    await expect(chart.getByText('Positive')).toBeVisible();
    await expect(chart.getByText('Negative')).toBeVisible();
    await expect(chart.getByText('Neutral')).toBeVisible();
    await expect(chart.getByText('Mixed')).toBeVisible();
  });

  test('should show customer personas chart', async ({ page }) => {
    await expect(page.getByText('Customer Personas')).toBeVisible();
    await expect(page.getByText('ClaimFrustrated').first()).toBeVisible();
    await expect(page.getByText('RenewalRisk')).toBeVisible();
  });

  test('should show recent analyses history table', async ({ page }) => {
    await expect(page.getByText('Recent Analyses')).toBeVisible();
    // Wait for table data to load
    await expect(page.getByText('I reported water damage')).toBeVisible({ timeout: 10_000 });
    // Table headers
    await expect(page.locator('th').filter({ hasText: 'Text Preview' })).toBeVisible();
    await expect(page.locator('th').filter({ hasText: 'Sentiment' })).toBeVisible();
    // Table rows from mock data
    await expect(page.getByText('Very satisfied with my policy')).toBeVisible();
  });

  test('should refresh data when Refresh button is clicked', async ({ page }) => {
    await expect(page.getByText('Total Analyses')).toBeVisible();
    // Click refresh and verify data still renders
    await page.getByRole('button', { name: 'Refresh' }).click();
    await expect(page.getByText('Total Analyses')).toBeVisible();
    await expect(page.locator('.metric-card').filter({ hasText: 'Total Analyses' }).locator('.text-2xl')).toContainText('42');
  });

  test('should show error state when API fails', async ({ page }) => {
    await mockApiError(page, '**/api/insurance/dashboard', 500);
    await page.goto('/dashboard');
    const errorBanner = page.locator('.border-l-rose-500');
    await expect(errorBanner).toBeVisible({ timeout: 10_000 });
  });
});
