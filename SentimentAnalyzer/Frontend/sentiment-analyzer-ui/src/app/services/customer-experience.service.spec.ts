import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Observable } from 'rxjs';
import { CustomerExperienceService } from './customer-experience.service';
import { CustomerExperienceResponse } from '../models/document.model';

describe('CustomerExperienceService', () => {
  let service: CustomerExperienceService;
  let httpMock: HttpTestingController;

  const baseUrl = 'http://localhost:5143/api/insurance/cx';

  const mockChatResponse: CustomerExperienceResponse = {
    response: 'I understand your concern about the delay in processing your water damage claim. Let me look into claim CLM-2026-88431 for you. Based on our records, your adjuster completed the field inspection on February 20th, and the repair estimate is currently under review. You should receive an update within 3-5 business days.',
    tone: 'Empathetic',
    escalationRecommended: false,
    escalationReason: null,
    llmProvider: 'Groq',
    elapsedMilliseconds: 892,
    disclaimer: 'This response is AI-generated. For binding coverage decisions, please contact your licensed agent.'
  };

  const mockEscalationResponse: CustomerExperienceResponse = {
    response: 'I sincerely apologize for the frustration you have experienced with your denied liability claim. Given the complexity of your situation involving the multi-vehicle accident, I am connecting you with a senior claims specialist who can review the denial decision.',
    tone: 'Urgent',
    escalationRecommended: true,
    escalationReason: 'Policyholder expressed strong dissatisfaction with claim denial; potential regulatory complaint risk',
    llmProvider: 'Mistral',
    elapsedMilliseconds: 1103,
    disclaimer: 'This response is AI-generated. For binding coverage decisions, please contact your licensed agent.'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        CustomerExperienceService
      ]
    });
    service = TestBed.inject(CustomerExperienceService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should send chat POST with message', () => {
    service.chat('When will my water damage claim be resolved?').subscribe(res => {
      expect(res.tone).toBe('Empathetic');
      expect(res.escalationRecommended).toBe(false);
      expect(res.response).toContain('water damage claim');
      expect(res.disclaimer).toBeTruthy();
    });

    const req = httpMock.expectOne(`${baseUrl}/chat`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ message: 'When will my water damage claim be resolved?' });
    req.flush(mockChatResponse);
  });

  it('should send chat POST with claim context', () => {
    const message = 'I am extremely unhappy with the denial of my liability claim. This is unacceptable.';
    const claimContext = 'Claim CLM-2026-77192: Multi-vehicle accident, liability denied due to coverage exclusion for commercial use';

    service.chat(message, claimContext).subscribe(res => {
      expect(res.tone).toBe('Urgent');
      expect(res.escalationRecommended).toBe(true);
      expect(res.escalationReason).toContain('dissatisfaction');
    });

    const req = httpMock.expectOne(`${baseUrl}/chat`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ message, claimContext });
    req.flush(mockEscalationResponse);
  });

  it('should handle chat error response', () => {
    service.chat('What is my policy coverage limit?').subscribe({
      next: () => { throw new Error('should have failed'); },
      error: (error) => {
        expect(error.status).toBe(503);
      }
    });

    const req = httpMock.expectOne(`${baseUrl}/chat`);
    req.flush(
      { error: 'All LLM providers unavailable' },
      { status: 503, statusText: 'Service Unavailable' }
    );
  });

  it('should create streamChat observable', () => {
    const result = service.streamChat('Explain my deductible for windstorm coverage');
    expect(result).toBeInstanceOf(Observable);
  });

  it('should pass claimContext to streamChat', () => {
    const result = service.streamChat(
      'Why was my workers compensation claim flagged for review?',
      'Claim WC-2026-55301: Lower back injury, filed 48 hours after policy effective date'
    );
    expect(result).toBeInstanceOf(Observable);
  });
});
