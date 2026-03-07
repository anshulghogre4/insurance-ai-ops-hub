import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { ProviderHealthComponent } from './provider-health';
import { ClaimsService } from '../../services/claims.service';
import { ExtendedProviderHealthResponse } from '../../models/claims.model';
import { of, throwError } from 'rxjs';

describe('ProviderHealthComponent', () => {
  let component: ProviderHealthComponent;
  let fixture: ComponentFixture<ProviderHealthComponent>;
  let claimsService: ClaimsService;

  const mockExtendedHealth: ExtendedProviderHealthResponse = {
    llmProviders: [
      { name: 'Groq', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
      { name: 'Cerebras', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
      { name: 'Mistral', status: 'Degraded', isAvailable: true, consecutiveFailures: 2, cooldownSeconds: 0, cooldownExpiresUtc: null },
      { name: 'Gemini', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
      { name: 'OpenRouter', status: 'Down', isAvailable: false, consecutiveFailures: 5, cooldownSeconds: 120, cooldownExpiresUtc: '2026-02-24T10:05:00Z' },
      { name: 'OpenAI', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
      { name: 'Ollama', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null }
    ],
    embeddingProviders: [
      { name: 'Voyage AI', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 1, freeTierLimit: '50M tokens' },
      { name: 'Cohere', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 2, freeTierLimit: '100 req/min' },
      { name: 'Ollama (Local)', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 6, freeTierLimit: 'Unlimited (local)' }
    ],
    ocrProviders: [
      { name: 'PdfPig (Local)', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 1, freeTierLimit: 'Unlimited (local)' },
      { name: 'Azure Document Intelligence', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 3, freeTierLimit: '500 pages/month' }
    ],
    nerProviders: [
      { name: 'HuggingFace BERT', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 1, freeTierLimit: '300 req/hr' },
      { name: 'Azure AI Language', status: 'NotConfigured', isConfigured: false, isAvailable: false, chainOrder: 2, freeTierLimit: '5K/month' }
    ],
    sttProviders: [
      { name: 'Deepgram', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 1, freeTierLimit: '$200 credit' }
    ],
    contentSafety: [
      { name: 'Azure AI Content Safety', isConfigured: true, status: 'Available' }
    ],
    translation: [
      { name: 'Azure AI Translator', isConfigured: true, status: 'Available' }
    ],
    checkedAt: '2026-02-24T10:00:00Z'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProviderHealthComponent, HttpClientTestingModule, RouterTestingModule]
    }).compileComponents();

    fixture = TestBed.createComponent(ProviderHealthComponent);
    component = fixture.componentInstance;
    claimsService = TestBed.inject(ClaimsService);
  });

  it('should create', () => {
    vi.spyOn(claimsService, 'getExtendedProviderHealth').mockReturnValue(of(mockExtendedHealth));
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should load extended provider health on init', () => {
    vi.spyOn(claimsService, 'getExtendedProviderHealth').mockReturnValue(of(mockExtendedHealth));
    fixture.detectChanges();

    expect(component.llmProviders().length).toBe(7);
    expect(component.embeddingProviders().length).toBe(3);
    expect(component.ocrProviders().length).toBe(2);
    expect(component.nerProviders().length).toBe(2);
    expect(component.sttProviders().length).toBe(1);
    expect(component.contentSafety().length).toBe(1);
    expect(component.translation().length).toBe(1);
    expect(component.lastChecked()).toBe('2026-02-24T10:00:00Z');
    expect(component.isLoading()).toBe(false);
  });

  it('should have LLM section expanded by default', () => {
    vi.spyOn(claimsService, 'getExtendedProviderHealth').mockReturnValue(of(mockExtendedHealth));
    fixture.detectChanges();

    expect(component.isSectionExpanded('llm')).toBe(true);
    expect(component.isSectionExpanded('embedding')).toBe(false);
    expect(component.isSectionExpanded('ocr')).toBe(false);
  });

  it('should toggle section expand/collapse', () => {
    vi.spyOn(claimsService, 'getExtendedProviderHealth').mockReturnValue(of(mockExtendedHealth));
    fixture.detectChanges();

    // Expand embedding
    component.toggleSection('embedding');
    expect(component.isSectionExpanded('embedding')).toBe(true);

    // Collapse embedding
    component.toggleSection('embedding');
    expect(component.isSectionExpanded('embedding')).toBe(false);

    // Collapse LLM
    component.toggleSection('llm');
    expect(component.isSectionExpanded('llm')).toBe(false);
  });

  it('should return correct LLM status dot classes', () => {
    expect(component.getLlmStatusDotClass({ name: 'Test', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null })).toBe('bg-emerald-400');
    expect(component.getLlmStatusDotClass({ name: 'Test', status: 'Degraded', isAvailable: true, consecutiveFailures: 2, cooldownSeconds: 0, cooldownExpiresUtc: null })).toBe('bg-amber-400');
    expect(component.getLlmStatusDotClass({ name: 'Test', status: 'Down', isAvailable: false, consecutiveFailures: 5, cooldownSeconds: 120, cooldownExpiresUtc: null })).toBe('bg-rose-500');
  });

  it('should return correct chain health classes for ProviderChainHealth', () => {
    const healthy = { name: 'Voyage AI', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 1, freeTierLimit: '50M tokens' };
    const notConfigured = { name: 'Jina', status: 'NotConfigured', isConfigured: false, isAvailable: false, chainOrder: 5, freeTierLimit: '1M tokens' };
    const degraded = { name: 'Test', status: 'Degraded', isConfigured: true, isAvailable: true, chainOrder: 2, freeTierLimit: null };

    expect(component.getChainHealthClass(healthy)).toContain('emerald');
    expect(component.getChainHealthClass(notConfigured)).toContain('slate');
    expect(component.getChainHealthClass(degraded)).toContain('amber');
  });

  it('should return correct chain status dot classes', () => {
    expect(component.getChainStatusDotClass({ name: 'Test', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 1, freeTierLimit: null })).toBe('bg-emerald-400');
    expect(component.getChainStatusDotClass({ name: 'Test', status: 'NotConfigured', isConfigured: false, isAvailable: false, chainOrder: 1, freeTierLimit: null })).toBe('bg-slate-400');
    expect(component.getChainStatusDotClass({ name: 'Test', status: 'Down', isConfigured: true, isAvailable: false, chainOrder: 1, freeTierLimit: null })).toBe('bg-rose-500');
  });

  it('should refresh on button click', () => {
    const spy = vi.spyOn(claimsService, 'getExtendedProviderHealth').mockReturnValue(of(mockExtendedHealth));
    fixture.detectChanges();

    component.refresh();
    expect(spy).toHaveBeenCalledTimes(2); // init + refresh
  });

  it('should handle error on refresh', () => {
    vi.spyOn(claimsService, 'getExtendedProviderHealth').mockReturnValue(throwError(() => new Error('Network error')));
    fixture.detectChanges();

    expect(component.isLoading()).toBe(false);
    expect(component.error()).toBe('Failed to load provider health. Please try again.');
  });

  it('should format time string', () => {
    const result = component.formatTime('2026-02-24T10:00:00Z');
    expect(result).toBeTruthy();
    expect(result.length).toBeGreaterThan(0);
  });

  it('should use mathMin helper correctly', () => {
    expect(component.mathMin(3, 5)).toBe(3);
    expect(component.mathMin(7, 5)).toBe(5);
  });

  it('should handle null arrays defensively', () => {
    const partialResponse: ExtendedProviderHealthResponse = {
      llmProviders: [],
      embeddingProviders: [],
      ocrProviders: [],
      nerProviders: [],
      sttProviders: [],
      contentSafety: [],
      translation: [],
      checkedAt: '2026-02-24T10:00:00Z'
    };

    vi.spyOn(claimsService, 'getExtendedProviderHealth').mockReturnValue(of(partialResponse));
    fixture.detectChanges();

    expect(component.llmProviders().length).toBe(0);
    expect(component.embeddingProviders().length).toBe(0);
    expect(component.isLoading()).toBe(false);
  });
});
