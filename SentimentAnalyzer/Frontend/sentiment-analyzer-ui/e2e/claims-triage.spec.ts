import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';
import { CLAIMS_TEST_TEXTS } from './fixtures/mock-data';

test.describe('Claims Triage', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/claims/triage');
  });

  test('should show page header and form elements', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Claims Triage');
    await expect(page.getByText('AI-powered severity assessment')).toBeVisible();
    await expect(page.locator('textarea')).toBeVisible();
    await expect(page.locator('#interactionType')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Submit claim for triage' })).toBeVisible();
  });

  test('should show quick template buttons that populate textarea', async ({ page }) => {
    await expect(page.getByRole('button', { name: /Water Damage/ })).toBeVisible();
    await expect(page.getByRole('button', { name: /Auto Accident/ })).toBeVisible();
    await expect(page.getByRole('button', { name: /Theft Report/ })).toBeVisible();
    await expect(page.getByRole('button', { name: /Liability Claim/ })).toBeVisible();

    // Click a template and verify textarea is populated
    await page.getByRole('button', { name: /Water Damage/ }).click();
    const textarea = page.locator('textarea');
    await expect(textarea).not.toHaveValue('');
    const value = await textarea.inputValue();
    expect(value.length).toBeGreaterThan(50);
    expect(value.toLowerCase()).toContain('water damage');
  });

  test('should disable submit button when textarea is empty', async ({ page }) => {
    const submitBtn = page.getByRole('button', { name: 'Submit claim for triage' });
    await expect(submitBtn).toBeDisabled();
  });

  test('should submit triage and show result with severity, urgency, fraud score, and actions', async ({ page }) => {
    await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
    await page.getByRole('button', { name: 'Submit claim for triage' }).click();

    // Wait for results
    await expect(page.getByText('Triage Complete')).toBeVisible({ timeout: 10_000 });

    // Severity badge
    await expect(page.getByText('Severity').first()).toBeVisible();
    await expect(page.getByText('High').first()).toBeVisible();

    // Urgency badge
    await expect(page.getByText('Urgency').first()).toBeVisible();
    await expect(page.getByText('Immediate').first()).toBeVisible();

    // Claim type
    await expect(page.getByText('Water Damage').first()).toBeVisible();

    // Fraud Risk Score gauge
    await expect(page.getByText('Fraud Risk Score')).toBeVisible();
    await expect(page.getByText('48').first()).toBeVisible();
    await expect(page.getByText('Medium').first()).toBeVisible();

    // Recommended actions
    await expect(page.getByText('Recommended Actions (3)')).toBeVisible();
    await expect(page.getByText('Assign field adjuster within 24 hours')).toBeVisible();
    await expect(page.getByText('Contact policyholder for additional photos')).toBeVisible();
    await expect(page.getByText('Schedule emergency mitigation')).toBeVisible();
  });

  test('should show error state on API failure', async ({ page }) => {
    await mockApiError(page, '**/api/insurance/claims/triage', 500);
    await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
    await page.getByRole('button', { name: 'Submit claim for triage' }).click();

    const errorBanner = page.locator('[role="alert"]');
    await expect(errorBanner).toBeVisible({ timeout: 10_000 });
  });

  test('should show error on 429 rate limit', async ({ page }) => {
    await page.route('**/api/insurance/claims/triage', route => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 429,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Rate limit exceeded. Free tier quota reached.' }),
        });
      }
      return route.continue();
    });

    await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
    await page.getByRole('button', { name: 'Submit claim for triage' }).click();

    const errorBanner = page.locator('[role="alert"]');
    await expect(errorBanner).toBeVisible({ timeout: 10_000 });
    await expect(errorBanner).toContainText('Rate limit reached');
  });

  test('should show error on 503 all providers down', async ({ page }) => {
    await page.route('**/api/insurance/claims/triage', route => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 503,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Service temporarily unavailable. All AI providers are down.' }),
        });
      }
      return route.continue();
    });

    await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
    await page.getByRole('button', { name: 'Submit claim for triage' }).click();

    const errorBanner = page.locator('[role="alert"]');
    await expect(errorBanner).toBeVisible({ timeout: 10_000 });
    await expect(errorBanner).toContainText('All AI services are currently down');
  });

  test('should show character count and Ctrl+Enter hint', async ({ page }) => {
    await expect(page.getByText('/ 10,000')).toBeVisible();
    await expect(page.getByText('Ctrl+Enter to submit')).toBeVisible();
  });

  test('should show estimated loss range in results', async ({ page }) => {
    await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
    await page.getByRole('button', { name: 'Submit claim for triage' }).click();
    await expect(page.getByText('Triage Complete')).toBeVisible({ timeout: 10_000 });

    await expect(page.getByText('Estimated Loss:')).toBeVisible();
    await expect(page.getByText('$5,000 - $15,000')).toBeVisible();
  });

  test('should show fraud flags in results', async ({ page }) => {
    await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
    await page.getByRole('button', { name: 'Submit claim for triage' }).click();
    await expect(page.getByText('Triage Complete')).toBeVisible({ timeout: 10_000 });

    await expect(page.getByText('Fraud Flags')).toBeVisible();
    await expect(page.getByText('Timing anomaly - claim filed within 30 days of policy inception')).toBeVisible();
  });

  test('should clear form with New Triage button after results', async ({ page }) => {
    await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
    await page.getByRole('button', { name: 'Submit claim for triage' }).click();
    await expect(page.getByText('Triage Complete')).toBeVisible({ timeout: 10_000 });

    // Use button role to avoid matching nav link "New Triage" elements
    await page.getByRole('button', { name: /New Triage/ }).click();
    await expect(page.getByText('Triage Complete')).toBeHidden();
    await expect(page.locator('textarea')).toHaveValue('');
  });
});
