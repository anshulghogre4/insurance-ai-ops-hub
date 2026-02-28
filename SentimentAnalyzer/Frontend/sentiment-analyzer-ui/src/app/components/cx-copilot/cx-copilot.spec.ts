import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CxCopilotComponent } from './cx-copilot';
import { CustomerExperienceService } from '../../services/customer-experience.service';
import { CustomerExperienceStreamChunk } from '../../models/document.model';

describe('CxCopilotComponent', () => {
  let fixture: ComponentFixture<CxCopilotComponent>;
  let component: CxCopilotComponent;

  const mockStreamChunks: CustomerExperienceStreamChunk[] = [
    { type: 'content', content: 'Based on your homeowners policy HO-2026-4491827, ', metadata: null },
    { type: 'content', content: 'water damage from burst pipes is covered under Section I - Dwelling Coverage.', metadata: null },
    {
      type: 'metadata',
      content: '',
      metadata: {
        response: 'Based on your homeowners policy HO-2026-4491827, water damage from burst pipes is covered under Section I - Dwelling Coverage.',
        tone: 'Professional',
        escalationRecommended: false,
        escalationReason: null,
        llmProvider: 'Groq',
        elapsedMilliseconds: 2150,
        disclaimer: 'This response is for informational purposes only and does not constitute a coverage determination. Please consult your policy documents or contact your agent for binding coverage decisions.'
      }
    }
  ];

  const mockSessionResponse = { sessionId: 'test-session-abc-123' };

  const mockHistoryResponse = {
    sessionId: 'test-session-abc-123',
    messages: [
      { role: 'user', content: 'What does my homeowners policy cover?', timestamp: '2026-02-28T10:00:00Z' },
      { role: 'assistant', content: 'Your homeowners policy covers dwelling, personal property, and liability.', timestamp: '2026-02-28T10:00:05Z' }
    ]
  };

  const mockCxService = {
    streamChat: vi.fn(),
    createSession: vi.fn(),
    getSessionHistory: vi.fn()
  };

  beforeEach(async () => {
    vi.clearAllMocks();

    // Default: session creation succeeds, no existing session
    mockCxService.createSession.mockReturnValue(of(mockSessionResponse));
    mockCxService.getSessionHistory.mockReturnValue(of(mockHistoryResponse));
    mockCxService.streamChat.mockReturnValue(of(...mockStreamChunks));

    // Clear sessionStorage
    sessionStorage.removeItem('cx-copilot-session-id');

    await TestBed.configureTestingModule({
      imports: [CxCopilotComponent],
      providers: [
        { provide: CustomerExperienceService, useValue: mockCxService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CxCopilotComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    sessionStorage.removeItem('cx-copilot-session-id');
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
    expect(component.isStreaming()).toBe(false);
    expect(component.currentStreamText()).toBe('');
    expect(component.error()).toBeNull();
    expect(component.showClaimContext()).toBe(false);
    expect(component.message).toBe('');
  });

  it('should not send empty message', () => {
    component.message = '   ';
    component.sendMessage();

    expect(mockCxService.streamChat).not.toHaveBeenCalled();
    expect(component.isStreaming()).toBe(false);
  });

  it('should call streamChat when sending a valid message with claim context', () => {
    component.sessionId.set('active-session-456');
    mockCxService.streamChat.mockReturnValue(of(...mockStreamChunks));

    component.message = 'Is water damage from a burst pipe covered under my homeowners policy?';
    component.claimContext = 'Claim CLM-2026-78901, burst pipe in basement, filed January 2026';
    component.sendMessage();

    expect(mockCxService.streamChat).toHaveBeenCalledWith(
      'Is water damage from a burst pipe covered under my homeowners policy?',
      'Claim CLM-2026-78901, burst pipe in basement, filed January 2026',
      'active-session-456'
    );
    // After all chunks processed, user message + assistant message should be in the list
    const msgs = component.messages();
    const userMsg = msgs.find(m => m.role === 'user');
    const assistantMsg = msgs.find(m => m.role === 'assistant' && m.tone === 'Professional');
    expect(userMsg).toBeTruthy();
    expect(userMsg!.content).toBe('Is water damage from a burst pipe covered under my homeowners policy?');
    expect(assistantMsg).toBeTruthy();
    expect(assistantMsg!.tone).toBe('Professional');
    expect(component.message).toBe('');
  });

  it('should return correct tone badge class', () => {
    expect(component.getToneBadgeClass('Professional')).toBe('badge-info');
    expect(component.getToneBadgeClass('Empathetic')).toBe('bg-purple-500/15 text-purple-400 border border-purple-500/30');
    expect(component.getToneBadgeClass('Urgent')).toBe('badge-danger');
    expect(component.getToneBadgeClass('Informational')).toBe('bg-teal-500/15 text-teal-400 border border-teal-500/30');
    expect(component.getToneBadgeClass('Unknown')).toBe('badge-neutral');
  });

  it('should show thinking dots when streaming with no text yet', () => {
    component.isStreaming.set(true);
    component.currentStreamText.set('');
    fixture.detectChanges();

    const thinkingIndicator = fixture.nativeElement.querySelector('[data-testid="thinking-indicator"]');
    expect(thinkingIndicator).toBeTruthy();

    const dots = thinkingIndicator.querySelectorAll('.thinking-dots span');
    expect(dots.length).toBe(3);
  });

  it('should not show thinking dots when streaming has text content', () => {
    component.isStreaming.set(true);
    component.currentStreamText.set('Based on your policy...');
    fixture.detectChanges();

    const thinkingIndicator = fixture.nativeElement.querySelector('[data-testid="thinking-indicator"]');
    expect(thinkingIndicator).toBeFalsy();
  });

  it('should not show thinking dots when not streaming', () => {
    component.isStreaming.set(false);
    component.currentStreamText.set('');
    fixture.detectChanges();

    const thinkingIndicator = fixture.nativeElement.querySelector('[data-testid="thinking-indicator"]');
    expect(thinkingIndicator).toBeFalsy();
  });

  // ────────────────────────────────────────────────────────────
  // Conversation Memory Tests
  // ────────────────────────────────────────────────────────────

  it('should create a new session on init when no saved session exists', () => {
    expect(mockCxService.createSession).toHaveBeenCalled();
    expect(component.sessionId()).toBe('test-session-abc-123');
  });

  it('should restore session from sessionStorage on init', () => {
    vi.clearAllMocks();
    sessionStorage.removeItem('cx-copilot-session-id');
    sessionStorage.setItem('cx-copilot-session-id', 'saved-session-xyz');
    mockCxService.getSessionHistory.mockReturnValue(of(mockHistoryResponse));
    mockCxService.createSession.mockReturnValue(of(mockSessionResponse));

    const fixture2 = TestBed.createComponent(CxCopilotComponent);
    const component2 = fixture2.componentInstance;
    fixture2.detectChanges();

    expect(component2.sessionId()).toBe('saved-session-xyz');
    expect(mockCxService.getSessionHistory).toHaveBeenCalledWith('saved-session-xyz');
  });

  it('should load and render history messages on session restore', () => {
    vi.clearAllMocks();
    sessionStorage.removeItem('cx-copilot-session-id');
    sessionStorage.setItem('cx-copilot-session-id', 'saved-session-xyz');
    mockCxService.getSessionHistory.mockReturnValue(of(mockHistoryResponse));
    mockCxService.createSession.mockReturnValue(of(mockSessionResponse));

    const fixture2 = TestBed.createComponent(CxCopilotComponent);
    const component2 = fixture2.componentInstance;
    fixture2.detectChanges();

    // Should have loaded 2 messages from history
    expect(component2.messages().length).toBe(2);
    expect(component2.messages()[0].role).toBe('user');
    expect(component2.messages()[0].content).toBe('What does my homeowners policy cover?');
    expect(component2.messages()[1].role).toBe('assistant');
    expect(component2.messages()[1].content).toContain('dwelling');
    expect(component2.isLoadingHistory()).toBe(false);
  });

  it('should clear session and messages on New Conversation', () => {
    component.sessionId.set('old-session-123');
    sessionStorage.setItem('cx-copilot-session-id', 'old-session-123');
    component.messages.set([
      { id: 1, role: 'user', content: 'Prior question about auto claim', timestamp: new Date() }
    ]);

    component.startNewConversation();

    // Messages and error state should be cleared
    expect(component.messages()).toEqual([]);
    expect(component.error()).toBeNull();
    expect(component.currentStreamText()).toBe('');
    // createSession should have been called to initialize a new session
    expect(mockCxService.createSession).toHaveBeenCalled();
    // New session ID should be set (from the synchronous mock response)
    expect(component.sessionId()).toBe('test-session-abc-123');
  });

  it('should pass sessionId to streamChat when sending a message', () => {
    component.sessionId.set('active-session-456');
    mockCxService.streamChat.mockReturnValue(of(...mockStreamChunks));

    component.message = 'Is water damage from a burst pipe covered under my homeowners policy?';
    component.sendMessage();

    expect(mockCxService.streamChat).toHaveBeenCalledWith(
      'Is water damage from a burst pipe covered under my homeowners policy?',
      undefined,
      'active-session-456'
    );
  });

  it('should handle history load failure gracefully', () => {
    vi.clearAllMocks();
    sessionStorage.setItem('cx-copilot-session-id', 'bad-session');
    mockCxService.getSessionHistory.mockReturnValue(throwError(() => new Error('Not found')));
    mockCxService.createSession.mockReturnValue(of(mockSessionResponse));

    const fixture2 = TestBed.createComponent(CxCopilotComponent);
    const component2 = fixture2.componentInstance;
    fixture2.detectChanges();

    expect(component2.isLoadingHistory()).toBe(false);
    expect(mockCxService.createSession).toHaveBeenCalled();
  });

  it('should handle session creation failure gracefully', () => {
    vi.clearAllMocks();
    sessionStorage.removeItem('cx-copilot-session-id');
    mockCxService.createSession.mockReturnValue(throwError(() => new Error('Service unavailable')));

    const fixture2 = TestBed.createComponent(CxCopilotComponent);
    const component2 = fixture2.componentInstance;
    fixture2.detectChanges();

    // Should not crash — session remains empty (stateless mode)
    expect(component2.sessionId()).toBe('');
  });
});
