import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { mockAllApis } from './helpers/api-mocks';

const AXE_EXCLUDE_RULES = ['color-contrast'];

test.describe('Breadcrumb Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
  });

  test('should not show breadcrumbs on landing page', async ({ page }) => {
    await page.goto('/');
    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeHidden();
  });

  test('should not show breadcrumbs on login page', async ({ page }) => {
    await page.goto('/login');
    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeHidden();
  });

  test('should not show breadcrumbs on dashboard alone', async ({ page }) => {
    await page.goto('/dashboard');
    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeHidden();
  });

  test('should show "Dashboard / Claims / New Triage" on /claims/triage', async ({ page }) => {
    await page.goto('/claims/triage');
    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    // Verify crumb labels
    const items = breadcrumbNav.locator('li');
    await expect(items).toHaveCount(3);

    // Dashboard link
    await expect(items.nth(0).locator('a')).toHaveText('Dashboard');
    // Claims link
    await expect(items.nth(1).locator('a')).toHaveText('Claims');
    // Current page (no link)
    await expect(items.nth(2).locator('span[aria-current="page"]')).toHaveText('New Triage');
  });

  test('should show "Dashboard / Claims / History" on /claims/history', async ({ page }) => {
    await page.goto('/claims/history');
    await page.waitForLoadState('networkidle');

    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    const items = breadcrumbNav.locator('li');
    await expect(items).toHaveCount(3);

    await expect(items.nth(0).locator('a')).toHaveText('Dashboard');
    await expect(items.nth(1).locator('a')).toHaveText('Claims');
    await expect(items.nth(2).locator('span[aria-current="page"]')).toHaveText('History');
  });

  test('should show "Dashboard / Fraud Alerts" on /dashboard/fraud', async ({ page }) => {
    await page.goto('/dashboard/fraud');
    await page.waitForLoadState('networkidle');

    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    const items = breadcrumbNav.locator('li');
    await expect(items).toHaveCount(2);

    await expect(items.nth(0).locator('a')).toHaveText('Dashboard');
    await expect(items.nth(1).locator('span[aria-current="page"]')).toHaveText('Fraud Alerts');
  });

  test('should show "Dashboard / Provider Health" on /dashboard/providers', async ({ page }) => {
    await page.goto('/dashboard/providers');
    await page.waitForLoadState('networkidle');

    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    const items = breadcrumbNav.locator('li');
    await expect(items).toHaveCount(2);

    await expect(items.nth(0).locator('a')).toHaveText('Dashboard');
    await expect(items.nth(1).locator('span[aria-current="page"]')).toHaveText('Provider Health');
  });

  test('should show "Dashboard / Sentiment Analysis" on /sentiment', async ({ page }) => {
    await page.goto('/sentiment');

    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    const items = breadcrumbNav.locator('li');
    await expect(items).toHaveCount(2);

    await expect(items.nth(0).locator('a')).toHaveText('Dashboard');
    await expect(items.nth(1).locator('span[aria-current="page"]')).toHaveText('Sentiment Analysis');
  });

  test('should show "Dashboard / Documents / Upload" on /documents/upload', async ({ page }) => {
    await page.goto('/documents/upload');

    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    const items = breadcrumbNav.locator('li');
    await expect(items).toHaveCount(3);

    await expect(items.nth(0).locator('a')).toHaveText('Dashboard');
    await expect(items.nth(1).locator('a')).toHaveText('Documents');
    await expect(items.nth(2).locator('span[aria-current="page"]')).toHaveText('Upload');
  });

  test('should show "Dashboard / Documents / Query" on /documents/query', async ({ page }) => {
    await page.goto('/documents/query');
    await page.waitForLoadState('networkidle');

    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    const items = breadcrumbNav.locator('li');
    await expect(items).toHaveCount(3);

    await expect(items.nth(0).locator('a')).toHaveText('Dashboard');
    await expect(items.nth(1).locator('a')).toHaveText('Documents');
    await expect(items.nth(2).locator('span[aria-current="page"]')).toHaveText('Query');
  });

  test('should show "Dashboard / CX Copilot" on /cx/copilot', async ({ page }) => {
    await page.goto('/cx/copilot');

    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    const items = breadcrumbNav.locator('li');
    await expect(items).toHaveCount(2);

    await expect(items.nth(0).locator('a')).toHaveText('Dashboard');
    await expect(items.nth(1).locator('span[aria-current="page"]')).toHaveText('CX Copilot');
  });

  test('should show "Dashboard / Fraud Alerts / Correlations" on /fraud/correlations/:claimId', async ({ page }) => {
    await page.goto('/fraud/correlations/101');
    await page.waitForLoadState('networkidle');

    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    const items = breadcrumbNav.locator('li');
    await expect(items).toHaveCount(3);

    await expect(items.nth(0).locator('a')).toHaveText('Dashboard');
    await expect(items.nth(1).locator('a')).toHaveText('Fraud Alerts');
    await expect(items.nth(2).locator('span[aria-current="page"]')).toHaveText('Correlations');
  });

  test('should show "Dashboard / Insurance Analysis" on /insurance', async ({ page }) => {
    await page.goto('/insurance');

    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    const items = breadcrumbNav.locator('li');
    await expect(items).toHaveCount(2);

    await expect(items.nth(0).locator('a')).toHaveText('Dashboard');
    await expect(items.nth(1).locator('span[aria-current="page"]')).toHaveText('Insurance Analysis');
  });
});

