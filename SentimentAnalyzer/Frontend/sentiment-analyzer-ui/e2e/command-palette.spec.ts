import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { mockAllApis } from './helpers/api-mocks';

/** Helper: skip test when viewport is narrower than md breakpoint (768px) */
function skipOnMobile(page: import('@playwright/test').Page) {
  const vp = page.viewportSize();
  test.skip(!!vp && vp.width < 768, 'Command palette nav button hidden on mobile');
}

/** Detect platform for Ctrl vs Meta key */
function getModifier(page: import('@playwright/test').Page): string {
  // Playwright uses 'ControlOrMeta' to abstract platform differences
  return 'ControlOrMeta';
}

test.describe('Command Palette', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should open palette with Ctrl+K keyboard shortcut', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();
    await expect(dialog).toHaveAttribute('aria-modal', 'true');
    await expect(dialog).toHaveAttribute('aria-label', 'Command palette');
  });

  test('should show search input with placeholder when opened', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const searchInput = page.locator('[data-testid="command-palette-search"]');
    await expect(searchInput).toBeVisible();
    await expect(searchInput).toHaveAttribute('placeholder', 'Search or jump to...');
    await expect(searchInput).toBeFocused();
  });

  test('should display all 10 commands when search is empty', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const options = page.locator('[role="option"]');
    await expect(options).toHaveCount(10);
  });

  test('should filter results when typing "triage"', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const searchInput = page.locator('[data-testid="command-palette-search"]');
    await searchInput.fill('triage');

    // Should show Claims - New Triage
    const triageOption = page.locator('[data-testid="command-nav-claims-triage"]');
    await expect(triageOption).toBeVisible();
    await expect(triageOption).toContainText('New Triage');
  });

  test('should filter results when typing "fraud"', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const searchInput = page.locator('[data-testid="command-palette-search"]');
    await searchInput.fill('fraud');

    const fraudOption = page.locator('[data-testid="command-nav-fraud-alerts"]');
    await expect(fraudOption).toBeVisible();
    await expect(fraudOption).toContainText('Fraud Alerts');
  });

  test('should filter case-insensitively', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const searchInput = page.locator('[data-testid="command-palette-search"]');
    await searchInput.fill('DASHBOARD');

    const dashboardOption = page.locator('[data-testid="command-nav-dashboard"]');
    await expect(dashboardOption).toBeVisible();
  });

  test('should show "No commands found" for unmatched search', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const searchInput = page.locator('[data-testid="command-palette-search"]');
    await searchInput.fill('xyznonexistentcommand');

    await expect(page.getByText('No commands found')).toBeVisible();
    const options = page.locator('[role="option"]');
    await expect(options).toHaveCount(0);
  });

  test('should navigate to /claims/triage when pressing Enter on filtered result', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const searchInput = page.locator('[data-testid="command-palette-search"]');
    await searchInput.fill('triage');

    // Wait for filter to apply
    await expect(page.locator('[data-testid="command-nav-claims-triage"]')).toBeVisible();

    await page.keyboard.press('Enter');

    // Wait for navigation
    await expect(page).toHaveURL(/.*\/claims\/triage/);
  });

  test('should navigate to /dashboard when clicking Dashboard Overview', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');

    const dashboardOption = page.locator('[data-testid="command-nav-dashboard"]');
    await expect(dashboardOption).toBeVisible();
    await dashboardOption.click();

    await expect(page).toHaveURL(/.*\/dashboard$/);
  });

  test('should close palette on Escape', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();

    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
  });

  test('should close palette when clicking backdrop', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();

    // Click the backdrop (outside the dialog card)
    const backdrop = page.locator('[data-testid="command-palette-backdrop"]');
    await backdrop.click({ force: true });
    await expect(dialog).toBeHidden();
  });

  test('should close palette after navigating', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();

    await page.locator('[data-testid="command-nav-sentiment"]').click();
    await expect(dialog).toBeHidden();
    await expect(page).toHaveURL(/.*\/sentiment/);
  });

  test('should navigate between items with ArrowDown and ArrowUp', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.waitForSelector('[role="option"]');

    // First item should be selected
    const firstOption = page.locator('[role="option"]').first();
    await expect(firstOption).toHaveAttribute('aria-selected', 'true');

    // Press ArrowDown to move to second
    await page.keyboard.press('ArrowDown');
    await page.waitForTimeout(100);
    const secondOption = page.locator('[role="option"]').nth(1);
    await expect(secondOption).toHaveAttribute('aria-selected', 'true');
    await expect(firstOption).toHaveAttribute('aria-selected', 'false');

    // Press ArrowUp to move back to first
    await page.keyboard.press('ArrowUp');
    await page.waitForTimeout(100);
    await expect(firstOption).toHaveAttribute('aria-selected', 'true');
  });

  test.skip('should wrap ArrowDown from last item to first', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.waitForSelector('[role="option"]');

    // Navigate to the last item
    const allOptions = page.locator('[role="option"]');
    const count = await allOptions.count();
    for (let i = 0; i < count - 1; i++) {
      await page.keyboard.press('ArrowDown');
      await page.waitForTimeout(50);
    }

    // Last item should be selected
    await expect(allOptions.last()).toHaveAttribute('aria-selected', 'true');

    // Press ArrowDown again to wrap to first
    await page.keyboard.press('ArrowDown');
    await page.waitForTimeout(100);
    await expect(allOptions.first()).toHaveAttribute('aria-selected', 'true');
  });

  test.skip('should wrap ArrowUp from first item to last', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');

    // First item should be selected
    const allOptions = page.locator('[role="option"]');
    await expect(allOptions.first()).toHaveAttribute('aria-selected', 'true');

    // Press ArrowUp to wrap to last
    await page.keyboard.press('ArrowUp');
    await expect(allOptions.last()).toHaveAttribute('aria-selected', 'true');
  });

  test('should navigate to correct route with ArrowDown + Enter', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');

    // Wait for palette to be ready
    await expect(page.locator('[role="dialog"]')).toBeVisible();
    await expect(page.locator('[data-testid="command-palette-search"]')).toBeFocused();

    // ArrowDown once to select Claims - New Triage (index 1)
    await page.keyboard.press('ArrowDown');

    // Verify selection moved before pressing Enter
    const secondOption = page.locator('[role="option"]').nth(1);
    await expect(secondOption).toHaveAttribute('aria-selected', 'true');

    await page.keyboard.press('Enter');

    await expect(page).toHaveURL(/.*\/claims\/triage/, { timeout: 10_000 });
  });

  test('should show keyboard shortcut hints in footer', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');

    const dialog = page.locator('[data-testid="command-palette-dialog"]');
    await expect(dialog).toContainText('navigate');
    await expect(dialog).toContainText('select');
    await expect(dialog).toContainText('close');
  });

  test('should have listbox role on results container', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const listbox = page.locator('[role="listbox"]');
    await expect(listbox).toBeVisible();
  });

  test('should have combobox role on search input', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const input = page.locator('[data-testid="command-palette-search"]');
    await expect(input).toHaveAttribute('role', 'combobox');
    await expect(input).toHaveAttribute('aria-expanded', 'true');
    await expect(input).toHaveAttribute('aria-autocomplete', 'list');
  });

  test('should have aria-live region for result count', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const liveRegion = page.getByTestId('command-palette-live');
    await expect(liveRegion).toContainText('10 results available');
  });

  test('should update aria-live region when filtering', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    const searchInput = page.locator('[data-testid="command-palette-search"]');
    await searchInput.fill('xyznotfound');

    const liveRegion = page.locator('[data-testid="command-palette-live"]');
    await expect(liveRegion).toContainText('No results found');
  });

  test('should show category headers', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');

    // Navigate category header should be present
    const resultsArea = page.locator('[data-testid="command-palette-results"]');
    await expect(resultsArea).toContainText('Navigate');
  });

  test('should show route hint on each command', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');

    const dashboardOption = page.locator('[data-testid="command-nav-dashboard"]');
    await expect(dashboardOption).toContainText('/dashboard');
  });
});

