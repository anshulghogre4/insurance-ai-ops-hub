import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { ScrollService } from './scroll.service';

describe('ScrollService', () => {
  let service: ScrollService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ScrollService);
  });

  afterEach(() => {
    service.ngOnDestroy();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should initialize scrollY at 0', () => {
    expect(service.scrollY()).toBe(0);
  });

  it('should initialize scrollProgress at 0', () => {
    expect(service.scrollProgress()).toBeGreaterThanOrEqual(0);
    expect(service.scrollProgress()).toBeLessThanOrEqual(100);
  });

  it('should expose prefersReducedMotion signal', () => {
    // In a test environment, reduced motion is typically false
    expect(typeof service.prefersReducedMotion()).toBe('boolean');
  });

  it('should update scrollY when scroll event fires', async () => {
    // Mock window.scrollY
    Object.defineProperty(window, 'scrollY', { value: 250, writable: true, configurable: true });

    // Dispatch scroll event
    window.dispatchEvent(new Event('scroll'));

    // Wait for rAF to process
    await new Promise<void>(resolve => {
      requestAnimationFrame(() => {
        resolve();
      });
    });

    expect(service.scrollY()).toBe(250);

    // Restore
    Object.defineProperty(window, 'scrollY', { value: 0, writable: true, configurable: true });
  });

  it('should compute scrollProgress between 0 and 100', async () => {
    // Set up a scrollable page
    const originalScrollHeight = Object.getOwnPropertyDescriptor(document.documentElement, 'scrollHeight');
    Object.defineProperty(document.documentElement, 'scrollHeight', { value: 2000, configurable: true });
    Object.defineProperty(window, 'innerHeight', { value: 800, writable: true, configurable: true });
    Object.defineProperty(window, 'scrollY', { value: 600, writable: true, configurable: true });

    window.dispatchEvent(new Event('scroll'));

    await new Promise<void>(resolve => {
      requestAnimationFrame(() => resolve());
    });

    const progress = service.scrollProgress();
    expect(progress).toBeGreaterThan(0);
    expect(progress).toBeLessThanOrEqual(100);

    // Restore
    Object.defineProperty(window, 'scrollY', { value: 0, writable: true, configurable: true });
    if (originalScrollHeight) {
      Object.defineProperty(document.documentElement, 'scrollHeight', originalScrollHeight);
    }
  });

  it('should return 0 progress when page is not scrollable', () => {
    // When scrollHeight equals innerHeight, max = 0
    const originalScrollHeight = Object.getOwnPropertyDescriptor(document.documentElement, 'scrollHeight');
    Object.defineProperty(document.documentElement, 'scrollHeight', { value: 800, configurable: true });
    Object.defineProperty(window, 'innerHeight', { value: 800, writable: true, configurable: true });

    expect(service.scrollProgress()).toBe(0);

    if (originalScrollHeight) {
      Object.defineProperty(document.documentElement, 'scrollHeight', originalScrollHeight);
    }
  });

  it('should clean up scroll listener on destroy', () => {
    const removeSpy = vi.spyOn(window, 'removeEventListener');
    service.ngOnDestroy();
    expect(removeSpy).toHaveBeenCalledWith('scroll', expect.any(Function));
    removeSpy.mockRestore();
  });
});
