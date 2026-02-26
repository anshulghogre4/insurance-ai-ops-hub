import { test, expect } from '@playwright/test';
import { mockAllApis, mockApiError } from './helpers/api-mocks';

test.describe('CX Copilot', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/cx/copilot');
  });

  test('should show page header', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('CX Copilot');
    await expect(page.getByText('AI-powered customer experience assistant')).toBeVisible();
  });

  test('should show empty state message initially', async ({ page }) => {
    await expect(page.getByText('Start a conversation')).toBeVisible();
    await expect(page.getByText('Ask questions about insurance policies')).toBeVisible();
  });

  test('should show chat input area', async ({ page }) => {
    await expect(page.getByLabel('Chat message input')).toBeVisible();
    await expect(page.getByLabel('Send message')).toBeVisible();
    await expect(page.getByText('Ctrl+Enter to send')).toBeVisible();
  });

  test('should disable send button when message is empty', async ({ page }) => {
    const sendBtn = page.getByLabel('Send message');
    await expect(sendBtn).toBeDisabled();
  });

  test('should send message and display user bubble', async ({ page }) => {
    const textarea = page.getByLabel('Chat message input');
    await textarea.fill('My water damage claim has been pending for 3 weeks. What is the status?');
    await page.getByLabel('Send message').click();

    // User message should appear (right-aligned bubble)
    await expect(page.getByText('My water damage claim has been pending for 3 weeks')).toBeVisible({ timeout: 5_000 });
  });

  test('should display AI response after streaming', async ({ page }) => {
    const textarea = page.getByLabel('Chat message input');
    await textarea.fill('My water damage claim has been pending for 3 weeks.');
    await page.getByLabel('Send message').click();

    // Wait for the SSE stream to complete and the metadata to arrive
    // The mock SSE sends content chunks then a metadata chunk with the full response
    // After metadata is received, the assistant message with tone badge should appear
    await expect(page.getByText('Empathetic')).toBeVisible({ timeout: 10_000 });

    // The full response from metadata should be visible
    await expect(page.getByText('I understand your concern about the delay')).toBeVisible();
  });

  test('should show disclaimer on AI message', async ({ page }) => {
    const textarea = page.getByLabel('Chat message input');
    await textarea.fill('What is the status of my claim?');
    await page.getByLabel('Send message').click();

    // Wait for the AI response
    await expect(page.getByText('Empathetic')).toBeVisible({ timeout: 10_000 });

    // Disclaimer should be visible
    await expect(page.getByText('AI-generated')).toBeVisible();
    await expect(page.getByText('does not constitute a binding commitment')).toBeVisible();
  });

  test('should show claim context toggle', async ({ page }) => {
    const toggleBtn = page.getByLabel('Toggle claim context');
    await toggleBtn.scrollIntoViewIfNeeded();
    await expect(toggleBtn).toBeVisible();

    // Claim context input should be initially hidden
    const claimInput = page.locator('input[aria-label="Claim context"]');
    await expect(claimInput).toBeHidden();

    // Click to expand
    await toggleBtn.click();
    await expect(claimInput).toBeVisible({ timeout: 5_000 });
  });

  test('should show LLM provider metadata', async ({ page }) => {
    const textarea = page.getByLabel('Chat message input');
    await textarea.fill('Tell me about my policy coverage.');
    await page.getByLabel('Send message').click();

    // Wait for the AI response to fully render
    await expect(page.getByText('Empathetic')).toBeVisible({ timeout: 10_000 });

    // Groq provider badge should appear in the metadata section
    await expect(page.getByText('Groq')).toBeVisible();
  });

  test('should handle API error', async ({ page }) => {
    await page.route('**/api/insurance/cx/stream', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'CX service unavailable' }),
        });
      }
      return route.continue();
    });

    const textarea = page.getByLabel('Chat message input');
    await textarea.fill('Help me with my claim.');
    await page.getByLabel('Send message').click();

    const errorBanner = page.locator('[role="alert"]');
    await expect(errorBanner).toBeVisible({ timeout: 10_000 });
  });
});
