/**
 * Screenshot capture script for README documentation.
 * Run: npx playwright test e2e/capture-screenshots.spec.ts --project=chromium
 * Output: docs/screenshots/*.png (project root)
 */
import { test, expect } from '@playwright/test';
import { mockAllApis } from './helpers/api-mocks';
import { INSURANCE_TEST_TEXTS, MOCK_CLAIM_TRIAGE_RESPONSE } from './fixtures/mock-data';

const SCREENSHOT_DIR = '../../../docs/screenshots';

test.describe('Screenshot Capture for README', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
  });

  test('01 - Landing Page', async ({ page }) => {
    await page.goto('/');
    // Wait for animations to settle
    await page.waitForTimeout(1500);
    await page.screenshot({
      path: `${SCREENSHOT_DIR}/01-landing-page.png`,
      fullPage: false,
    });
  });

  test('02 - Landing Page - Agent Orchestration Section', async ({ page }) => {
    await page.goto('/');
    await page.waitForTimeout(1000);
    // Scroll to agent orchestration section
    const agentSection = page.locator('text=Agent Orchestration').first();
    if (await agentSection.isVisible()) {
      await agentSection.scrollIntoViewIfNeeded();
      await page.waitForTimeout(800);
    }
    await page.screenshot({
      path: `${SCREENSHOT_DIR}/02-landing-agents.png`,
      fullPage: false,
    });
  });

  test('03 - Sentiment Analyzer v1', async ({ page }) => {
    await page.goto('/sentiment');
    await page.waitForTimeout(500);

    // Fill in sample text and submit to show results
    const textarea = page.locator('textarea').first();
    if (await textarea.isVisible()) {
      await textarea.fill(
        'My agent Sarah was incredibly helpful during my auto claim. She guided me through every step and the settlement was processed in just 5 business days.'
      );
      const submitBtn = page.locator('button[type="submit"], button:has-text("Analyze")').first();
      if (await submitBtn.isVisible()) {
        await submitBtn.click();
        await page.waitForTimeout(1500);
      }
    }

    await page.screenshot({
      path: `${SCREENSHOT_DIR}/03-sentiment-analyzer.png`,
      fullPage: false,
    });
  });

  test('04 - Insurance Analyzer v2', async ({ page }) => {
    await page.goto('/insurance');
    await page.waitForTimeout(500);

    // Fill sample text and submit
    const textarea = page.locator('textarea').first();
    if (await textarea.isVisible()) {
      await textarea.fill(INSURANCE_TEST_TEXTS.claimComplaint);
      const submitBtn = page.locator('button[type="submit"], button:has-text("Analyze")').first();
      if (await submitBtn.isVisible()) {
        await submitBtn.click();
        await page.waitForTimeout(2000);
      }
    }

    await page.screenshot({
      path: `${SCREENSHOT_DIR}/04-insurance-analyzer.png`,
      fullPage: false,
    });
  });

  test('05 - Dashboard', async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForTimeout(1500);
    await page.screenshot({
      path: `${SCREENSHOT_DIR}/05-dashboard.png`,
      fullPage: false,
    });
  });

  test('06 - Claims Triage (Form)', async ({ page }) => {
    await page.goto('/claims/triage');
    await page.waitForTimeout(500);

    // Fill in claim text to show the form in use
    const textarea = page.locator('textarea').first();
    if (await textarea.isVisible()) {
      await textarea.fill(
        'Water pipe burst in basement causing significant flooding. Damage to flooring, drywall, and personal property. Policy HO-2024-789456.'
      );
    }

    await page.screenshot({
      path: `${SCREENSHOT_DIR}/06-claims-triage-form.png`,
      fullPage: false,
    });
  });

  test('07 - Claims Triage (Results)', async ({ page }) => {
    await page.goto('/claims/triage');
    await page.waitForTimeout(500);

    // Submit claim to show results
    const textarea = page.locator('textarea').first();
    if (await textarea.isVisible()) {
      await textarea.fill(
        'Water pipe burst in basement causing significant flooding. Damage to flooring, drywall, and personal property.'
      );
      const submitBtn = page.locator('button:has-text("Triage"), button[type="submit"]').first();
      if (await submitBtn.isVisible()) {
        await submitBtn.click();
        await page.waitForTimeout(2000);
      }
    }

    await page.screenshot({
      path: `${SCREENSHOT_DIR}/07-claims-triage-result.png`,
      fullPage: false,
    });
  });

  test('08 - Claims History', async ({ page }) => {
    await page.goto('/claims/history');
    await page.waitForTimeout(1000);
    await page.screenshot({
      path: `${SCREENSHOT_DIR}/08-claims-history.png`,
      fullPage: false,
    });
  });

  test('09 - Claim Detail', async ({ page }) => {
    await page.goto('/claims/101');
    await page.waitForTimeout(1000);
    await page.screenshot({
      path: `${SCREENSHOT_DIR}/09-claim-detail.png`,
      fullPage: false,
    });
  });

  test('10 - Provider Health', async ({ page }) => {
    await page.goto('/dashboard/providers');
    await page.waitForTimeout(1000);
    await page.screenshot({
      path: `${SCREENSHOT_DIR}/10-provider-health.png`,
      fullPage: false,
    });
  });

  test('11 - Fraud Alerts', async ({ page }) => {
    await page.goto('/dashboard/fraud');
    await page.waitForTimeout(1000);
    await page.screenshot({
      path: `${SCREENSHOT_DIR}/11-fraud-alerts.png`,
      fullPage: false,
    });
  });

  test('12 - Login Page', async ({ page }) => {
    await page.goto('/login');
    await page.waitForTimeout(500);
    await page.screenshot({
      path: `${SCREENSHOT_DIR}/12-login.png`,
      fullPage: false,
    });
  });
});
