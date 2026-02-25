import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { FraudAlertsComponent } from './fraud-alerts';
import { ClaimsService } from '../../services/claims.service';
import { ClaimTriageResponse } from '../../models/claims.model';
import { of, throwError, Subject } from 'rxjs';

describe('FraudAlertsComponent', () => {
  let component: FraudAlertsComponent;
  let fixture: ComponentFixture<FraudAlertsComponent>;
  let claimsService: ClaimsService;

  const mockAlerts: ClaimTriageResponse[] = [
    {
      claimId: 10, severity: 'Critical', urgency: 'Emergency', claimType: 'Structure Fire',
      fraudScore: 82, fraudRiskLevel: 'VeryHigh', estimatedLossRange: '$100K-$500K',
      recommendedActions: [{ action: 'Refer to SIU', priority: 'Critical', reasoning: 'Multiple fraud indicators' }],
      fraudFlags: ['Timing anomaly', 'Financial motive', 'Inconsistent documentation'],
      evidence: [], status: 'UnderReview', createdAt: '2026-02-24T08:00:00Z'
    },
    {
      claimId: 15, severity: 'High', urgency: 'Priority', claimType: 'Theft',
      fraudScore: 65, fraudRiskLevel: 'High', estimatedLossRange: '$10K-$25K',
      recommendedActions: [], fraudFlags: ['Pattern match with known fraud ring'],
      evidence: [], status: 'UnderReview', createdAt: '2026-02-23T14:00:00Z'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FraudAlertsComponent, HttpClientTestingModule, RouterTestingModule]
    }).compileComponents();

    fixture = TestBed.createComponent(FraudAlertsComponent);
    component = fixture.componentInstance;
    claimsService = TestBed.inject(ClaimsService);
  });

  it('should create', () => {
    vi.spyOn(claimsService, 'getFraudAlerts').mockReturnValue(of(mockAlerts));
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should load and sort fraud alerts by score (descending)', () => {
    vi.spyOn(claimsService, 'getFraudAlerts').mockReturnValue(of(mockAlerts));
    fixture.detectChanges();

    expect(component.alerts().length).toBe(2);
    expect(component.alerts()[0].fraudScore).toBe(82);
    expect(component.alerts()[1].fraudScore).toBe(65);
    expect(component.isLoading()).toBe(false);
  });

  it('should calculate risk counts correctly', () => {
    vi.spyOn(claimsService, 'getFraudAlerts').mockReturnValue(of(mockAlerts));
    fixture.detectChanges();

    expect(component.getCriticalCount()).toBe(0);  // score >= 85 (CRITICAL_THRESHOLD) — only 82 in mock, below 85
    expect(component.getHighCount()).toBe(1);       // score 55-74
    expect(component.getAvgScore()).toBe(74);       // (82+65)/2 = 73.5 → 74
    expect(component.getSIUReferralCount()).toBe(1); // score >= 75 (SIU_THRESHOLD)
  });

  it('should identify SIU referrals', () => {
    expect(component.isSIUReferral(mockAlerts[0])).toBe(true);   // score 82
    expect(component.isSIUReferral(mockAlerts[1])).toBe(false);  // score 65
  });

  it('should return correct fraud score colors', () => {
    expect(component.getFraudScoreColor(80)).toBe('text-rose-400');
    expect(component.getFraudScoreColor(60)).toBe('text-orange-400');
    expect(component.getFraudScoreColor(40)).toBe('text-amber-400');
  });

  it('should return correct risk badge classes', () => {
    expect(component.getRiskBadgeClass('VeryHigh')).toBe('badge-danger');
    expect(component.getRiskBadgeClass('High')).toContain('orange');
    expect(component.getRiskBadgeClass('Medium')).toBe('badge-warning');
  });

  it('should handle error loading alerts', () => {
    vi.spyOn(claimsService, 'getFraudAlerts').mockReturnValue(throwError(() => new Error('Network error')));
    fixture.detectChanges();

    expect(component.error()).toBeTruthy();
    expect(component.isLoading()).toBe(false);
  });

  it('should show empty state when no alerts', () => {
    vi.spyOn(claimsService, 'getFraudAlerts').mockReturnValue(of([]));
    fixture.detectChanges();

    expect(component.alerts().length).toBe(0);
    expect(component.isLoading()).toBe(false);
  });

  it('should run deep analysis on a claim', () => {
    vi.spyOn(claimsService, 'getFraudAlerts').mockReturnValue(of(mockAlerts));
    const analyzeSpy = vi.spyOn(claimsService, 'analyzeFraud').mockReturnValue(of({
      claimId: 10, fraudScore: 85, riskLevel: 'VeryHigh',
      indicators: [], recommendedActions: [],
      referToSIU: true, siuReferralReason: 'High risk', confidence: 0.9
    }));
    fixture.detectChanges();

    component.runDeepAnalysis(10);

    expect(analyzeSpy).toHaveBeenCalledWith(10);
    expect(component.analyzingClaimIds().has(10)).toBe(false); // removed after completion
  });

  it('should track multiple concurrent deep analyses', () => {
    vi.spyOn(claimsService, 'getFraudAlerts').mockReturnValue(of(mockAlerts));

    const subject1 = new Subject<any>();
    const subject2 = new Subject<any>();

    vi.spyOn(claimsService, 'analyzeFraud')
      .mockReturnValueOnce(subject1.asObservable())
      .mockReturnValueOnce(subject2.asObservable());
    fixture.detectChanges();

    component.runDeepAnalysis(10);
    component.runDeepAnalysis(15);

    // Both should be in the analyzing set
    expect(component.analyzingClaimIds().has(10)).toBe(true);
    expect(component.analyzingClaimIds().has(15)).toBe(true);

    // Complete first analysis
    subject1.next({});
    subject1.complete();

    // Only 10 should be removed
    expect(component.analyzingClaimIds().has(10)).toBe(false);
    expect(component.analyzingClaimIds().has(15)).toBe(true);

    // Complete second analysis
    subject2.next({});
    subject2.complete();

    expect(component.analyzingClaimIds().has(15)).toBe(false);
  });

  it('should set error message when deep analysis fails', () => {
    vi.spyOn(claimsService, 'getFraudAlerts').mockReturnValue(of(mockAlerts));
    vi.spyOn(claimsService, 'analyzeFraud').mockReturnValue(throwError(() => new Error('Analysis failed')));
    fixture.detectChanges();

    component.runDeepAnalysis(10);

    expect(component.analyzingClaimIds().has(10)).toBe(false);
    expect(component.error()).toBe('Deep analysis failed for claim #10. Please try again.');
  });

  it('should return correct fraud bar class', () => {
    expect(component.getFraudBarClass(80)).toContain('rose');
    expect(component.getFraudBarClass(60)).toContain('orange');
    expect(component.getFraudBarClass(40)).toContain('amber');
  });

  it('should return correct severity color', () => {
    expect(component.getSeverityColor('Critical')).toBe('text-rose-400');
    expect(component.getSeverityColor('High')).toBe('text-orange-400');
    expect(component.getSeverityColor('Medium')).toBe('text-amber-400');
    expect(component.getSeverityColor('Low')).toBe('text-emerald-400');
  });

  it('should return correct flag category class', () => {
    expect(component.getFlagCategoryClass('Timing anomaly')).toContain('blue');
    expect(component.getFlagCategoryClass('Behavioral pattern')).toContain('purple');
    expect(component.getFlagCategoryClass('Financial motive')).toContain('rose');
    expect(component.getFlagCategoryClass('Document issue')).toContain('slate');
    expect(component.getFlagCategoryClass('Unknown flag')).toContain('amber');
  });
});
