import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { InsuranceAnalyzerComponent } from './insurance-analyzer';
import { InsuranceService } from '../../services/insurance.service';
import { InsuranceAnalysisResponse } from '../../models/insurance.model';
import { of, throwError } from 'rxjs';

describe('InsuranceAnalyzerComponent', () => {
  let component: InsuranceAnalyzerComponent;
  let fixture: ComponentFixture<InsuranceAnalyzerComponent>;
  let insuranceService: InsuranceService;

  const mockResponse: InsuranceAnalysisResponse = {
    sentiment: 'Positive',
    confidenceScore: 0.85,
    explanation: 'Customer shows strong interest in coverage',
    emotionBreakdown: { trust: 0.7, satisfaction: 0.6, anxiety: 0.1 },
    insuranceAnalysis: {
      purchaseIntentScore: 80,
      customerPersona: 'CoverageFocused',
      journeyStage: 'Decision',
      riskIndicators: { churnRisk: 'Low', complaintEscalationRisk: 'Low', fraudIndicators: 'None' },
      policyRecommendations: [{ product: 'Health Gold', reasoning: 'Active comparison' }],
      interactionType: 'Email',
      keyTopics: ['coverage', 'family plan']
    },
    quality: { isValid: true, qualityScore: 92, issues: [], suggestions: [], warnings: [] }
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InsuranceAnalyzerComponent, HttpClientTestingModule, FormsModule],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { params: {} } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(InsuranceAnalyzerComponent);
    component = fixture.componentInstance;
    insuranceService = TestBed.inject(InsuranceService);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with default values', () => {
    expect(component.inputText).toBe('');
    expect(component.interactionType).toBe('General');
    expect(component.isLoading()).toBe(false);
    expect(component.result()).toBeNull();
    expect(component.error()).toBeNull();
  });

  it('should have all interaction types', () => {
    expect(component.interactionTypes).toEqual(['General', 'Email', 'Call', 'Chat', 'Review', 'Complaint']);
  });

  it('should set error when analyzing with empty text', () => {
    component.inputText = '   ';
    component.analyze();
    expect(component.error()).toBe('Please enter text to analyze.');
  });

  it('should call service and set result on successful analysis', () => {
    vi.spyOn(insuranceService, 'analyzeInsurance').mockReturnValue(of(mockResponse));

    component.inputText = 'I want to compare health insurance plans.';
    component.interactionType = 'Email';
    component.analyze();

    expect(insuranceService.analyzeInsurance).toHaveBeenCalledWith(
      'I want to compare health insurance plans.',
      'Email'
    );
    expect(component.result()).toEqual(mockResponse);
    expect(component.isLoading()).toBe(false);
  });

  it('should set error on failed analysis', () => {
    vi.spyOn(insuranceService, 'analyzeInsurance').mockReturnValue(
      throwError(() => ({ error: { error: 'AI provider unavailable' } }))
    );

    component.inputText = 'Test insurance text';
    component.analyze();

    expect(component.error()).toBe('AI provider unavailable');
    expect(component.isLoading()).toBe(false);
  });

  it('should set generic error when error response has no message', () => {
    vi.spyOn(insuranceService, 'analyzeInsurance').mockReturnValue(
      throwError(() => ({ status: 500 }))
    );

    component.inputText = 'Test insurance text';
    component.analyze();

    expect(component.error()).toBe('An error occurred during analysis. Please try again.');
  });

  it('should clear all state on clearAll', () => {
    component.inputText = 'Some text';
    component.interactionType = 'Complaint';
    component.result.set(mockResponse);
    component.error.set('Some error');

    component.clearAll();

    expect(component.inputText).toBe('');
    expect(component.interactionType).toBe('General');
    expect(component.result()).toBeNull();
    expect(component.error()).toBeNull();
  });

  it('should return sorted emotion entries', () => {
    component.result.set(mockResponse);
    const entries = component.getEmotionEntries();
    expect(entries.length).toBe(3);
    expect(entries[0][0]).toBe('trust'); // highest: 0.7
    expect(entries[1][0]).toBe('satisfaction'); // 0.6
    expect(entries[2][0]).toBe('anxiety'); // 0.1
  });

  it('should return empty emotion entries when no result', () => {
    expect(component.getEmotionEntries()).toEqual([]);
  });

  it('should return correct sentiment class', () => {
    component.result.set(mockResponse);
    expect(component.getSentimentClass()).toBe('positive');
  });

  it('should return correct intent color based on score', () => {
    component.result.set(mockResponse); // score = 80
    expect(component.getIntentColor()).toBe('text-green-400');

    component.result.set({ ...mockResponse, insuranceAnalysis: { ...mockResponse.insuranceAnalysis, purchaseIntentScore: 50 } });
    expect(component.getIntentColor()).toBe('text-yellow-400');

    component.result.set({ ...mockResponse, insuranceAnalysis: { ...mockResponse.insuranceAnalysis, purchaseIntentScore: 20 } });
    expect(component.getIntentColor()).toBe('text-red-400');
  });

  it('should return correct risk badge classes', () => {
    expect(component.getRiskBadge('High')).toBe('badge-danger');
    expect(component.getRiskBadge('Medium')).toBe('badge-warning');
    expect(component.getRiskBadge('Low')).toBe('badge-success');
    expect(component.getRiskBadge('Unknown')).toBe('badge-neutral');
  });

  it('should return correct persona icons', () => {
    component.result.set(mockResponse); // CoverageFocused
    expect(component.getPersonaIcon()).toBe('🛡️');
  });

  it('should return correct journey icons', () => {
    component.result.set(mockResponse); // Decision
    expect(component.getJourneyIcon()).toBe('✅');
  });
});
