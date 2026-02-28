import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Component } from '@angular/core';
import { BreadcrumbService } from './breadcrumb.service';

/** Minimal stub component for test routes. */
@Component({ standalone: true, template: '' })
class StubComponent {}

/** Test routes matching the real app route structure with breadcrumb data. */
const TEST_ROUTES = [
  { path: '', component: StubComponent },
  { path: 'login', component: StubComponent },
  { path: 'dashboard', component: StubComponent, data: { breadcrumb: 'Dashboard' } },
  { path: 'sentiment', component: StubComponent, data: { breadcrumb: 'Sentiment Analysis' } },
  { path: 'insurance', component: StubComponent, data: { breadcrumb: 'Insurance Analysis' } },
  { path: 'claims/triage', component: StubComponent, data: { breadcrumb: 'New Triage' } },
  { path: 'claims/history', component: StubComponent, data: { breadcrumb: 'History' } },
  { path: 'claims/:id', component: StubComponent, data: { breadcrumb: 'Claim :id' } },
  { path: 'dashboard/providers', component: StubComponent, data: { breadcrumb: 'Provider Health' } },
  { path: 'dashboard/fraud', component: StubComponent, data: { breadcrumb: 'Fraud Alerts' } },
  { path: 'documents/upload', component: StubComponent, data: { breadcrumb: 'Upload' } },
  { path: 'documents/query', component: StubComponent, data: { breadcrumb: 'Query' } },
  { path: 'documents/:id', component: StubComponent, data: { breadcrumb: 'Document :id' } },
  { path: 'cx/copilot', component: StubComponent, data: { breadcrumb: 'CX Copilot' } },
  { path: 'fraud/correlations/:claimId', component: StubComponent, data: { breadcrumb: 'Correlations' } },
];

