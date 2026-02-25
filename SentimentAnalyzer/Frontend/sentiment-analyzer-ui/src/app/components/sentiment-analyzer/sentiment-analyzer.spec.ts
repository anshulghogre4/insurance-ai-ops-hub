import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { SentimentAnalyzer } from './sentiment-analyzer';
import { SentimentService } from '../../services/sentiment.service';
import { SentimentResponse } from '../../models/sentiment.model';

describe('SentimentAnalyzer', () => {
  let component: SentimentAnalyzer;
  let fixture: ComponentFixture<SentimentAnalyzer>;
  let mockSentimentService: { analyzeSentiment: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    mockSentimentService = { analyzeSentiment: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [SentimentAnalyzer, FormsModule],
      providers: [
        { provide: SentimentService, useValue: mockSentimentService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SentimentAnalyzer);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with empty values', () => {
    expect(component.inputText()).toBe('');
    expect(component.isLoading()).toBe(false);
    expect(component.result()).toBeNull();
    expect(component.error()).toBeNull();
  });

  it('should show error when analyzing empty text', () => {
    component.inputText.set('');
    component.analyzeSentiment();

    expect(component.error()).toBe('Please enter some text to analyze');
    expect(mockSentimentService.analyzeSentiment).not.toHaveBeenCalled();
  });

  it('should show error when analyzing whitespace text', () => {
    component.inputText.set('   ');
    component.analyzeSentiment();

    expect(component.error()).toBe('Please enter some text to analyze');
    expect(mockSentimentService.analyzeSentiment).not.toHaveBeenCalled();
  });

  it('should call service when analyzing valid text', () => {
    const mockResponse: SentimentResponse = {
      sentiment: 'Positive',
      confidenceScore: 0.95,
      explanation: 'Test explanation',
      emotionBreakdown: { joy: 0.9 }
    };

    mockSentimentService.analyzeSentiment.mockReturnValue(of(mockResponse));
    component.inputText.set('I love this!');
    component.analyzeSentiment();

    expect(mockSentimentService.analyzeSentiment).toHaveBeenCalledWith('I love this!');
    expect(component.result()).toEqual(mockResponse);
    expect(component.error()).toBeNull();
    expect(component.isLoading()).toBe(false);
  });

  it('should handle service error', () => {
    mockSentimentService.analyzeSentiment.mockReturnValue(
      throwError(() => new Error('Service error'))
    );

    component.inputText.set('Test text');
    component.analyzeSentiment();

    expect(component.error()).toBe('Error analyzing sentiment. Please try again.');
    expect(component.result()).toBeNull();
    expect(component.isLoading()).toBe(false);
  });

  it('should clear all data', () => {
    component.inputText.set('Test');
    component.result.set({
      sentiment: 'Positive',
      confidenceScore: 0.9,
      explanation: 'Test',
      emotionBreakdown: {}
    });
    component.error.set('Error');

    component.clearAll();

    expect(component.inputText()).toBe('');
    expect(component.result()).toBeNull();
    expect(component.error()).toBeNull();
  });

  it('should return sorted emotion entries', () => {
    component.result.set({
      sentiment: 'Positive',
      confidenceScore: 0.9,
      explanation: 'Test',
      emotionBreakdown: {
        joy: 0.8,
        excitement: 0.9,
        satisfaction: 0.7
      }
    });

    const entries = component.getEmotionEntries();

    expect(entries.length).toBe(3);
    expect(entries[0][0]).toBe('excitement');
    expect(entries[0][1]).toBe(0.9);
    expect(entries[1][0]).toBe('joy');
    expect(entries[2][0]).toBe('satisfaction');
  });

  it('should return empty array when no emotions', () => {
    component.result.set(null);
    expect(component.getEmotionEntries()).toEqual([]);
  });

  it('should return correct sentiment class', () => {
    component.result.set({ sentiment: 'Positive', confidenceScore: 0.9, explanation: '', emotionBreakdown: {} });
    expect(component.getSentimentClass()).toBe('positive');

    component.result.set({ sentiment: 'Negative', confidenceScore: 0.9, explanation: '', emotionBreakdown: {} });
    expect(component.getSentimentClass()).toBe('negative');

    component.result.set({ sentiment: 'Neutral', confidenceScore: 0.9, explanation: '', emotionBreakdown: {} });
    expect(component.getSentimentClass()).toBe('neutral');
  });
});
