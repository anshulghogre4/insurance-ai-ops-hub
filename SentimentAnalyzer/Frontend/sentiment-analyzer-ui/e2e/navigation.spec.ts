import { test, expect } from '@playwright/test';
import { mockAllApis } from './helpers/api-mocks';

/** Helper: skip test when viewport is narrower than md breakpoint (768px) */
function skipOnMobile(page: import('@playwright/test').Page) {
  const vp = page.viewportSize();
  test.skip(!!vp && vp.width < 768, 'Desktop nav links hidden on mobile');
}

test.describe('Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
  });

  test('should load landing page at root', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('h1')).toBeVisible();
  });

  test('should load sentiment analyzer at /sentiment', async ({ page }) => {
    await page.goto('/sentiment');
    await expect(page.locator('h1')).toContainText('AI Sentiment Analyzer');
  });

  test('should navigate to insurance analysis page', async ({ page }) => {
    skipOnMobile(page);
    await page.goto('/');
    await page.click('a[href="/insurance"]');
    await expect(page.locator('h1')).toContainText('Insurance Sentiment Analysis');
  });

  test('should navigate to dashboard page', async ({ page }) => {
    skipOnMobile(page);
    await page.goto('/');
    // Dashboard is now a dropdown menu; hover to open, then click "Overview"
    const dashButton = page.locator('.hidden.md\\:flex >> button:has-text("Dashboard")');
    await dashButton.hover();
    await page.getByText('Overview').first().click();
    await expect(page.locator('h1')).toContainText('Analytics Dashboard');
  });

  test('should navigate to login page', async ({ page }) => {
    await page.goto('/login');
    await expect(page.locator('h1')).toContainText('Welcome back');
  });

  test('should highlight active nav link', async ({ page }) => {
    skipOnMobile(page);
    await page.goto('/insurance');
    // /insurance is under the Analyze dropdown; the dropdown button gets nav-link-active
    const activeButton = page.locator('.hidden.md\\:flex >> button.nav-link-active:has-text("Analyze")');
    await expect(activeButton).toBeVisible();
  });

  test('should show logo that links to home', async ({ page }) => {
    await page.goto('/insurance');
    const logo = page.locator('a[href="/"]').first();
    await expect(logo).toBeVisible();
    await logo.click();
    await expect(page).toHaveURL('/');
  });

  test('should redirect unknown routes to home', async ({ page }) => {
    await page.goto('/nonexistent-page');
    await expect(page).toHaveURL('/');
  });

  test('should show all nav links when auth is disabled', async ({ page }) => {
    skipOnMobile(page);
    await page.goto('/');
    // Nav uses dropdown buttons: Analyze, Claims, Workspace, Dashboard
    await expect(page.locator('.hidden.md\\:flex >> button:has-text("Analyze")')).toBeVisible();
    await expect(page.locator('.hidden.md\\:flex >> button:has-text("Claims")')).toBeVisible();
    await expect(page.locator('.hidden.md\\:flex >> button:has-text("Workspace")')).toBeVisible();
    await expect(page.locator('.hidden.md\\:flex >> button:has-text("Dashboard")')).toBeVisible();
  });

  test('should navigate to claims triage via dropdown menu', async ({ page }) => {
    skipOnMobile(page);
    await page.goto('/');
    // Hover over Claims dropdown to open it
    const claimsButton = page.locator('.hidden.md\\:flex >> button:has-text("Claims")');
    await claimsButton.hover();
    // Click New Triage link in dropdown
    await page.getByText('New Triage').first().click();
    await expect(page).toHaveURL('/claims/triage');
    await expect(page.locator('h1')).toContainText('Claims Triage');
  });

  test('should navigate to claims history via dropdown menu', async ({ page }) => {
    skipOnMobile(page);
    await page.goto('/');
    const claimsButton = page.locator('.hidden.md\\:flex >> button:has-text("Claims")');
    await claimsButton.hover();
    await page.getByText('History').first().click();
    await expect(page).toHaveURL('/claims/history');
    await expect(page.locator('h1')).toContainText('Claims History');
  });

  test('should navigate to provider health via dashboard dropdown', async ({ page }) => {
    skipOnMobile(page);
    await page.goto('/');
    const dashButton = page.locator('.hidden.md\\:flex >> button:has-text("Dashboard")');
    await dashButton.hover();
    await page.getByText('Providers').first().click();
    await expect(page).toHaveURL('/dashboard/providers');
    await expect(page.locator('h1')).toContainText('AI Provider Health');
  });

  test('should navigate to fraud alerts via dashboard dropdown', async ({ page }) => {
    skipOnMobile(page);
    await page.goto('/');
    const dashButton = page.locator('.hidden.md\\:flex >> button:has-text("Dashboard")');
    await dashButton.hover();
    await page.getByText('Fraud Alerts').first().click();
    await expect(page).toHaveURL('/dashboard/fraud');
    await expect(page.locator('h1')).toContainText('Fraud Alerts');
  });
});

test.describe('Mobile Navigation', () => {
  test.use({ viewport: { width: 375, height: 667 } });

  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
  });

  test('should show hamburger menu on mobile', async ({ page }) => {
    await page.goto('/');
    // Desktop nav links should be hidden
    const desktopNav = page.locator('.hidden.md\\:flex >> text=Sentiment v1');
    await expect(desktopNav).toBeHidden();
    // Hamburger button should be visible
    const hamburger = page.locator('button.md\\:hidden');
    await expect(hamburger).toBeVisible();
  });

  test('should open and close mobile menu', async ({ page }) => {
    await page.goto('/');
    const hamburger = page.getByLabel('Toggle navigation menu');
    await hamburger.click();
    // Mobile menu should appear with nav links (link text is "Insurance", not "Insurance Analysis")
    const insuranceLink = page.locator('.md\\:hidden >> text=Insurance').first();
    await expect(insuranceLink).toBeVisible();
    // Click hamburger again to close
    await hamburger.click();
    await expect(insuranceLink).toBeHidden();
  });

  test('should close mobile menu after navigation', async ({ page }) => {
    await page.goto('/');
    const hamburger = page.locator('button.md\\:hidden');
    await hamburger.click();
    const insuranceLink = page.locator('.md\\:hidden >> text=Insurance');
    await insuranceLink.click();
    await expect(page).toHaveURL('/insurance');
    // Menu should be closed after navigation
    await expect(insuranceLink).toBeHidden();
  });

  test('should navigate to claims triage via mobile menu', async ({ page }) => {
    await page.goto('/');
    const hamburger = page.locator('button.md\\:hidden');
    await hamburger.click();
    const triageLink = page.locator('.md\\:hidden >> text=New Triage');
    await expect(triageLink).toBeVisible();
    await triageLink.click();
    await expect(page).toHaveURL('/claims/triage');
  });

  test('should navigate to provider health via mobile menu', async ({ page }) => {
    await page.goto('/');
    const hamburger = page.locator('button.md\\:hidden');
    await hamburger.click();
    const providersLink = page.locator('.md\\:hidden >> text=Providers');
    await expect(providersLink).toBeVisible();
    await providersLink.click();
    await expect(page).toHaveURL('/dashboard/providers');
  });

  test('should navigate to fraud alerts via mobile menu', async ({ page }) => {
    await page.goto('/');
    const hamburger = page.locator('button.md\\:hidden');
    await hamburger.click();
    const fraudLink = page.locator('.md\\:hidden >> text=Fraud Alerts');
    await expect(fraudLink).toBeVisible();
    await fraudLink.click();
    await expect(page).toHaveURL('/dashboard/fraud');
  });
});
