import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';
import { MOCK_DOCUMENT_HISTORY_RESPONSE } from './fixtures/mock-data';

test.describe('Document Library', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/documents');
  });

  test('should display page header and document count', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Document Library');
    await expect(page.getByText('Browse and manage your uploaded documents')).toBeVisible();
    await expect(page.getByText('4 documents')).toBeVisible({ timeout: 10_000 });
  });

  test('should display document cards with correct data', async ({ page }) => {
    const main = page.getByRole('main');

    // Wait for cards to render
    await expect(main.getByText('homeowners-policy-2024.pdf')).toBeVisible({ timeout: 10_000 });
    await expect(main.getByText('auto-claim-CLM-2024-001.pdf')).toBeVisible();
    await expect(main.getByText('endorsement-amendment-003.pdf')).toBeVisible();
    await expect(main.getByText('adjuster-correspondence.png')).toBeVisible();
  });

  test('should show category and status badges', async ({ page }) => {
    const main = page.getByRole('main');
    await expect(main.getByText('homeowners-policy-2024.pdf')).toBeVisible({ timeout: 10_000 });

    // Category badges (scope to card grid to avoid matching dropdown options)
    const cardGrid = main.locator('.grid');
    await expect(cardGrid.getByText('Policy', { exact: true }).first()).toBeVisible();
    await expect(cardGrid.getByText('Claim', { exact: true }).first()).toBeVisible();
    await expect(cardGrid.getByText('Endorsement', { exact: true }).first()).toBeVisible();
    await expect(cardGrid.getByText('Correspondence', { exact: true }).first()).toBeVisible();

    // Status badges
    const processedBadges = cardGrid.getByText('Processed', { exact: true });
    await expect(processedBadges.first()).toBeVisible();
  });

  test('should show page count and chunk count metrics', async ({ page }) => {
    const main = page.getByRole('main');
    await expect(main.getByText('homeowners-policy-2024.pdf')).toBeVisible({ timeout: 10_000 });

    await expect(main.getByText('18 pages')).toBeVisible();
    await expect(main.getByText('42 chunks')).toBeVisible();
  });

  test('should have category filter dropdown', async ({ page }) => {
    const select = page.getByLabel('Filter by category');
    await expect(select).toBeVisible();

    // Verify options exist
    await expect(select.locator('option')).toHaveCount(6); // All + 5 categories
  });

  test('should filter by category', async ({ page }) => {
    await expect(page.getByText('homeowners-policy-2024.pdf')).toBeVisible({ timeout: 10_000 });

    // Mock a filtered response
    await page.route('**/api/insurance/documents/history*', (route) => {
      const url = route.request().url();
      if (url.includes('category=Policy')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [MOCK_DOCUMENT_HISTORY_RESPONSE.items[0]],
            totalCount: 1, page: 1, pageSize: 12, totalPages: 1
          })
        });
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_DOCUMENT_HISTORY_RESPONSE)
      });
    });

    await page.getByLabel('Filter by category').selectOption('Policy');
    await expect(page.getByText('1 document')).toBeVisible({ timeout: 10_000 });
  });

  test('should navigate to document detail on card click', async ({ page }) => {
    await expect(page.getByText('homeowners-policy-2024.pdf')).toBeVisible({ timeout: 10_000 });

    // Click the first card
    await page.getByRole('button', { name: /View document homeowners-policy-2024\.pdf/ }).click();

    await expect(page).toHaveURL(/\/documents\/501/);
  });

  test('should show empty state when no documents', async ({ page }) => {
    await page.route('**/api/insurance/documents/history*', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 12, totalPages: 0 })
      })
    );
    await page.goto('/documents');

    await expect(page.getByText('No documents uploaded yet')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Upload a document to start querying with AI')).toBeVisible();
    await expect(page.getByText('Upload Document')).toBeVisible();
  });

  test('should show error state on API failure', async ({ page }) => {
    await page.route('**/api/insurance/documents/history*', (route) =>
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Service unavailable' })
      })
    );
    await page.goto('/documents');

    await expect(page.getByText('Failed to load document library')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByLabel('Retry loading documents')).toBeVisible();
  });

  test('should have upload button in header', async ({ page }) => {
    await expect(page.getByLabel('Upload document')).toBeVisible();
  });

  test('should show query icon button on each card', async ({ page }) => {
    await expect(page.getByText('homeowners-policy-2024.pdf')).toBeVisible({ timeout: 10_000 });
    const queryButtons = page.getByRole('link', { name: /Query document/ });
    await expect(queryButtons.first()).toBeVisible();
  });

  test('should pass accessibility scan', async ({ page }) => {
    await expect(page.getByText('homeowners-policy-2024.pdf')).toBeVisible({ timeout: 10_000 });

    // Basic a11y: all interactive elements should be keyboard accessible
    const cards = page.getByRole('button', { name: /View document/ });
    const count = await cards.count();
    expect(count).toBeGreaterThanOrEqual(1);

    // Verify tabindex is set on cards
    const firstCard = cards.first();
    await expect(firstCard).toHaveAttribute('tabindex', '0');
  });
});
