import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';
import { INSURANCE_TEST_TEXTS } from './fixtures/mock-data';

test.describe('Sentiment Analyzer (v1)', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/');
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
    await page.click('button:has-text("Analyze Sentiment")');

    // Should show sentiment result
    await expect(page.locator('text=Sentiment Result')).toBeVisible();
    await expect(page.locator('text=Positive')).toBeVisible();

    // Should show confidence score
    await expect(page.locator('text=Confidence Score')).toBeVisible();

    // Should show explanation
    await expect(page.getByRole('heading', { name: 'Analysis' })).toBeVisible();

    // Should show emotion breakdown
    await expect(page.locator('text=Emotion Breakdown')).toBeVisible();
  });

  test('should show legacy footer in results', async ({ page }) => {
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.positiveReview);
    await page.click('button:has-text("Analyze Sentiment")');
    await expect(page.locator('text=v1.0 Legacy Engine')).toBeVisible();
  });

  test('should clear results when Clear button is clicked', async ({ page }) => {
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.positiveReview);
    await page.click('button:has-text("Analyze Sentiment")');
    await expect(page.locator('text=Sentiment Result')).toBeVisible();

    await page.click('button:has-text("Clear")');
    await expect(page.locator('text=Sentiment Result')).toBeHidden();
    const textarea = page.locator('textarea');
    await expect(textarea).toHaveValue('');
  });

  test('should show error when API fails', async ({ page }) => {
    await mockApiError(page, '**/api/sentiment/analyze', 500);
    await page.locator('textarea').fill(INSURANCE_TEST_TEXTS.positiveReview);
    await page.click('button:has-text("Analyze Sentiment")');
    // Should show an error message
    const errorBanner = page.locator('.border-l-rose-500');
    await expect(errorBanner).toBeVisible();
  });
});
