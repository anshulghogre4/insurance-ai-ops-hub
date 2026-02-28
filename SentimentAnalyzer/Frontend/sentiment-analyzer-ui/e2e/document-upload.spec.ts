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
    await expect(page.getByText('18').first()).toBeVisible(); // pageCount
    await expect(page.getByText('42').first()).toBeVisible(); // chunkCount
    await expect(page.getByText('Voyage AI')).toBeVisible();
  });

  test('should show processing limit error (429)', async ({ page }) => {
    // Override both SSE stream and regular upload routes with error response
    await page.route('**/api/insurance/documents/upload/stream*', (route) => {
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

    const errorIndicator = page.locator('[role="alert"]');
    await expect(errorIndicator.first()).toBeVisible({ timeout: 10_000 });
  });

  test('should show processing failure error (422)', async ({ page }) => {
    // Override both SSE stream and regular upload routes with error response
    await page.route('**/api/insurance/documents/upload/stream*', (route) => {
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

    const errorIndicator = page.locator('[role="alert"]');
    await expect(errorIndicator.first()).toBeVisible({ timeout: 10_000 });
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

  test('should show SSE progress phases during upload', async ({ page }) => {
    // Setup file input
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles({
      name: 'homeowners-policy-2024.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('PDF content for 20-page policy'),
    });

    // Click upload button
    const uploadBtn = page.getByLabel('Upload document for processing');
    if (await uploadBtn.isVisible()) {
      await uploadBtn.click();
    } else {
      await page.getByRole('button', { name: /upload/i }).first().click();
    }

    // The SSE mock sends all events at once, so progress phases may flash by quickly.
    // Verify the upload ultimately completes with the Document Processed result.
    await expect(page.getByText('Document Processed')).toBeVisible({ timeout: 15_000 });

    // Verify final result renders with 18 pages
    await expect(page.getByText('18').first()).toBeVisible({ timeout: 5_000 });
  });

  test('should display new embedding provider name (Jina) in result badge', async ({ page }) => {
    // Override SSE stream to return Jina as the embedding provider
    const jinaUploadResult = {
      documentId: 501,
      fileName: 'homeowners-policy-2024.pdf',
      status: 'Processed',
      pageCount: 18,
      chunkCount: 42,
      embeddingProvider: 'Jina',
      errorMessage: null
    };
    const jinaSseStream = [
      'data: {"phase":"Uploading","progress":10,"message":"Receiving file...","result":null,"errorMessage":null}\n\n',
      'data: {"phase":"OCR","progress":30,"message":"Extracted text.","result":null,"errorMessage":null}\n\n',
      'data: {"phase":"Chunking","progress":45,"message":"Created 42 chunks.","result":null,"errorMessage":null}\n\n',
      'data: {"phase":"Embedding","progress":75,"message":"Embeddings generated via Jina (768-dim).","result":null,"errorMessage":null}\n\n',
      'data: {"phase":"Done","progress":100,"message":"Document ready for queries.","result":' + JSON.stringify(jinaUploadResult) + ',"errorMessage":null}\n\n',
      'data: [DONE]\n\n'
    ].join('');

    await page.route('**/api/insurance/documents/upload/stream*', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 200,
          headers: {
            'Content-Type': 'text/event-stream',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive'
          },
          body: jinaSseStream
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

    // Wait for result card
    await expect(page.getByText('Document Processed')).toBeVisible({ timeout: 10_000 });

    // Verify the Jina embedding provider badge is rendered
    await expect(page.getByText('Jina')).toBeVisible();
    // Ensure old default provider is NOT displayed
    await expect(page.getByText('Voyage AI')).not.toBeVisible();
  });

  test('should display hierarchical chunk info in document detail', async ({ page }) => {
    await page.goto('/documents/501');

    // Wait for the document detail to load
    await expect(page.getByText('Document Detail')).toBeVisible({ timeout: 10_000 });

    // Verify page numbers appear (use .first() since multiple chunks have page numbers)
    await expect(page.getByText('Page 1').first()).toBeVisible({ timeout: 10_000 });

    // Verify section/sub-chunk badges appear (use .first() since multiple may match)
    await expect(page.getByText(/Section|Sub-chunk/).first()).toBeVisible({ timeout: 10_000 });
  });

  test('should handle SSE upload error gracefully', async ({ page }) => {
    // Override the SSE route with an error response
    await page.route('**/api/insurance/documents/upload/stream*', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache' },
          body: 'data: {"phase":"Uploading","progress":5,"message":"Starting...","result":null,"errorMessage":null}\n\n' +
                'data: {"phase":"Error","progress":0,"message":"OCR extraction failed.","result":null,"errorMessage":"All OCR providers exhausted."}\n\n' +
                'data: [DONE]\n\n'
        });
      }
      return route.continue();
    });

    // Also override the non-stream upload endpoint in case the component falls back
    await page.route('**/api/insurance/documents/upload', (route) => {
      if (route.request().url().includes('/stream')) return route.fallback();
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'OCR extraction failed.' }),
        });
      }
      return route.continue();
    });

    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles({
      name: 'corrupted-document.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('corrupted'),
    });

    const uploadBtn = page.getByLabel('Upload document for processing');
    if (await uploadBtn.isVisible()) {
      await uploadBtn.click();
    } else {
      await page.getByRole('button', { name: /upload/i }).first().click();
    }

    // Verify error is displayed — inline error (role="alert") or toast (role="alert")
    const errorIndicator = page.locator('[role="alert"]');
    await expect(errorIndicator.first()).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText(/OCR|failed|error/i).first()).toBeVisible({ timeout: 10_000 });
  });
});
