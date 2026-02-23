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

  test('should load home page with sentiment analyzer', async ({ page }) => {
    await page.goto('/');
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
    await page.click('a[href="/dashboard"]');
    await expect(page.locator('h1')).toContainText('Analytics Dashboard');
  });

  test('should navigate to login page', async ({ page }) => {
    await page.goto('/login');
    await expect(page.locator('h1')).toContainText('Welcome back');
  });

  test('should highlight active nav link', async ({ page }) => {
    skipOnMobile(page);
    await page.goto('/insurance');
    const activeLink = page.locator('a[href="/insurance"].nav-link-active');
    await expect(activeLink).toBeVisible();
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
    await expect(page.locator('text=Sentiment v1')).toBeVisible();
    await expect(page.locator('text=Insurance Analysis')).toBeVisible();
    await expect(page.locator('text=Dashboard')).toBeVisible();
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
    const hamburger = page.locator('button.md\\:hidden');
    await hamburger.click();
    // Mobile menu should appear with nav links
    const mobileMenu = page.locator('.md\\:hidden >> text=Insurance Analysis');
    await expect(mobileMenu).toBeVisible();
    // Click hamburger again to close
    await hamburger.click();
    await expect(mobileMenu).toBeHidden();
  });

  test('should close mobile menu after navigation', async ({ page }) => {
    await page.goto('/');
    const hamburger = page.locator('button.md\\:hidden');
    await hamburger.click();
    const insuranceLink = page.locator('.md\\:hidden >> text=Insurance Analysis');
    await insuranceLink.click();
    await expect(page).toHaveURL('/insurance');
    // Menu should be closed after navigation
    await expect(insuranceLink).toBeHidden();
  });
});