test.describe('Command Palette - Route Discovery', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('Dashboard Overview is discoverable', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.locator('[data-testid="command-palette-search"]').fill('dashboard');
    await expect(page.locator('[data-testid="command-nav-dashboard"]')).toBeVisible();
  });

  test('Claims - New Triage is discoverable', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.locator('[data-testid="command-palette-search"]').fill('triage');
    await expect(page.locator('[data-testid="command-nav-claims-triage"]')).toBeVisible();
  });

  test('Claims - History is discoverable', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.locator('[data-testid="command-palette-search"]').fill('history');
    await expect(page.locator('[data-testid="command-nav-claims-history"]')).toBeVisible();
  });

  test('Fraud Alerts is discoverable', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.locator('[data-testid="command-palette-search"]').fill('fraud');
    await expect(page.locator('[data-testid="command-nav-fraud-alerts"]')).toBeVisible();
  });

  test('Provider Health is discoverable', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.locator('[data-testid="command-palette-search"]').fill('provider');
    await expect(page.locator('[data-testid="command-nav-provider-health"]')).toBeVisible();
  });

  test('Document Upload is discoverable', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.locator('[data-testid="command-palette-search"]').fill('upload');
    await expect(page.locator('[data-testid="command-nav-doc-upload"]')).toBeVisible();
  });

  test('Document Query is discoverable', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.locator('[data-testid="command-palette-search"]').fill('query');
    await expect(page.locator('[data-testid="command-nav-doc-query"]')).toBeVisible();
  });

  test('CX Copilot is discoverable', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.locator('[data-testid="command-palette-search"]').fill('copilot');
    await expect(page.locator('[data-testid="command-nav-cx-copilot"]')).toBeVisible();
  });

  test('Sentiment Analysis is discoverable', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.locator('[data-testid="command-palette-search"]').fill('sentiment');
    await expect(page.locator('[data-testid="command-nav-sentiment"]')).toBeVisible();
  });

  test('Insurance Analysis is discoverable', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.locator('[data-testid="command-palette-search"]').fill('insurance');
    await expect(page.locator('[data-testid="command-nav-insurance"]')).toBeVisible();
  });
});

