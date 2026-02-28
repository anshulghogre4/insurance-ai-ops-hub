import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { BreadcrumbComponent } from './breadcrumb';
import { BreadcrumbService } from '../../services/breadcrumb.service';

/** Minimal stub component for test routes. */
@Component({ standalone: true, template: '' })
class StubComponent {}

const TEST_ROUTES = [
  { path: '', component: StubComponent },
  { path: 'dashboard', component: StubComponent, data: { breadcrumb: 'Dashboard' } },
  { path: 'claims/triage', component: StubComponent, data: { breadcrumb: 'New Triage' } },
  { path: 'claims/history', component: StubComponent, data: { breadcrumb: 'History' } },
  { path: 'dashboard/fraud', component: StubComponent, data: { breadcrumb: 'Fraud Alerts' } },
  { path: 'dashboard/providers', component: StubComponent, data: { breadcrumb: 'Provider Health' } },
  { path: 'sentiment', component: StubComponent, data: { breadcrumb: 'Sentiment Analysis' } },
  { path: 'fraud/correlations/:claimId', component: StubComponent, data: { breadcrumb: 'Correlations' } },
];

describe('BreadcrumbComponent', () => {
  let component: BreadcrumbComponent;
  let fixture: ComponentFixture<BreadcrumbComponent>;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BreadcrumbComponent],
      providers: [
        provideRouter(TEST_ROUTES),
        BreadcrumbService,
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(BreadcrumbComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should not render breadcrumb nav on landing page', async () => {
    await router.navigateByUrl('/');
    fixture.detectChanges();

    const nav = fixture.nativeElement.querySelector('nav[aria-label="Breadcrumb"]');
    expect(nav).toBeNull();
  });

  it('should not render breadcrumb nav on dashboard alone', async () => {
    await router.navigateByUrl('/dashboard');
    fixture.detectChanges();

    const nav = fixture.nativeElement.querySelector('nav[aria-label="Breadcrumb"]');
    expect(nav).toBeNull();
  });

  it('should render breadcrumb nav with correct crumbs on /claims/triage', async () => {
    await router.navigateByUrl('/claims/triage');
    fixture.detectChanges();

    const nav = fixture.nativeElement.querySelector('nav[aria-label="Breadcrumb"]');
    expect(nav).toBeTruthy();

    const listItems = nav.querySelectorAll('li');
    expect(listItems.length).toBe(3);

    // First crumb: Dashboard (link)
    const dashboardLink = listItems[0].querySelector('a');
    expect(dashboardLink).toBeTruthy();
    expect(dashboardLink.textContent.trim()).toBe('Dashboard');

    // Second crumb: Claims (link)
    const claimsLink = listItems[1].querySelector('a');
    expect(claimsLink).toBeTruthy();
    expect(claimsLink.textContent.trim()).toBe('Claims');

    // Third crumb: New Triage (current page, no link)
    const currentPage = listItems[2].querySelector('span[aria-current="page"]');
    expect(currentPage).toBeTruthy();
    expect(currentPage.textContent.trim()).toBe('New Triage');
  });

  it('should render parent crumbs as clickable links', async () => {
    await router.navigateByUrl('/claims/triage');
    fixture.detectChanges();

    const links = fixture.nativeElement.querySelectorAll('nav a');
    expect(links.length).toBe(2); // Dashboard + Claims
    expect(links[0].textContent.trim()).toBe('Dashboard');
    expect(links[1].textContent.trim()).toBe('Claims');
  });

  it('should render current page crumb without a link', async () => {
    await router.navigateByUrl('/sentiment');
    fixture.detectChanges();

    const currentSpan = fixture.nativeElement.querySelector('span[aria-current="page"]');
    expect(currentSpan).toBeTruthy();
    expect(currentSpan.textContent.trim()).toBe('Sentiment Analysis');

    // Should not be wrapped in an <a> tag
    const parentElement = currentSpan.closest('li');
    expect(parentElement.querySelector('a')).toBeNull();
  });

  it('should have aria-label="Breadcrumb" on the nav element', async () => {
    await router.navigateByUrl('/sentiment');
    fixture.detectChanges();

    const nav = fixture.nativeElement.querySelector('nav');
    expect(nav.getAttribute('aria-label')).toBe('Breadcrumb');
  });

  it('should use an ordered list for breadcrumb items', async () => {
    await router.navigateByUrl('/sentiment');
    fixture.detectChanges();

    const ol = fixture.nativeElement.querySelector('nav ol');
    expect(ol).toBeTruthy();

    const items = ol.querySelectorAll('li');
    expect(items.length).toBeGreaterThan(0);
  });

  it('should render separator characters between crumbs', async () => {
    await router.navigateByUrl('/claims/triage');
    fixture.detectChanges();

    // Separators are <span aria-hidden="true">/</span> inside each li (except the first)
    const separators = fixture.nativeElement.querySelectorAll('span[aria-hidden="true"]');
    expect(separators.length).toBe(2); // Between Dashboard-Claims and Claims-NewTriage
    expect(separators[0].textContent).toBe('/');
  });

  it('should apply stagger animation delays to each crumb', async () => {
    await router.navigateByUrl('/claims/triage');
    fixture.detectChanges();

    const items = fixture.nativeElement.querySelectorAll('li');
    expect(items[0].style.animationDelay).toBe('0ms');
    expect(items[1].style.animationDelay).toBe('50ms');
    expect(items[2].style.animationDelay).toBe('100ms');
  });

  it('should show "Dashboard / Fraud Alerts" on /dashboard/fraud', async () => {
    await router.navigateByUrl('/dashboard/fraud');
    fixture.detectChanges();

    const items = fixture.nativeElement.querySelectorAll('li');
    expect(items.length).toBe(2);

    const dashLink = items[0].querySelector('a');
    expect(dashLink.textContent.trim()).toBe('Dashboard');

    const currentPage = items[1].querySelector('span[aria-current="page"]');
    expect(currentPage.textContent.trim()).toBe('Fraud Alerts');
  });

  it('should show "Dashboard / Fraud Alerts / Correlations" on /fraud/correlations/:claimId', async () => {
    await router.navigateByUrl('/fraud/correlations/101');
    fixture.detectChanges();

    const items = fixture.nativeElement.querySelectorAll('li');
    expect(items.length).toBe(3);

    expect(items[0].querySelector('a').textContent.trim()).toBe('Dashboard');
    expect(items[1].querySelector('a').textContent.trim()).toBe('Fraud Alerts');
    expect(items[2].querySelector('span[aria-current="page"]').textContent.trim()).toBe('Correlations');
  });
});
