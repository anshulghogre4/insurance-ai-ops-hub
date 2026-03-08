import { test, expect } from '@playwright/test';
import { mockAllApis } from './helpers/api-mocks';
import AxeBuilder from '@axe-core/playwright';

test.describe('Live Dashboard (SignalR Real-Time)', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);

    // Mock the SignalR negotiate endpoint to fail gracefully
    // (no real backend, so SignalR cannot connect)
    await page.route('**/hubs/**', (route) => {
      const url = route.request().url();
      if (url.includes('/negotiate')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Hub not available in test mode' })
        });
      }
      return route.abort();
    });

    await page.goto('/dashboard/live');
    // Wait for connection attempts to complete/fail and loading to clear
    await page.waitForTimeout(3000);
  });

  // ==================== Page Structure ====================

  test('should show live dashboard header and subtitle', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Live Dashboard');
    await expect(page.getByText('Real-time claims, fraud alerts, and provider health')).toBeVisible();
  });

  test('should show Static Dashboard navigation link with correct href', async ({ page }) => {
    const staticLink = page.getByRole('link', { name: 'Static Dashboard' });
    await expect(staticLink).toBeVisible();
    await expect(staticLink).toHaveAttribute('href', '/dashboard');
  });

  // ==================== Connection State ====================

  test('should show connection status indicator with role and aria-live', async ({ page }) => {
    const statusIndicator = page.locator('[role="status"][aria-live="polite"]');
    await expect(statusIndicator).toBeVisible();
    // Should show either Disconnected, Reconnecting, or Live text
    await expect(statusIndicator).toContainText(/(Disconnected|Reconnecting|Live)/);
  });

  test('should show error or disconnected banner when SignalR unavailable', async ({ page }) => {
    // Without a real backend, the dashboard should show degradation UI
    const main = page.getByRole('main');
    const errorBanner = main.getByText('Failed to connect to real-time services');
    const disconnectedBanner = main.getByText('Live updates paused');

    // At least one degradation indicator must be visible
    await expect(
      errorBanner.or(disconnectedBanner).first()
    ).toBeVisible({ timeout: 10000 });
  });

  // ==================== Metric Cards (rendered after loading clears) ====================

  test('should show all 4 metric card labels', async ({ page }) => {
    // Even in error state, if loading cleared, metric cards render with defaults
    await expect(page.getByText('Claims / Hour')).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('Avg Triage Time')).toBeVisible();
    await expect(page.getByText('Fraud Detection Rate')).toBeVisible();
    await expect(page.getByText('Doc Queries / Hour')).toBeVisible();
  });

  test('should show default metric values of 0', async ({ page }) => {
    await expect(page.getByText('Claims / Hour')).toBeVisible({ timeout: 5000 });
    // Default values when no SignalR data flows
    const claimsCard = page.locator('.rounded-xl').filter({ hasText: 'Claims / Hour' });
    await expect(claimsCard.locator('.text-3xl')).toContainText('0');
  });

  // ==================== Feed Empty States ====================

  test('should show empty state for claims feed', async ({ page }) => {
    await expect(page.getByText('No recent claims. Waiting for triage events...')).toBeVisible({ timeout: 5000 });
  });

  test('should show empty state for fraud alerts feed', async ({ page }) => {
    await expect(page.getByText('No fraud alerts detected yet.')).toBeVisible({ timeout: 5000 });
  });

  test('should show waiting message for provider health', async ({ page }) => {
    await expect(page.getByText('Waiting for health data...')).toBeVisible({ timeout: 5000 });
  });

  // ==================== Section Headers ====================

  test('should show all 3 feed section headers with icons', async ({ page }) => {
    const main = page.getByRole('main');
    await expect(main.getByRole('heading', { name: 'Provider Health' })).toBeVisible({ timeout: 5000 });
    await expect(main.getByRole('heading', { name: 'Recent Claims' })).toBeVisible();
    await expect(main.getByRole('heading', { name: 'Fraud Alerts' })).toBeVisible();
  });

  // ==================== Navigation ====================

  test('should navigate to static dashboard via link', async ({ page }) => {
    const staticLink = page.getByRole('link', { name: 'Static Dashboard' });
    await staticLink.click();
    await expect(page).toHaveURL(/\/dashboard$/);
  });

  // ==================== Accessibility ====================

  test('should pass axe-core accessibility scan', async ({ page }) => {
    const results = await new AxeBuilder({ page })
      .exclude('.animate-pulse') // Exclude loading skeleton animations
      .analyze();

    expect(results.violations).toEqual([]);
  });

  // ==================== Responsive Layout ====================

  test('should render correctly on mobile viewport', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/dashboard/live');
    await page.waitForTimeout(3000);

    // Page should still render header and sections vertically
    await expect(page.getByText('Live Dashboard')).toBeVisible();
    await expect(page.getByText('Claims / Hour')).toBeVisible({ timeout: 5000 });
  });
});
