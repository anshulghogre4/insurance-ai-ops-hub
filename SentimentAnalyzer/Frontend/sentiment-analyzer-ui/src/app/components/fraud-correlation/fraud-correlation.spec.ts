import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { FraudCorrelationComponent } from './fraud-correlation';
import { FraudCorrelationService } from '../../services/fraud-correlation.service';
import { FraudCorrelationResponse } from '../../models/document.model';

describe('FraudCorrelationComponent', () => {
  let fixture: ComponentFixture<FraudCorrelationComponent>;
  let component: FraudCorrelationComponent;

  const mockCorrelations: FraudCorrelationResponse[] = [
    {
      id: 1,
      sourceClaimId: 101,
      correlatedClaimId: 204,
      correlationType: 'DateProximity',
      correlationTypes: ['DateProximity', 'SimilarNarrative'],
      correlationScore: 0.82,
      details: 'Both claims filed within 14 days involving rear-end collisions on the same highway corridor. Narrative similarity score: 0.78.',
      sourceClaimSeverity: 'High',
      sourceClaimType: 'Auto',
      sourceFraudScore: 65,
      correlatedClaimSeverity: 'Medium',
      correlatedClaimType: 'Auto',
      correlatedFraudScore: 71,
      detectedAt: '2026-02-18T09:30:00Z',
      status: 'Pending',
      reviewedBy: null,
      reviewedAt: null,
      dismissalReason: null
    },
    {
      id: 2,
      sourceClaimId: 101,
      correlatedClaimId: 312,
      correlationType: 'SharedFlags',
      correlationTypes: ['SharedFlags', 'SameSeverity'],
      correlationScore: 0.58,
      details: 'Both claims share suspicious medical provider Dr. Martinez and reported identical soft-tissue injuries. Same severity classification.',
      sourceClaimSeverity: 'High',
      sourceClaimType: 'Auto',
      sourceFraudScore: 65,
      correlatedClaimSeverity: 'High',
      correlatedClaimType: 'Auto',
      correlatedFraudScore: 44,
      detectedAt: '2026-02-19T14:15:00Z',
      status: 'Confirmed',
      reviewedBy: 'Analyst',
      reviewedAt: '2026-02-20T11:00:00Z',
      dismissalReason: null
    },
    {
      id: 3,
      sourceClaimId: 101,
      correlatedClaimId: 415,
      correlationType: 'DateProximity',
      correlationTypes: ['DateProximity'],
      correlationScore: 0.39,
      details: 'Claims filed within 45-day window. Low narrative overlap. Different geographic regions.',
      sourceClaimSeverity: 'High',
      sourceClaimType: 'Auto',
      sourceFraudScore: 65,
      correlatedClaimSeverity: 'Low',
      correlatedClaimType: 'Property',
      correlatedFraudScore: 22,
      detectedAt: '2026-02-21T08:00:00Z',
      status: 'Dismissed',
      reviewedBy: 'Senior Adjuster',
      reviewedAt: '2026-02-22T16:30:00Z',
      dismissalReason: 'Claims involve different policy types and geographic areas. No meaningful pattern.'
    }
  ];

  const mockFraudService = {
    getCorrelations: vi.fn(),
    correlate: vi.fn(),
    reviewCorrelation: vi.fn()
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    mockFraudService.getCorrelations.mockReturnValue(
      of({ items: mockCorrelations, totalCount: 3, page: 1, pageSize: 20, totalPages: 1 })
    );

    await TestBed.configureTestingModule({
      imports: [FraudCorrelationComponent],
      providers: [
        provideRouter([]),
        { provide: FraudCorrelationService, useValue: mockFraudService },
        {
          provide: ActivatedRoute,
          useValue: {
            params: of({ claimId: '101' }),
            snapshot: { paramMap: { get: () => '101' } }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FraudCorrelationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
    expect(component.claimId()).toBe(101);
    expect(component.error()).toBeNull();
    expect(component.activeTab()).toBe('All');
  });

  it('should load correlations on init', () => {
    expect(mockFraudService.getCorrelations).toHaveBeenCalledWith(101);
    expect(component.correlations().length).toBe(3);
    expect(component.isLoading()).toBe(false);
    expect(component.correlations()[0].sourceClaimId).toBe(101);
    expect(component.correlations()[0].correlatedClaimId).toBe(204);
  });

  it('should filter correlations by status tab', () => {
    // Default: All
    expect(component.filteredCorrelations().length).toBe(3);

    // Filter to Pending
    component.activeTab.set('Pending');
    expect(component.filteredCorrelations().length).toBe(1);
    expect(component.filteredCorrelations()[0].status).toBe('Pending');
    expect(component.filteredCorrelations()[0].correlatedClaimId).toBe(204);

    // Filter to Confirmed
    component.activeTab.set('Confirmed');
    expect(component.filteredCorrelations().length).toBe(1);
    expect(component.filteredCorrelations()[0].status).toBe('Confirmed');

    // Filter to Dismissed
    component.activeTab.set('Dismissed');
    expect(component.filteredCorrelations().length).toBe(1);
    expect(component.filteredCorrelations()[0].dismissalReason).toContain('different policy types');

    // Back to All
    component.activeTab.set('All');
    expect(component.filteredCorrelations().length).toBe(3);
  });

  it('should open dismiss modal for a correlation', () => {
    expect(component.dismissTarget()).toBeNull();
    expect(component.dismissReason).toBe('');

    const pendingCorrelation = component.correlations()[0];
    component.openDismissModal(pendingCorrelation);

    expect(component.dismissTarget()).toEqual(pendingCorrelation);
    expect(component.dismissTarget()!.id).toBe(1);
    expect(component.dismissReason).toBe('');

    // Close the modal
    component.closeDismissModal();
    expect(component.dismissTarget()).toBeNull();
  });
});