test.describe('Breadcrumb Navigation - Click Behavior', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
  });

  test('clicking Dashboard crumb navigates to /dashboard', async ({ page }) => {
    await page.goto('/claims/triage');
    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    // Click Dashboard crumb
    await breadcrumbNav.locator('a').filter({ hasText: 'Dashboard' }).click();
    await expect(page).toHaveURL('/dashboard');
  });

  test('clicking Claims crumb navigates to /claims/history', async ({ page }) => {
    await page.goto('/claims/triage');
    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    // Click Claims crumb
    await breadcrumbNav.locator('a').filter({ hasText: 'Claims' }).click();
    await expect(page).toHaveURL('/claims/history');
  });

  test('clicking Fraud Alerts crumb from correlations navigates to /dashboard/fraud', async ({ page }) => {
    await page.goto('/fraud/correlations/101');
    await page.waitForLoadState('networkidle');

    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    // Click Fraud Alerts crumb
    await breadcrumbNav.locator('a').filter({ hasText: 'Fraud Alerts' }).click();
    await expect(page).toHaveURL('/dashboard/fraud');
  });

  test('clicking Documents crumb from query page navigates to /documents/upload', async ({ page }) => {
    await page.goto('/documents/query');
    await page.waitForLoadState('networkidle');

    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    // Click Documents crumb
    await breadcrumbNav.locator('a').filter({ hasText: 'Documents' }).click();
    await expect(page).toHaveURL('/documents/upload');
  });

  test('breadcrumbs update after navigation', async ({ page }) => {
    // Start at claims triage
    await page.goto('/claims/triage');
    let breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav.locator('span[aria-current="page"]')).toHaveText('New Triage');

    // Navigate to dashboard via breadcrumb
    await breadcrumbNav.locator('a').filter({ hasText: 'Dashboard' }).click();
    await expect(page).toHaveURL('/dashboard');

    // Breadcrumbs should now be hidden (dashboard alone = depth 1)
    await expect(breadcrumbNav).toBeHidden();
  });
});

test.describe('Breadcrumb Accessibility', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
  });

  test('breadcrumb nav should have proper ARIA structure', async ({ page }) => {
    await page.goto('/claims/triage');
    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    // Should have nav with aria-label
    await expect(breadcrumbNav).toHaveAttribute('aria-label', 'Breadcrumb');

    // Should use ordered list
    const ol = breadcrumbNav.locator('ol');
    await expect(ol).toBeVisible();

    // Current page should have aria-current="page"
    const currentPage = breadcrumbNav.locator('[aria-current="page"]');
    await expect(currentPage).toBeVisible();
    await expect(currentPage).toHaveText('New Triage');

    // Separators should be hidden from screen readers
    const separators = breadcrumbNav.locator('[aria-hidden="true"]');
    const count = await separators.count();
    expect(count).toBe(2); // Two separators between 3 crumbs
  });

  test('breadcrumb links should be keyboard navigable', async ({ page }) => {
    await page.goto('/claims/triage');
    const breadcrumbNav = page.locator('nav[aria-label="Breadcrumb"]');
    await expect(breadcrumbNav).toBeVisible();

    // Tab into breadcrumb links
    const links = breadcrumbNav.locator('a');
    const linkCount = await links.count();
    expect(linkCount).toBe(2); // Dashboard + Claims

    // Focus first link
    await links.nth(0).focus();
    await expect(links.nth(0)).toBeFocused();

    // Tab to second link
    await page.keyboard.press('Tab');
    await expect(links.nth(1)).toBeFocused();
  });

  test('breadcrumb page with crumbs should pass axe-core scan', async ({ page }) => {
    await page.goto('/claims/triage');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(AXE_EXCLUDE_RULES)
      .analyze();
    expect(results.violations).toEqual([]);
  });

  test('fraud correlations page with deep breadcrumbs should pass axe-core scan', async ({ page }) => {
    await page.goto('/fraud/correlations/101');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1')).toBeVisible({ timeout: 10_000 });

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(AXE_EXCLUDE_RULES)
      .analyze();
    expect(results.violations).toEqual([]);
  });

  test('documents upload page with breadcrumbs should pass axe-core scan', async ({ page }) => {
    await page.goto('/documents/upload');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(AXE_EXCLUDE_RULES)
      .analyze();
    expect(results.violations).toEqual([]);
  });
});
