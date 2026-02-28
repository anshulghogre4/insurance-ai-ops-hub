import { Component, computed, DestroyRef, effect, ElementRef, inject, OnInit, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DocumentService } from '../../services/document.service';
import { DocumentDetailResult, DocumentQueryResult, ChunkSummary, QAPair } from '../../models/document.model';

@Component({
  selector: 'app-document-result',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-8">

      <!-- Header -->
      <div class="text-center mb-8 animate-fade-in-up">
        <div class="inline-flex items-center gap-3 mb-3">
          <div class="w-12 h-12 rounded-2xl bg-gradient-to-br from-indigo-500 via-purple-500 to-pink-500 flex items-center justify-center shadow-lg shadow-indigo-500/25">
            <svg class="w-6 h-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
            </svg>
          </div>
          <div>
            <h1 class="text-2xl sm:text-3xl font-bold" [style.color]="'var(--text-primary)'">Document Detail</h1>
            <p class="text-sm" [style.color]="'var(--text-muted)'">View document metadata and indexed chunks</p>
          </div>
        </div>
      </div>

      <!-- Loading -->
      @if (isLoading()) {
        <div class="glass-card-static p-6 animate-fade-in" role="status">
          <div class="flex items-center gap-4">
            <svg class="w-5 h-5 text-indigo-400 animate-spin" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
            </svg>
            <p class="font-semibold" [style.color]="'var(--text-primary)'">Loading document...</p>
          </div>
        </div>
      }

      <!-- Error / Not Found -->
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

      @if (document(); as doc) {
        <div class="space-y-5 animate-fade-in-up" aria-live="polite">

          <!-- Metadata Card -->
          <div class="glass-card-static p-6 sm:p-8">
            <div class="flex items-center gap-3 mb-6">
              <h2 class="text-lg font-bold truncate" [style.color]="'var(--text-primary)'">{{ doc.fileName || 'Untitled Document' }}</h2>
              <span class="badge text-[10px] flex-shrink-0" [class]="getCategoryBadge(doc.category)">{{ doc.category || 'Unknown' }}</span>
              <span class="badge badge-success text-[10px] ml-auto flex-shrink-0">{{ doc.status || 'Unknown' }}</span>
            </div>

            <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-4">
              <div class="metric-card text-center">
                <p class="text-[10px] uppercase tracking-wider mb-1 font-semibold" [style.color]="'var(--text-muted)'">Pages</p>
                <span class="text-lg font-bold" [style.color]="'var(--text-primary)'">{{ doc.pageCount }}</span>
              </div>
              <div class="metric-card text-center">
                <p class="text-[10px] uppercase tracking-wider mb-1 font-semibold" [style.color]="'var(--text-muted)'">Chunks</p>
                <span class="text-lg font-bold" [style.color]="'var(--text-primary)'">{{ doc.chunkCount }}</span>
              </div>
              <div class="metric-card text-center">
                <p class="text-[10px] uppercase tracking-wider mb-1 font-semibold" [style.color]="'var(--text-muted)'">Embeddings</p>
                <span class="badge badge-info text-xs">{{ doc.embeddingProvider || 'N/A' }}</span>
              </div>
              <div class="metric-card text-center">
                <p class="text-[10px] uppercase tracking-wider mb-1 font-semibold" [style.color]="'var(--text-muted)'">Type</p>
                <span class="text-xs" [style.color]="'var(--text-secondary)'">{{ doc.mimeType || 'N/A' }}</span>
              </div>
            </div>

            <p class="text-xs" [style.color]="'var(--text-muted)'">Uploaded {{ doc.createdAt ? formatDate(doc.createdAt) : 'Date unavailable' }}</p>
          </div>

          <!-- Chunks Browser -->
          @if (doc.chunks && doc.chunks.length > 0) {
            <div class="glass-card-static p-6 sm:p-8">
              <h3 class="text-sm font-bold uppercase tracking-wider mb-4" [style.color]="'var(--text-muted)'">
                Document Chunks ({{ doc.chunks.length }})
              </h3>
              <div class="space-y-2 max-h-[400px] overflow-y-auto pr-2 scrollbar-thin">
                @for (chunk of doc.chunks; track chunk.chunkIndex; let i = $index) {
                  <div [class]="chunk.chunkLevel > 0 ? 'ml-6 border-l-2 border-indigo-500/20 pl-4' : ''">
                    <div class="p-3 rounded-xl transition-all duration-200 cursor-pointer"
                         [style.background]="'var(--bg-surface)'"
                         [style.border]="'1px solid var(--border-secondary)'"
                         role="button" tabindex="0"
                         [attr.aria-label]="'Toggle chunk ' + chunk.chunkIndex + ': ' + (chunk.sectionName || 'unnamed section')"
                         [attr.aria-expanded]="expandedChunks().includes(i)"
                         (click)="toggleChunk(i)"
                         (keydown.enter)="toggleChunk(i)"
                         (keydown.space)="$event.preventDefault(); toggleChunk(i)">
                      <div class="flex items-center gap-2 flex-wrap">
                        <span class="text-xs font-mono text-indigo-400 flex-shrink-0">#{{ chunk.chunkIndex }}</span>
                        <span class="badge badge-info text-[10px] flex-shrink-0">{{ chunk.sectionName || 'N/A' }}</span>
                        <span class="text-[10px] flex-shrink-0" [style.color]="'var(--text-muted)'">{{ chunk.tokenCount || 0 }} tokens</span>
                        @if (chunk.pageNumber) {
                          <span class="badge badge-info text-[9px]">Page {{ chunk.pageNumber }}</span>
                        }
                        @if (chunk.chunkLevel === 0 && parentChunkIndices().has(chunk.chunkIndex)) {
                          <span class="badge badge-neutral text-[9px]">Section</span>
                        } @else if (chunk.chunkLevel === 1) {
                          <span class="badge badge-neutral text-[9px]">Sub-chunk</span>
                        }
                        @if (chunk.isSafe === false) {
                          <span class="badge badge-danger text-[10px]" aria-label="Content flagged">Flagged: {{ (chunk.safetyFlags || 'Unknown').split('|').join(', ') }}</span>
                        } @else if (chunk.isSafe === true) {
                          <span class="badge badge-success text-[10px]" aria-label="Content safe">Safe</span>
                        } @else {
                          <span class="badge badge-neutral text-[10px]" aria-label="Content unscreened">Unscreened</span>
                        }
                        <svg class="w-3.5 h-3.5 ml-auto transition-transform flex-shrink-0" [class.rotate-180]="expandedChunks().includes(i)"
                             [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                        </svg>
                      </div>
                      @if (expandedChunks().includes(i)) {
                        <p class="text-xs mt-2 leading-relaxed animate-fade-in p-2 rounded-lg" [style.color]="'var(--text-secondary)'" [style.background]="'var(--input-bg)'">{{ chunk.contentPreview || 'No preview available' }}</p>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>
          }

          <!-- Fine-Tuning Training Data -->
          <div class="glass-card-static p-6 sm:p-8 animate-fade-in-up" aria-label="Fine-tuning training data">
            <div class="flex items-center justify-between mb-4">
              <div class="flex items-center gap-2">
                <svg class="w-5 h-5 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z"/>
                </svg>
                <h3 class="text-sm font-bold uppercase tracking-wider" [style.color]="'var(--text-muted)'">
                  Fine-Tuning Training Data
                </h3>
              </div>
              <div class="flex items-center gap-3">
                @if (qaPairs().length > 0) {
                  <span class="badge badge-info text-[10px]">{{ qaPairs().length }} Training Pairs Generated</span>
                }
                @if (qaProvider()) {
                  <span class="badge badge-neutral text-[9px]">{{ qaProvider() }}</span>
                }
              </div>
            </div>

            <!-- Generate Button -->
            <div class="mb-4">
              <button
                (click)="generateQAPairs()"
                [disabled]="qaLoading()"
                class="btn-primary flex items-center gap-2 text-sm"
                aria-label="Generate QA training pairs"
              >
                @if (qaLoading()) {
                  <svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
                  </svg>
                  <span>Generating...</span>
                } @else {
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
                  </svg>
                  <span>{{ qaPairs().length > 0 ? 'Regenerate Q&A Pairs' : 'Generate Q&A Pairs' }}</span>
                }
              </button>
            </div>

            <!-- QA Error -->
            @if (qaError()) {
              <div class="mb-4 p-3 rounded-xl border border-rose-500/30 bg-rose-500/10 animate-fade-in" role="alert" aria-label="QA generation error">
                <p class="text-xs text-rose-400">{{ qaError() }}</p>
              </div>
            }

            <!-- QA Pairs List -->
            @if (qaPairs().length > 0) {
              <div class="space-y-3 max-h-[500px] overflow-y-auto pr-2 scrollbar-thin">
                @for (pair of qaPairs(); track pair.id) {
                  <div class="p-4 rounded-xl animate-fade-in-up focus-visible:ring-2 focus-visible:ring-indigo-500/50 focus-visible:outline-none" [style.background]="'var(--bg-surface)'" [style.border]="'1px solid var(--border-secondary)'"
                       role="button" tabindex="0"
                       [attr.aria-label]="'Q&A pair: ' + (pair.question || 'Question')"
                       [attr.aria-expanded]="expandedQAPairs().includes(pair.id)"
                       (click)="toggleQAPair(pair.id)"
                       (keydown.enter)="toggleQAPair(pair.id)"
                       (keydown.space)="$event.preventDefault(); toggleQAPair(pair.id)">
                    <div class="flex items-start gap-3">
                      <div class="flex-1 min-w-0">
                        <p class="text-sm font-semibold leading-relaxed" [style.color]="'var(--text-primary)'">{{ pair.question || 'N/A' }}</p>
                        <div class="flex items-center gap-2 mt-2 flex-wrap">
                          <span class="badge text-[10px]" [class]="getQACategoryBadge(pair.category)">{{ pair.category || 'N/A' }}</span>
                          <span class="text-[10px]" [style.color]="'var(--text-muted)'">Confidence: {{ pair.confidence != null ? (pair.confidence * 100).toFixed(0) + '%' : 'N/A' }}</span>
                          <span class="badge badge-info text-[9px]">{{ pair.sectionName || 'N/A' }}</span>
                        </div>
                      </div>
                      <svg class="w-3.5 h-3.5 transition-transform flex-shrink-0 mt-1" [class.rotate-180]="expandedQAPairs().includes(pair.id)"
                           [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                      </svg>
                    </div>
                    @if (expandedQAPairs().includes(pair.id)) {
                      <div class="mt-3 p-3 rounded-lg animate-fade-in" [style.background]="'var(--input-bg)'">
                        <p class="text-xs leading-relaxed" [style.color]="'var(--text-secondary)'">{{ pair.answer || 'N/A' }}</p>
                      </div>
                    }
                  </div>
                }
              </div>
            } @else if (!qaLoading() && !qaError()) {
              <div class="text-center py-6" aria-label="No training data">
                <svg class="w-10 h-10 mx-auto mb-2 opacity-30" [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z"/>
                </svg>
                <p class="text-sm" [style.color]="'var(--text-muted)'">No training data yet</p>
                <p class="text-xs mt-1" [style.color]="'var(--text-muted)'">Click "Generate Q&A Pairs" to create synthetic training data from this document.</p>
              </div>
            }
          </div>

          <!-- Inline Q&A -->
          <div class="glass-card-static p-6 sm:p-8">
            <h3 class="text-sm font-bold uppercase tracking-wider mb-4" [style.color]="'var(--text-muted)'">
              Ask About This Document
            </h3>
            <div class="flex items-end gap-3">
              <textarea
                [(ngModel)]="inlineQuestion"
                class="input-field min-h-[44px] max-h-[100px] resize-none flex-1"
                placeholder="Ask a question about this document..."
                [maxLength]="2000"
                aria-label="Inline document question"
                (keydown.control.enter)="submitInlineQuery()"
                (keydown.meta.enter)="submitInlineQuery()"
              ></textarea>
              <button
                (click)="submitInlineQuery()"
                [disabled]="!inlineQuestion.trim() || isQuerying()"
                class="btn-primary p-3 rounded-xl flex-shrink-0"
                aria-label="Submit question"
              >
                @if (isQuerying()) {
                  <svg class="w-5 h-5 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
                  </svg>
                } @else {
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                  </svg>
                }
              </button>
            </div>

            @if (queryError()) {
              <div class="mt-4 p-3 rounded-xl border border-rose-500/30 bg-rose-500/10 animate-fade-in" role="alert" aria-label="Query error">
                <p class="text-xs text-rose-400">{{ queryError() }}</p>
              </div>
            }

            @if (queryResult(); as qr) {
              @if (qr.answerSafety?.isSafe === false) {
                <div class="mt-4 glass-card-static p-5 border-l-4 border-rose-500 animate-fade-in" role="alert" aria-label="Content safety warning">
                  <div class="flex items-center gap-3">
                    <svg class="w-5 h-5 text-rose-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
                    </svg>
                    <p class="text-sm text-rose-400">Content Safety Warning: This response has been flagged for: {{ qr.answerSafety?.flaggedCategories?.join(', ') || 'Unknown categories' }}</p>
                  </div>
                </div>
              }
              <div class="mt-4 p-4 rounded-xl animate-fade-in" [style.background]="'var(--bg-surface)'" [style.border]="'1px solid var(--border-secondary)'">
                <p class="text-sm leading-relaxed" [style.color]="'var(--text-secondary)'">{{ qr.answer || 'No answer available' }}</p>
                <div class="flex items-center gap-2 mt-2">
                  <span class="text-[10px]" [style.color]="'var(--text-muted)'">Confidence: {{ qr.confidence != null ? (qr.confidence * 100).toFixed(0) + '%' : 'N/A' }}</span>
                  <span class="badge badge-neutral text-[9px]">{{ qr.llmProvider || 'Unknown' }}</span>
                </div>
              </div>
            }
          </div>

          <!-- Actions -->
          <div class="flex flex-wrap gap-3">
            <a [routerLink]="['/documents/query']" [queryParams]="{ documentId: doc.id }" class="btn-primary flex items-center gap-2 text-sm">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
              </svg>
              Full Query Interface
            </a>
            <button (click)="showDeleteModal.set(true)" class="btn-ghost text-sm flex items-center gap-2 text-rose-400 hover:text-rose-300">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/>
              </svg>
              Delete Document
            </button>
            <a routerLink="/documents/upload" class="btn-ghost text-sm flex items-center gap-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18"/>
              </svg>
              Back
            </a>
          </div>
        </div>
      }

      <!-- Delete Confirmation Modal -->
      @if (showDeleteModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-label="Delete document confirmation" (keydown.escape)="showDeleteModal.set(false)">
          <div class="absolute inset-0 bg-black/50 backdrop-blur-sm" (click)="showDeleteModal.set(false)"></div>
          <div class="glass-card-static p-6 rounded-2xl max-w-md w-full relative z-10 animate-fade-in"
               (keydown)="$event.key === 'Tab' && trapFocus($event)">
            <h3 class="text-lg font-bold mb-3" [style.color]="'var(--text-primary)'">Delete Document?</h3>
            <p class="text-sm mb-6" [style.color]="'var(--text-secondary)'">
              This will permanently delete the document and all its indexed chunks. This action cannot be undone.
            </p>
            <div class="flex gap-3 justify-end">
              <button #cancelBtn (click)="showDeleteModal.set(false)" class="btn-ghost text-sm">Cancel</button>
              <button (click)="deleteDocument()" [disabled]="isDeleting()" class="px-4 py-2 rounded-lg bg-rose-500 text-white text-sm font-medium hover:bg-rose-600 transition-colors flex items-center gap-2">
                @if (isDeleting()) {
                  <svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
                  </svg>
                }
                Delete
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class DocumentResultComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private documentService = inject(DocumentService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  @ViewChild('cancelBtn') cancelBtn?: ElementRef<HTMLButtonElement>;

  documentId = signal<number>(0);
  document = signal<DocumentDetailResult | null>(null);
  isLoading = signal(true);
  error = signal<string | null>(null);
  expandedChunks = signal<number[]>([]);
  showDeleteModal = signal(false);
  isDeleting = signal(false);

  private modalFocusEffect = effect(() => {
    if (this.showDeleteModal()) {
      setTimeout(() => this.cancelBtn?.nativeElement?.focus(), 0);
    }
  });

  // Fine-Tuning Q&A state
  qaPairs = signal<QAPair[]>([]);
  qaLoading = signal(false);
  qaError = signal<string | null>(null);
  qaProvider = signal<string>('');
  expandedQAPairs = signal<number[]>([]);

  inlineQuestion = '';
  isQuerying = signal(false);
  queryResult = signal<DocumentQueryResult | null>(null);
  queryError = signal<string | null>(null);

  ngOnInit(): void {
    this.route.params
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        const id = +params['id'];
        if (!id || isNaN(id)) {
          this.error.set('Invalid document ID.');
          this.isLoading.set(false);
          return;
        }
        this.documentId.set(id);
        this.loadDocument(id);
        this.loadQAPairs(id);
      });
  }

  private loadDocument(id: number): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.documentService.getDocumentById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (doc) => {
          this.document.set(doc);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.error.set(err.status === 404 ? 'Document not found.' : 'Failed to load document.');
          this.isLoading.set(false);
        }
      });
  }

  /** Load existing Q&A pairs for this document. */
  private loadQAPairs(id: number): void {
    this.documentService.getQAPairs(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.qaPairs.set(result.pairs ?? []);
          this.qaProvider.set(result.llmProvider ?? '');
        },
        error: () => {
          // Silently ignore — no existing pairs is fine
        }
      });
  }

  /** Generate synthetic Q&A pairs for this document. */
  generateQAPairs(): void {
    if (this.qaLoading()) return;

    this.qaLoading.set(true);
    this.qaError.set(null);

    this.documentService.generateQAPairs(this.documentId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.qaPairs.set(result.pairs ?? []);
          this.qaProvider.set(result.llmProvider ?? '');
          this.qaLoading.set(false);
          if (result.errorMessage) {
            this.qaError.set(result.errorMessage);
          }
        },
        error: (err) => {
          this.qaError.set(err.error?.errorMessage || err.error?.error || 'Failed to generate Q&A pairs. Please try again.');
          this.qaLoading.set(false);
        }
      });
  }

  /** Toggle expansion of a Q&A pair card. */
  toggleQAPair(id: number): void {
    this.expandedQAPairs.update(arr =>
      arr.includes(id) ? arr.filter(i => i !== id) : [...arr, id]
    );
  }

  /** Get Tailwind badge classes for Q&A category. */
  getQACategoryBadge(category: string): string {
    switch (category?.toLowerCase()) {
      case 'factual': return 'bg-indigo-500/15 text-indigo-400 border border-indigo-500/30';
      case 'inferential': return 'bg-amber-500/15 text-amber-400 border border-amber-500/30';
      case 'procedural': return 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30';
      default: return 'badge-neutral';
    }
  }

  submitInlineQuery(): void {
    if (!this.inlineQuestion.trim() || this.isQuerying()) return;

    this.isQuerying.set(true);
    this.queryResult.set(null);
    this.queryError.set(null);

    this.documentService.queryDocuments(this.inlineQuestion, this.documentId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.queryResult.set(result);
          this.isQuerying.set(false);
        },
        error: (err) => {
          this.queryError.set(err.error?.error || 'Failed to query document. Please try again.');
          this.isQuerying.set(false);
        }
      });
  }

  deleteDocument(): void {
    this.isDeleting.set(true);
    this.documentService.deleteDocument(this.documentId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.router.navigate(['/documents/upload']);
        },
        error: () => {
          this.isDeleting.set(false);
          this.showDeleteModal.set(false);
          this.error.set('Failed to delete document.');
        }
      });
  }

  /** Traps focus within the delete modal dialog. */
  trapFocus(event: Event): void {
    const keyEvent = event as KeyboardEvent;
    const modal = (keyEvent.target as HTMLElement).closest('[role="dialog"]');
    if (!modal) return;
    const focusable = modal.querySelectorAll<HTMLElement>('button:not([disabled]), [tabindex]:not([tabindex="-1"])');
    if (focusable.length === 0) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (keyEvent.shiftKey && document.activeElement === first) {
      keyEvent.preventDefault();
      last.focus();
    } else if (!keyEvent.shiftKey && document.activeElement === last) {
      keyEvent.preventDefault();
      first.focus();
    }
  }

  /** Pre-computed set of chunk indices that are parents (O(1) lookup instead of O(n) per chunk). */
  parentChunkIndices = computed(() => {
    const doc = this.document();
    if (!doc?.chunks) return new Set<number>();
    const parents = new Set<number>();
    for (const c of doc.chunks) {
      if (c.parentChunkId != null && c.chunkLevel === 1) {
        parents.add(c.parentChunkId);
      }
    }
    return parents;
  });

  toggleChunk(index: number): void {
    this.expandedChunks.update(arr =>
      arr.includes(index) ? arr.filter(i => i !== index) : [...arr, index]
    );
  }

  getCategoryBadge(category: string): string {
    switch (category) {
      case 'Policy': return 'bg-indigo-500/15 text-indigo-400 border border-indigo-500/30';
      case 'Claim': return 'bg-purple-500/15 text-purple-400 border border-purple-500/30';
      case 'Endorsement': return 'bg-cyan-500/15 text-cyan-400 border border-cyan-500/30';
      case 'Correspondence': return 'bg-teal-500/15 text-teal-400 border border-teal-500/30';
      default: return 'badge-neutral';
    }
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit'
    });
  }
}
