import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';
import { INSURANCE_TEST_TEXTS } from './fixtures/mock-data';

test.describe('Sentiment Analyzer (v1)', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/sentiment');
  });

  test('should show empty state before first analysis', async ({ page }) => {
    await expect(page.locator('text=AI Sentiment Analyzer')).toBeVisible();
    await expect(page.locator('textarea')).toBeVisible();
    await expect(page.locator('text=Analyze Sentiment')).toBeVisible();
  });

  test('should show character count', async ({ page }) => {
    await expect(page.locator('text=/ 5,000 characters')).toBeVisible();
  });

  test('should disable analyze button when textarea is empty', async ({ page }) => {
    const analyzeBtn = page.locator('button:has-text("Analyze Sentiment")');
    await expect(analyzeBtn).toBeDisabled();
  });

  test('should enable analyze button when text is entered', async ({ page }) => {
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.positiveReview);
    const analyzeBtn = page.locator('button:has-text("Analyze Sentiment")');
    await expect(analyzeBtn).toBeEnabled();
  });

  test('should analyze text and show results', async ({ page }) => {
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.positiveReview);
    await page.locator('button:has-text("Analyze Sentiment")').click();

    // Should show sentiment result
    await expect(page.getByText('Sentiment Result')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Positive').first()).toBeVisible();

    // Should show confidence score
    await expect(page.getByText('Confidence Score')).toBeVisible();

    // Should show explanation - "Analysis" is an h3 text, not a heading role
    await expect(page.getByText('Analysis').first()).toBeVisible();

    // Should show emotion breakdown
    await expect(page.getByText('Emotion Breakdown')).toBeVisible();
  });

  test('should show legacy footer in results', async ({ page }) => {
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.positiveReview);
    await page.locator('button:has-text("Analyze Sentiment")').click();
    await expect(page.getByText('v1.0 Legacy Engine')).toBeVisible({ timeout: 10_000 });
  });

  test('should clear results when Clear button is clicked', async ({ page }) => {
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.positiveReview);
    await page.locator('button:has-text("Analyze Sentiment")').click();
    await expect(page.getByText('Sentiment Result')).toBeVisible({ timeout: 10_000 });

    await page.locator('button:has-text("Clear")').click();
    await expect(page.getByText('Sentiment Result')).toBeHidden();
    const textarea = page.locator('textarea');
    await expect(textarea).toHaveValue('');
  });

  test('should show error when API fails', async ({ page }) => {
    await mockApiError(page, '**/api/sentiment/analyze', 500);
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.positiveReview);
    await page.locator('button:has-text("Analyze Sentiment")').click();
    // Should show an error message
    const errorBanner = page.locator('.border-l-rose-500');
    await expect(errorBanner).toBeVisible({ timeout: 10_000 });
  });
});
