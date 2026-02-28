import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';

test.describe('Fine-Tuning Q&A Pipeline', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    // Navigate to document detail page (doc ID 501 from MOCK_DOCUMENT_DETAIL)
    await page.goto('/documents/501');
  });

  test('should show Generate Q&A button on document detail page', async ({ page }) => {
    const generateBtn = page.getByLabel('Generate QA training pairs');
    await expect(generateBtn).toBeVisible({ timeout: 10_000 });
    await expect(generateBtn).toContainText('Generate Q&A Pairs');
  });

  test('should show Fine-Tuning Training Data section header', async ({ page }) => {
    const section = page.locator('[aria-label="Fine-tuning training data"]');
    await expect(section).toBeVisible({ timeout: 10_000 });
    await expect(section).toContainText('Fine-Tuning Training Data');
  });

  test('should load existing Q&A pairs on page init', async ({ page }) => {
    // The mock returns 6 pairs from MOCK_QA_PAIRS via the getQAPairs endpoint
    await expect(page.getByText('6 Training Pairs Generated')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('What is the deductible for comprehensive coverage')).toBeVisible();
  });

  test('should click generate and render Q&A pairs', async ({ page }) => {
    // Click generate button
    const generateBtn = page.getByLabel('Generate QA training pairs');
    await expect(generateBtn).toBeVisible({ timeout: 10_000 });
    await generateBtn.click();

    // Wait for pairs to render
    await expect(page.getByText('6 Training Pairs Generated')).toBeVisible({ timeout: 10_000 });

    // Verify questions are visible
    await expect(page.getByText('What is the deductible for comprehensive coverage')).toBeVisible();
    await expect(page.getByText('How would a total loss claim be processed')).toBeVisible();
    await expect(page.getByText('What steps should a policyholder take to file a claim')).toBeVisible();
  });

  test('should show question, answer on expand, and category badge', async ({ page }) => {
    // Wait for pairs to load
    await expect(page.getByText('6 Training Pairs Generated')).toBeVisible({ timeout: 10_000 });

    // Verify question is visible
    const firstQuestion = page.getByText('What is the deductible for comprehensive coverage');
    await expect(firstQuestion).toBeVisible();

    // Click to expand and see the answer
    const firstCard = page.locator('[aria-label="Fine-tuning training data"] [role="button"]').first();
    await firstCard.click();

    // Answer should now be visible
    await expect(page.getByText('comprehensive coverage deductible is $500 per occurrence')).toBeVisible();
  });

  test('should render correct category badge styling', async ({ page }) => {
    // Wait for pairs to load
    await expect(page.getByText('6 Training Pairs Generated')).toBeVisible({ timeout: 10_000 });

    // Verify category badges exist
    const section = page.locator('[aria-label="Fine-tuning training data"]');
    await expect(section.locator('.badge').filter({ hasText: 'factual' }).first()).toBeVisible();
    await expect(section.locator('.badge').filter({ hasText: 'inferential' }).first()).toBeVisible();
    await expect(section.locator('.badge').filter({ hasText: 'procedural' }).first()).toBeVisible();
  });

  test('should show section name badges', async ({ page }) => {
    // Wait for pairs to load
    await expect(page.getByText('6 Training Pairs Generated')).toBeVisible({ timeout: 10_000 });

    const section = page.locator('[aria-label="Fine-tuning training data"]');
    await expect(section.getByText('Coverage Details').first()).toBeVisible();
    await expect(section.getByText('Claims Procedure').first()).toBeVisible();
  });

  test('should show LLM provider badge', async ({ page }) => {
    // Wait for pairs to load
    await expect(page.getByText('6 Training Pairs Generated')).toBeVisible({ timeout: 10_000 });

    const section = page.locator('[aria-label="Fine-tuning training data"]');
    await expect(section.getByText('Groq')).toBeVisible();
  });

  test('should handle Q&A generation error gracefully', async ({ page }) => {
    // Override the generate-qa endpoint with an error
    await page.route('**/api/insurance/documents/*/generate-qa*', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'All LLM providers are unavailable.' }),
        });
      }
      return route.continue();
    });

    const generateBtn = page.getByLabel('Generate QA training pairs');
    await expect(generateBtn).toBeVisible({ timeout: 10_000 });
    await generateBtn.click();

    // Error message should appear
    const errorAlert = page.locator('[aria-label="QA generation error"]');
    await expect(errorAlert).toBeVisible({ timeout: 10_000 });
  });

  test('should show confidence percentages for Q&A pairs', async ({ page }) => {
    // Wait for pairs to load
    await expect(page.getByText('6 Training Pairs Generated')).toBeVisible({ timeout: 10_000 });

    // First pair has confidence 0.95 = 95%
    const section = page.locator('[aria-label="Fine-tuning training data"]');
    await expect(section.getByText('95%').first()).toBeVisible();
  });
});
