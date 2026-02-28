import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { mockAllApis, mockApiError } from './helpers/api-mocks';
import { CLAIMS_TEST_TEXTS } from './fixtures/mock-data';

const AXE_EXCLUDE_RULES = ['color-contrast'];

test.describe('Micro-Interactions & Animations', () => {

  test.describe('Nav Dropdown Animations', () => {
    test.beforeEach(async ({ page }) => {
      await mockAllApis(page);
      await page.goto('/dashboard');
    });

    test('should apply animate-dropdown-enter class when nav dropdown opens', async ({ page }) => {
      // Hover over the Dashboard dropdown to trigger it
      const dashButton = page.getByRole('button', { name: /Dashboard/ });
      await dashButton.hover();

      // The dropdown inner div should have the animate-dropdown-enter class
      const dropdown = page.locator('.animate-dropdown-enter');
      await expect(dropdown.first()).toBeVisible({ timeout: 3_000 });
    });

    test('should apply animate-dropdown-enter to Analyze dropdown', async ({ page }) => {
      const analyzeButton = page.getByRole('button', { name: /Analyze/ });
      await analyzeButton.hover();

      const dropdown = page.locator('.animate-dropdown-enter');
      await expect(dropdown.first()).toBeVisible({ timeout: 3_000 });
    });

    test('should apply animate-dropdown-enter to Claims dropdown', async ({ page }) => {
      const claimsButton = page.getByRole('button', { name: /Claims/ });
      await claimsButton.hover();

      const dropdown = page.locator('.animate-dropdown-enter');
      await expect(dropdown.first()).toBeVisible({ timeout: 3_000 });
    });

    test('should apply animate-dropdown-enter to Workspace dropdown', async ({ page }) => {
      const workspaceButton = page.getByRole('button', { name: /Workspace/ });
      await workspaceButton.hover();

      const dropdown = page.locator('.animate-dropdown-enter');
      await expect(dropdown.first()).toBeVisible({ timeout: 3_000 });
    });
  });

  test.describe('Mobile Menu Animation', () => {
    test('should apply mobile-menu-enter class on mobile menu open', async ({ page }) => {
      // Use mobile viewport
      await page.setViewportSize({ width: 375, height: 667 });
      await mockAllApis(page);
      await page.goto('/dashboard');

      // Click the hamburger menu
      const hamburger = page.getByRole('button', { name: 'Toggle navigation menu' });
      await hamburger.click();

      // Mobile menu should have the mobile-menu-enter class
      const mobileMenu = page.locator('.mobile-menu-enter');
      await expect(mobileMenu).toBeVisible({ timeout: 3_000 });
    });
  });

  test.describe('Claims Triage Submit Button', () => {
    test.beforeEach(async ({ page }) => {
      await mockAllApis(page);
      await page.goto('/claims/triage');
    });

    test('should show "Analyzing..." loading state during submission', async ({ page }) => {
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);

      // Submit
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      // Should show "Analyzing..." text during loading
      await expect(page.getByText('Analyzing...')).toBeVisible({ timeout: 5_000 });
    });

    test('should show "Complete" briefly after successful triage', async ({ page }) => {
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      // Wait for completion
      await expect(page.getByText('Triage Complete')).toBeVisible({ timeout: 10_000 });

      // "Complete" text should appear on the button briefly
      await expect(page.getByText('Complete')).toBeVisible({ timeout: 3_000 });
    });

    test('should have btn-spring class on submit button for press effect', async ({ page }) => {
      const submitBtn = page.getByRole('button', { name: 'Submit claim for triage' });
      await expect(submitBtn).toHaveClass(/btn-spring/);
    });

    test('should apply animate-result-slide-up class to results section', async ({ page }) => {
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      // Wait for results
      await expect(page.getByText('Triage Complete')).toBeVisible({ timeout: 10_000 });

      // Results container should have animate-result-slide-up class
      const resultsSection = page.locator('.animate-result-slide-up');
      await expect(resultsSection).toBeVisible();
    });

    test('should apply fraud-meter-animated class to fraud score gauge', async ({ page }) => {
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      await expect(page.getByText('Triage Complete')).toBeVisible({ timeout: 10_000 });

      // Fraud meter should have the animated class
      const fraudMeter = page.locator('.fraud-meter-animated');
      await expect(fraudMeter).toBeVisible();
    });
  });

  test.describe('Toast Progress Bar', () => {
    test.beforeEach(async ({ page }) => {
      await mockAllApis(page);
      await page.goto('/claims/triage');
    });

    test('should show progress bar inside toast notification', async ({ page }) => {
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      // Wait for success toast
      const toast = page.locator('[data-toast-type="success"]');
      await expect(toast).toBeVisible({ timeout: 10_000 });

      // Toast should contain a progress bar
      const progressBar = toast.locator('[data-testid="toast-progress"]');
      await expect(progressBar).toBeAttached();
    });

    test('should apply correct color class to progress bar for success toast', async ({ page }) => {
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      const toast = page.locator('[data-toast-type="success"]');
      await expect(toast).toBeVisible({ timeout: 10_000 });

      const progressBar = toast.locator('[data-testid="toast-progress"]');
      await expect(progressBar).toHaveClass(/toast-progress-success/);
    });

    test('should apply correct color class to progress bar for error toast', async ({ page }) => {
      await mockApiError(page, '**/api/insurance/claims/triage*', 500);
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      const toast = page.locator('[data-toast-type="error"]');
      await expect(toast).toBeVisible({ timeout: 10_000 });

      const progressBar = toast.locator('[data-testid="toast-progress"]');
      await expect(progressBar).toHaveClass(/toast-progress-error/);
    });
  });

  test.describe('Dashboard Chart Animations', () => {
    test.beforeEach(async ({ page }) => {
      await mockAllApis(page);
      await page.goto('/dashboard');
    });

    test('should render sentiment distribution chart canvas', async ({ page }) => {
      const chartCanvas = page.locator('canvas[aria-label="Sentiment distribution doughnut chart"]');
      await expect(chartCanvas).toBeVisible({ timeout: 10_000 });
    });

    test('should render customer personas chart canvas', async ({ page }) => {
      const chartCanvas = page.locator('canvas[aria-label="Customer personas horizontal bar chart"]');
      await expect(chartCanvas).toBeVisible({ timeout: 10_000 });
    });
  });

  test.describe('Card Hover Effects', () => {
    test.beforeEach(async ({ page }) => {
      await mockAllApis(page);
      await page.goto('/dashboard');
    });

    test('should have glass-card-static elements with transition styles', async ({ page }) => {
      const card = page.locator('.glass-card-static').first();
      await expect(card).toBeVisible();

      // Verify the element has the glass-card-static class (which now includes hover transitions)
      await expect(card).toHaveClass(/glass-card-static/);
    });
  });

  test.describe('Skeleton Loading', () => {
    test('should show skeleton elements during dashboard loading', async ({ page }) => {
      // Delay the API response to catch skeleton state
      await page.route('**/api/insurance/dashboard', async (route) => {
        await new Promise(r => setTimeout(r, 1000));
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            metrics: { totalAnalyses: 42, avgPurchaseIntent: 67, avgSentimentScore: 72, highRiskCount: 7 },
            sentimentDistribution: { positive: 45, negative: 20, neutral: 25, mixed: 10 },
            topPersonas: [{ name: 'Cautious Policyholder', count: 15 }]
          }),
        });
      });
      await mockAllApis(page);
      await page.goto('/dashboard');

      // Skeleton elements should appear during loading
      const skeletons = page.locator('.skeleton');
      await expect(skeletons.first()).toBeVisible({ timeout: 3_000 });
    });
  });

  test.describe('Page Transition', () => {
    test.beforeEach(async ({ page }) => {
      await mockAllApis(page);
    });

    test('should have route-transition wrapper around router-outlet content', async ({ page }) => {
      await page.goto('/dashboard');
      const transitionWrapper = page.locator('.route-transition');
      await expect(transitionWrapper).toBeVisible();
    });
  });

  test.describe('CX Copilot Thinking Dots', () => {
    test.beforeEach(async ({ page }) => {
      await mockAllApis(page);
      await page.goto('/cx/copilot');
    });

    test('should show thinking indicator before first SSE token', async ({ page }) => {
      // Type and send a message - the streaming will start and thinking dots should appear
      const messageInput = page.locator('textarea[aria-label="Chat message input"]');
      await messageInput.fill('What does my homeowners policy cover for water damage?');

      await page.getByRole('button', { name: 'Send message' }).click();

      // Thinking indicator should appear while waiting for first token
      const thinkingIndicator = page.locator('[data-testid="thinking-indicator"]');
      // It may be very brief if the mock responds quickly, so just check it was attached
      await expect(thinkingIndicator).toBeAttached({ timeout: 5_000 });
    });
  });

  test.describe('Accessibility with Animations', () => {
    test.beforeEach(async ({ page }) => {
      await mockAllApis(page);
    });

    test('dashboard page with animations should have no accessibility violations', async ({ page }) => {
      await page.goto('/dashboard');

      // Wait for content to load
      await expect(page.getByText('Analytics Dashboard')).toBeVisible({ timeout: 10_000 });

      const results = await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa'])
        .disableRules(AXE_EXCLUDE_RULES)
        .analyze();
      expect(results.violations).toEqual([]);
    });

    test('claims triage page with animations should have no accessibility violations', async ({ page }) => {
      await page.goto('/claims/triage');

      await expect(page.locator('h1')).toContainText('Claims Triage');

      const results = await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa'])
        .disableRules(AXE_EXCLUDE_RULES)
        .analyze();
      expect(results.violations).toEqual([]);
    });

    test('reduced-motion media query should be defined in styles', async ({ page }) => {
      await page.goto('/dashboard');

      // Verify that the prefers-reduced-motion CSS rule is applied
      // by checking that the skeleton class has animation property
      const hasReducedMotionRule = await page.evaluate(() => {
        const sheets = Array.from(document.styleSheets);
        for (const sheet of sheets) {
          try {
            const rules = Array.from(sheet.cssRules);
            for (const rule of rules) {
              if (rule instanceof CSSMediaRule &&
                  rule.conditionText?.includes('prefers-reduced-motion')) {
                return true;
              }
            }
          } catch {
            // Cross-origin stylesheets will throw
          }
        }
        return false;
      });
      expect(hasReducedMotionRule).toBe(true);
    });
  });
});
