import { test, expect } from '@playwright/test';
import { mockAllApis } from './helpers/api-mocks';

test.describe('Provider Health Monitor', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/dashboard/providers');
  });

  test('should show page header', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('AI Provider Health');
  });

  test('should display 7 LLM provider cards', async ({ page }) => {
    await expect(page.getByText('LLM Providers (7)')).toBeVisible({ timeout: 10_000 });

    // Verify each provider name
    await expect(page.getByText('Groq').first()).toBeVisible();
    await expect(page.getByText('Cerebras').first()).toBeVisible();
    await expect(page.getByText('Mistral').first()).toBeVisible();
    await expect(page.getByText('Gemini').first()).toBeVisible();
    await expect(page.getByText('OpenRouter').first()).toBeVisible();
    await expect(page.getByText('OpenAI').first()).toBeVisible();
    await expect(page.getByText('Ollama').first()).toBeVisible();
  });

  test('should show Healthy status with green indicator for healthy providers', async ({ page }) => {
    await expect(page.getByText('LLM Providers (7)')).toBeVisible({ timeout: 10_000 });

    // Groq card should show Healthy status
    const groqCard = page.locator('.glass-card').filter({ hasText: 'Groq' }).first();
    await expect(groqCard.getByText('Healthy')).toBeVisible();
    await expect(groqCard.getByText('Yes')).toBeVisible(); // isAvailable
  });

  test('should show Down status for unavailable providers', async ({ page }) => {
    await expect(page.getByText('LLM Providers (7)')).toBeVisible({ timeout: 10_000 });

    // OpenRouter card should show Down status
    const openRouterCard = page.locator('.glass-card').filter({ hasText: 'OpenRouter' }).first();
    // Use exact match to avoid matching "Cooldown" which also contains "Down"
    await expect(openRouterCard.getByText('Down', { exact: true })).toBeVisible();
    await expect(openRouterCard.getByText('No')).toBeVisible(); // not available
    await expect(openRouterCard.getByText('5')).toBeVisible(); // consecutive failures
  });

  test('should display 6 multimodal service cards', async ({ page }) => {
    await expect(page.getByText('Multimodal Services (6)')).toBeVisible({ timeout: 10_000 });

    await expect(page.getByText('Deepgram STT').first()).toBeVisible();
    await expect(page.getByText('Azure Vision').first()).toBeVisible();
    await expect(page.getByText('Cloudflare Vision').first()).toBeVisible();
    await expect(page.getByText('OCR.space').first()).toBeVisible();
    await expect(page.getByText('HuggingFace NER').first()).toBeVisible();
    await expect(page.getByText('Voyage AI Embeddings').first()).toBeVisible();
  });

  test('should show fallback chain visualization', async ({ page }) => {
    await expect(page.getByText('LLM Fallback Chain')).toBeVisible({ timeout: 10_000 });

    // The fallback chain should show all provider names in order
    const chainSection = page.locator('.glass-card-static').filter({ hasText: 'LLM Fallback Chain' });
    await expect(chainSection).toBeVisible();
  });

  test('should have a refresh button', async ({ page }) => {
    await expect(page.getByLabel('Refresh health status')).toBeVisible();
  });

  test('should show configured vs not configured services', async ({ page }) => {
    await expect(page.getByText('Multimodal Services (6)')).toBeVisible({ timeout: 10_000 });

    // Configured services show "Configured" text
    const azureCard = page.locator('.glass-card').filter({ hasText: 'Azure Vision' }).first();
    // Use exact match to avoid matching "Not Configured" substring
    await expect(azureCard.getByText('Configured', { exact: true })).toBeVisible();

    // Not configured services show "Not Configured" text
    // Use .first() because both the status label and badge show "Not Configured"
    const huggingCard = page.locator('.glass-card').filter({ hasText: 'HuggingFace NER' }).first();
    await expect(huggingCard.getByText('Not Configured').first()).toBeVisible();
  });
});
