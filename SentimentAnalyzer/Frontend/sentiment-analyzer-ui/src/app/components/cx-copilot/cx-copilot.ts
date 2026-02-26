import { Component, DestroyRef, ElementRef, inject, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CustomerExperienceService } from '../../services/customer-experience.service';
import { ChatMessage, CustomerExperienceStreamChunk } from '../../models/document.model';

let chatMsgIdCounter = 0;

@Component({
  selector: 'app-cx-copilot',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-8 flex flex-col" style="height: calc(100vh - 5rem);">

      <!-- Header -->
      <div class="text-center mb-6 animate-fade-in-up flex-shrink-0">
        <div class="inline-flex items-center gap-3 mb-3">
          <div class="w-12 h-12 rounded-2xl bg-gradient-to-br from-indigo-500 via-purple-500 to-pink-500 flex items-center justify-center shadow-lg shadow-indigo-500/25">
            <svg class="w-6 h-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z"/>
            </svg>
          </div>
          <div>
            <h1 class="text-2xl sm:text-3xl font-bold" [style.color]="'var(--text-primary)'">CX Copilot</h1>
            <p class="text-sm" [style.color]="'var(--text-muted)'">AI-powered customer experience assistant</p>
          </div>
        </div>
      </div>

      <!-- Chat Messages Area -->
      <div class="flex-1 overflow-y-auto mb-4 space-y-4 scrollbar-thin" #chatContainer>
        @if (messages().length === 0 && !isStreaming()) {
          <div class="text-center py-16 animate-fade-in">
            <svg class="w-16 h-16 mx-auto mb-4 text-indigo-500/30" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1" d="M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z"/>
            </svg>
            <p class="text-lg font-semibold mb-2" [style.color]="'var(--text-secondary)'">Start a conversation</p>
            <p class="text-sm" [style.color]="'var(--text-muted)'">Ask questions about insurance policies, claims processes, or coverage details.</p>
          </div>
        }

        @for (msg of messages(); track msg.id) {
          @if (msg.role === 'user') {
            <!-- User Message -->
            <div class="flex justify-end animate-fade-in">
              <div class="max-w-[75%] rounded-2xl rounded-br-md px-4 py-3 bg-gradient-to-br from-indigo-500 to-purple-600 text-white shadow-lg shadow-indigo-500/20">
                <p class="text-sm whitespace-pre-wrap">{{ msg.content }}</p>
              </div>
            </div>
          } @else {
            <!-- Assistant Message -->
            <div class="flex justify-start animate-fade-in">
              <div class="max-w-[85%]">
                <!-- Tone Badge -->
                @if (msg.tone) {
                  <div class="flex items-center gap-2 mb-1.5">
                    <span class="badge text-[10px]" [class]="getToneBadgeClass(msg.tone)">{{ msg.tone }}</span>
                    @if (msg.escalationRecommended) {
                      <span class="badge badge-danger text-[10px] animate-pulse flex items-center gap-1">
                        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
                        </svg>
                        Escalation Recommended
                      </span>
                    }
                  </div>
                }
                <div class="glass-card-static rounded-2xl rounded-bl-md px-4 py-3">
                  <p class="text-sm whitespace-pre-wrap leading-relaxed" [style.color]="'var(--text-primary)'">{{ msg.content }}</p>

                  @if (msg.escalationReason) {
                    <div class="mt-3 p-2.5 rounded-lg bg-rose-500/10 border border-rose-500/20">
                      <p class="text-xs text-rose-400">
                        <span class="font-semibold">Escalation reason:</span> {{ msg.escalationReason }}
                      </p>
                    </div>
                  }

                  <!-- Disclaimer -->
                  @if (msg.disclaimer) {
                    <p class="mt-3 text-[10px] leading-relaxed" [style.color]="'var(--text-muted)'">
                      <svg class="w-3 h-3 inline-block mr-0.5 -mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
                      </svg>
                      {{ msg.disclaimer }}
                    </p>
                  }

                  <!-- Metadata -->
                  @if (msg.llmProvider) {
                    <div class="flex items-center gap-2 mt-2 pt-2" [style.border-top]="'1px solid var(--border-secondary)'">
                      <span class="badge badge-neutral text-[9px]">{{ msg.llmProvider }}</span>
                      @if (msg.elapsedMs) {
                        <span class="text-[10px]" [style.color]="'var(--text-muted)'">{{ (msg.elapsedMs / 1000).toFixed(1) }}s</span>
                      }
                    </div>
                  }
                </div>
              </div>
            </div>
          }
        }

        <!-- Streaming indicator -->
        @if (isStreaming()) {
          <div class="flex justify-start animate-fade-in">
            <div class="max-w-[85%]">
              <div class="glass-card-static rounded-2xl rounded-bl-md px-4 py-3">
                <p class="text-sm whitespace-pre-wrap leading-relaxed" [style.color]="'var(--text-primary)'">{{ currentStreamText() }}<span class="inline-block w-0.5 h-4 bg-indigo-400 animate-pulse ml-0.5"></span></p>
              </div>
            </div>
          </div>
        }
      </div>

      <!-- Error State -->
      @if (error()) {
        <div class="glass-card-static p-4 mb-4 border-l-4 border-rose-500 animate-fade-in flex-shrink-0" role="alert">
          <div class="flex items-center gap-3">
            <svg class="w-4 h-4 text-rose-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
            </svg>
            <p class="text-xs" [style.color]="'var(--text-primary)'">{{ error() }}</p>
          </div>
        </div>
      }

      <!-- Input Area -->
      <div class="flex-shrink-0 glass-card-static p-4 rounded-2xl">
        <!-- Claim Context Toggle -->
        <div class="mb-3">
          <button (click)="toggleClaimContext()" class="text-xs flex items-center gap-1 transition-colors" [style.color]="'var(--text-muted)'" aria-label="Toggle claim context">
            <svg class="w-3.5 h-3.5 transition-transform" [class.rotate-90]="showClaimContext()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
            </svg>
            Add claim context (optional)
          </button>
          @if (showClaimContext()) {
            <input
              type="text"
              [(ngModel)]="claimContext"
              class="input-field mt-2 text-xs"
              placeholder="e.g., Claim CLM-2024-78901, water damage, pending 3 weeks"
              aria-label="Claim context"
            />
          }
        </div>

        <!-- Message Input -->
        <div class="flex items-end gap-3">
          <textarea
            [(ngModel)]="message"
            class="input-field min-h-[44px] max-h-[120px] resize-none flex-1"
            placeholder="Ask about insurance policies, claims, coverage..."
            [maxLength]="5000"
            rows="1"
            aria-label="Chat message input"
            (keydown.control.enter)="sendMessage()"
            (keydown.meta.enter)="sendMessage()"
            (input)="autoResize($event)"
          ></textarea>
          <button
            (click)="sendMessage()"
            [disabled]="!message.trim() || isStreaming()"
            class="btn-primary p-3 rounded-xl flex-shrink-0"
            aria-label="Send message"
          >
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8"/>
            </svg>
          </button>
        </div>
        <div class="flex justify-between mt-1.5 text-[10px]" [style.color]="'var(--text-muted)'">
          <span>Ctrl+Enter to send</span>
          <span>{{ message.length }} / 5,000</span>
        </div>
      </div>
    </div>
  `
})
export class CxCopilotComponent {
  private destroyRef = inject(DestroyRef);
  private cxService = inject(CustomerExperienceService);

  @ViewChild('chatContainer') chatContainer?: ElementRef<HTMLElement>;

  message = '';
  claimContext = '';

  messages = signal<ChatMessage[]>([]);
  isStreaming = signal(false);
  currentStreamText = signal('');
  error = signal<string | null>(null);
  showClaimContext = signal(false);

  toggleClaimContext(): void {
    this.showClaimContext.update(v => !v);
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.chatContainer?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
  }

  sendMessage(): void {
    const text = this.message.trim();
    if (!text || this.isStreaming()) return;

    // Add user message
    const userMsg: ChatMessage = { id: ++chatMsgIdCounter, role: 'user', content: text, timestamp: new Date() };
    this.messages.update(msgs => [...msgs, userMsg]);
    this.message = '';
    this.error.set(null);
    this.isStreaming.set(true);
    this.currentStreamText.set('');
    this.scrollToBottom();

    const context = this.claimContext.trim() || undefined;

    this.cxService.streamChat(text, context)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (chunk: CustomerExperienceStreamChunk) => {
          if (chunk.type === 'content') {
            this.currentStreamText.update(t => t + chunk.content);
            this.scrollToBottom();
          } else if (chunk.type === 'metadata' && chunk.metadata) {
            const assistantMsg: ChatMessage = {
              id: ++chatMsgIdCounter,
              role: 'assistant',
              content: chunk.metadata.response,
              tone: chunk.metadata.tone,
              escalationRecommended: chunk.metadata.escalationRecommended,
              escalationReason: chunk.metadata.escalationReason,
              llmProvider: chunk.metadata.llmProvider,
              elapsedMs: chunk.metadata.elapsedMilliseconds,
              disclaimer: chunk.metadata.disclaimer,
              timestamp: new Date()
            };
            this.messages.update(msgs => [...msgs, assistantMsg]);
            this.currentStreamText.set('');
            this.isStreaming.set(false);
            this.scrollToBottom();
          } else if (chunk.type === 'error') {
            this.error.set(chunk.content || 'An error occurred during the conversation.');
            this.isStreaming.set(false);
            this.currentStreamText.set('');
          }
        },
        error: (err) => {
          this.error.set(err.message || 'Failed to connect to CX Copilot. Please try again.');
          this.isStreaming.set(false);
          this.currentStreamText.set('');
        },
        complete: () => {
          // If streaming completed without metadata chunk, finalize
          if (this.isStreaming() && this.currentStreamText()) {
            const assistantMsg: ChatMessage = {
              id: ++chatMsgIdCounter,
              role: 'assistant',
              content: this.currentStreamText(),
              timestamp: new Date()
            };
            this.messages.update(msgs => [...msgs, assistantMsg]);
            this.currentStreamText.set('');
            this.isStreaming.set(false);
          }
        }
      });
  }

  getToneBadgeClass(tone: string): string {
    switch (tone) {
      case 'Professional': return 'badge-info';
      case 'Empathetic': return 'bg-purple-500/15 text-purple-400 border border-purple-500/30';
      case 'Urgent': return 'badge-danger';
      case 'Informational': return 'bg-teal-500/15 text-teal-400 border border-teal-500/30';
      default: return 'badge-neutral';
    }
  }

  autoResize(event: Event): void {
    const textarea = event.target as HTMLTextAreaElement;
    textarea.style.height = 'auto';
    textarea.style.height = Math.min(textarea.scrollHeight, 120) + 'px';
  }
}
