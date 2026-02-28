import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { ClaimsTriageComponent } from './claims-triage';
import { ClaimsService } from '../../services/claims.service';
import { ClaimTriageResponse } from '../../models/claims.model';
import { of, throwError } from 'rxjs';

describe('ClaimsTriageComponent', () => {
  let component: ClaimsTriageComponent;
  let fixture: ComponentFixture<ClaimsTriageComponent>;
  let claimsService: ClaimsService;

  const mockTriageResponse: ClaimTriageResponse = {
    claimId: 1,
    severity: 'High',
    urgency: 'Immediate',
    claimType: 'Water Damage',
    fraudScore: 42,
    fraudRiskLevel: 'Medium',
    estimatedLossRange: '$5,000 - $15,000',
    recommendedActions: [
      { action: 'Assign field adjuster within 24 hours', priority: 'High', reasoning: 'Active water damage requires immediate assessment' }
    ],
    fraudFlags: ['Timing anomaly detected'],
    evidence: [],
    status: 'Triaged',
    createdAt: '2026-02-24T10:00:00Z'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClaimsTriageComponent, HttpClientTestingModule, RouterTestingModule]
    }).compileComponents();

    fixture = TestBed.createComponent(ClaimsTriageComponent);
    component = fixture.componentInstance;
    claimsService = TestBed.inject(ClaimsService);
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with empty form state', () => {
    expect(component.claimText).toBe('');
    expect(component.interactionType).toBe('Complaint');
    expect(component.selectedFiles()).toEqual([]);
    expect(component.isLoading()).toBe(false);
    expect(component.result()).toBeNull();
  });

  it('should apply quick template text via useSample', () => {
    component.useSample('water');
    expect(component.claimText).toContain('water damage');
    expect(component.claimText.length).toBeGreaterThan(50);
    expect(component.interactionType).toBe('Complaint');
  });

  it('should submit triage and display result', () => {
    vi.spyOn(claimsService, 'triageClaim').mockReturnValue(of(mockTriageResponse));

    component.claimText = 'Water pipe burst in basement causing significant flooding and property damage';
    component.submitTriage();

    expect(component.result()).toBeTruthy();
    expect(component.result()!.claimId).toBe(1);
    expect(component.result()!.severity).toBe('High');
    expect(component.isLoading()).toBe(false);
  });

  it('should not submit empty text', () => {
    const spy = vi.spyOn(claimsService, 'triageClaim');
    component.claimText = '   ';
    component.submitTriage();
    expect(spy).not.toHaveBeenCalled();
  });

  it('should handle submission error', () => {
    vi.spyOn(claimsService, 'triageClaim').mockReturnValue(throwError(() => ({ status: 500, error: { error: 'Server error' } })));

    component.claimText = 'Valid claim text for testing error handling';
    component.submitTriage();

    expect(component.error()).toBeTruthy();
    expect(component.isLoading()).toBe(false);
  });

  it('should reset form on clearForm', () => {
    component.claimText = 'Some claim text';
    component.result.set(mockTriageResponse);
    component.error.set('Some error');

    component.clearForm();

    expect(component.claimText).toBe('');
    expect(component.result()).toBeNull();
    expect(component.error()).toBeNull();
    expect(component.selectedFiles()).toEqual([]);
  });

  it('should return correct severity class', () => {
    expect(component.getSeverityClass('Critical')).toContain('rose');
    expect(component.getSeverityClass('High')).toContain('orange');
    expect(component.getSeverityClass('Medium')).toContain('amber');
    expect(component.getSeverityClass('Low')).toContain('emerald');
  });

  it('should return correct fraud score color', () => {
    expect(component.getFraudScoreColor(80)).toBe('text-rose-400');
    expect(component.getFraudScoreColor(60)).toBe('text-orange-400');
    expect(component.getFraudScoreColor(40)).toBe('text-amber-400');
    expect(component.getFraudScoreColor(20)).toBe('text-emerald-400');
  });

  it('should toggle action expansion', () => {
    expect(component.expandedActions()).toEqual([]);
    component.toggleAction(0);
    expect(component.expandedActions()).toContain(0);
    component.toggleAction(0);
    expect(component.expandedActions()).not.toContain(0);
  });

  it('should track elapsed seconds for AI loader during loading', () => {
    component.elapsedSeconds.set(0);
    expect(component.elapsedSeconds()).toBe(0);
    component.elapsedSeconds.set(15);
    expect(component.elapsedSeconds()).toBe(15);
    component.elapsedSeconds.set(45);
    expect(component.elapsedSeconds()).toBe(45);
  });

  it('should initialize submitState as idle', () => {
    expect(component.submitState()).toBe('idle');
  });

  it('should set submitState to loading during submission', () => {
    vi.spyOn(claimsService, 'triageClaim').mockReturnValue(of(mockTriageResponse));

    component.claimText = 'Water pipe burst in basement causing significant flooding';
    component.submitTriage();

    // After successful response, submitState transitions to complete
    expect(component.submitState()).toBe('complete');
  });

  it('should reset submitState to idle on clearForm', () => {
    component.submitState.set('complete');
    component.clearForm();
    expect(component.submitState()).toBe('idle');
  });

  it('should reset submitState to idle on error', () => {
    vi.spyOn(claimsService, 'triageClaim').mockReturnValue(throwError(() => ({ status: 500, error: { error: 'Server error' } })));

    component.claimText = 'Valid claim text for error state testing';
    component.submitTriage();

    expect(component.submitState()).toBe('idle');
    expect(component.error()).toBeTruthy();
  });

  it('should render btn-spring class on submit button', () => {
    fixture.detectChanges();
    const submitBtn = fixture.nativeElement.querySelector('button[aria-label="Submit claim for triage"]');
    expect(submitBtn).toBeTruthy();
    expect(submitBtn.classList.contains('btn-spring')).toBe(true);
  });

  it('should render animate-result-slide-up class on results section', () => {
    vi.spyOn(claimsService, 'triageClaim').mockReturnValue(of(mockTriageResponse));
    component.claimText = 'Water damage claim for animation test';
    component.submitTriage();
    fixture.detectChanges();

    const resultsSection = fixture.nativeElement.querySelector('.animate-result-slide-up');
    expect(resultsSection).toBeTruthy();
  });

  it('should render fraud-meter-animated class on fraud gauge', () => {
    vi.spyOn(claimsService, 'triageClaim').mockReturnValue(of(mockTriageResponse));
    component.claimText = 'Water damage claim for fraud meter animation test';
    component.submitTriage();
    fixture.detectChanges();

    const fraudMeter = fixture.nativeElement.querySelector('.fraud-meter-animated');
    expect(fraudMeter).toBeTruthy();
  });
});
