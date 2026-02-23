import { TestBed } from '@angular/core/testing';
import { ThemeService, ThemeMode } from './theme.service';

describe('ThemeService', () => {
  let service: ThemeService;

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('theme-dark', 'theme-semi-dark', 'theme-light');

    TestBed.configureTestingModule({});
    service = TestBed.inject(ThemeService);
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('theme-dark', 'theme-semi-dark', 'theme-light');
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should default to dark theme when no localStorage value', () => {
    expect(service.currentTheme()).toBe('dark');
  });

  it('should persist theme to localStorage', () => {
    service.setTheme('semi-dark');
    TestBed.flushEffects();
    expect(localStorage.getItem('insuresense-theme')).toBe('semi-dark');
  });

  it('should load saved theme from localStorage', () => {
    // Reset TestBed to pick up the new localStorage value
    localStorage.setItem('insuresense-theme', 'light');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const freshService = TestBed.inject(ThemeService);
    expect(freshService.currentTheme()).toBe('light');
  });

  it('should default to dark for invalid localStorage value', () => {
    localStorage.setItem('insuresense-theme', 'invalid-value');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const freshService = TestBed.inject(ThemeService);
    expect(freshService.currentTheme()).toBe('dark');
  });

  it('should cycle through themes: dark -> semi-dark -> light -> dark', () => {
    expect(service.currentTheme()).toBe('dark');

    service.cycleTheme();
    expect(service.currentTheme()).toBe('semi-dark');

    service.cycleTheme();
    expect(service.currentTheme()).toBe('light');

    service.cycleTheme();
    expect(service.currentTheme()).toBe('dark');
  });

  it('should set specific theme', () => {
    service.setTheme('light');
    expect(service.currentTheme()).toBe('light');

    service.setTheme('semi-dark');
    expect(service.currentTheme()).toBe('semi-dark');

    service.setTheme('dark');
    expect(service.currentTheme()).toBe('dark');
  });

  it('should apply theme class to document element', () => {
    service.setTheme('semi-dark');
    TestBed.flushEffects();
    expect(document.documentElement.classList.contains('theme-semi-dark')).toBe(true);
    expect(document.documentElement.classList.contains('theme-dark')).toBe(false);
  });

  it('should remove previous theme class when changing themes', () => {
    service.setTheme('light');
    TestBed.flushEffects();
    expect(document.documentElement.classList.contains('theme-light')).toBe(true);

    service.setTheme('dark');
    TestBed.flushEffects();
    expect(document.documentElement.classList.contains('theme-dark')).toBe(true);
    expect(document.documentElement.classList.contains('theme-light')).toBe(false);
  });
});
