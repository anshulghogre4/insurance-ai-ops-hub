import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';

test.describe('Provider Health Monitor (Extended)', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/dashboard/providers');
  });

  test('should show page header', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('AI Provider Health');
  });

  test('should have a refresh button', async ({ page }) => {
    await expect(page.getByLabel('Refresh health status')).toBeVisible();
  });

  // ==================== LLM Providers (expanded by default) ====================

  test('should display LLM Providers section expanded by default with 7 providers', async ({ page }) => {
    await expect(page.getByText('LLM Providers (7)')).toBeVisible({ timeout: 10_000 });

    // Verify LLM fallback chain is visible
    await expect(page.getByText('LLM Fallback Chain')).toBeVisible();

    // Verify each provider name in cards
    await expect(page.getByText('Groq').first()).toBeVisible();
    await expect(page.getByText('Cerebras').first()).toBeVisible();
    await expect(page.getByText('Mistral').first()).toBeVisible();
    await expect(page.getByText('Gemini').first()).toBeVisible();
    await expect(page.getByText('OpenRouter').first()).toBeVisible();
    await expect(page.getByText('OpenAI').first()).toBeVisible();
    await expect(page.getByText('Ollama').first()).toBeVisible();
  });

  test('should show Healthy status with green indicator for healthy LLM providers', async ({ page }) => {
    await expect(page.getByText('LLM Providers (7)')).toBeVisible({ timeout: 10_000 });

    const groqCard = page.locator('.glass-card').filter({ hasText: 'Groq' }).first();
    await expect(groqCard.getByText('Healthy')).toBeVisible();
    await expect(groqCard.getByText('Yes').first()).toBeVisible();
  });

  test('should show Down status for unavailable LLM providers', async ({ page }) => {
    await expect(page.getByText('LLM Providers (7)')).toBeVisible({ timeout: 10_000 });

    const openRouterCard = page.locator('.glass-card').filter({ hasText: 'OpenRouter' }).first();
    await expect(openRouterCard.getByText('Down', { exact: true })).toBeVisible();
    await expect(openRouterCard.getByText('No').first()).toBeVisible();
    await expect(openRouterCard.getByText('5')).toBeVisible();
  });

  // ==================== Embedding Providers ====================

  test('should display Embedding Providers section with 6 providers after expanding', async ({ page }) => {
    const header = page.getByLabel('Toggle Embedding Providers section');
    await expect(header).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Embedding Providers (6)')).toBeVisible();

    // Expand section
    await header.click();

    // Verify fallback chain
    await expect(page.getByText('Voyage AI').first()).toBeVisible();
    await expect(page.getByText('Cohere').first()).toBeVisible();

    // Verify chain order badges
    const voyageCard = page.locator('.glass-card').filter({ hasText: 'Voyage AI' }).first();
    await expect(voyageCard.getByText('#1')).toBeVisible();
    await expect(voyageCard.getByText('50M tokens')).toBeVisible();
  });

  // ==================== OCR Providers ====================

  test('should display OCR Providers section with 6 providers after expanding', async ({ page }) => {
    const header = page.getByLabel('Toggle OCR Providers section');
    await expect(header).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('OCR Providers (6)')).toBeVisible();

    // Expand section
    await header.click();

    // Verify chain visualization
    await expect(page.getByText('PdfPig (Local)').first()).toBeVisible();

    // Verify free tier limits on cards
    const pdfPigCard = page.locator('.glass-card').filter({ hasText: 'PdfPig (Local)' }).first();
    await expect(pdfPigCard.getByText('#1')).toBeVisible();
    await expect(pdfPigCard.getByText('Unlimited (local)')).toBeVisible();
  });

  // ==================== NER Providers ====================

  test('should display NER Providers section with 2 providers after expanding', async ({ page }) => {
    const header = page.getByLabel('Toggle NER Providers section');
    await expect(header).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('NER Providers (2)')).toBeVisible();

    await header.click();

    await expect(page.getByText('HuggingFace BERT').first()).toBeVisible();
    await expect(page.getByText('Azure AI Language').first()).toBeVisible();
  });

  // ==================== STT Providers ====================

  test('should display STT Providers section with 2 providers after expanding', async ({ page }) => {
    const header = page.getByLabel('Toggle STT Providers section');
    await expect(header).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('STT Providers (2)')).toBeVisible();

    await header.click();

    await expect(page.getByText('Deepgram').first()).toBeVisible();
    await expect(page.getByText('Azure AI Speech').first()).toBeVisible();

    // Verify not configured badge
    const azureSpeechCard = page.locator('.glass-card').filter({ hasText: 'Azure AI Speech' }).first();
    await expect(azureSpeechCard.getByText('No').first()).toBeVisible();
  });

  // ==================== Content Safety ====================

  test('should display Content Safety section with 1 service after expanding', async ({ page }) => {
    const header = page.getByLabel('Toggle Content Safety section');
    await expect(header).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Content Safety (1)')).toBeVisible();

    await header.click();

    await expect(page.getByText('Azure AI Content Safety').first()).toBeVisible();

    const card = page.locator('.glass-card').filter({ hasText: 'Azure AI Content Safety' }).first();
    await expect(card.getByText('Configured', { exact: true })).toBeVisible();
    await expect(card.getByText('Available')).toBeVisible();
  });

  // ==================== Translation ====================

  test('should display Translation section with 1 service after expanding', async ({ page }) => {
    const header = page.getByLabel('Toggle Translation section');
    await expect(header).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Translation (1)')).toBeVisible();

    await header.click();

    await expect(page.getByText('Azure AI Translator').first()).toBeVisible();
  });

  // ==================== Collapse/Expand ====================

  test('should toggle section collapse when clicking header', async ({ page }) => {
    await expect(page.getByText('LLM Providers (7)')).toBeVisible({ timeout: 10_000 });

    // LLM is expanded by default - verify chain is visible
    await expect(page.getByText('LLM Fallback Chain')).toBeVisible();

    // Collapse LLM
    await page.getByLabel('Toggle LLM Providers section').click();
    await expect(page.getByText('LLM Fallback Chain')).not.toBeVisible();

    // Re-expand LLM
    await page.getByLabel('Toggle LLM Providers section').click();
    await expect(page.getByText('LLM Fallback Chain')).toBeVisible();
  });

  // ==================== Error State ====================

  test('should show error banner when API fails', async ({ page }) => {
    // Create a new page with error mock
    await mockApiError(page, '**/api/insurance/health/providers/extended*', 500);
    await page.goto('/dashboard/providers');

    await expect(page.getByText('Failed to load provider health')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByLabel('Retry loading health status')).toBeVisible();
  });

  // ==================== Navigation ====================

  test('should have back to dashboard link', async ({ page }) => {
    const backLink = page.getByRole('link', { name: 'Back to Dashboard' });
    await expect(backLink).toBeVisible();
    await expect(backLink).toHaveAttribute('href', '/dashboard');
  });
});
