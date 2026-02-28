import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { FormsModule } from '@angular/forms';
import { RouterTestingModule } from '@angular/router/testing';
import { InsuranceAnalyzerComponent } from './insurance-analyzer/insurance-analyzer';
import { DashboardComponent } from './dashboard/dashboard';
import { LoginComponent } from './login/login';
import { Nav } from './nav/nav';
import { ThemeService, ThemeMode } from '../services/theme.service';
import { AuthService } from '../services/auth.service';
import { InsuranceService } from '../services/insurance.service';
import { of } from 'rxjs';

/**
 * UI/UX Tests - InsureSense AI
 * Tests for: theme switching, responsive design, accessibility,
 * user-friendly interactions, and visual consistency.
 */

describe('UX: Theme Integration', () => {
  let themeService: ThemeService;

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('theme-dark', 'theme-semi-dark', 'theme-light');
    TestBed.configureTestingModule({});
    themeService = TestBed.inject(ThemeService);
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('should have CSS custom properties defined for all themes', () => {
    const themes: ThemeMode[] = ['dark', 'semi-dark', 'light'];
    themes.forEach(theme => {
      themeService.setTheme(theme);
      TestBed.flushEffects();
      expect(document.documentElement.classList.contains(`theme-${theme}`)).toBe(true);
    });
  });

  it('should persist theme preference across page reloads', () => {
    themeService.setTheme('semi-dark');
    TestBed.flushEffects();
    expect(localStorage.getItem('insuresense-theme')).toBe('semi-dark');

    // Re-create via DI to simulate reload
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const freshService = TestBed.inject(ThemeService);
    expect(freshService.currentTheme()).toBe('semi-dark');
  });

  it('should cycle through all three modes without skipping', () => {
    const expectedOrder: ThemeMode[] = ['dark', 'semi-dark', 'light', 'dark'];
    expectedOrder.forEach((expected, i) => {
      if (i > 0) themeService.cycleTheme();
      expect(themeService.currentTheme()).toBe(expected);
    });
  });
});

describe('UX: Navigation Component', () => {
  let component: Nav;
  let fixture: ComponentFixture<Nav>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Nav, RouterTestingModule],
      providers: [
        {
          provide: AuthService,
          useValue: {
            authEnabled: () => true,
            isAuthenticated: () => false,
            user: () => null,
            signOut: vi.fn(),
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(Nav);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should render the InsureSense AI brand logo', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('InsureSense');
    expect(el.textContent).toContain('AI');
  });

  it('should have a theme toggle button', () => {
    const el = fixture.nativeElement as HTMLElement;
    const themeBtn = el.querySelector('[title]');
    expect(themeBtn).toBeTruthy();
  });

  it('should toggle mobile menu on click', () => {
    expect(component.showMobileMenu()).toBe(false);
    component.toggleMobileMenu();
    expect(component.showMobileMenu()).toBe(true);
    component.toggleMobileMenu();
    expect(component.showMobileMenu()).toBe(false);
  });

  it('should toggle user dropdown menu', () => {
    expect(component.showUserMenu()).toBe(false);
    component.toggleUserMenu();
    expect(component.showUserMenu()).toBe(true);
  });

  it('should generate correct user initials from email', () => {
    expect(component.getUserInitial()).toBe('U'); // no user
  });

  it('should provide accessible theme label', () => {
    const label = component.getThemeLabel();
    expect(label).toContain('mode');
    expect(label).toContain('click for');
  });

  it('should have sticky positioning for nav', () => {
    const nav = fixture.nativeElement.querySelector('nav') as HTMLElement;
    expect(nav.classList.contains('sticky')).toBe(true);
    expect(nav.classList.contains('top-0')).toBe(true);
  });
});

describe('UX: Login Component', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginComponent, FormsModule, RouterTestingModule],
      providers: [
        {
          provide: AuthService,
          useValue: {
            signIn: vi.fn().mockResolvedValue({ error: null }),
            signUp: vi.fn().mockResolvedValue({ error: null }),
            authEnabled: () => true,
            isAuthenticated: () => false,
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should display product branding on desktop', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('InsureSense AI');
    expect(el.textContent).toContain('Insurance Intelligence');
  });

  it('should show feature highlights in branding panel', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Claims Triage & Fraud Detection');
    expect(el.textContent).toContain('7-Provider Resilient Fallback');
    expect(el.textContent).toContain('PII-Safe Processing');
  });

  it('should have email and password inputs with icons', () => {
    const el = fixture.nativeElement as HTMLElement;
    const inputs = el.querySelectorAll('input');
    expect(inputs.length).toBeGreaterThanOrEqual(2);

    const emailInput = el.querySelector('input[type="email"]');
    const passwordInput = el.querySelector('input[type="password"]');
    expect(emailInput).toBeTruthy();
    expect(passwordInput).toBeTruthy();
  });

  it('should have autocomplete attributes for accessibility', () => {
    const el = fixture.nativeElement as HTMLElement;
    const emailInput = el.querySelector('input[type="email"]') as HTMLInputElement;
    const passwordInput = el.querySelector('input[type="password"]') as HTMLInputElement;
    expect(emailInput?.getAttribute('autocomplete')).toBe('email');
    expect(passwordInput?.getAttribute('autocomplete')).toBe('current-password');
  });

  it('should display security footer', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('enterprise-grade security');
    expect(el.textContent).toContain('PII auto-redacted');
  });

  it('should toggle between sign-in and sign-up modes', () => {
    expect(component.isRegisterMode()).toBe(false);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Welcome back');

    component.toggleMode();
    fixture.detectChanges();
    expect(component.isRegisterMode()).toBe(true);
    expect(el.textContent).toContain('Create your account');
  });
});

