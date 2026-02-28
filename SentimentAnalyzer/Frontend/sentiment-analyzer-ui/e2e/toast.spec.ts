import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';
import { CLAIMS_TEST_TEXTS } from './fixtures/mock-data';

test.describe('Toast Notification System', () => {

  test.describe('Claims Triage - Success Toast', () => {
    test.beforeEach(async ({ page }) => {
      await mockAllApis(page);
      await page.goto('/claims/triage');
    });

    test('should show success toast after successful triage submission', async ({ page }) => {
      // Fill claim text
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);

      // Submit triage
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      // Wait for triage to complete
      await expect(page.getByText('Triage Complete')).toBeVisible({ timeout: 10_000 });

      // Verify success toast appears
      const toast = page.locator('[data-toast-type="success"]');
      await expect(toast).toBeVisible({ timeout: 5_000 });
      await expect(toast).toContainText('Claim triaged successfully');
    });

    test('should auto-dismiss success toast after timeout', async ({ page }) => {
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      // Wait for toast to appear
      const toast = page.locator('[data-toast-type="success"]');
      await expect(toast).toBeVisible({ timeout: 10_000 });

      // Wait for toast to auto-dismiss (5 second timeout + buffer)
      await expect(toast).toBeHidden({ timeout: 8_000 });
    });

    test('should dismiss toast when clicking the X button', async ({ page }) => {
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      // Wait for toast to appear
      const toast = page.locator('[data-toast-type="success"]');
      await expect(toast).toBeVisible({ timeout: 10_000 });

      // Click dismiss button
      await page.locator('[data-toast-type="success"] button[aria-label="Dismiss notification"]').click();

      // Verify toast is gone
      await expect(toast).toBeHidden({ timeout: 2_000 });
    });
  });

  test.describe('Claims Triage - Error Toast', () => {
    test('should show error toast when triage API fails', async ({ page }) => {
      // Must register general mocks FIRST, then error mock AFTER
      // Playwright uses LIFO route matching — last registered route wins
      await mockAllApis(page);
      await mockApiError(page, '**/api/insurance/claims/triage*', 500);

      await page.goto('/claims/triage');

      // Fill and submit
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.autoAccident);
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      // Verify error toast appears
      const toast = page.locator('[data-toast-type="error"]');
      await expect(toast).toBeVisible({ timeout: 10_000 });
      await expect(toast).toContainText('Failed to triage claim');
    });
  });

  test.describe('Document Upload - Toasts', () => {
    test.beforeEach(async ({ page }) => {
      await mockAllApis(page);
      await page.goto('/documents/upload');
    });

    test('should show success toast after successful document upload', async ({ page }) => {
      // Create a mock file and trigger upload via the file input
      const fileInput = page.locator('input[type="file"]');

      // Upload a test file
      await fileInput.setInputFiles({
        name: 'homeowners-policy-2024.pdf',
        mimeType: 'application/pdf',
        buffer: Buffer.from('Mock PDF content for insurance policy document'),
      });

      // Click upload button
      await page.getByRole('button', { name: 'Upload document for processing' }).click();

      // Verify success toast
      const toast = page.locator('[data-toast-type="success"]');
      await expect(toast).toBeVisible({ timeout: 10_000 });
      await expect(toast).toContainText('Document uploaded and indexed');
    });
  });

  test.describe('Accessibility', () => {
    test.beforeEach(async ({ page }) => {
      await mockAllApis(page);
      await page.goto('/claims/triage');
    });

    test('should have aria-live region for screen readers', async ({ page }) => {
      // The toast container should always be present with aria-live
      const ariaLiveRegion = page.locator('[aria-live="polite"]');
      // There are multiple aria-live regions on the page (loading spinner, results area, toast container)
      // The toast container is the one that is fixed positioned
      const toastContainer = page.locator('.fixed[aria-live="polite"]');
      await expect(toastContainer).toBeAttached();
    });

    test('should have role="alert" on each toast notification', async ({ page }) => {
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      // Wait for toast
      const toast = page.locator('[data-toast-type="success"]');
      await expect(toast).toBeVisible({ timeout: 10_000 });

      // Verify role="alert"
      await expect(toast).toHaveAttribute('role', 'alert');
    });

    test('should have dismiss button with accessible label', async ({ page }) => {
      await page.locator('textarea').fill(CLAIMS_TEST_TEXTS.waterDamage);
      await page.getByRole('button', { name: 'Submit claim for triage' }).click();

      const toast = page.locator('[data-toast-type="success"]');
      await expect(toast).toBeVisible({ timeout: 10_000 });

      const dismissBtn = toast.locator('button[aria-label="Dismiss notification"]');
      await expect(dismissBtn).toBeVisible();
    });
  });
});
