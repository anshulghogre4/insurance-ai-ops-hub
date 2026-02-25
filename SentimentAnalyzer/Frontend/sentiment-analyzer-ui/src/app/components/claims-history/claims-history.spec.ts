import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { ClaimsHistoryComponent } from './claims-history';
import { ClaimsService } from '../../services/claims.service';
import { ClaimTriageResponse, PaginatedResponse } from '../../models/claims.model';
import { of, throwError } from 'rxjs';

describe('ClaimsHistoryComponent', () => {
  let component: ClaimsHistoryComponent;
  let fixture: ComponentFixture<ClaimsHistoryComponent>;
  let claimsService: ClaimsService;

  const mockClaims: PaginatedResponse<ClaimTriageResponse> = {
    items: [
      {
        claimId: 1, severity: 'High', urgency: 'Immediate', claimType: 'Water Damage',
        fraudScore: 42, fraudRiskLevel: 'Medium', estimatedLossRange: '$5K-$15K',
        recommendedActions: [], fraudFlags: [], evidence: [],
        status: 'Triaged', createdAt: '2026-02-24T10:00:00Z'
      },
      {
        claimId: 2, severity: 'Low', urgency: 'Standard', claimType: 'Auto Scratch',
        fraudScore: 12, fraudRiskLevel: 'Low', estimatedLossRange: '$500-$1,500',
        recommendedActions: [], fraudFlags: [], evidence: [],
        status: 'Resolved', createdAt: '2026-02-23T14:00:00Z'
      },
      {
        claimId: 3, severity: 'Critical', urgency: 'Emergency', claimType: 'Structure Fire',
        fraudScore: 78, fraudRiskLevel: 'High', estimatedLossRange: '$50K-$200K',
        recommendedActions: [], fraudFlags: ['Timing anomaly', 'Financial motive'],
        evidence: [], status: 'UnderReview', createdAt: '2026-02-22T08:00:00Z'
      }
    ],
    totalCount: 3,
    page: 1,
    pageSize: 20,
    totalPages: 1
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClaimsHistoryComponent, HttpClientTestingModule, RouterTestingModule]
    }).compileComponents();

    fixture = TestBed.createComponent(ClaimsHistoryComponent);
    component = fixture.componentInstance;
    claimsService = TestBed.inject(ClaimsService);
  });

  it('should create', () => {
    vi.spyOn(claimsService, 'getClaimsHistory').mockReturnValue(of(mockClaims));
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should load claims on init', () => {
    vi.spyOn(claimsService, 'getClaimsHistory').mockReturnValue(of(mockClaims));
    fixture.detectChanges();

    expect(component.claims().length).toBe(3);
    expect(component.totalCount()).toBe(3);
    expect(component.isLoading()).toBe(false);
  });

  it('should apply severity filter', () => {
    const spy = vi.spyOn(claimsService, 'getClaimsHistory').mockReturnValue(of(mockClaims));
    fixture.detectChanges();

    component.filterSeverity = 'High';
    component.applyFilters();

    expect(spy).toHaveBeenCalledWith(expect.objectContaining({ severity: 'High' }));
  });

  it('should apply status filter', () => {
    const spy = vi.spyOn(claimsService, 'getClaimsHistory').mockReturnValue(of(mockClaims));
    fixture.detectChanges();

    component.filterStatus = 'Triaged';
    component.applyFilters();

    expect(spy).toHaveBeenCalledWith(expect.objectContaining({ status: 'Triaged' }));
  });

  it('should clear filters', () => {
    vi.spyOn(claimsService, 'getClaimsHistory').mockReturnValue(of(mockClaims));
    fixture.detectChanges();

    component.filterSeverity = 'High';
    component.filterStatus = 'Triaged';
    component.clearFilters();

    expect(component.filterSeverity).toBe('');
    expect(component.filterStatus).toBe('');
  });

  it('should handle pagination via nextPage', () => {
    const multiPageResponse: PaginatedResponse<ClaimTriageResponse> = {
      ...mockClaims,
      totalCount: 40,
      totalPages: 2
    };
    const spy = vi.spyOn(claimsService, 'getClaimsHistory').mockReturnValue(of(multiPageResponse));
    fixture.detectChanges();

    component.nextPage();
    expect(component.currentPage()).toBe(2);
    expect(spy).toHaveBeenCalledWith(expect.objectContaining({ page: 2 }));
  });

  it('should handle pagination via prevPage', () => {
    const multiPageResponse: PaginatedResponse<ClaimTriageResponse> = {
      ...mockClaims,
      totalCount: 40,
      totalPages: 2
    };
    vi.spyOn(claimsService, 'getClaimsHistory').mockReturnValue(of(multiPageResponse));
    fixture.detectChanges();

    component.nextPage(); // go to page 2
    component.prevPage(); // go back to page 1
    expect(component.currentPage()).toBe(1);
  });

  it('should handle error loading claims and set error message', () => {
    vi.spyOn(claimsService, 'getClaimsHistory').mockReturnValue(throwError(() => new Error('Network error')));
    fixture.detectChanges();

    expect(component.claims()).toEqual([]);
    expect(component.isLoading()).toBe(false);
    expect(component.error()).toBe('Failed to load claims history. Please try again.');
  });

  it('should clear error on successful reload after error', () => {
    const spy = vi.spyOn(claimsService, 'getClaimsHistory')
      .mockReturnValueOnce(throwError(() => new Error('Network error')));
    fixture.detectChanges();

    expect(component.error()).toBeTruthy();

    spy.mockReturnValue(of(mockClaims));
    component.loadClaims();

    expect(component.error()).toBeNull();
    expect(component.claims().length).toBe(3);
  });

  it('should return correct severity class', () => {
    expect(component.getSeverityClass('Critical')).toContain('rose');
    expect(component.getSeverityClass('High')).toContain('orange');
    expect(component.getSeverityClass('Low')).toContain('emerald');
  });

  it('should return correct fraud score color', () => {
    expect(component.getFraudColor(80)).toContain('rose');
    expect(component.getFraudColor(60)).toContain('orange');
    expect(component.getFraudColor(20)).toContain('emerald');
  });

  it('should format date string', () => {
    const result = component.formatDate('2026-02-24T10:00:00Z');
    expect(result).toBeTruthy();
    expect(result.length).toBeGreaterThan(0);
  });
});
