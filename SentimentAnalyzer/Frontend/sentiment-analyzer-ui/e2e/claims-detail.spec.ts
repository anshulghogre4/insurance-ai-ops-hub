import { test, expect } from '@playwright/test';
import { mockAllApis } from './helpers/api-mocks';

test.describe('Claim Detail Page', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/claims/101');
  });

  test('should show claim header with claim ID', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Claim #101');
  });

  test('should display severity badge and triage assessment', async ({ page }) => {
    await expect(page.getByText('Triage Assessment')).toBeVisible({ timeout: 10_000 });

    // Severity
    await expect(page.getByText('Severity').first()).toBeVisible();
    await expect(page.getByText('High').first()).toBeVisible();

    // Urgency
    await expect(page.getByText('Urgency').first()).toBeVisible();
    await expect(page.getByText('Immediate').first()).toBeVisible();

    // Claim Type
    await expect(page.getByText('Water Damage').first()).toBeVisible();
  });

  test('should show fraud score gauge', async ({ page }) => {
    await expect(page.getByText('Triage Assessment')).toBeVisible({ timeout: 10_000 });

    await expect(page.getByText('Fraud Risk Score')).toBeVisible();
    await expect(page.getByText('42').first()).toBeVisible();
    await expect(page.getByText('/100')).toBeVisible();
    await expect(page.getByText('Low Risk')).toBeVisible();
    await expect(page.getByText('High Risk')).toBeVisible();
  });

  test('should display recommended actions list', async ({ page }) => {
    await expect(page.getByText('Recommended Actions')).toBeVisible({ timeout: 10_000 });

    await expect(page.getByText('Assign field adjuster within 24 hours')).toBeVisible();
    await expect(page.getByText('Contact policyholder for additional photos')).toBeVisible();
    await expect(page.getByText('Schedule emergency mitigation')).toBeVisible();
  });

  test('should have back to history link', async ({ page }) => {
    const backLink = page.getByLabel('Back to claims history');
    await expect(backLink).toBeVisible();
    await backLink.click();
    await expect(page).toHaveURL('/claims/history');
  });

  test('should show Run Deep Fraud Analysis button', async ({ page }) => {
    await expect(page.getByText('Triage Assessment')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByLabel('Run deep fraud analysis')).toBeVisible();
  });
});
