import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Observable } from 'rxjs';
import { CustomerExperienceService } from './customer-experience.service';
import { CustomerExperienceResponse, CxSessionResponse, CxMessageHistoryResponse } from '../models/document.model';

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

  const mockSessionResponse: CxSessionResponse = {
    sessionId: 'e7a1b2c3-d4e5-6f78-9a0b-cdef12345678'
  };

  const mockHistoryResponse: CxMessageHistoryResponse = {
    sessionId: 'e7a1b2c3-d4e5-6f78-9a0b-cdef12345678',
    messages: [
      { role: 'user', content: 'What does my homeowners policy cover for water damage?', timestamp: '2026-02-28T10:00:00Z' },
      { role: 'assistant', content: 'Your homeowners policy covers sudden and accidental water damage from burst pipes. Gradual damage and flooding from external sources require separate flood insurance.', timestamp: '2026-02-28T10:00:03Z' },
      { role: 'user', content: 'What is my deductible for this type of claim?', timestamp: '2026-02-28T10:01:00Z' },
      { role: 'assistant', content: 'Your water damage deductible is $1,000 per occurrence as stated in your policy declarations page.', timestamp: '2026-02-28T10:01:04Z' }
    ]
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

  it('should send chat POST with sessionId', () => {
    const message = 'What is the status of my water damage claim?';
    const sessionId = 'e7a1b2c3-d4e5-6f78-9a0b-cdef12345678';

    service.chat(message, undefined, sessionId).subscribe(res => {
      expect(res.tone).toBe('Empathetic');
    });

    const req = httpMock.expectOne(`${baseUrl}/chat`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ message, sessionId });
    req.flush(mockChatResponse);
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

  it('should pass sessionId to streamChat', () => {
    const result = service.streamChat(
      'What is the status of my claim?',
      undefined,
      'e7a1b2c3-d4e5-6f78-9a0b-cdef12345678'
    );
    expect(result).toBeInstanceOf(Observable);
  });

  // ────────────────────────────────────────────────────────────
  // Session Management Tests
  // ────────────────────────────────────────────────────────────

  it('should create a new session via POST', () => {
    service.createSession().subscribe(res => {
      expect(res.sessionId).toBe('e7a1b2c3-d4e5-6f78-9a0b-cdef12345678');
    });

    const req = httpMock.expectOne(`${baseUrl}/sessions`);
    expect(req.request.method).toBe('POST');
    req.flush(mockSessionResponse);
  });

  it('should get session history via GET', () => {
    const sessionId = 'e7a1b2c3-d4e5-6f78-9a0b-cdef12345678';

    service.getSessionHistory(sessionId).subscribe(res => {
      expect(res.sessionId).toBe(sessionId);
      expect(res.messages.length).toBe(4);
      expect(res.messages[0].role).toBe('user');
      expect(res.messages[0].content).toContain('water damage');
      expect(res.messages[1].role).toBe('assistant');
      expect(res.messages[2].role).toBe('user');
      expect(res.messages[3].role).toBe('assistant');
    });

    const req = httpMock.expectOne(`${baseUrl}/sessions/${sessionId}/history`);
    expect(req.request.method).toBe('GET');
    req.flush(mockHistoryResponse);
  });

  it('should handle session creation failure', () => {
    service.createSession().subscribe({
      next: () => { throw new Error('should have failed'); },
      error: (error) => {
        expect(error.status).toBe(500);
      }
    });

    const req = httpMock.expectOne(`${baseUrl}/sessions`);
    req.flush(
      { error: 'Internal server error' },
      { status: 500, statusText: 'Internal Server Error' }
    );
  });

  it('should handle session not found on history request', () => {
    service.getSessionHistory('nonexistent-session-id').subscribe({
      next: () => { throw new Error('should have failed'); },
      error: (error) => {
        expect(error.status).toBe(404);
      }
    });

    const req = httpMock.expectOne(`${baseUrl}/sessions/nonexistent-session-id/history`);
    req.flush(
      { error: 'Session not found' },
      { status: 404, statusText: 'Not Found' }
    );
  });
});