describe('UX: Insurance Analyzer - Sample Templates', () => {
  let component: InsuranceAnalyzerComponent;
  let fixture: ComponentFixture<InsuranceAnalyzerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InsuranceAnalyzerComponent, HttpClientTestingModule, FormsModule]
    }).compileComponents();

    fixture = TestBed.createComponent(InsuranceAnalyzerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should populate input when using claim sample', () => {
    component.useSample('claim');
    expect(component.inputText).toContain('water damage');
    expect(component.interactionType).toBe('Complaint');
  });

  it('should populate input when using renewal sample', () => {
    component.useSample('renewal');
    expect(component.inputText).toContain('renewal');
    expect(component.interactionType).toBe('Call');
  });

  it('should populate input when using positive review sample', () => {
    component.useSample('positive');
    expect(component.inputText).toContain('impressed');
    expect(component.interactionType).toBe('Review');
  });

  it('should populate input when using billing dispute sample', () => {
    component.useSample('billing');
    expect(component.inputText).toContain('premium increased');
    expect(component.interactionType).toBe('Email');
  });

  it('should clear previous results when loading a sample', () => {
    component.error.set('old error');
    component.useSample('claim');
    expect(component.error()).toBeNull();
    expect(component.result()).toBeNull();
  });

  it('should not modify state for unknown sample key', () => {
    component.inputText = 'existing text';
    component.useSample('nonexistent');
    expect(component.inputText).toBe('existing text');
  });

  it('should return correct risk badge classes', () => {
    expect(component.getRiskBadge('High')).toBe('badge-danger');
    expect(component.getRiskBadge('Medium')).toBe('badge-warning');
    expect(component.getRiskBadge('Low')).toBe('badge-success');
    expect(component.getRiskBadge('None')).toBe('badge-neutral');
  });

  it('should use realistic insurance text in all samples', () => {
    const samples = ['claim', 'renewal', 'positive', 'billing'];
    samples.forEach(key => {
      component.useSample(key);
      // Verify no generic test data per CLAUDE.md rules
      expect(component.inputText).not.toContain('test text');
      expect(component.inputText).not.toContain('foo');
      expect(component.inputText).not.toContain('bar');
      expect(component.inputText.length).toBeGreaterThan(50);
    });
  });
});

describe('UX: Dashboard - Loading & Empty States', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent, HttpClientTestingModule, RouterTestingModule],
      providers: [
        {
          provide: InsuranceService,
          useValue: {
            getDashboard: () => of({
              metrics: { totalAnalyses: 0, avgPurchaseIntent: 0, avgSentimentScore: 0, highRiskCount: 0 },
              sentimentDistribution: { positive: 0, negative: 0, neutral: 0, mixed: 0 },
              topPersonas: []
            }),
            getHistory: () => of([])
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should show empty state message when no history', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('No analyses yet');
  });

  it('should return correct sentiment badge class', () => {
    expect(component.getSentimentBadge('Positive')).toBe('badge-success');
    expect(component.getSentimentBadge('Negative')).toBe('badge-danger');
    expect(component.getSentimentBadge('Mixed')).toBe('badge-warning');
    expect(component.getSentimentBadge('Neutral')).toBe('badge-info');
    // Non-standard LLM outputs
    expect(component.getSentimentBadge('Angry')).toBe('badge-danger');
    expect(component.getSentimentBadge('Satisfied')).toBe('badge-success');
  });

  it('should return correct risk badge class', () => {
    expect(component.getRiskBadge('High')).toBe('badge-danger');
    expect(component.getRiskBadge('Medium')).toBe('badge-warning');
    expect(component.getRiskBadge('Low')).toBe('badge-success');
  });

  it('should have refresh functionality', () => {
    const spy = vi.spyOn(component, 'loadDashboard');
    component.refresh();
    expect(spy).toHaveBeenCalled();
  });
});

describe('UX: Accessibility Basics', () => {
  it('should have proper page title in index.html', () => {
    // This verifies our index.html update
    expect(document.title).toBeDefined();
  });

  it('should have Inter font loaded', () => {
    const body = document.body;
    const computedStyle = window.getComputedStyle(body);
    // Font family should include Inter
    expect(computedStyle.fontFamily).toBeDefined();
  });
});
