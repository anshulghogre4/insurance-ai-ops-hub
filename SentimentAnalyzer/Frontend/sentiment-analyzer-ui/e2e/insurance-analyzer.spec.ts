import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';
import { INSURANCE_TEST_TEXTS } from './fixtures/mock-data';

test.describe('Insurance Analyzer (v2)', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/insurance');
  });

  test('should show page header and input area', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Insurance Sentiment Analysis');
    await expect(page.getByText('Multi-agent AI analysis')).toBeVisible();
    await expect(page.locator('textarea')).toBeVisible();
  });

  test('should show empty state with 7 dimension badges', async ({ page }) => {
    await expect(page.getByText('Ready to Analyze')).toBeVisible();
    const emptyState = page.locator('text=Ready to Analyze >> ..');
    const badges = emptyState.locator('.. >> .badge.badge-info');
    await expect(badges).toHaveCount(7);
  });

  test('should show interaction type dropdown with all types', async ({ page }) => {
    const select = page.locator('#interaction-type');
    await expect(select).toBeVisible();
    const options = select.locator('option');
    const optionTexts = await options.allTextContents();
    expect(optionTexts).toContain('General');
    expect(optionTexts).toContain('Email');
    expect(optionTexts).toContain('Complaint');
  });

  test('should show sample template buttons', async ({ page }) => {
    await expect(page.getByRole('button', { name: 'Claim Complaint' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Renewal Inquiry' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Positive Review' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Billing Dispute' })).toBeVisible();
  });

  test('should fill textarea when sample template is clicked', async ({ page }) => {
    await page.getByRole('button', { name: 'Claim Complaint' }).click();
    const textarea = page.locator('textarea');
    // Wait for Angular to update the textarea value after button click
    await expect(textarea).not.toHaveValue('');
    const value = await textarea.inputValue();
    expect(value.length).toBeGreaterThan(10);
  });

  test('should analyze insurance text and show all result cards', async ({ page }) => {
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.claimComplaint);
    await page.getByRole('main').getByRole('button', { name: 'Analyze' }).click();

    // Wait for results to appear
    await expect(page.getByText('Overall Sentiment')).toBeVisible({ timeout: 10_000 });

    // Sentiment card - use aria label for specificity
    await expect(page.getByLabel(/Sentiment is Negative/)).toBeVisible();

    // Purchase intent card
    await expect(page.getByText('Purchase Intent').first()).toBeVisible();

    // Customer persona card
    await expect(page.getByText('Customer Persona')).toBeVisible();
    await expect(page.getByText('ClaimFrustrated').first()).toBeVisible();

    // Journey stage card
    await expect(page.getByText('Journey Stage')).toBeVisible();

    // Risk indicators card
    await expect(page.getByText('Risk Indicators')).toBeVisible();

    // Explanation
    await expect(page.getByText('Analysis Explanation')).toBeVisible();

    // Emotion breakdown
    await expect(page.getByText('Emotion Breakdown')).toBeVisible();

    // Key topics
    await expect(page.getByText('Key Topics')).toBeVisible();

    // Policy recommendations
    await expect(page.getByText('Policy Recommendations')).toBeVisible();
    await expect(page.getByText('Claims Priority Service')).toBeVisible();

    // Quality footer
    await expect(page.getByText('Multi-Agent AI System')).toBeVisible();
  });

  test('should show action buttons after results', async ({ page }) => {
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.claimComplaint);
    await page.getByRole('main').getByRole('button', { name: 'Analyze' }).click();
    await expect(page.getByText('Overall Sentiment')).toBeVisible({ timeout: 10_000 });

    await expect(page.getByText('Analyze Another')).toBeVisible();
    await expect(page.getByText('View Dashboard')).toBeVisible();
  });

  test('should clear results and scroll to input', async ({ page }) => {
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.claimComplaint);
    await page.getByRole('main').getByRole('button', { name: 'Analyze' }).click();
    await expect(page.getByText('Overall Sentiment')).toBeVisible({ timeout: 10_000 });

    await page.getByText('Analyze Another').click();
    await expect(page.getByText('Ready to Analyze')).toBeVisible();
  });

  test('should show error state on API failure', async ({ page }) => {
    await mockApiError(page, '**/api/insurance/analyze', 500);
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.claimComplaint);
    await page.getByRole('main').getByRole('button', { name: 'Analyze' }).click();

    const errorBanner = page.locator('.border-l-rose-500');
    await expect(errorBanner).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('button', { name: 'Retry' })).toBeVisible();
  });

  test('should show character count and Ctrl+Enter hint', async ({ page }) => {
    await expect(page.getByText('/ 10,000 characters')).toBeVisible();
    await expect(page.getByText('Ctrl+Enter to analyze')).toBeVisible();
  });

  test('should submit analysis with Ctrl+Enter keyboard shortcut', async ({ page }) => {
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.claimComplaint);
    await page.locator('textarea').press('Control+Enter');
    await expect(page.getByText('Overall Sentiment')).toBeVisible({ timeout: 10_000 });
  });

  test('should show error state on 429 rate limit', async ({ page }) => {
    await mockApiError(page, '**/api/insurance/analyze', 429);
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.claimComplaint);
    await page.getByRole('main').getByRole('button', { name: 'Analyze' }).click();

    const errorBanner = page.locator('[role="alert"], .border-l-rose-500');
    await expect(errorBanner).toBeVisible({ timeout: 10_000 });
  });

  test('should show error state on 503 all providers down', async ({ page }) => {
    await mockApiError(page, '**/api/insurance/analyze', 503);
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.claimComplaint);
    await page.getByRole('main').getByRole('button', { name: 'Analyze' }).click();

    const errorBanner = page.locator('[role="alert"], .border-l-rose-500');
    await expect(errorBanner).toBeVisible({ timeout: 10_000 });
  });

  test('should retry analysis after error', async ({ page }) => {
    // First request fails
    await mockApiError(page, '**/api/insurance/analyze', 500);
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.claimComplaint);
    await page.getByRole('main').getByRole('button', { name: 'Analyze' }).click();
    await expect(page.getByRole('button', { name: 'Retry' })).toBeVisible({ timeout: 10_000 });

    // Re-mock with success for retry
    await mockAllApis(page);
    await page.getByRole('button', { name: 'Retry' }).click();
    await expect(page.getByText('Overall Sentiment')).toBeVisible({ timeout: 10_000 });
  });

  test('should navigate to dashboard via View Dashboard button', async ({ page }) => {
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.claimComplaint);
    await page.getByRole('main').getByRole('button', { name: 'Analyze' }).click();
    await expect(page.getByText('Overall Sentiment')).toBeVisible({ timeout: 10_000 });

    await page.getByText('View Dashboard').click();
    await expect(page).toHaveURL('/dashboard');
    await expect(page.locator('h1')).toContainText('Analytics Dashboard');
  });

  test('should change interaction type dropdown', async ({ page }) => {
    const select = page.locator('#interaction-type');
    await select.selectOption('Complaint');
    await expect(select).toHaveValue('Complaint');

    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.billingDispute);
    await page.getByRole('main').getByRole('button', { name: 'Analyze' }).click();
    await expect(page.getByText('Overall Sentiment')).toBeVisible({ timeout: 10_000 });
  });
});
