import { test, expect } from '@playwright/test';
import { mockAllApis } from './helpers/api-mocks';

test.describe('Login Page', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/login');
  });

  test('should show login form with email and password fields', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Welcome back');
    await expect(page.locator('#login-email')).toBeVisible();
    await expect(page.locator('#login-password')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toContainText('Sign In');
  });

  test('should show branding panel on desktop', async ({ page }) => {
    const viewportWidth = page.viewportSize()?.width ?? 0;
    if (viewportWidth < 1024) {
      test.skip();
      return;
    }
    // Branding panel is the left side with gradient bg (hidden on mobile, visible on lg+)
    const brandingPanel = page.locator('div.from-indigo-600').first();
    await expect(brandingPanel).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('InsureSense AI').first()).toBeVisible();
    // Verify features listed in the branding panel (use .first() to avoid strict mode on partial matches)
    await expect(page.getByText('Claims Triage').first()).toBeVisible();
    await expect(page.getByText('PII-Safe Processing').first()).toBeVisible();
  });

  test('should toggle between login and register mode', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Welcome back');
    await page.getByRole('button', { name: 'Sign up for free' }).click();
    await expect(page.locator('h1')).toContainText('Create your account');
    await expect(page.locator('button[type="submit"]')).toContainText('Create Account');

    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.locator('h1')).toContainText('Welcome back');
  });

  test('should show password hint in register mode', async ({ page }) => {
    await page.getByRole('button', { name: 'Sign up for free' }).click();
    await expect(page.getByText('Minimum 6 characters required')).toBeVisible();
  });

  test('should show error for empty form submission', async ({ page }) => {
    await page.click('button[type="submit"]');
    await expect(page.locator('[role="alert"]')).toBeVisible();
    await expect(page.getByText('Please enter both email and password')).toBeVisible();
  });

  test('should toggle password visibility', async ({ page }) => {
    const passwordInput = page.locator('#login-password');
    await expect(passwordInput).toHaveAttribute('type', 'password');

    const toggleBtn = page.locator('button[aria-label="Show password"]');
    await toggleBtn.click();
    await expect(passwordInput).toHaveAttribute('type', 'text');

    const hideBtn = page.locator('button[aria-label="Hide password"]');
    await hideBtn.click();
    await expect(passwordInput).toHaveAttribute('type', 'password');
  });

  test('should show forgot password link in login mode', async ({ page }) => {
    await expect(page.getByText('Forgot password?')).toBeVisible();
  });

  test('should hide forgot password in register mode', async ({ page }) => {
    await page.getByRole('button', { name: 'Sign up for free' }).click();
    await expect(page.getByText('Forgot password?')).toBeHidden();
  });

  test('should show security footer', async ({ page }) => {
    await expect(page.getByText('enterprise-grade security')).toBeVisible();
    await expect(page.getByText('PII auto-redacted')).toBeVisible();
  });

  test('should have proper form labels and autocomplete', async ({ page }) => {
    await expect(page.locator('label[for="login-email"]')).toContainText('Email address');
    await expect(page.locator('label[for="login-password"]')).toContainText('Password');
    await expect(page.locator('#login-email')).toHaveAttribute('autocomplete', 'email');
    await expect(page.locator('#login-password')).toHaveAttribute('autocomplete', 'current-password');
  });
});
