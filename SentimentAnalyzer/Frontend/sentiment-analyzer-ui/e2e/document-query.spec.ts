import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';

test.describe('Document Query', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/documents/query');
  });

  test('should show page header and form', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Document Query');
    await expect(page.locator('textarea#question')).toBeVisible();
    await expect(page.getByLabel('Submit document query')).toBeVisible();
  });

  test('should disable submit when question is empty', async ({ page }) => {
    const submitBtn = page.getByLabel('Submit document query');
    await expect(submitBtn).toBeDisabled();
  });

  test('should show document filter dropdown', async ({ page }) => {
    const select = page.locator('#documentFilter');
    await expect(select).toBeVisible();

    // Wait for document history to load (3 items from mock)
    await expect(select.locator('option')).toHaveCount(4); // "All documents" + 3 items
    await expect(select.locator('option').nth(1)).toContainText('homeowners-policy-2024.pdf');
    await expect(select.locator('option').nth(2)).toContainText('auto-claim-CLM-2024-001.pdf');
    await expect(select.locator('option').nth(3)).toContainText('endorsement-amendment-003.pdf');
  });

  test('should submit query and show answer', async ({ page }) => {
    await page.locator('textarea#question').fill('What is covered for water damage?');
    await page.getByLabel('Submit document query').click();

    // Wait for answer to appear
    await expect(page.getByText('Answer')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('water damage caused by sudden and accidental discharge')).toBeVisible();
  });

  test('should show confidence gauge', async ({ page }) => {
    await page.locator('textarea#question').fill('What is covered for water damage?');
    await page.getByLabel('Submit document query').click();

    await expect(page.getByText('Answer')).toBeVisible({ timeout: 10_000 });

    // Confidence: 0.87 = 87%
    await expect(page.getByText('Retrieval Confidence')).toBeVisible();
    await expect(page.getByText('87%')).toBeVisible();
  });

  test('should show citations', async ({ page }) => {
    await page.locator('textarea#question').fill('What is covered for water damage?');
    await page.getByLabel('Submit document query').click();

    await expect(page.getByText('Answer')).toBeVisible({ timeout: 10_000 });

    // 2 citations
    await expect(page.getByText('Citations (2)')).toBeVisible();
    await expect(page.getByText('COVERAGE A - DWELLING')).toBeVisible();
    await expect(page.getByText('EXCLUSIONS')).toBeVisible();
  });

  test('should expand and collapse citations', async ({ page }) => {
    await page.locator('textarea#question').fill('What is covered for water damage?');
    await page.getByLabel('Submit document query').click();

    await expect(page.getByText('Citations (2)')).toBeVisible({ timeout: 10_000 });

    // Click first citation to expand
    const firstCitation = page.locator('[role="button"]').filter({ hasText: 'COVERAGE A - DWELLING' });
    await firstCitation.click();

    // Verify expanded text is visible
    await expect(page.getByText('We cover sudden and accidental discharge')).toBeVisible();

    // Click again to collapse
    await firstCitation.click();
    await expect(page.getByText('We cover sudden and accidental discharge')).toBeHidden();
  });

  test('should show LLM provider badge', async ({ page }) => {
    await page.locator('textarea#question').fill('What is covered for water damage?');
    await page.getByLabel('Submit document query').click();

    await expect(page.getByText('Answer')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Groq')).toBeVisible();
  });

  test('should show elapsed time', async ({ page }) => {
    await page.locator('textarea#question').fill('What is covered for water damage?');
    await page.getByLabel('Submit document query').click();

    await expect(page.getByText('Answer')).toBeVisible({ timeout: 10_000 });
    // elapsedMilliseconds: 1842 => 1.8s
    await expect(page.getByText('1.8s')).toBeVisible();
  });

  test('should handle API error (503)', async ({ page }) => {
    await page.route('**/api/insurance/documents/query', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 503,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Service temporarily unavailable. All AI providers are down.' }),
        });
      }
      return route.continue();
    });

    await page.locator('textarea#question').fill('What is covered for water damage?');
    await page.getByLabel('Submit document query').click();

    const errorBanner = page.locator('[role="alert"]');
    await expect(errorBanner).toBeVisible({ timeout: 10_000 });
  });
});