describe('BreadcrumbService', () => {
  let service: BreadcrumbService;
  let router: Router;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(TEST_ROUTES),
        BreadcrumbService,
      ],
    });
    service = TestBed.inject(BreadcrumbService);
    router = TestBed.inject(Router);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should return empty breadcrumbs on landing page', async () => {
    await router.navigateByUrl('/');
    expect(service.breadcrumbs()).toEqual([]);
    expect(service.isVisible()).toBe(false);
  });

  it('should return empty breadcrumbs on login page', async () => {
    await router.navigateByUrl('/login');
    expect(service.breadcrumbs()).toEqual([]);
    expect(service.isVisible()).toBe(false);
  });

  it('should return single Dashboard crumb on /dashboard (not visible)', async () => {
    await router.navigateByUrl('/dashboard');
    expect(service.breadcrumbs().length).toBe(1);
    expect(service.breadcrumbs()[0].label).toBe('Dashboard');
    expect(service.breadcrumbs()[0].isCurrentPage).toBe(true);
    expect(service.isVisible()).toBe(false);
  });

  it('should show "Dashboard / Sentiment Analysis" on /sentiment', async () => {
    await router.navigateByUrl('/sentiment');
    const crumbs = service.breadcrumbs();
    expect(crumbs.length).toBe(2);
    expect(crumbs[0]).toEqual({ label: 'Dashboard', url: '/dashboard', isCurrentPage: false });
    expect(crumbs[1]).toEqual({ label: 'Sentiment Analysis', url: '/sentiment', isCurrentPage: true });
    expect(service.isVisible()).toBe(true);
  });

  it('should show "Dashboard / Insurance Analysis" on /insurance', async () => {
    await router.navigateByUrl('/insurance');
    const crumbs = service.breadcrumbs();
    expect(crumbs.length).toBe(2);
    expect(crumbs[0].label).toBe('Dashboard');
    expect(crumbs[1].label).toBe('Insurance Analysis');
    expect(crumbs[1].isCurrentPage).toBe(true);
  });

  it('should show "Dashboard / Claims / New Triage" on /claims/triage', async () => {
    await router.navigateByUrl('/claims/triage');
    const crumbs = service.breadcrumbs();
    expect(crumbs.length).toBe(3);
    expect(crumbs[0]).toEqual({ label: 'Dashboard', url: '/dashboard', isCurrentPage: false });
    expect(crumbs[1]).toEqual({ label: 'Claims', url: '/claims/history', isCurrentPage: false });
    expect(crumbs[2]).toEqual({ label: 'New Triage', url: '/claims/triage', isCurrentPage: true });
  });

  it('should show "Dashboard / Claims / History" on /claims/history', async () => {
    await router.navigateByUrl('/claims/history');
    const crumbs = service.breadcrumbs();
    expect(crumbs.length).toBe(3);
    expect(crumbs[0]).toEqual({ label: 'Dashboard', url: '/dashboard', isCurrentPage: false });
    expect(crumbs[1]).toEqual({ label: 'Claims', url: '/claims/history', isCurrentPage: false });
    expect(crumbs[2]).toEqual({ label: 'History', url: '/claims/history', isCurrentPage: true });
  });

  it('should show "Dashboard / Provider Health" on /dashboard/providers', async () => {
    await router.navigateByUrl('/dashboard/providers');
    const crumbs = service.breadcrumbs();
    expect(crumbs.length).toBe(2);
    expect(crumbs[0]).toEqual({ label: 'Dashboard', url: '/dashboard', isCurrentPage: false });
    expect(crumbs[1]).toEqual({ label: 'Provider Health', url: '/dashboard/providers', isCurrentPage: true });
  });

  it('should show "Dashboard / Fraud Alerts" on /dashboard/fraud', async () => {
    await router.navigateByUrl('/dashboard/fraud');
    const crumbs = service.breadcrumbs();
    expect(crumbs.length).toBe(2);
    expect(crumbs[0]).toEqual({ label: 'Dashboard', url: '/dashboard', isCurrentPage: false });
    expect(crumbs[1]).toEqual({ label: 'Fraud Alerts', url: '/dashboard/fraud', isCurrentPage: true });
  });

  it('should show "Dashboard / Documents / Upload" on /documents/upload', async () => {
    await router.navigateByUrl('/documents/upload');
    const crumbs = service.breadcrumbs();
    expect(crumbs.length).toBe(3);
    expect(crumbs[0]).toEqual({ label: 'Dashboard', url: '/dashboard', isCurrentPage: false });
    expect(crumbs[1]).toEqual({ label: 'Documents', url: '/documents/upload', isCurrentPage: false });
    // Upload is the current page even though its URL matches the Documents parent link
    expect(crumbs[2]).toEqual({ label: 'Upload', url: '/documents/upload', isCurrentPage: true });
  });

  it('should show "Dashboard / Documents / Query" on /documents/query', async () => {
    await router.navigateByUrl('/documents/query');
    const crumbs = service.breadcrumbs();
    expect(crumbs.length).toBe(3);
    expect(crumbs[0].label).toBe('Dashboard');
    expect(crumbs[1].label).toBe('Documents');
    expect(crumbs[2]).toEqual({ label: 'Query', url: '/documents/query', isCurrentPage: true });
  });

  it('should show "Dashboard / CX Copilot" on /cx/copilot', async () => {
    await router.navigateByUrl('/cx/copilot');
    const crumbs = service.breadcrumbs();
    expect(crumbs.length).toBe(2);
    expect(crumbs[0]).toEqual({ label: 'Dashboard', url: '/dashboard', isCurrentPage: false });
    expect(crumbs[1]).toEqual({ label: 'CX Copilot', url: '/cx/copilot', isCurrentPage: true });
  });

  it('should show "Dashboard / Fraud Alerts / Correlations" on /fraud/correlations/:claimId', async () => {
    await router.navigateByUrl('/fraud/correlations/101');
    const crumbs = service.breadcrumbs();
    expect(crumbs.length).toBe(3);
    expect(crumbs[0]).toEqual({ label: 'Dashboard', url: '/dashboard', isCurrentPage: false });
    expect(crumbs[1]).toEqual({ label: 'Fraud Alerts', url: '/dashboard/fraud', isCurrentPage: false });
    expect(crumbs[2]).toEqual({ label: 'Correlations', url: '/fraud/correlations/101', isCurrentPage: true });
  });

  it('should substitute dynamic :id parameter in claim route', async () => {
    await router.navigateByUrl('/claims/CLM-2024-001');
    const crumbs = service.breadcrumbs();
    expect(crumbs.length).toBe(3);
    expect(crumbs[2].label).toBe('Claim CLM-2024-001');
    expect(crumbs[2].isCurrentPage).toBe(true);
  });

  it('should substitute dynamic :id parameter in document route', async () => {
    await router.navigateByUrl('/documents/501');
    const crumbs = service.breadcrumbs();
    expect(crumbs.length).toBe(3);
    expect(crumbs[2].label).toBe('Document 501');
    expect(crumbs[2].isCurrentPage).toBe(true);
  });

  it('should always include home crumb linking to /dashboard for authenticated routes', async () => {
    await router.navigateByUrl('/claims/triage');
    expect(service.breadcrumbs()[0]).toEqual({ label: 'Dashboard', url: '/dashboard', isCurrentPage: false });

    await router.navigateByUrl('/documents/query');
    expect(service.breadcrumbs()[0]).toEqual({ label: 'Dashboard', url: '/dashboard', isCurrentPage: false });

    await router.navigateByUrl('/fraud/correlations/101');
    expect(service.breadcrumbs()[0]).toEqual({ label: 'Dashboard', url: '/dashboard', isCurrentPage: false });
  });

  it('should mark only the last crumb as current page', async () => {
    await router.navigateByUrl('/claims/triage');
    const crumbs = service.breadcrumbs();
    expect(crumbs[0].isCurrentPage).toBe(false);
    expect(crumbs[1].isCurrentPage).toBe(false);
    expect(crumbs[2].isCurrentPage).toBe(true);
  });

  it('should update breadcrumbs on navigation change', async () => {
    await router.navigateByUrl('/dashboard');
    expect(service.breadcrumbs().length).toBe(1);

    await router.navigateByUrl('/claims/triage');
    expect(service.breadcrumbs().length).toBe(3);
    expect(service.breadcrumbs()[2].label).toBe('New Triage');

    await router.navigateByUrl('/dashboard/fraud');
    expect(service.breadcrumbs().length).toBe(2);
    expect(service.breadcrumbs()[1].label).toBe('Fraud Alerts');
  });

  it('should strip query parameters from URL', async () => {
    await router.navigateByUrl('/claims/history?severity=High&page=1');
    const crumbs = service.breadcrumbs();
    // Should still produce correct breadcrumbs without query params in url
    expect(crumbs[crumbs.length - 1].url).toBe('/claims/history');
  });
});
