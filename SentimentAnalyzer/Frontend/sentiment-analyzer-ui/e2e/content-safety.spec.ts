import { test, expect } from '@playwright/test';
import { mockAllApis } from './helpers/api-mocks';
import { MOCK_DOCUMENT_QUERY_RESULT, MOCK_DOCUMENT_DETAIL } from './fixtures/mock-data';

test.describe('Content Safety - Document Chunks', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
  });

  test('should display green "Safe" badge on safe chunks', async ({ page }) => {
    await page.goto('/documents/501');
    await expect(page.getByText('Document Detail')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Document Chunks')).toBeVisible({ timeout: 10_000 });

    // All safe chunks should show "Safe" badge
    const safeBadges = page.getByLabel('Content safe');
    await expect(safeBadges.first()).toBeVisible();

    // We have 6 safe chunks, so there should be at least 6 Safe badges
    const safeBadgeCount = await safeBadges.count();
    expect(safeBadgeCount).toBeGreaterThanOrEqual(6);
  });

  test('should display red "Flagged" badge on flagged chunk', async ({ page }) => {
    await page.goto('/documents/501');
    await expect(page.getByText('Document Detail')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Document Chunks')).toBeVisible({ timeout: 10_000 });

    // Flagged chunk should show "Flagged: Violence, SelfHarm" (pipe-separated flags formatted with commas)
    const flaggedBadge = page.getByLabel('Content flagged');
    await expect(flaggedBadge).toBeVisible();
    await expect(flaggedBadge).toContainText('Flagged: Violence, SelfHarm');
  });

  test('should show exactly one flagged badge among all chunks', async ({ page }) => {
    await page.goto('/documents/501');
    await expect(page.getByText('Document Detail')).toBeVisible({ timeout: 10_000 });

    const flaggedBadges = page.getByLabel('Content flagged');
    await expect(flaggedBadges).toHaveCount(1);
  });
});

test.describe('Content Safety - Document Query', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
  });

  test('should NOT show safety warning when answer is safe (null answerSafety)', async ({ page }) => {
    await page.goto('/documents/query');

    await page.locator('textarea#question').fill('What is covered for water damage?');
    await page.getByLabel('Submit document query').click();

    // Wait for answer
    await expect(page.getByText('Answer')).toBeVisible({ timeout: 10_000 });

    // No safety warning should be visible since answerSafety is null
    await expect(page.getByLabel('Content safety warning')).toBeHidden();
  });

  test('should show safety warning banner when answer is flagged', async ({ page }) => {
    // Override query mock with a flagged answer
    const flaggedResult = {
      ...MOCK_DOCUMENT_QUERY_RESULT,
      answerSafety: {
        isSafe: false,
        flaggedCategories: ['Hate', 'Violence'],
        provider: 'Azure Content Safety'
      }
    };

    await page.route('**/api/insurance/documents/query*', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(flaggedResult)
        });
      }
      return route.continue();
    });

    await page.goto('/documents/query');
    await page.locator('textarea#question').fill('What is covered for water damage?');
    await page.getByLabel('Submit document query').click();

    // Wait for answer
    await expect(page.getByText('Answer')).toBeVisible({ timeout: 10_000 });

    // Safety warning should be visible
    const warning = page.getByLabel('Content safety warning');
    await expect(warning).toBeVisible();
    await expect(warning).toContainText('Content Safety Warning');
    await expect(warning).toContainText('Hate, Violence');
  });

  test('should show safety warning in inline query on document detail page', async ({ page }) => {
    // Override query mock with a flagged answer for the inline query
    const flaggedResult = {
      ...MOCK_DOCUMENT_QUERY_RESULT,
      answerSafety: {
        isSafe: false,
        flaggedCategories: ['SelfHarm'],
        provider: 'Azure Content Safety'
      }
    };

    await page.route('**/api/insurance/documents/query*', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(flaggedResult)
        });
      }
      return route.continue();
    });

    await page.goto('/documents/501');
    await expect(page.getByText('Document Detail')).toBeVisible({ timeout: 10_000 });

    // Fill and submit inline query
    await page.getByLabel('Inline document question').fill('What are the policy exclusions?');
    await page.getByLabel('Submit question').click();

    // Safety warning should appear
    const warning = page.getByLabel('Content safety warning');
    await expect(warning).toBeVisible({ timeout: 10_000 });
    await expect(warning).toContainText('Content Safety Warning');
    await expect(warning).toContainText('SelfHarm');
  });
});