test.describe('Command Palette - Nav Button', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should show command palette button in desktop nav', async ({ page }) => {
    skipOnMobile(page);
    const btn = page.locator('[data-testid="nav-command-palette-btn"]');
    await expect(btn).toBeVisible();
  });

  test('should open palette when nav button is clicked', async ({ page }) => {
    skipOnMobile(page);
    const btn = page.locator('[data-testid="nav-command-palette-btn"]');
    await btn.click();

    const dialog = page.locator('[role="dialog"]');
    await expect(dialog).toBeVisible();
  });
});

test.describe('Command Palette - Accessibility', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApis(page);
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should have no accessibility violations when palette is open', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await expect(page.locator('[role="dialog"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(['color-contrast'])
      .analyze();
    expect(results.violations).toEqual([]);
  });

  test('should have no accessibility violations with filtered results', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.locator('[data-testid="command-palette-search"]').fill('claims');
    await expect(page.locator('[role="option"]').first()).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(['color-contrast'])
      .analyze();
    expect(results.violations).toEqual([]);
  });

  test('should have no accessibility violations with no results', async ({ page }) => {
    await page.keyboard.press('ControlOrMeta+k');
    await page.locator('[data-testid="command-palette-search"]').fill('xyznotfound');
    await expect(page.getByText('No commands found')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .disableRules(['color-contrast'])
      .analyze();
    expect(results.violations).toEqual([]);
  });
});
