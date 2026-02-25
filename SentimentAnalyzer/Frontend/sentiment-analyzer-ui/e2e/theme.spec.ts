import { test, expect } from '@playwright/test';
import { mockAllApis } from './helpers/api-mocks';

test.describe('Theme Toggle', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/sentiment');
  });

  test('should have theme toggle button in nav', async ({ page }) => {
    const themeButton = page.locator('button[title*="mode"]');
    await expect(themeButton).toBeVisible();
  });

  test('should cycle through themes without breaking layout', async ({ page }) => {
    const themeButton = page.locator('button[title*="mode"]');

    // Click once - cycles to next theme
    await themeButton.click();
    // Page should still render correctly
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.locator('textarea')).toBeVisible();

    // Click again
    await themeButton.click();
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.locator('textarea')).toBeVisible();

    // Click third time - back to original
    await themeButton.click();
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.locator('textarea')).toBeVisible();
  });

  test('should maintain theme when navigating between pages', async ({ page }) => {
    const themeButton = page.locator('button[title*="mode"]');
    const initialTitle = await themeButton.getAttribute('title');

    // Toggle theme and wait for the title attribute to change
    await themeButton.click();
    await expect(themeButton).not.toHaveAttribute('title', initialTitle!);

    // Navigate to insurance page (use goto for mobile compatibility)
    await page.goto('/insurance');
    await expect(page.locator('h1')).toContainText('Insurance');

    // Theme should persist - get the new title after toggle for comparison
    const themeAfterNav = page.locator('button[title*="mode"]');
    await expect(themeAfterNav).not.toHaveAttribute('title', initialTitle!);
  });

  test('should not have invisible text in any theme', async ({ page }) => {
    const themeButton = page.locator('button[title*="mode"]');

    for (let i = 0; i < 3; i++) {
      // Verify heading is visible and has content
      const heading = page.locator('h1');
      await expect(heading).toBeVisible();
      const text = await heading.textContent();
      expect(text?.trim().length).toBeGreaterThan(0);

      // Toggle to next theme
      await themeButton.click();
      await page.waitForTimeout(300); // wait for theme transition
    }
  });
});
