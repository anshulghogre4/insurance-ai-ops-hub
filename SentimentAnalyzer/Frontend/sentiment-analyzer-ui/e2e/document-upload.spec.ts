import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';

test.describe('Document Upload', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/documents/upload');
  });

  test('should show page header and upload form', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Document Upload');
    // Drop zone visible
    await expect(page.locator('input[type="file"]')).toBeAttached();
    // Category selector visible
    await expect(page.locator('#category')).toBeVisible();
  });

  test('should show category selector with all options', async ({ page }) => {
    const select = page.locator('#category');
    await expect(select).toBeVisible();

    const options = select.locator('option');
    await expect(options).toHaveCount(5);
    await expect(options.nth(0)).toHaveText('Policy');
    await expect(options.nth(1)).toHaveText('Claim');
    await expect(options.nth(2)).toHaveText('Endorsement');
    await expect(options.nth(3)).toHaveText('Correspondence');
    await expect(options.nth(4)).toHaveText('Other');
  });

  test('should show drag-and-drop zone with instructions', async ({ page }) => {
    await expect(page.getByText('Drop file here or')).toBeVisible();
    await expect(page.getByText('browse')).toBeVisible();
    await expect(page.getByText('PDF, PNG, JPEG, TIFF')).toBeVisible();
  });

  test('should show validation error for invalid file type', async ({ page }) => {
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles({
      name: 'invalid-file.exe',
      mimeType: 'application/octet-stream',
      buffer: Buffer.from('fake-content'),
    });

    await expect(page.getByText('Unsupported file type')).toBeVisible({ timeout: 5_000 });
  });

  test('should show successful upload result', async ({ page }) => {
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles({
      name: 'homeowners-policy.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('fake-pdf-content'),
    });

    await page.getByLabel('Upload document for processing').click();

    // Wait for result card
    await expect(page.getByText('Document Processed')).toBeVisible({ timeout: 10_000 });

    // Verify result details
    await expect(page.getByText('#501')).toBeVisible();
    await expect(page.getByText('homeowners-policy-2024.pdf')).toBeVisible();
    await expect(page.getByText('4').first()).toBeVisible(); // pageCount
    await expect(page.getByText('12').first()).toBeVisible(); // chunkCount
    await expect(page.getByText('Voyage AI')).toBeVisible();
  });

  test('should show processing limit error (429)', async ({ page }) => {
    await page.route('**/api/insurance/documents/upload*', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 429,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Document limit reached. Delete existing documents to upload more.' }),
        });
      }
      return route.continue();
    });

    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles({
      name: 'homeowners-policy.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('fake-pdf-content'),
    });

    await page.getByLabel('Upload document for processing').click();

    const errorBanner = page.locator('[role="alert"]');
    await expect(errorBanner).toBeVisible({ timeout: 10_000 });
    // Error banner should be visible (exact text depends on how the backend returns the error)
  });

  test('should show processing failure error (422)', async ({ page }) => {
    await page.route('**/api/insurance/documents/upload*', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 422,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Document processing failed. The file may be corrupted or unsupported.' }),
        });
      }
      return route.continue();
    });

    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles({
      name: 'homeowners-policy.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('fake-pdf-content'),
    });

    await page.getByLabel('Upload document for processing').click();

    const errorBanner = page.locator('[role="alert"]');
    await expect(errorBanner).toBeVisible({ timeout: 10_000 });
  });

  test('should have query action button after upload', async ({ page }) => {
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles({
      name: 'homeowners-policy.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('fake-pdf-content'),
    });

    await page.getByLabel('Upload document for processing').click();
    await expect(page.getByText('Document Processed')).toBeVisible({ timeout: 10_000 });

    await expect(page.getByRole('link', { name: /Query This Document/ })).toBeVisible();
  });

  test('should have view details action after upload', async ({ page }) => {
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles({
      name: 'homeowners-policy.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('fake-pdf-content'),
    });

    await page.getByLabel('Upload document for processing').click();
    await expect(page.getByText('Document Processed')).toBeVisible({ timeout: 10_000 });

    await expect(page.getByRole('link', { name: /View Details/ })).toBeVisible();
  });

  test('should have Upload Another button to reset after upload', async ({ page }) => {
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles({
      name: 'homeowners-policy.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('fake-pdf-content'),
    });

    await page.getByLabel('Upload document for processing').click();
    await expect(page.getByText('Document Processed')).toBeVisible({ timeout: 10_000 });

    const uploadAnotherBtn = page.getByRole('button', { name: /Upload Another/ });
    await expect(uploadAnotherBtn).toBeVisible();

    await uploadAnotherBtn.click();
    await expect(page.getByText('Document Processed')).toBeHidden();
  });
});
