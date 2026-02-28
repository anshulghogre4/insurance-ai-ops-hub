import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { CommandPaletteComponent } from './command-palette';
import { CommandRegistryService } from '../../services/command-registry.service';

/** Minimal stub component for test routes. */
@Component({ standalone: true, template: '' })
class StubComponent {}

const TEST_ROUTES = [
  { path: '', component: StubComponent },
  { path: 'dashboard', component: StubComponent },
  { path: 'claims/triage', component: StubComponent },
  { path: 'claims/history', component: StubComponent },
  { path: 'dashboard/fraud', component: StubComponent },
  { path: 'dashboard/providers', component: StubComponent },
  { path: 'documents/upload', component: StubComponent },
  { path: 'documents/query', component: StubComponent },
  { path: 'cx/copilot', component: StubComponent },
  { path: 'sentiment', component: StubComponent },
  { path: 'insurance', component: StubComponent },
];

describe('CommandPaletteComponent', () => {
  let component: CommandPaletteComponent;
  let fixture: ComponentFixture<CommandPaletteComponent>;
  let router: Router;
  let registry: CommandRegistryService;

  beforeEach(async () => {
    vi.useFakeTimers();

    await TestBed.configureTestingModule({
      imports: [CommandPaletteComponent],
      providers: [
        provideRouter(TEST_ROUTES),
        CommandRegistryService,
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    registry = TestBed.inject(CommandRegistryService);
    fixture = TestBed.createComponent(CommandPaletteComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should be closed by default', () => {
    expect(component.isOpen()).toBe(false);
    const dialog = fixture.nativeElement.querySelector('[role="dialog"]');
    expect(dialog).toBeNull();
  });

  it('should open on Ctrl+K', () => {
    const event = new KeyboardEvent('keydown', {
      key: 'k',
      ctrlKey: true,
      bubbles: true,
    });
    document.dispatchEvent(event);
    fixture.detectChanges();

    expect(component.isOpen()).toBe(true);
    const dialog = fixture.nativeElement.querySelector('[role="dialog"]');
    expect(dialog).toBeTruthy();
  });

  it('should open on Meta+K (Cmd+K on Mac)', () => {
    const event = new KeyboardEvent('keydown', {
      key: 'k',
      metaKey: true,
      bubbles: true,
    });
    document.dispatchEvent(event);
    fixture.detectChanges();

    expect(component.isOpen()).toBe(true);
  });

  it('should close on Escape', () => {
    component.open();
    fixture.detectChanges();
    expect(component.isOpen()).toBe(true);

    const event = new KeyboardEvent('keydown', {
      key: 'Escape',
      bubbles: true,
    });
    document.dispatchEvent(event);

    // Advance past the close animation timer (150ms)
    vi.advanceTimersByTime(200);
    fixture.detectChanges();

    expect(component.isOpen()).toBe(false);
  });

  it('should show search input when open', () => {
    component.open();
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('[data-testid="command-palette-search"]');
    expect(input).toBeTruthy();
    expect(input.getAttribute('placeholder')).toBe('Search or jump to...');
  });

  it('should display all commands when search is empty', () => {
    component.open();
    fixture.detectChanges();

    const options = fixture.nativeElement.querySelectorAll('[role="option"]');
    expect(options.length).toBe(10);
  });

  it('should filter results when typing in search', () => {
    component.open();
    fixture.detectChanges();

    registry.setSearchQuery('fraud');
    fixture.detectChanges();

    const options = fixture.nativeElement.querySelectorAll('[role="option"]');
    expect(options.length).toBeGreaterThanOrEqual(1);

    // Should contain Fraud Alerts
    const fraudOption = fixture.nativeElement.querySelector('[data-testid="command-nav-fraud-alerts"]');
    expect(fraudOption).toBeTruthy();
  });

  it('should show "No commands found" when search yields no results', () => {
    component.open();
    fixture.detectChanges();

    registry.setSearchQuery('xyznonexistentcommand');
    fixture.detectChanges();

    const noResults = fixture.nativeElement.querySelector('[role="listbox"]');
    expect(noResults.textContent).toContain('No commands found');
  });

  it('should move highlight down with ArrowDown', () => {
    component.open();
    fixture.detectChanges();

    expect(component.selectedIndex()).toBe(0);

    const event = new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true });
    component.onSearchKeydown(event);
    expect(component.selectedIndex()).toBe(1);
  });

  it('should move highlight up with ArrowUp', () => {
    component.open();
    fixture.detectChanges();

    // Move down first
    component.selectedIndex.set(3);

    const event = new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true });
    component.onSearchKeydown(event);
    expect(component.selectedIndex()).toBe(2);
  });

  it('should wrap ArrowDown from last to first', () => {
    component.open();
    fixture.detectChanges();

    const lastIndex = component.flatResults().length - 1;
    component.selectedIndex.set(lastIndex);

    const event = new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true });
    component.onSearchKeydown(event);
    expect(component.selectedIndex()).toBe(0);
  });

  it('should wrap ArrowUp from first to last', () => {
    component.open();
    fixture.detectChanges();

    component.selectedIndex.set(0);

    const event = new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true });
    component.onSearchKeydown(event);
    expect(component.selectedIndex()).toBe(component.flatResults().length - 1);
  });

  it('should navigate to selected route on Enter', () => {
    const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    component.open();
    fixture.detectChanges();

    // First command is Dashboard Overview (/dashboard)
    component.selectedIndex.set(0);

    const event = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true });
    component.onSearchKeydown(event);

    // Advance past the navigation delay (50ms) and close delay (150ms)
    vi.advanceTimersByTime(200);

    expect(navigateSpy).toHaveBeenCalledWith('/dashboard');
  });

  it('should navigate to correct route when clicking a result', () => {
    const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    component.open();
    fixture.detectChanges();

    // Click the Claims - New Triage option
    const triageOption = fixture.nativeElement.querySelector('[data-testid="command-nav-claims-triage"]');
    expect(triageOption).toBeTruthy();
    triageOption.click();

    vi.advanceTimersByTime(200);

    expect(navigateSpy).toHaveBeenCalledWith('/claims/triage');
  });

  it('should close palette after navigating', () => {
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    component.open();
    fixture.detectChanges();

    const command = component.flatResults()[0];
    component.executeCommand(command);

    vi.advanceTimersByTime(200);
    fixture.detectChanges();

    expect(component.isOpen()).toBe(false);
  });

  it('should have role="dialog" with aria-modal="true"', () => {
    component.open();
    fixture.detectChanges();

    const dialog = fixture.nativeElement.querySelector('[role="dialog"]');
    expect(dialog).toBeTruthy();
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(dialog.getAttribute('aria-label')).toBe('Command palette');
  });

  it('should have role="listbox" on results container', () => {
    component.open();
    fixture.detectChanges();

    const listbox = fixture.nativeElement.querySelector('[role="listbox"]');
    expect(listbox).toBeTruthy();
  });

  it('should have role="option" on each result', () => {
    component.open();
    fixture.detectChanges();

    const options = fixture.nativeElement.querySelectorAll('[role="option"]');
    expect(options.length).toBe(10);
  });

  it('should set aria-selected on the highlighted option', () => {
    component.open();
    fixture.detectChanges();

    const firstOption = fixture.nativeElement.querySelector('[role="option"]');
    expect(firstOption.getAttribute('aria-selected')).toBe('true');

    // Move highlight down
    component.selectedIndex.set(1);
    fixture.detectChanges();

    const updatedFirst = fixture.nativeElement.querySelectorAll('[role="option"]')[0];
    expect(updatedFirst.getAttribute('aria-selected')).toBe('false');

    const second = fixture.nativeElement.querySelectorAll('[role="option"]')[1];
    expect(second.getAttribute('aria-selected')).toBe('true');
  });

  it('should have aria-live="polite" for result count', () => {
    component.open();
    fixture.detectChanges();

    const liveRegion = fixture.nativeElement.querySelector('[aria-live="polite"]');
    expect(liveRegion).toBeTruthy();
    expect(liveRegion.textContent).toContain('10 results available');
  });

  it('should update live region when results change', () => {
    component.open();
    fixture.detectChanges();

    registry.setSearchQuery('xyznotfound');
    fixture.detectChanges();

    const liveRegion = fixture.nativeElement.querySelector('[aria-live="polite"]');
    expect(liveRegion.textContent).toContain('No results found');
  });

  it('should trap Tab key', () => {
    component.open();
    fixture.detectChanges();

    const event = new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true });
    const preventSpy = vi.spyOn(event, 'preventDefault');
    component.onSearchKeydown(event);
    expect(preventSpy).toHaveBeenCalled();
  });

  it('should reset search and selection when opening', () => {
    registry.setSearchQuery('test');
    component.selectedIndex.set(5);

    component.open();
    fixture.detectChanges();

    expect(registry.searchQuery()).toBe('');
    expect(component.selectedIndex()).toBe(0);
  });

  it('should close when backdrop is clicked', () => {
    component.open();
    fixture.detectChanges();

    const backdrop = fixture.nativeElement.querySelector('[data-testid="command-palette-backdrop"]');
    expect(backdrop).toBeTruthy();
    backdrop.click();

    vi.advanceTimersByTime(200);
    fixture.detectChanges();

    expect(component.isOpen()).toBe(false);
  });

  it('should open via registry requestOpen signal', () => {
    registry.requestOpen();
    TestBed.flushEffects();
    vi.advanceTimersByTime(100);
    fixture.detectChanges();

    expect(component.isOpen()).toBe(true);
  });

  it('should show category headers', () => {
    component.open();
    fixture.detectChanges();

    const categoryHeaders = fixture.nativeElement.querySelectorAll('[aria-hidden="true"]');
    const navCategory = Array.from(categoryHeaders as NodeListOf<HTMLElement>).find(
      (el: HTMLElement) => el.textContent?.trim() === 'Navigate'
    );
    expect(navCategory).toBeTruthy();
  });

  it('should display route hints on each command', () => {
    component.open();
    fixture.detectChanges();

    // Check the first command shows its route
    const firstOption = fixture.nativeElement.querySelector('[data-testid="command-nav-dashboard"]');
    expect(firstOption).toBeTruthy();
    expect(firstOption.textContent).toContain('/dashboard');
  });

  it('should show footer with keyboard shortcut hints', () => {
    component.open();
    fixture.detectChanges();

    const footer = fixture.nativeElement.querySelector('[data-testid="command-palette-dialog"]');
    expect(footer.textContent).toContain('navigate');
    expect(footer.textContent).toContain('select');
    expect(footer.textContent).toContain('close');
  });

  it('should set combobox role on search input', () => {
    component.open();
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('[data-testid="command-palette-search"]');
    expect(input.getAttribute('role')).toBe('combobox');
    expect(input.getAttribute('aria-expanded')).toBe('true');
    expect(input.getAttribute('aria-autocomplete')).toBe('list');
  });

  it('should update selectedIndex on mouseenter', () => {
    component.open();
    fixture.detectChanges();

    const thirdOption = fixture.nativeElement.querySelectorAll('[role="option"]')[2];
    thirdOption.dispatchEvent(new MouseEvent('mouseenter', { bubbles: true }));
    fixture.detectChanges();

    expect(component.selectedIndex()).toBe(2);
  });

  it('should reset selectedIndex when search query changes', () => {
    component.open();
    fixture.detectChanges();

    component.selectedIndex.set(5);

    // Simulate typing in search input
    const event = { target: { value: 'claims' } } as unknown as Event;
    component.onSearchInput(event);
    fixture.detectChanges();

    expect(component.selectedIndex()).toBe(0);
  });
});
