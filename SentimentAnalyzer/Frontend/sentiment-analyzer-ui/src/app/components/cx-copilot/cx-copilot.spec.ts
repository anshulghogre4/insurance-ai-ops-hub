import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
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

  const mockCxService = {
    streamChat: vi.fn()
  };

  beforeEach(async () => {
    vi.clearAllMocks();

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

  it('should create the component', () => {
    expect(component).toBeTruthy();
    expect(component.messages()).toEqual([]);
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
    expect(component.messages()).toEqual([]);
    expect(component.isStreaming()).toBe(false);
  });

  it('should call streamChat when sending a valid message', () => {
    mockCxService.streamChat.mockReturnValue(of(...mockStreamChunks));

    component.message = 'Is water damage from a burst pipe covered under my homeowners policy?';
    component.claimContext = 'Claim CLM-2026-78901, burst pipe in basement, filed January 2026';
    component.sendMessage();

    expect(mockCxService.streamChat).toHaveBeenCalledWith(
      'Is water damage from a burst pipe covered under my homeowners policy?',
      'Claim CLM-2026-78901, burst pipe in basement, filed January 2026'
    );
    // After all chunks processed, user message + assistant message should be in the list
    expect(component.messages().length).toBe(2);
    expect(component.messages()[0].role).toBe('user');
    expect(component.messages()[0].content).toBe('Is water damage from a burst pipe covered under my homeowners policy?');
    expect(component.messages()[1].role).toBe('assistant');
    expect(component.messages()[1].tone).toBe('Professional');
    expect(component.message).toBe('');
  });

  it('should return correct tone badge class', () => {
    expect(component.getToneBadgeClass('Professional')).toBe('badge-info');
    expect(component.getToneBadgeClass('Empathetic')).toBe('bg-purple-500/15 text-purple-400 border border-purple-500/30');
    expect(component.getToneBadgeClass('Urgent')).toBe('badge-danger');
    expect(component.getToneBadgeClass('Informational')).toBe('bg-teal-500/15 text-teal-400 border border-teal-500/30');
    expect(component.getToneBadgeClass('Unknown')).toBe('badge-neutral');
  });
});
