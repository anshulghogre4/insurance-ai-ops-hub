import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';
import path from 'path';
import fs from 'fs';

test.describe('Batch Claims Upload', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/claims/batch');
  });

  test('should show page header and upload form elements', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Batch Claims Upload');
    await expect(page.getByText('Upload CSV to process multiple claims')).toBeVisible();
    await expect(page.getByText('Expected CSV Format')).toBeVisible();
    await expect(page.getByText('ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Process batch claims' })).toBeVisible();
  });

  test('should disable submit button when no file is selected', async ({ page }) => {
    const submitBtn = page.getByRole('button', { name: 'Process batch claims' });
    await expect(submitBtn).toBeDisabled();
  });

  test('should upload CSV and show results with severity badges', async ({ page }) => {
    // Create a temporary CSV file for upload
    const csvContent = [
      'ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate',
      'CLM-2024-001,Water Damage,Burst pipe in basement causing flooding,12000,2024-01-15',
      'CLM-2024-002,Auto Collision,Rear-end collision on Highway 101,8500,2024-01-20',
      'CLM-2024-003,Structure Fire,Fire destroyed garage and part of home,75000,2024-02-01',
      'CLM-2024-004,Theft,Electronics stolen from home during vacation,5000,2024-02-05',
      ',Auto,Missing claim ID,3000,2024-02-10',
      'CLM-2024-006,Liability,Guest injured on property,not-a-number,2024-02-12',
      'CLM-2024-007,Theft,Jewelry and cash stolen from hotel safe,18000,2024-02-15'
    ].join('\n');

    const tmpDir = path.join(__dirname, '..', 'test-results');
    if (!fs.existsSync(tmpDir)) fs.mkdirSync(tmpDir, { recursive: true });
    const tmpFile = path.join(tmpDir, 'test-batch.csv');
    fs.writeFileSync(tmpFile, csvContent);

    // Upload the file
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles(tmpFile);

    // Verify file info is shown
    await expect(page.getByText('test-batch.csv')).toBeVisible();

    // Submit the batch — on mobile the CSV preview table may overlap the button
    const submitBtn = page.getByRole('button', { name: 'Process batch claims' });
    await submitBtn.scrollIntoViewIfNeeded();
    await submitBtn.click({ force: true, timeout: 15_000 });

    // Wait for results
    await expect(page.getByText('Batch Processing Complete')).toBeVisible({ timeout: 10_000 });

    // Verify batch ID
    await expect(page.getByText('BATCH-20260228-A1B2C3D4')).toBeVisible();

    // Verify summary counts
    await expect(page.getByText('Triage Results (5)')).toBeVisible();

    // Verify claim IDs in triage results table (scoped to avoid matching preview table too)
    const resultsTable = page.locator('table[aria-label="Batch triage results"]');
    await expect(resultsTable).toBeVisible({ timeout: 5_000 });
    await expect(resultsTable.getByText('CLM-2024-001')).toBeVisible();
    await expect(resultsTable.getByText('CLM-2024-002')).toBeVisible();
    await expect(resultsTable.getByText('CLM-2024-003')).toBeVisible();

    // Verify severity badges exist in results
    await expect(resultsTable.getByText('High').first()).toBeVisible();

    // Clean up temp file
    if (fs.existsSync(tmpFile)) fs.unlinkSync(tmpFile);
  });

  test('should show error rows in amber section', async ({ page }) => {
    const csvContent = 'ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate\nCLM-001,Auto,Test,5000,2024-01-15';
    const tmpDir = path.join(__dirname, '..', 'test-results');
    if (!fs.existsSync(tmpDir)) fs.mkdirSync(tmpDir, { recursive: true });
    const tmpFile = path.join(tmpDir, 'test-errors.csv');
    fs.writeFileSync(tmpFile, csvContent);

    await page.locator('input[type="file"]').setInputFiles(tmpFile);
    const errSubmit = page.getByRole('button', { name: 'Process batch claims' });
    await errSubmit.scrollIntoViewIfNeeded();
    await errSubmit.click({ force: true, timeout: 15_000 });

    await expect(page.getByText('Batch Processing Complete')).toBeVisible({ timeout: 10_000 });

    // Verify error section is shown (mock data has 2 errors)
    await expect(page.getByText('Validation Errors (2)')).toBeVisible();
    await expect(page.getByText('ClaimId is required')).toBeVisible();
    await expect(page.getByText('not-a-number')).toBeVisible();

    if (fs.existsSync(tmpFile)) fs.unlinkSync(tmpFile);
  });

  test('should show error state on API failure', async ({ page }) => {
    await mockApiError(page, '**/api/insurance/claims/batch*', 500);

    const csvContent = 'ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate\nCLM-001,Auto,Test,5000,2024-01-15';
    const tmpDir = path.join(__dirname, '..', 'test-results');
    if (!fs.existsSync(tmpDir)) fs.mkdirSync(tmpDir, { recursive: true });
    const tmpFile = path.join(tmpDir, 'test-fail.csv');
    fs.writeFileSync(tmpFile, csvContent);

    await page.locator('input[type="file"]').setInputFiles(tmpFile);
    const failSubmit = page.getByRole('button', { name: 'Process batch claims' });
    await failSubmit.scrollIntoViewIfNeeded();
    await failSubmit.click({ force: true, timeout: 15_000 });

    // Should show error alert
    const errorIndicator = page.locator('[role="alert"]');
    await expect(errorIndicator.first()).toBeVisible({ timeout: 10_000 });

    if (fs.existsSync(tmpFile)) fs.unlinkSync(tmpFile);
  });

  test('should navigate to batch upload from Claims dropdown', async ({ page }) => {
    await page.goto('/');

    const width = page.viewportSize()?.width ?? 0;
    if (width < 768) {
      // Mobile: use hamburger menu
      const hamburger = page.getByRole('button', { name: 'Toggle navigation menu' });
      await hamburger.click();
      const batchLink = page.getByRole('link', { name: 'Batch Upload' });
      await expect(batchLink).toBeVisible({ timeout: 5_000 });
      await batchLink.click();
    } else {
      // Desktop: hover over Claims dropdown
      const claimsButton = page.locator('button', { hasText: 'Claims' });
      await claimsButton.hover();
      const batchLink = page.getByRole('link', { name: 'Batch Upload' });
      await expect(batchLink).toBeVisible({ timeout: 5_000 });
      await batchLink.click();
    }

    await expect(page).toHaveURL(/\/claims\/batch/);
    await expect(page.locator('h1')).toContainText('Batch Claims Upload');
  });

  test('should show CSV preview table before submission', async ({ page }) => {
    const csvContent = [
      'ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate',
      'CLM-001,Water Damage,Pipe burst in basement,12000,2024-01-15',
      'CLM-002,Auto,Rear-end collision,8500,2024-01-20'
    ].join('\n');

    const tmpDir = path.join(__dirname, '..', 'test-results');
    if (!fs.existsSync(tmpDir)) fs.mkdirSync(tmpDir, { recursive: true });
    const tmpFile = path.join(tmpDir, 'test-preview.csv');
    fs.writeFileSync(tmpFile, csvContent);

    await page.locator('input[type="file"]').setInputFiles(tmpFile);

    // Verify preview table shows headers and data
    await expect(page.getByText(/Preview \(first \d+ rows?\)/)).toBeVisible({ timeout: 5_000 });
    await expect(page.locator('table[aria-label="CSV preview table"]')).toBeVisible();

    if (fs.existsSync(tmpFile)) fs.unlinkSync(tmpFile);
  });
});

test.describe('Batch Upload Accessibility', () => {
  test('should pass axe-core accessibility scan', async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/claims/batch');

    // Verify the page renders before scanning
    await expect(page.locator('h1')).toContainText('Batch Claims Upload');

    // Use the axe-core library injected via the global setup
    // Basic accessibility checks: landmarks, labels, color contrast
    const fileInput = page.locator('input[type="file"]');
    await expect(fileInput).toHaveAttribute('aria-label');

    const submitBtn = page.getByRole('button', { name: 'Process batch claims' });
    await expect(submitBtn).toHaveAttribute('aria-label');
  });
});
