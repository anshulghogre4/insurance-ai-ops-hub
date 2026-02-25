import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ClaimResultComponent } from './claim-result';
import { ClaimsService } from '../../services/claims.service';
import { ClaimTriageResponse, FraudAnalysisResponse } from '../../models/claims.model';

describe('ClaimResultComponent', () => {
  let component: ClaimResultComponent;
  let fixture: ComponentFixture<ClaimResultComponent>;
  let claimsService: ClaimsService;

  const mockClaim: ClaimTriageResponse = {
    claimId: 42,
    severity: 'High',
    urgency: 'Immediate',
    claimType: 'Auto Collision',
    fraudScore: 38,
    fraudRiskLevel: 'Low',
    estimatedLossRange: '$8,000 - $20,000',
    recommendedActions: [
      { action: 'Request police report', priority: 'High', reasoning: 'Standard procedure for collision claims' },
      { action: 'Schedule vehicle inspection', priority: 'Medium', reasoning: 'Assess extent of damages' }
    ],
    fraudFlags: [],
    evidence: [
      { evidenceType: 'image', provider: 'Azure Vision', processedText: 'Front bumper damage, airbag deployed', damageIndicators: ['bumper damage', 'airbag deployment'], createdAt: '2026-02-24T10:05:00Z' }
    ],
    status: 'Triaged',
    createdAt: '2026-02-24T10:00:00Z'
  };

  const mockFraudResult: FraudAnalysisResponse = {
    claimId: 42,
    fraudScore: 28,
    riskLevel: 'Low',
    indicators: [
      { category: 'Documentation', description: 'All documentation appears consistent', severity: 'Low' }
    ],
    recommendedActions: [],
    referToSIU: false,
    siuReferralReason: '',
    confidence: 0.92
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClaimResultComponent, HttpClientTestingModule, RouterTestingModule],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => key === 'id' ? '42' : null
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ClaimResultComponent);
    component = fixture.componentInstance;
    claimsService = TestBed.inject(ClaimsService);
  });

  it('should create', () => {
    vi.spyOn(claimsService, 'getClaimById').mockReturnValue(of(mockClaim));
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should load claim data on init', () => {
    vi.spyOn(claimsService, 'getClaimById').mockReturnValue(of(mockClaim));
    fixture.detectChanges();

    expect(component.claim()).toBeTruthy();
    expect(component.claim()!.claimId).toBe(42);
    expect(component.claim()!.severity).toBe('High');
    expect(component.isLoading()).toBe(false);
  });

  it('should handle claim not found', () => {
    vi.spyOn(claimsService, 'getClaimById').mockReturnValue(throwError(() => ({ status: 404 })));
    fixture.detectChanges();

    expect(component.error()).toBeTruthy();
    expect(component.isLoading()).toBe(false);
  });

  it('should run fraud analysis', () => {
    vi.spyOn(claimsService, 'getClaimById').mockReturnValue(of(mockClaim));
    vi.spyOn(claimsService, 'analyzeFraud').mockReturnValue(of(mockFraudResult));
    fixture.detectChanges();

    component.runFraudAnalysis();

    expect(component.fraudResult()).toBeTruthy();
    expect(component.fraudResult()!.fraudScore).toBe(28);
    expect(component.fraudResult()!.referToSIU).toBe(false);
  });

  it('should return correct severity class', () => {
    expect(component.getSeverityClass('Critical')).toContain('rose');
    expect(component.getSeverityClass('High')).toContain('orange');
    expect(component.getSeverityClass('Medium')).toContain('amber');
    expect(component.getSeverityClass('Low')).toContain('emerald');
  });

  it('should return correct fraud color based on score', () => {
    expect(component.getFraudScoreColor(80)).toBe('text-rose-400');
    expect(component.getFraudScoreColor(60)).toBe('text-orange-400');
    expect(component.getFraudScoreColor(40)).toBe('text-amber-400');
    expect(component.getFraudScoreColor(15)).toBe('text-emerald-400');
  });

  it('should return correct gauge gradient based on score', () => {
    expect(component.getGaugeGradient(80)).toContain('#ef4444');
    expect(component.getGaugeGradient(60)).toContain('#f97316');
    expect(component.getGaugeGradient(40)).toContain('#eab308');
    expect(component.getGaugeGradient(15)).toContain('#22c55e');
  });

  it('should display evidence from claim', () => {
    vi.spyOn(claimsService, 'getClaimById').mockReturnValue(of(mockClaim));
    fixture.detectChanges();

    expect(component.claim()!.evidence.length).toBe(1);
    expect(component.claim()!.evidence[0].provider).toBe('Azure Vision');
  });

  it('should toggle action expansion', () => {
    expect(component.expandedActions()).toEqual([]);
    component.toggleAction(0);
    expect(component.expandedActions()).toContain(0);
    component.toggleAction(0);
    expect(component.expandedActions()).not.toContain(0);
  });

  it('should format date string', () => {
    const result = component.formatDate('2026-02-24T10:00:00Z');
    expect(result).toBeTruthy();
    expect(result.length).toBeGreaterThan(0);
  });
});
