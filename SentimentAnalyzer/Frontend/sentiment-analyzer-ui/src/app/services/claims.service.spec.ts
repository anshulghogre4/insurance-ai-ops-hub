import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ClaimsService } from './claims.service';
import {
  ClaimTriageResponse,
  ClaimEvidenceResponse,
  FraudAnalysisResponse,
  ProviderHealthResponse,
  PaginatedResponse
} from '../models/claims.model';

describe('ClaimsService', () => {
  let service: ClaimsService;
  let httpMock: HttpTestingController;

  const mockTriageResponse: ClaimTriageResponse = {
    claimId: 42,
    severity: 'High',
    urgency: 'Immediate',
    claimType: 'Water Damage',
    fraudScore: 35,
    fraudRiskLevel: 'Low',
    estimatedLossRange: '$5,000 - $15,000',
    recommendedActions: [
      { action: 'Assign field adjuster', priority: 'High', reasoning: 'Immediate inspection needed for water damage claims' }
    ],
    fraudFlags: [],
    evidence: [],
    status: 'Triaged',
    createdAt: '2026-02-24T10:00:00Z'
  };

  const mockEvidenceResponse: ClaimEvidenceResponse = {
    evidenceType: 'image',
    provider: 'Azure Vision',
    processedText: 'Water damage visible on ceiling and walls. Mold growth detected.',
    damageIndicators: ['water staining', 'mold growth', 'structural damage'],
    createdAt: '2026-02-24T10:05:00Z'
  };

  const mockFraudResponse: FraudAnalysisResponse = {
    claimId: 42,
    fraudScore: 72,
    riskLevel: 'High',
    indicators: [
      { category: 'Timing', description: 'Claim filed within 30 days of policy inception', severity: 'High' }
    ],
    recommendedActions: [
      { action: 'Refer to SIU for investigation', priority: 'Critical', reasoning: 'Multiple fraud indicators detected' }
    ],
    referToSIU: true,
    siuReferralReason: 'Multiple high-severity fraud indicators detected',
    confidence: 0.85
  };

  const mockHealthResponse: ProviderHealthResponse = {
    llmProviders: [
      { name: 'Groq', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
      { name: 'Ollama', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null }
    ],
    multimodalServices: [
      { name: 'Azure Vision', isConfigured: true, status: 'Available' }
    ],
    checkedAt: '2026-02-24T10:00:00Z'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [ClaimsService]
    });
    service = TestBed.inject(ClaimsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should send POST to triage a claim', () => {
    service.triageClaim('Water pipe burst in basement causing significant flooding', 'Complaint').subscribe(res => {
      expect(res.claimId).toBe(42);
      expect(res.severity).toBe('High');
      expect(res.fraudScore).toBe(35);
    });

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/claims/triage');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ text: 'Water pipe burst in basement causing significant flooding', interactionType: 'Complaint' });
    req.flush(mockTriageResponse);
  });

  it('should default interactionType to Complaint', () => {
    service.triageClaim('Rear-end collision at intersection').subscribe();

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/claims/triage');
    expect(req.request.body.interactionType).toBe('Complaint');
    req.flush(mockTriageResponse);
  });

  it('should upload evidence as FormData', () => {
    const file = new File(['image data'], 'damage-photo.jpg', { type: 'image/jpeg' });

    service.uploadEvidence(42, file).subscribe(res => {
      expect(res.evidenceType).toBe('image');
      expect(res.provider).toBe('Azure Vision');
      expect(res.damageIndicators.length).toBe(3);
    });

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/claims/upload');
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);
    req.flush(mockEvidenceResponse);
  });

  it('should get claim by ID', () => {
    service.getClaimById(42).subscribe(res => {
      expect(res.claimId).toBe(42);
      expect(res.status).toBe('Triaged');
    });

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/claims/42');
    expect(req.request.method).toBe('GET');
    req.flush(mockTriageResponse);
  });

  it('should get claims history with filters', () => {
    const mockPaginated: PaginatedResponse<ClaimTriageResponse> = {
      items: [mockTriageResponse],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1
    };

    service.getClaimsHistory({ severity: 'High', page: 1, pageSize: 20 }).subscribe(res => {
      expect(res.items.length).toBe(1);
      expect(res.totalCount).toBe(1);
    });

    const req = httpMock.expectOne(r => r.url === 'http://localhost:5143/api/insurance/claims/history');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('severity')).toBe('High');
    expect(req.request.params.get('pageSize')).toBe('20');
    req.flush(mockPaginated);
  });

  it('should get claims history without filters', () => {
    const mockPaginated: PaginatedResponse<ClaimTriageResponse> = {
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0
    };

    service.getClaimsHistory().subscribe(res => {
      expect(res.items.length).toBe(0);
    });

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/claims/history');
    expect(req.request.method).toBe('GET');
    req.flush(mockPaginated);
  });

  it('should send POST to analyze fraud', () => {
    service.analyzeFraud(42).subscribe(res => {
      expect(res.fraudScore).toBe(72);
      expect(res.referToSIU).toBe(true);
      expect(res.indicators.length).toBe(1);
    });

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/fraud/analyze');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ claimId: 42 });
    req.flush(mockFraudResponse);
  });

  it('should get fraud score by claim ID', () => {
    service.getFraudScore(42).subscribe(res => {
      expect(res.riskLevel).toBe('High');
      expect(res.confidence).toBe(0.85);
    });

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/fraud/score/42');
    expect(req.request.method).toBe('GET');
    req.flush(mockFraudResponse);
  });

  it('should get fraud alerts with default params', () => {
    service.getFraudAlerts().subscribe(res => {
      expect(res.length).toBe(1);
    });

    const req = httpMock.expectOne(r => r.url === 'http://localhost:5143/api/insurance/fraud/alerts');
    expect(req.request.params.get('minScore')).toBe('55');
    expect(req.request.params.get('pageSize')).toBe('50');
    req.flush([mockTriageResponse]);
  });

  it('should get fraud alerts with custom params', () => {
    service.getFraudAlerts(70, 25).subscribe();

    const req = httpMock.expectOne(r => r.url === 'http://localhost:5143/api/insurance/fraud/alerts');
    expect(req.request.params.get('minScore')).toBe('70');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush([]);
  });

  it('should get provider health', () => {
    service.getProviderHealth().subscribe(res => {
      expect(res.llmProviders.length).toBe(2);
      expect(res.multimodalServices.length).toBe(1);
      expect(res.llmProviders[0].name).toBe('Groq');
    });

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/health/providers');
    expect(req.request.method).toBe('GET');
    req.flush(mockHealthResponse);
  });

  it('should handle triage error response', () => {
    service.triageClaim('Test claim text').subscribe({
      next: () => { throw new Error('should have failed'); },
      error: (error) => {
        expect(error.status).toBe(429);
      }
    });

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/claims/triage');
    req.flush({ error: 'Rate limit exceeded' }, { status: 429, statusText: 'Too Many Requests' });
  });
});
