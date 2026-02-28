import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { Injectable, signal, computed } from '@angular/core';
import { LandingComponent } from './landing';
import { ScrollService } from '../../services/scroll.service';

/**
 * Mock ScrollService that exposes writable signals for test control.
 * The real ScrollService listens to window.scroll — the mock decouples from DOM.
 */
@Injectable()
class MockScrollService {
  private _scrollY = signal(0);
  readonly scrollY = this._scrollY.asReadonly();
  readonly scrollProgress = computed(() => {
    const max = 2000; // simulate a 2000px scrollable page
    return max > 0 ? (this._scrollY() / max) * 100 : 0;
  });
  readonly prefersReducedMotion = signal(false);

  /** Test helper: set scroll position directly. */
  setScrollY(value: number): void {
    this._scrollY.set(value);
  }

  ngOnDestroy(): void { /* no-op */ }
}

describe('LandingComponent', () => {
  let component: LandingComponent;
  let fixture: ComponentFixture<LandingComponent>;
  let mockScroll: MockScrollService;

  beforeEach(async () => {
    mockScroll = new MockScrollService();

    await TestBed.configureTestingModule({
      imports: [LandingComponent, RouterTestingModule],
      providers: [
        { provide: ScrollService, useValue: mockScroll },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LandingComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    fixture.destroy();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  // ─── ScrollService Signals ───

  describe('ScrollService integration', () => {
    it('should inject ScrollService', () => {
      expect(component.scrollService).toBeTruthy();
    });

    it('should compute scrollProgressWidth as percentage string', () => {
      expect(component.scrollProgressWidth()).toMatch(/^\d+(\.\d+)?%$/);
    });

    it('should compute scroll indicator opacity between 0 and 1', () => {
      const opacity = component.scrollIndicatorOpacity();
      expect(opacity).toBeGreaterThanOrEqual(0);
      expect(opacity).toBeLessThanOrEqual(1);
    });

    it('should return "none" transforms when reduced motion is preferred', () => {
      mockScroll.prefersReducedMotion.set(true);

      expect(component.heroGridTransform()).toBe('none');
      expect(component.heroOrb1Transform()).toBe('none');
      expect(component.heroOrb2Transform()).toBe('none');
      expect(component.heroOrb3Transform()).toBe('none');
      expect(component.heroPillsTransform()).toBe('none');
    });

    it('should compute parallax transforms with scroll offset', () => {
      mockScroll.setScrollY(100);
      mockScroll.prefersReducedMotion.set(false);

      const gridTransform = component.heroGridTransform();
      expect(gridTransform).toContain('translateY(');
      expect(gridTransform).toContain('px)');
    });
  });

  // ─── Typewriter Effect ───

  describe('Typewriter Effect', () => {
    it('should start with empty typewriter text', () => {
      expect(component.typewriterText()).toBe('');
      expect(component.typewriterComplete()).toBe(false);
    });

    it('should show all text immediately when reduced motion is preferred', () => {
      mockScroll.prefersReducedMotion.set(true);

      component.startTypewriter();

      expect(component.typewriterText()).toContain('9 AI Agents.');
      expect(component.typewriterText()).toContain('7 LLM Providers.');
      expect(component.typewriterText()).toContain('1 Intelligent Platform.');
      expect(component.typewriterComplete()).toBe(true);
    });

    it('should complete all 3 lines of typewriter', () => {
      vi.useFakeTimers();
      mockScroll.prefersReducedMotion.set(false);

      component.startTypewriter();

      // Fast-forward past all the typewriter timers (generous: 5 seconds)
      vi.advanceTimersByTime(5000);

      const text = component.typewriterText();
      expect(text).toContain('9 AI Agents.');
      expect(text).toContain('7 LLM Providers.');
      expect(text).toContain('1 Intelligent Platform.');
      expect(component.typewriterComplete()).toBe(true);

      vi.useRealTimers();
    });

    it('should split typewriter text into lines for rendering', () => {
      component.typewriterText.set('Line 1\nLine 2');
      const lines = component.typewriterLines$();
      expect(lines).toHaveLength(2);
      expect(lines[0]).toBe('Line 1');
      expect(lines[1]).toBe('Line 2');
    });
  });

  // ─── Stats Counter Animation ───

  describe('Stats Counter Animation', () => {
    it('should initialize animated stat values', () => {
      fixture.detectChanges();
      const values = component.animatedStatValues();
      expect(values.length).toBe(component.stats.length);
    });

    it('should not be animated initially', () => {
      expect(component.statsAnimated()).toBe(false);
    });

    it('should parse numeric stat values correctly', () => {
      // Access private method via bracket notation for testing
      const parseStatValue = (component as unknown as { parseStatValue(v: string): number | null }).parseStatValue.bind(component);

      expect(parseStatValue('9')).toBe(9);
      expect(parseStatValue('99.9%')).toBe(99.9);
      expect(parseStatValue('1053+')).toBe(1053);
      expect(parseStatValue('< 60s')).toBe(60);
      expect(parseStatValue('N/A')).toBeNull();
    });
  });

  // ─── Mobile Detection ───

  describe('Mobile detection', () => {
    it('should detect mobile based on window width', () => {
      expect(typeof component.isMobile()).toBe('boolean');
    });
  });

  // ─── Parallax Transforms ───

  describe('Parallax transforms', () => {
    it('should compute heroGridTransform with translateY', () => {
      mockScroll.setScrollY(200);
      mockScroll.prefersReducedMotion.set(false);

      const transform = component.heroGridTransform();
      expect(transform).toContain('translateY(');
    });

    it('should compute heroOrb2Transform with negative values (opposite direction)', () => {
      mockScroll.setScrollY(100);
      mockScroll.prefersReducedMotion.set(false);

      const transform = component.heroOrb2Transform();
      // Uses translate() shorthand with negative values for opposite drift
      expect(transform).toMatch(/translate\(-/);
    });

    it('should compute heroOrb3Transform with both translateX and translateY', () => {
      mockScroll.setScrollY(100);
      mockScroll.prefersReducedMotion.set(false);

      const transform = component.heroOrb3Transform();
      expect(transform).toContain('translateX(');
      expect(transform).toContain('translateY(');
    });

    it('should compute heroPillsTransform with negative translateY (moves up)', () => {
      mockScroll.setScrollY(100);
      mockScroll.prefersReducedMotion.set(false);

      const transform = component.heroPillsTransform();
      expect(transform).toMatch(/translateY\(-\d/);
    });

    it('should halve parallax multipliers on mobile', () => {
      mockScroll.setScrollY(100);
      mockScroll.prefersReducedMotion.set(false);

      // Desktop
      (component as unknown as { _isMobile: { set(v: boolean): void } })._isMobile.set(false);
      const desktopGrid = component.heroGridTransform();

      // Mobile (halved multiplier)
      (component as unknown as { _isMobile: { set(v: boolean): void } })._isMobile.set(true);
      const mobileGrid = component.heroGridTransform();

      // Extract pixel values
      const desktopPx = parseFloat(desktopGrid.match(/translateY\(([^)]+)px\)/)?.[1] ?? '0');
      const mobilePx = parseFloat(mobileGrid.match(/translateY\(([^)]+)px\)/)?.[1] ?? '0');

      // Mobile should be ~half of desktop
      expect(mobilePx).toBeCloseTo(desktopPx / 2, 0);
    });
  });

  // ─── Scroll Indicator ───

  describe('Scroll indicator', () => {
    it('should have full opacity at scrollY=0', () => {
      mockScroll.setScrollY(0);
      expect(component.scrollIndicatorOpacity()).toBe(1);
    });

    it('should be invisible after 80px scroll', () => {
      mockScroll.setScrollY(80);
      expect(component.scrollIndicatorOpacity()).toBe(0);
    });

    it('should be partially visible at 40px scroll', () => {
      mockScroll.setScrollY(40);
      expect(component.scrollIndicatorOpacity()).toBeCloseTo(0.5, 1);
    });
  });
});
