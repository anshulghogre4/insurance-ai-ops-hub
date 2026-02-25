import { vi, describe, it, expect, beforeEach, beforeAll } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { DashboardComponent } from './dashboard';
import { InsuranceService } from '../../services/insurance.service';
import { ClaimsService } from '../../services/claims.service';
import { DashboardData, AnalysisHistoryItem } from '../../models/insurance.model';
import { of, throwError } from 'rxjs';

// Mock canvas context for Chart.js in JSDOM
beforeAll(() => {
  HTMLCanvasElement.prototype.getContext = vi.fn().mockReturnValue({
    canvas: { width: 300, height: 150 },
    clearRect: vi.fn(),
    beginPath: vi.fn(),
    moveTo: vi.fn(),
    lineTo: vi.fn(),
    stroke: vi.fn(),
    fill: vi.fn(),
    arc: vi.fn(),
    closePath: vi.fn(),
    save: vi.fn(),
    restore: vi.fn(),
    translate: vi.fn(),
    scale: vi.fn(),
    rotate: vi.fn(),
    measureText: vi.fn().mockReturnValue({ width: 0 }),
    setTransform: vi.fn(),
    resetTransform: vi.fn(),
    createLinearGradient: vi.fn().mockReturnValue({ addColorStop: vi.fn() }),
    createRadialGradient: vi.fn().mockReturnValue({ addColorStop: vi.fn() }),
    fillText: vi.fn(),
    strokeText: vi.fn(),
    fillRect: vi.fn(),
    strokeRect: vi.fn(),
    drawImage: vi.fn(),
    getImageData: vi.fn().mockReturnValue({ data: new Uint8ClampedArray(4) }),
    putImageData: vi.fn(),
    setLineDash: vi.fn(),
    getLineDash: vi.fn().mockReturnValue([]),
    clip: vi.fn(),
    isPointInPath: vi.fn(),
    quadraticCurveTo: vi.fn(),
    bezierCurveTo: vi.fn(),
    rect: vi.fn(),
    createPattern: vi.fn(),
  } as unknown as CanvasRenderingContext2D);
});

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let insuranceService: InsuranceService;
  let claimsService: ClaimsService;

  const mockClaimsHistory = {
    items: [
      { claimId: 1, severity: 'Critical', fraudScore: 78, urgency: 'Emergency', claimType: 'Fire', fraudRiskLevel: 'High', estimatedLossRange: '$50K', recommendedActions: [], fraudFlags: [], evidence: [], status: 'Triaged', createdAt: '2026-02-24T10:00:00Z' },
      { claimId: 2, severity: 'Low', fraudScore: 22, urgency: 'Standard', claimType: 'Scratch', fraudRiskLevel: 'Low', estimatedLossRange: '$500', recommendedActions: [], fraudFlags: [], evidence: [], status: 'Resolved', createdAt: '2026-02-23T10:00:00Z' }
    ],
    totalCount: 2, page: 1, pageSize: 100, totalPages: 1
  };

  const mockDashboard: DashboardData = {
    metrics: { totalAnalyses: 25, avgPurchaseIntent: 62, avgSentimentScore: 0.74, highRiskCount: 3 },
    sentimentDistribution: { positive: 45, negative: 25, neutral: 20, mixed: 10 },
    topPersonas: [
      { name: 'CoverageFocused', count: 10, percentage: 40 },
      { name: 'PriceSensitive', count: 8, percentage: 32 }
    ]
  };

  const mockHistory: AnalysisHistoryItem[] = [
    {
      id: 1,
      inputTextPreview: 'I submitted my claim three weeks ago...',
      sentiment: 'Negative',
      purchaseIntentScore: 10,
      customerPersona: 'ClaimFrustrated',
      interactionType: 'Complaint',
      churnRisk: 'High',
      createdAt: '2026-02-17T10:00:00Z'
    },
    {
      id: 2,
      inputTextPreview: 'Looking to get coverage for my family...',
      sentiment: 'Positive',
      purchaseIntentScore: 85,
      customerPersona: 'CoverageFocused',
      interactionType: 'Email',
      churnRisk: 'Low',
      createdAt: '2026-02-17T09:00:00Z'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent, HttpClientTestingModule, RouterTestingModule]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    insuranceService = TestBed.inject(InsuranceService);
    claimsService = TestBed.inject(ClaimsService);
    // Default mock for claims KPIs - individual tests can override
    vi.spyOn(claimsService, 'getClaimsHistory').mockReturnValue(of(mockClaimsHistory as any));
  });

  it('should create', () => {
    vi.spyOn(insuranceService, 'getDashboard').mockReturnValue(of(mockDashboard));
    vi.spyOn(insuranceService, 'getHistory').mockReturnValue(of(mockHistory));
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should load dashboard data on init', () => {
    vi.spyOn(insuranceService, 'getDashboard').mockReturnValue(of(mockDashboard));
    vi.spyOn(insuranceService, 'getHistory').mockReturnValue(of(mockHistory));
    fixture.detectChanges();

    expect(component.metrics().totalAnalyses).toBe(25);
    expect(component.sentimentDistribution().positive).toBe(45);
    expect(component.topPersonas().length).toBe(2);
    expect(component.isLoading()).toBe(false);
  });

  it('should load history on init', () => {
    vi.spyOn(insuranceService, 'getDashboard').mockReturnValue(of(mockDashboard));
    vi.spyOn(insuranceService, 'getHistory').mockReturnValue(of(mockHistory));
    fixture.detectChanges();

    expect(component.recentHistory().length).toBe(2);
    expect(component.recentHistory()[0].sentiment).toBe('Negative');
  });

  it('should set error when dashboard load fails', () => {
    vi.spyOn(insuranceService, 'getDashboard').mockReturnValue(throwError(() => new Error('Network error')));
    vi.spyOn(insuranceService, 'getHistory').mockReturnValue(of(mockHistory));
    fixture.detectChanges();

    expect(component.error()).toBe('Failed to load dashboard data.');
    expect(component.isLoading()).toBe(false);
  });

  it('should refresh data when refresh is called', () => {
    const dashboardSpy = vi.spyOn(insuranceService, 'getDashboard').mockReturnValue(of(mockDashboard));
    const historySpy = vi.spyOn(insuranceService, 'getHistory').mockReturnValue(of(mockHistory));
    fixture.detectChanges();

    component.error.set('Some previous error');
    component.refresh();

    expect(component.error()).toBeNull();
    expect(dashboardSpy).toHaveBeenCalledTimes(2);
    expect(historySpy).toHaveBeenCalledTimes(2);
  });

  it('should return correct sentiment colors', () => {
    expect(component.getSentimentColor('Positive')).toBe('text-emerald-400');
    expect(component.getSentimentColor('Negative')).toBe('text-rose-400');
    expect(component.getSentimentColor('Mixed')).toBe('text-amber-400');
    expect(component.getSentimentColor('Neutral')).toBe('text-cyan-400');
  });

  it('should return correct risk colors', () => {
    expect(component.getRiskColor('High')).toBe('text-rose-400');
    expect(component.getRiskColor('Medium')).toBe('text-amber-400');
    expect(component.getRiskColor('Low')).toBe('text-emerald-400');
  });

  it('should initialize with default metrics', () => {
    vi.spyOn(insuranceService, 'getDashboard').mockReturnValue(of(mockDashboard));
    vi.spyOn(insuranceService, 'getHistory').mockReturnValue(of(mockHistory));

    expect(component.metrics().totalAnalyses).toBe(0);
    expect(component.recentHistory().length).toBe(0);
  });

  it('should load claims KPIs on init', () => {
    vi.spyOn(insuranceService, 'getDashboard').mockReturnValue(of(mockDashboard));
    vi.spyOn(insuranceService, 'getHistory').mockReturnValue(of(mockHistory));
    fixture.detectChanges();

    expect(component.claimsTotal()).toBe(2);
    expect(component.claimsCritical()).toBe(1);
    expect(component.claimsAvgFraud()).toBe(50); // (78+22)/2 = 50
  });

  it('should update chart data after loading', () => {
    vi.spyOn(insuranceService, 'getDashboard').mockReturnValue(of(mockDashboard));
    vi.spyOn(insuranceService, 'getHistory').mockReturnValue(of(mockHistory));
    fixture.detectChanges();

    expect(component.sentimentChartData.datasets[0].data).toEqual([45, 25, 20, 10]);
    expect(component.personaChartData.labels).toEqual(['CoverageFocused', 'PriceSensitive']);
    expect(component.personaChartData.datasets[0].data).toEqual([10, 8]);
  });
});
