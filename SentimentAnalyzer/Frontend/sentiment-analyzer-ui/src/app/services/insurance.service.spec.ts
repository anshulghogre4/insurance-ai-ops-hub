import { vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { InsuranceService } from './insurance.service';
import { InsuranceAnalysisResponse, DashboardData, AnalysisHistoryItem } from '../models/insurance.model';

describe('InsuranceService', () => {
  let service: InsuranceService;
  let httpMock: HttpTestingController;

  const mockAnalysisResponse: InsuranceAnalysisResponse = {
    sentiment: 'Positive',
    confidenceScore: 0.85,
    explanation: 'Customer shows strong interest in insurance coverage',
    emotionBreakdown: { trust: 0.7, satisfaction: 0.6 },
    insuranceAnalysis: {
      purchaseIntentScore: 75,
      customerPersona: 'CoverageFocused',
      journeyStage: 'Decision',
      riskIndicators: {
        churnRisk: 'Low',
        complaintEscalationRisk: 'Low',
        fraudIndicators: 'None'
      },
      policyRecommendations: [{ product: 'Health Gold Plan', reasoning: 'Active comparison' }],
      interactionType: 'Email',
      keyTopics: ['coverage comparison', 'family plan']
    },
    quality: { isValid: true, qualityScore: 92, issues: [], suggestions: [], warnings: [] }
  };

  const mockDashboardData: DashboardData = {
    metrics: { totalAnalyses: 10, avgPurchaseIntent: 65, avgSentimentScore: 0.78, highRiskCount: 2 },
    sentimentDistribution: { positive: 50, negative: 20, neutral: 20, mixed: 10 },
    topPersonas: [{ name: 'CoverageFocused', count: 5, percentage: 50 }]
  };

  const mockHistory: AnalysisHistoryItem[] = [
    {
      id: 1,
      inputTextPreview: 'I need health insurance for my family...',
      sentiment: 'Positive',
      purchaseIntentScore: 80,
      customerPersona: 'CoverageFocused',
      interactionType: 'Email',
      churnRisk: 'Low',
      createdAt: '2026-02-17T10:00:00Z'
    }
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [InsuranceService]
    });
    service = TestBed.inject(InsuranceService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should send POST request to analyze insurance text', () => {
    const text = 'I want to compare health insurance plans for my family.';
    const interactionType = 'Email';

    service.analyzeInsurance(text, interactionType).subscribe(response => {
      expect(response).toEqual(mockAnalysisResponse);
      expect(response.insuranceAnalysis.purchaseIntentScore).toBe(75);
    });

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/analyze');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ text, interactionType, customerId: undefined });
    req.flush(mockAnalysisResponse);
  });

  it('should include customerId when provided', () => {
    service.analyzeInsurance('Test text', 'General', 'CUST-001').subscribe();

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/analyze');
    expect(req.request.body.customerId).toBe('CUST-001');
    req.flush(mockAnalysisResponse);
  });

  it('should fetch dashboard data', () => {
    service.getDashboard().subscribe(data => {
      expect(data.metrics.totalAnalyses).toBe(10);
      expect(data.sentimentDistribution.positive).toBe(50);
      expect(data.topPersonas.length).toBe(1);
    });

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/dashboard');
    expect(req.request.method).toBe('GET');
    req.flush(mockDashboardData);
  });

  it('should fetch history with default count', () => {
    service.getHistory().subscribe(items => {
      expect(items.length).toBe(1);
      expect(items[0].sentiment).toBe('Positive');
    });

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/history?count=20');
    expect(req.request.method).toBe('GET');
    req.flush(mockHistory);
  });

  it('should fetch history with custom count', () => {
    service.getHistory(5).subscribe();

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/history?count=5');
    expect(req.request.method).toBe('GET');
    req.flush(mockHistory);
  });

  it('should handle analysis error response', () => {
    service.analyzeInsurance('Test text').subscribe({
      next: () => { throw new Error('should have failed'); },
      error: (error) => {
        expect(error.status).toBe(400);
      }
    });

    const req = httpMock.expectOne('http://localhost:5143/api/insurance/analyze');
    req.flush({ error: 'Text cannot be empty.' }, { status: 400, statusText: 'Bad Request' });
  });
});
