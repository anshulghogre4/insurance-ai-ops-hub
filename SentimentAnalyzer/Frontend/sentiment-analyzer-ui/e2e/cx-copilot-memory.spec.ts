import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { mockAllApis, mockApiError } from './helpers/api-mocks';
import {
  MOCK_CX_SESSION_RESPONSE,
  MOCK_CX_SESSION_HISTORY_RESPONSE,
} from './fixtures/mock-data';

test.describe('CX Copilot — Conversation Memory', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
  });

  // ────────────────────────────────────────────────────────────
  // Session Initialization
  // ────────────────────────────────────────────────────────────

  test('should create a session on first visit and show Session Active indicator', async ({ page }) => {
    let sessionCreated = false;
    await page.route('**/api/insurance/cx/sessions', (route) => {
      if (route.request().method() === 'POST') {
        sessionCreated = true;
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_CX_SESSION_RESPONSE),
        });
      }
      return route.continue();
    });

    await page.goto('/cx/copilot');

    // Session creation should have been called
    expect(sessionCreated).toBe(true);

    // Session Active indicator should appear
    await expect(page.getByLabel('Session active indicator')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('Session Active')).toBeVisible();
  });

  test('should show New Conversation button', async ({ page }) => {
    await page.goto('/cx/copilot');

    const newConvoBtn = page.getByLabel('New conversation');
    await expect(newConvoBtn).toBeVisible();
    await expect(newConvoBtn).toContainText('New Conversation');
  });

  // ────────────────────────────────────────────────────────────
  // Send Message with Session
  // ────────────────────────────────────────────────────────────

  test('should pass sessionId in stream request body', async ({ page }) => {
    let capturedBody: Record<string, unknown> | null = null;

    // Override stream mock to capture the request body
    await page.route('**/api/insurance/cx/stream*', async (route) => {
      if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON();
        capturedBody = body;
        // Still fulfill with a valid SSE stream
        return route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache', 'Connection': 'keep-alive' },
          body: [
            'data: {"type":"content","content":"Your policy covers water damage.","metadata":null}\n\n',
            `data: {"type":"metadata","content":"","metadata":{"response":"Your policy covers water damage.","tone":"Professional","escalationRecommended":false,"escalationReason":null,"llmProvider":"Groq","elapsedMilliseconds":1200,"disclaimer":"AI-generated response."}}\n\n`,
            'data: [DONE]\n\n'
          ].join('')
        });
      }
      return route.continue();
    });

    await page.goto('/cx/copilot');

    // Wait for session creation to complete
    await expect(page.getByText('Session Active')).toBeVisible({ timeout: 5_000 });

    // Send a message
    const textarea = page.getByLabel('Chat message input');
    await textarea.fill('Does my homeowners policy cover water damage from burst pipes?');
    await page.getByLabel('Send message').click();

    // Wait for the AI response to appear
    await expect(page.getByText('Professional')).toBeVisible({ timeout: 10_000 });

    // Verify sessionId was sent in the request body
    expect(capturedBody).not.toBeNull();
    expect(capturedBody!['sessionId']).toBe(MOCK_CX_SESSION_RESPONSE.sessionId);
  });

  test('should send message and display both user and assistant bubbles', async ({ page }) => {
    await page.goto('/cx/copilot');
    await expect(page.getByText('Session Active')).toBeVisible({ timeout: 5_000 });

    const textarea = page.getByLabel('Chat message input');
    await textarea.fill('What is my deductible for windstorm coverage?');
    await page.getByLabel('Send message').click();

    // User message should appear
    await expect(page.getByText('What is my deductible for windstorm coverage?')).toBeVisible({ timeout: 5_000 });

    // AI response should appear after streaming completes
    await expect(page.getByText('Empathetic')).toBeVisible({ timeout: 10_000 });

    // Message input should be cleared
    await expect(textarea).toHaveValue('');
  });

  // ────────────────────────────────────────────────────────────
  // New Conversation
  // ────────────────────────────────────────────────────────────

  test('New Conversation button should clear messages and create new session', async ({ page }) => {
    let sessionCreateCount = 0;

    await page.route('**/api/insurance/cx/sessions', (route) => {
      if (route.request().method() === 'POST') {
        sessionCreateCount++;
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_CX_SESSION_RESPONSE),
        });
      }
      return route.continue();
    });

    await page.goto('/cx/copilot');
    await expect(page.getByText('Session Active')).toBeVisible({ timeout: 5_000 });

    // Send a message first to populate the chat
    const textarea = page.getByLabel('Chat message input');
    await textarea.fill('Help me understand my auto policy coverage limits.');
    await page.getByLabel('Send message').click();

    // Wait for AI response
    await expect(page.getByText('Empathetic')).toBeVisible({ timeout: 10_000 });

    // Verify at least one message exists
    await expect(page.getByText('Help me understand my auto policy coverage limits.')).toBeVisible();

    const initialCreateCount = sessionCreateCount;

    // Click New Conversation
    await page.getByLabel('New conversation').click();

    // Empty state should return
    await expect(page.getByText('Start a conversation')).toBeVisible({ timeout: 5_000 });

    // Previous messages should be gone
    await expect(page.getByText('Help me understand my auto policy coverage limits.')).not.toBeVisible();

    // A new session should have been created
    expect(sessionCreateCount).toBeGreaterThan(initialCreateCount);
  });

  // ────────────────────────────────────────────────────────────
  // Session Restore & History Loading
  // ────────────────────────────────────────────────────────────

  test('should load history messages when session exists in sessionStorage', async ({ page }) => {
    let historyRequested = false;

    // Override history mock to track the request
    await page.route('**/api/insurance/cx/sessions/*/history', (route) => {
      historyRequested = true;
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_CX_SESSION_HISTORY_RESPONSE),
      });
    });

    // Set the sessionStorage before navigating
    await page.goto('/cx/copilot');
    await page.evaluate((sessionId) => {
      sessionStorage.setItem('cx-copilot-session-id', sessionId);
    }, MOCK_CX_SESSION_RESPONSE.sessionId);

    // Reload to trigger session restore
    await page.reload();

    // History should have been requested
    expect(historyRequested).toBe(true);

    // History messages should be visible
    await expect(page.getByText('What does my homeowners policy cover for water damage?')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('Your homeowners policy covers sudden and accidental water damage')).toBeVisible();
    await expect(page.getByText('How do I file a water damage claim?')).toBeVisible();
    await expect(page.getByText('To file a water damage claim, call our claims hotline')).toBeVisible();

    // Session Active indicator should be visible
    await expect(page.getByText('Session Active')).toBeVisible();
  });

  // ────────────────────────────────────────────────────────────
  // Error Handling
  // ────────────────────────────────────────────────────────────

  test('should handle session creation failure gracefully', async ({ page }) => {
    // Override session creation to fail
    await page.route('**/api/insurance/cx/sessions', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Session service unavailable' }),
        });
      }
      return route.continue();
    });

    await page.goto('/cx/copilot');

    // Page should still load — stateless mode
    await expect(page.locator('h1')).toContainText('CX Copilot');

    // Empty state should be visible (component works without session)
    await expect(page.getByText('Start a conversation')).toBeVisible({ timeout: 5_000 });

    // Session Active indicator should NOT be visible (session failed)
    await expect(page.getByText('Session Active')).not.toBeVisible();
  });

  test('should handle history load failure and start fresh session', async ({ page }) => {
    // Set a stale session ID in sessionStorage
    await page.goto('/cx/copilot');
    await page.evaluate(() => {
      sessionStorage.setItem('cx-copilot-session-id', 'stale-expired-session-id');
    });

    // Override history to return 404 (session not found)
    await page.route('**/api/insurance/cx/sessions/*/history', (route) => {
      return route.fulfill({
        status: 404,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Session not found' }),
      });
    });

    // Reload to trigger restore with stale session
    await page.reload();

    // Should recover — page loads normally
    await expect(page.locator('h1')).toContainText('CX Copilot');

    // Should have created a new session after history failure
    await expect(page.getByText('Session Active')).toBeVisible({ timeout: 5_000 });
  });

  test('should show error alert when stream fails during active session', async ({ page }) => {
    await page.goto('/cx/copilot');
    await expect(page.getByText('Session Active')).toBeVisible({ timeout: 5_000 });

    // Override stream to return 500
    await page.route('**/api/insurance/cx/stream*', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'CX service temporarily unavailable' }),
        });
      }
      return route.continue();
    });

    const textarea = page.getByLabel('Chat message input');
    await textarea.fill('What is the claims process for flood damage?');
    await page.getByLabel('Send message').click();

    // Error should appear
    const errorAlert = page.locator('[role="alert"]');
    await expect(errorAlert.first()).toBeVisible({ timeout: 10_000 });
  });

  // ────────────────────────────────────────────────────────────
  // Accessibility
  // ────────────────────────────────────────────────────────────

  test('CX Copilot page with session should have no accessibility violations', async ({ page }) => {
    await page.goto('/cx/copilot');
    await expect(page.getByText('Session Active')).toBeVisible({ timeout: 5_000 });

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(['color-contrast'])
      .analyze();

    expect(results.violations).toEqual([]);
  });

  test('CX Copilot page with loaded history should have no accessibility violations', async ({ page }) => {
    // Set sessionStorage so history loads
    await page.goto('/cx/copilot');
    await page.evaluate((sessionId) => {
      sessionStorage.setItem('cx-copilot-session-id', sessionId);
    }, MOCK_CX_SESSION_RESPONSE.sessionId);
    await page.reload();

    // Wait for history to render
    await expect(page.getByText('What does my homeowners policy cover for water damage?')).toBeVisible({ timeout: 5_000 });

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(['color-contrast'])
      .analyze();

    expect(results.violations).toEqual([]);
  });
});
