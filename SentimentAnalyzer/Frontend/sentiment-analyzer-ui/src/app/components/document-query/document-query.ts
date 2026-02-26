import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DocumentService } from '../../services/document.service';
import { DocumentQueryResult, DocumentSummary } from '../../models/document.model';

@Component({
  selector: 'app-document-query',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-8">

      <!-- Header -->
      <div class="text-center mb-8 animate-fade-in-up">
        <div class="inline-flex items-center gap-3 mb-3">
          <div class="w-12 h-12 rounded-2xl bg-gradient-to-br from-indigo-500 via-purple-500 to-pink-500 flex items-center justify-center shadow-lg shadow-indigo-500/25">
            <svg class="w-6 h-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
            </svg>
          </div>
          <div>
            <h1 class="text-2xl sm:text-3xl font-bold" [style.color]="'var(--text-primary)'">Document Query</h1>
            <p class="text-sm" [style.color]="'var(--text-muted)'">Ask questions about your insurance documents</p>
          </div>
        </div>
      </div>

      <!-- Query Form -->
      <div class="glass-card-static p-6 sm:p-8 mb-6 animate-fade-in-up stagger-1">

        <!-- Document Filter -->
        <label for="documentFilter" class="block text-sm font-semibold mb-2" [style.color]="'var(--text-secondary)'">
          Scope (Optional)
        </label>
        <select
          id="documentFilter"
          [(ngModel)]="selectedDocumentId"
          class="input-field mb-5"
          aria-label="Document scope filter"
        >
          <option [ngValue]="null">All documents</option>
          @for (doc of documents(); track doc.id) {
            <option [ngValue]="doc.id">{{ doc.fileName }} ({{ doc.category }})</option>
          }
        </select>

        <!-- Question -->
        <label for="question" class="block text-sm font-semibold mb-2" [style.color]="'var(--text-secondary)'">
          Your Question
        </label>
        <textarea
          id="question"
          [(ngModel)]="question"
          class="input-field min-h-[100px] resize-y"
          placeholder="e.g., What is the coverage limit for water damage? What are the exclusions for flood damage?"
          [maxLength]="2000"
          aria-label="Document query question"
          (keydown.control.enter)="submitQuery()"
          (keydown.meta.enter)="submitQuery()"
        ></textarea>
        <div class="flex justify-between items-center mt-1.5 text-xs" [style.color]="'var(--text-muted)'">
          <span>Ctrl+Enter to submit</span>
          <span [class.text-rose-400]="question.length > 1900">{{ question.length }} / 2,000</span>
        </div>

        <div class="mt-5">
          <button
            (click)="submitQuery()"
            [disabled]="!question.trim() || isQuerying()"
            class="btn-primary flex items-center gap-2"
            aria-label="Submit document query"
          >
            @if (isQuerying()) {
              <svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
              </svg>
              Searching...
            } @else {
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
              </svg>
              Ask
            }
          </button>
        </div>
      </div>

      <!-- Error State -->
      @if (error()) {
        <div class="glass-card-static p-5 mb-6 border-l-4 border-rose-500 animate-fade-in" role="alert">
          <div class="flex items-center gap-3">
            <svg class="w-5 h-5 text-rose-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
            </svg>
            <p class="text-sm" [style.color]="'var(--text-primary)'">{{ error() }}</p>
          </div>
        </div>
      }

      <!-- Query Result -->
      @if (queryResult(); as res) {
        <div class="space-y-5 animate-fade-in-up" aria-live="polite">

          <!-- Answer Card -->
          <div class="glass-card-static p-6 sm:p-8">
            <div class="flex items-center gap-3 mb-4">
              <svg class="w-5 h-5 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z"/>
              </svg>
              <h2 class="text-lg font-bold" [style.color]="'var(--text-primary)'">Answer</h2>
            </div>
            <p class="text-sm leading-relaxed whitespace-pre-wrap" [style.color]="'var(--text-secondary)'">{{ res.answer }}</p>

            <!-- Confidence + Metadata -->
            <div class="mt-5 flex items-center gap-4">
              <div class="flex-1">
                <div class="flex items-center justify-between mb-1">
                  <span class="text-xs font-semibold" [style.color]="'var(--text-muted)'">Retrieval Confidence</span>
                  <span class="text-sm font-bold" [class]="getConfidenceColor(res.confidence)">{{ (res.confidence * 100).toFixed(0) }}%</span>
                </div>
                <div class="h-2 rounded-full overflow-hidden" [style.background]="'var(--bg-surface-hover)'">
                  <div class="h-full rounded-full transition-all duration-1000" [style.width.%]="res.confidence * 100" [style.background]="getConfidenceGradient(res.confidence)"></div>
                </div>
              </div>
              <span class="badge badge-neutral text-[9px]">{{ res.llmProvider }}</span>
              <span class="text-[10px]" [style.color]="'var(--text-muted)'">{{ (res.elapsedMilliseconds / 1000).toFixed(1) }}s</span>
            </div>
          </div>

          <!-- Citations -->
          @if (res.citations?.length) {
            <div class="glass-card-static p-6 sm:p-8">
              <h3 class="text-sm font-bold uppercase tracking-wider mb-4" [style.color]="'var(--text-muted)'">
                Citations ({{ res.citations.length }})
              </h3>
              <div class="space-y-3">
                @for (citation of res.citations; track citation.chunkIndex; let i = $index) {
                  <div class="p-4 rounded-xl transition-all duration-200 cursor-pointer animate-fade-in-up"
                       [class]="'stagger-' + Math.min(i + 1, 5)"
                       [style.background]="'var(--bg-surface)'"
                       [style.border]="'1px solid var(--border-secondary)'"
                       role="button" tabindex="0"
                       [attr.aria-expanded]="expandedCitations().includes(i)"
                       (click)="toggleCitation(i)"
                       (keydown.enter)="toggleCitation(i)"
                       (keydown.space)="$event.preventDefault(); toggleCitation(i)">
                    <div class="flex items-start gap-3">
                      <span class="badge badge-info text-[10px] flex-shrink-0 mt-0.5">{{ citation.sectionName }}</span>
                      <div class="flex-1 min-w-0">
                        <p class="text-sm font-medium truncate" [style.color]="'var(--text-primary)'">{{ citation.fileName }}</p>
                        <div class="flex items-center gap-2 mt-1">
                          <span class="text-[10px]" [style.color]="'var(--text-muted)'">Chunk #{{ citation.chunkIndex }}</span>
                          <div class="flex items-center gap-1">
                            <div class="w-12 h-1.5 rounded-full overflow-hidden" [style.background]="'var(--bg-surface-hover)'">
                              <div class="h-full rounded-full bg-indigo-500" [style.width.%]="citation.similarity * 100"></div>
                            </div>
                            <span class="text-[10px] font-medium" [style.color]="'var(--text-muted)'">{{ (citation.similarity * 100).toFixed(0) }}%</span>
                          </div>
                        </div>
                        @if (expandedCitations().includes(i)) {
                          <p class="text-xs mt-3 leading-relaxed animate-fade-in p-3 rounded-lg" [style.color]="'var(--text-secondary)'" [style.background]="'var(--input-bg)'">{{ citation.relevantText }}</p>
                        }
                      </div>
                      <svg class="w-4 h-4 transition-transform flex-shrink-0" [class.rotate-180]="expandedCitations().includes(i)"
                           [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                      </svg>
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class DocumentQueryComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private documentService = inject(DocumentService);
  private route = inject(ActivatedRoute);

  Math = Math;

  question = '';
  selectedDocumentId: number | null = null;

  documents = signal<DocumentSummary[]>([]);
  isQuerying = signal(false);
  queryResult = signal<DocumentQueryResult | null>(null);
  error = signal<string | null>(null);
  expandedCitations = signal<number[]>([]);

  ngOnInit(): void {
    // Load documents for filter dropdown
    this.documentService.getDocumentHistory({ pageSize: 100 })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => this.documents.set(res.items),
        error: () => {} // Silently fail — dropdown just won't have options
      });

    // Read documentId from query params
    this.route.queryParams
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        if (params['documentId']) {
          this.selectedDocumentId = +params['documentId'];
        }
      });
  }

  submitQuery(): void {
    if (!this.question.trim() || this.isQuerying()) return;

    this.isQuerying.set(true);
    this.error.set(null);
    this.queryResult.set(null);
    this.expandedCitations.set([]);

    this.documentService.queryDocuments(this.question, this.selectedDocumentId ?? undefined)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.queryResult.set(result);
          this.isQuerying.set(false);
        },
        error: (err) => {
          this.error.set(err.error?.error || 'Failed to query documents. Please try again.');
          this.isQuerying.set(false);
        }
      });
  }

  toggleCitation(index: number): void {
    this.expandedCitations.update(arr =>
      arr.includes(index) ? arr.filter(i => i !== index) : [...arr, index]
    );
  }

  getConfidenceColor(confidence: number): string {
    if (confidence >= 0.7) return 'text-emerald-400';
    if (confidence >= 0.4) return 'text-amber-400';
    return 'text-rose-400';
  }

  getConfidenceGradient(confidence: number): string {
    if (confidence >= 0.7) return 'linear-gradient(90deg, #10b981, #34d399)';
    if (confidence >= 0.4) return 'linear-gradient(90deg, #f59e0b, #fbbf24)';
    return 'linear-gradient(90deg, #ef4444, #f87171)';
  }
}
