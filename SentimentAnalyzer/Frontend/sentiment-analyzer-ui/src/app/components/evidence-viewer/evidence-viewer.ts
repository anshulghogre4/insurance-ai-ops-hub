import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ClaimEvidenceResponse } from '../../models/claims.model';

@Component({
  selector: 'app-evidence-viewer',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-4 rounded-xl transition-all duration-200"
         [style.background]="'var(--bg-surface)'"
         [style.border]="'1px solid var(--border-secondary)'">
      <div class="flex items-start gap-3">
        <!-- Evidence Type Icon -->
        <div class="w-10 h-10 rounded-xl flex items-center justify-center flex-shrink-0"
             [class]="getIconBg()">
          @switch (getTypeIcon()) {
            @case ('camera') {
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z"/><circle cx="12" cy="13" r="3"/></svg>
            }
            @case ('microphone') {
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z"/></svg>
            }
            @case ('document') {
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z"/></svg>
            }
            @default {
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M15.172 7l-6.586 6.586a2 2 0 102.828 2.828l6.414-6.586a4 4 0 00-5.656-5.656l-6.415 6.585a6 6 0 108.486 8.486L20.5 13"/></svg>
            }
          }
        </div>

        <div class="flex-1 min-w-0">
          <!-- Header -->
          <div class="flex items-center gap-2 flex-wrap mb-2">
            <span class="text-sm font-semibold" [style.color]="'var(--text-primary)'">
              {{ getTypeLabel() }}
            </span>
            <span class="badge text-[10px]" [class]="getProviderBadge()">{{ evidence.provider }}</span>
          </div>

          <!-- Processed Text -->
          @if (evidence.processedText) {
            <div class="p-3 rounded-lg text-xs leading-relaxed mb-2"
                 [style.background]="'var(--input-bg)'"
                 [style.color]="'var(--text-secondary)'"
                 [style.border]="'1px solid var(--border-secondary)'">
              <p class="whitespace-pre-wrap">{{ evidence.processedText }}</p>
            </div>
          }

          <!-- Damage Indicators -->
          @if (evidence.damageIndicators?.length) {
            <div class="flex flex-wrap gap-1.5">
              @for (indicator of evidence.damageIndicators; track indicator) {
                <span class="px-2 py-0.5 rounded-md text-[10px] font-medium bg-rose-500/10 text-rose-400 border border-rose-500/20">
                  {{ indicator }}
                </span>
              }
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class EvidenceViewerComponent {
  @Input({ required: true }) evidence!: ClaimEvidenceResponse;

  getTypeIcon(): string {
    switch (this.evidence.evidenceType) {
      case 'image': return 'camera';
      case 'audio': return 'microphone';
      case 'document': return 'document';
      default: return 'paperclip';
    }
  }

  getIconBg(): string {
    switch (this.evidence.evidenceType) {
      case 'image': return 'bg-blue-500/15';
      case 'audio': return 'bg-purple-500/15';
      case 'document': return 'bg-amber-500/15';
      default: return 'bg-slate-500/15';
    }
  }

  getTypeLabel(): string {
    switch (this.evidence.evidenceType) {
      case 'image': return 'Image Analysis';
      case 'audio': return 'Audio Transcription';
      case 'document': return 'Document OCR';
      default: return 'Evidence';
    }
  }

  getProviderBadge(): string {
    if (this.evidence.provider.includes('Vision') || this.evidence.provider.includes('Cloudflare')) return 'badge-info';
    if (this.evidence.provider.includes('Deepgram')) return 'badge-success';
    if (this.evidence.provider.includes('Ocr')) return 'badge-warning';
    return 'badge-neutral';
  }
}
