import { Injectable, NgZone, OnDestroy, signal, computed } from '@angular/core';

/**
 * ScrollService — singleton service that tracks window scroll position
 * using a passive listener throttled to ~60fps via requestAnimationFrame.
 * All scroll-dependent UI (parallax, progress bar, fade effects) derives
 * state from the reactive signals exposed here.
 */
@Injectable({ providedIn: 'root' })
export class ScrollService implements OnDestroy {

  /** Raw scrollY pixel offset updated at ~60fps. */
  private readonly _scrollY = signal(0);
  readonly scrollY = this._scrollY.asReadonly();

  /** 0-100 percentage representing how far down the page the user has scrolled. */
  readonly scrollProgress = computed(() => {
    const max = document.documentElement.scrollHeight - window.innerHeight;
    return max > 0 ? (this._scrollY() / max) * 100 : 0;
  });

  /** Whether the user prefers reduced motion. */
  readonly prefersReducedMotion = signal(this.checkReducedMotion());

  private ticking = false;
  private readonly boundOnScroll: () => void;
  private readonly boundOnMotionChange: (e: MediaQueryListEvent) => void;
  private motionQuery: MediaQueryList | null = null;

  constructor(private ngZone: NgZone) {
    this.boundOnScroll = this.onScroll.bind(this);
    this.boundOnMotionChange = (e: MediaQueryListEvent) => {
      this.prefersReducedMotion.set(e.matches);
    };

    // Run scroll listener outside Angular's change detection for performance
    this.ngZone.runOutsideAngular(() => {
      window.addEventListener('scroll', this.boundOnScroll, { passive: true });
    });

    // Listen for reduced-motion preference changes
    if (typeof window !== 'undefined' && window.matchMedia) {
      this.motionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
      this.motionQuery.addEventListener('change', this.boundOnMotionChange);
    }
  }

  ngOnDestroy(): void {
    window.removeEventListener('scroll', this.boundOnScroll);
    if (this.motionQuery) {
      this.motionQuery.removeEventListener('change', this.boundOnMotionChange);
    }
  }

  /** Throttle scroll updates to one per animation frame (~16ms / 60fps). */
  private onScroll(): void {
    if (!this.ticking) {
      this.ticking = true;
      requestAnimationFrame(() => {
        this._scrollY.set(window.scrollY);
        this.ticking = false;
      });
    }
  }

  /** Check whether the user prefers reduced motion. */
  private checkReducedMotion(): boolean {
    if (typeof window === 'undefined' || !window.matchMedia) return false;
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  }
}
