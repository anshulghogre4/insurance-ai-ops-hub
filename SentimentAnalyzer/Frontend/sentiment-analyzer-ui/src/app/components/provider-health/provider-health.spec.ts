import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { ProviderHealthComponent } from './provider-health';
import { ClaimsService } from '../../services/claims.service';
import { ProviderHealthResponse } from '../../models/claims.model';
import { of, throwError } from 'rxjs';

describe('ProviderHealthComponent', () => {
  let component: ProviderHealthComponent;
  let fixture: ComponentFixture<ProviderHealthComponent>;
  let claimsService: ClaimsService;

  const mockHealth: ProviderHealthResponse = {
    llmProviders: [
      { name: 'Groq', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
      { name: 'Cerebras', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
      { name: 'Mistral', status: 'Degraded', isAvailable: true, consecutiveFailures: 2, cooldownSeconds: 0, cooldownExpiresUtc: null },
      { name: 'Gemini', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
      { name: 'OpenRouter', status: 'Down', isAvailable: false, consecutiveFailures: 5, cooldownSeconds: 120, cooldownExpiresUtc: '2026-02-24T10:05:00Z' },
      { name: 'OpenAI', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
      { name: 'Ollama', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null }
    ],
    multimodalServices: [
      { name: 'Deepgram STT', isConfigured: true, status: 'Available' },
      { name: 'Azure Vision', isConfigured: true, status: 'Available' },
      { name: 'Cloudflare Vision', isConfigured: true, status: 'Available' },
      { name: 'OCR.space', isConfigured: true, status: 'Available' },
      { name: 'HuggingFace NER', isConfigured: false, status: 'Not Configured' },
      { name: 'Voyage AI Embeddings', isConfigured: false, status: 'Not Configured' }
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
    vi.spyOn(claimsService, 'getProviderHealth').mockReturnValue(of(mockHealth));
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should load provider health on init', () => {
    vi.spyOn(claimsService, 'getProviderHealth').mockReturnValue(of(mockHealth));
    fixture.detectChanges();

    expect(component.llmProviders().length).toBe(7);
    expect(component.multimodalServices().length).toBe(6);
    expect(component.lastChecked()).toBe('2026-02-24T10:00:00Z');
    expect(component.isLoading()).toBe(false);
  });

  it('should return correct status dot classes', () => {
    expect(component.getStatusDotClass({ name: 'Test', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null })).toBe('bg-emerald-400');
    expect(component.getStatusDotClass({ name: 'Test', status: 'Degraded', isAvailable: true, consecutiveFailures: 2, cooldownSeconds: 0, cooldownExpiresUtc: null })).toBe('bg-amber-400');
    expect(component.getStatusDotClass({ name: 'Test', status: 'Down', isAvailable: false, consecutiveFailures: 5, cooldownSeconds: 120, cooldownExpiresUtc: null })).toBe('bg-rose-500');
  });

  it('should return correct chain classes', () => {
    const healthy = { name: 'Groq', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null };
    const degraded = { name: 'Mistral', status: 'Degraded', isAvailable: true, consecutiveFailures: 2, cooldownSeconds: 0, cooldownExpiresUtc: null };
    const down = { name: 'OpenRouter', status: 'Down', isAvailable: false, consecutiveFailures: 5, cooldownSeconds: 120, cooldownExpiresUtc: null };

    expect(component.getChainClass(healthy)).toContain('emerald');
    expect(component.getChainClass(degraded)).toContain('amber');
    expect(component.getChainClass(down)).toContain('rose');
  });

  it('should return correct service icons', () => {
    expect(component.getServiceIcon({ name: 'Deepgram STT', isConfigured: true, status: 'Available' })).toBe('microphone');
    expect(component.getServiceIcon({ name: 'Azure Vision', isConfigured: true, status: 'Available' })).toBe('eye');
    expect(component.getServiceIcon({ name: 'Cloudflare Vision', isConfigured: true, status: 'Available' })).toBe('cloud');
    expect(component.getServiceIcon({ name: 'OCR.space', isConfigured: true, status: 'Available' })).toBe('document');
  });

  it('should refresh on button click', () => {
    const spy = vi.spyOn(claimsService, 'getProviderHealth').mockReturnValue(of(mockHealth));
    fixture.detectChanges();

    component.refresh();
    expect(spy).toHaveBeenCalledTimes(2); // init + refresh
  });

  it('should handle error on refresh', () => {
    vi.spyOn(claimsService, 'getProviderHealth').mockReturnValue(throwError(() => new Error('Network error')));
    fixture.detectChanges();

    expect(component.isLoading()).toBe(false);
  });

  it('should format time string', () => {
    const result = component.formatTime('2026-02-24T10:00:00Z');
    expect(result).toBeTruthy();
    expect(result.length).toBeGreaterThan(0);
  });
});
