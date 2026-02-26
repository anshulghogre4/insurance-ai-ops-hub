import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { FraudCorrelationService } from './fraud-correlation.service';
import { CorrelateResult, FraudCorrelationResponse, ReviewCorrelationRequest } from '../models/document.model';
import { PaginatedResponse } from '../models/claims.model';

describe('FraudCorrelationService', () => {
  let service: FraudCorrelationService;
  let httpMock: HttpTestingController;

  const baseUrl = 'http://localhost:5143/api/insurance/fraud';

  const mockCorrelation: FraudCorrelationResponse = {
    id: 501,
    sourceClaimId: 42,
    correlatedClaimId: 78,
    correlationType: 'SimilarNarrative',
    correlationTypes: ['SimilarNarrative', 'DateProximity'],
    correlationScore: 0.87,
    details: 'Both claims describe rear-end collision at same intersection within 14-day window. Matching body shop referenced in both repair estimates.',
    sourceClaimSeverity: 'High',
    sourceClaimType: 'Auto',
    sourceFraudScore: 68,
    correlatedClaimSeverity: 'Medium',
    correlatedClaimType: 'Auto',
    correlatedFraudScore: 72,
    detectedAt: '2026-02-25T16:45:00Z',
    status: 'Pending',
    reviewedBy: null,
    reviewedAt: null,
    dismissalReason: null
  };

  const mockCorrelateResult: CorrelateResult = {
    claimId: 42,
    correlations: [mockCorrelation],
    count: 1
  };

  const mockPaginatedCorrelations: PaginatedResponse<FraudCorrelationResponse> = {
    items: [mockCorrelation],
    totalCount: 1,
    page: 1,
    pageSize: 20,
    totalPages: 1
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        FraudCorrelationService
      ]
    });
    service = TestBed.inject(FraudCorrelationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should trigger correlation analysis for a claim', () => {
    service.correlate(42).subscribe(res => {
      expect(res.claimId).toBe(42);
      expect(res.count).toBe(1);
      expect(res.correlations.length).toBe(1);
      expect(res.correlations[0].correlationType).toBe('SimilarNarrative');
      expect(res.correlations[0].correlationScore).toBe(0.87);
    });

    const req = httpMock.expectOne(`${baseUrl}/correlate`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ claimId: 42 });
    req.flush(mockCorrelateResult);
  });

  it('should get paginated correlations for a claim', () => {
    service.getCorrelations(42, 1, 10).subscribe(res => {
      expect(res.items.length).toBe(1);
      expect(res.totalCount).toBe(1);
      expect(res.items[0].sourceClaimId).toBe(42);
      expect(res.items[0].correlatedClaimId).toBe(78);
      expect(res.items[0].correlationTypes).toContain('DateProximity');
    });

    const req = httpMock.expectOne(r => r.url === `${baseUrl}/correlations/42`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('10');
    req.flush(mockPaginatedCorrelations);
  });

  it('should review (PATCH) a correlation', () => {
    const reviewRequest: ReviewCorrelationRequest = {
      status: 'Confirmed',
      reviewedBy: 'adjuster.martinez@insureco.com'
    };

    const mockReviewResponse = {
      id: 501,
      status: 'Confirmed',
      message: 'Correlation 501 updated to Confirmed'
    };

    service.reviewCorrelation(501, reviewRequest).subscribe(res => {
      expect(res.id).toBe(501);
      expect(res.status).toBe('Confirmed');
      expect(res.message).toContain('Confirmed');
    });

    const req = httpMock.expectOne(`${baseUrl}/correlations/501/review`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual(reviewRequest);
    req.flush(mockReviewResponse);
  });

  it('should delete correlations for a claim', () => {
    service.deleteCorrelations(42).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/correlations/42`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('should handle error on correlate', () => {
    service.correlate(9999).subscribe({
      next: () => { throw new Error('should have failed'); },
      error: (error) => {
        expect(error.status).toBe(404);
      }
    });

    const req = httpMock.expectOne(`${baseUrl}/correlate`);
    req.flush(
      { error: 'Claim 9999 not found' },
      { status: 404, statusText: 'Not Found' }
    );
  });
});
