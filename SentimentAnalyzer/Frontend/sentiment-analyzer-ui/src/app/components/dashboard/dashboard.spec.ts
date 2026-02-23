import { vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { DashboardComponent } from './dashboard';
import { InsuranceService } from '../../services/insurance.service';
import { DashboardData, AnalysisHistoryItem } from '../../models/insurance.model';
import { of, throwError } from 'rxjs';

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let insuranceService: InsuranceService;

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
      imports: [DashboardComponent, HttpClientTestingModule]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    insuranceService = TestBed.inject(InsuranceService);
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

    expect(component.metrics.totalAnalyses).toBe(25);
    expect(component.sentimentDistribution.positive).toBe(45);
    expect(component.topPersonas.length).toBe(2);
    expect(component.isLoading()).toBe(false);
  });

  it('should load history on init', () => {
    vi.spyOn(insuranceService, 'getDashboard').mockReturnValue(of(mockDashboard));
    vi.spyOn(insuranceService, 'getHistory').mockReturnValue(of(mockHistory));
    fixture.detectChanges();

    expect(component.recentHistory.length).toBe(2);
    expect(component.recentHistory[0].sentiment).toBe('Negative');
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
    // Called twice: once on init, once on refresh
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

    expect(component.metrics.totalAnalyses).toBe(0);
    expect(component.recentHistory.length).toBe(0);
  });
});
