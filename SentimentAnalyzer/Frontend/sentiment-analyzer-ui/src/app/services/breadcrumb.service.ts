import { Injectable, signal, computed, inject, DestroyRef } from '@angular/core';
import { Router, ActivatedRoute, NavigationEnd, Data } from '@angular/router';
import { filter } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

/** A single breadcrumb item in the navigation trail. */
export interface Breadcrumb {
  label: string;
  url: string;
  isCurrentPage: boolean;
}

/**
 * Builds a breadcrumb trail from Angular route data.
 * Listens to NavigationEnd events and constructs the trail from the
 * activated route tree. Home crumb always links to /dashboard.
 */
@Injectable({ providedIn: 'root' })
export class BreadcrumbService {
  private router = inject(Router);
  private activatedRoute = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);

  /** Internal writable signal updated on each navigation. */
  private _breadcrumbs = signal<Breadcrumb[]>([]);

  /** Public readonly signal exposing the current breadcrumb trail. */
  readonly breadcrumbs = this._breadcrumbs.asReadonly();

  /** Whether breadcrumbs should be visible (depth > 1 and not on landing page). */
  readonly isVisible = computed(() => this._breadcrumbs().length > 1);

  /**
   * Static mapping from route path prefixes to their logical parent breadcrumbs.
   * This handles cases where the URL structure does not match the desired
   * breadcrumb hierarchy (e.g., /claims/triage should show "Dashboard / Claims / New Triage"
   * even though there is no /claims parent route).
   */
  private static readonly PARENT_CRUMBS: Record<string, Breadcrumb[]> = {
    'claims': [
      { label: 'Claims', url: '/claims/history', isCurrentPage: false }
    ],
    'documents': [
      { label: 'Documents', url: '/documents/upload', isCurrentPage: false }
    ],
    'fraud/correlations': [
      { label: 'Fraud Alerts', url: '/dashboard/fraud', isCurrentPage: false }
    ],
  };

  constructor() {
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.updateBreadcrumbs();
    });
  }

  /** Rebuild the breadcrumb trail from the current activated route tree. */
  private updateBreadcrumbs(): void {
    const url = this.router.url.split('?')[0]; // strip query params

    // Landing page, login, and root — no breadcrumbs
    if (url === '/' || url === '/login' || url === '') {
      this._breadcrumbs.set([]);
      return;
    }

    // Dashboard alone — single crumb, hidden by isVisible computed
    if (url === '/dashboard') {
      this._breadcrumbs.set([
        { label: 'Dashboard', url: '/dashboard', isCurrentPage: true }
      ]);
      return;
    }

    const crumbs: Breadcrumb[] = [];

    // Home crumb always links to /dashboard
    crumbs.push({ label: 'Dashboard', url: '/dashboard', isCurrentPage: false });

    // Get the leaf route data for the breadcrumb label
    const leafRoute = this.getLeafRoute(this.activatedRoute);
    const routeData: Data = leafRoute.snapshot.data;
    const breadcrumbLabel = routeData['breadcrumb'] as string | undefined;
    const params = leafRoute.snapshot.params;

    // Add intermediate parent crumbs based on URL prefix
    this.addParentCrumbs(url, crumbs);

    // Add the current page crumb
    if (breadcrumbLabel) {
      let label = breadcrumbLabel;
      // Substitute dynamic parameters (e.g., :id, :claimId)
      label = this.substituteParams(label, params);
      crumbs.push({ label, url, isCurrentPage: true });
    } else {
      // Fallback: use last URL segment, title-cased
      const segments = url.split('/').filter(Boolean);
      const lastSegment = segments[segments.length - 1];
      crumbs.push({
        label: this.titleCase(lastSegment),
        url,
        isCurrentPage: true
      });
    }

    this._breadcrumbs.set(crumbs);
  }

  /** Walk the activated route tree to find the deepest child. */
  private getLeafRoute(route: ActivatedRoute): ActivatedRoute {
    let current = route;
    while (current.firstChild) {
      current = current.firstChild;
    }
    return current;
  }

  /** Insert logical parent crumbs for URL prefixes that need intermediate breadcrumbs. */
  private addParentCrumbs(url: string, crumbs: Breadcrumb[]): void {
    // Check longest prefix first for specificity
    const prefixes = Object.keys(BreadcrumbService.PARENT_CRUMBS)
      .sort((a, b) => b.length - a.length);

    for (const prefix of prefixes) {
      if (url.startsWith('/' + prefix + '/') || url === '/' + prefix) {
        const parents = BreadcrumbService.PARENT_CRUMBS[prefix];
        for (const parent of parents) {
          crumbs.push({ ...parent });
        }
        break; // Only match the most specific prefix
      }
    }
  }

  /** Replace parameter placeholders like :id or :claimId with actual values. */
  private substituteParams(label: string, params: Record<string, string>): string {
    let result = label;
    for (const [key, value] of Object.entries(params)) {
      result = result.replace(`:${key}`, value);
    }
    return result;
  }

  /** Convert a URL segment to title case (e.g., "fraud" -> "Fraud"). */
  private titleCase(segment: string): string {
    return segment
      .split('-')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ');
  }
}
