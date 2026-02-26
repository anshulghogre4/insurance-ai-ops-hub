import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DocumentService } from '../../services/document.service';
import { DocumentDetailResult, DocumentQueryResult } from '../../models/document.model';

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
              <h2 class="text-lg font-bold truncate" [style.color]="'var(--text-primary)'">{{ doc.fileName }}</h2>
              <span class="badge text-[10px] flex-shrink-0" [class]="getCategoryBadge(doc.category)">{{ doc.category }}</span>
              <span class="badge badge-success text-[10px] ml-auto flex-shrink-0">{{ doc.status }}</span>
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
                <span class="badge badge-info text-xs">{{ doc.embeddingProvider }}</span>
              </div>
              <div class="metric-card text-center">
                <p class="text-[10px] uppercase tracking-wider mb-1 font-semibold" [style.color]="'var(--text-muted)'">Type</p>
                <span class="text-xs" [style.color]="'var(--text-secondary)'">{{ doc.mimeType }}</span>
              </div>
            </div>

            <p class="text-xs" [style.color]="'var(--text-muted)'">Uploaded {{ formatDate(doc.createdAt) }}</p>
          </div>

          <!-- Chunks Browser -->
          @if (doc.chunks?.length) {
            <div class="glass-card-static p-6 sm:p-8">
              <h3 class="text-sm font-bold uppercase tracking-wider mb-4" [style.color]="'var(--text-muted)'">
                Document Chunks ({{ doc.chunks.length }})
              </h3>
              <div class="space-y-2 max-h-[400px] overflow-y-auto pr-2 scrollbar-thin">
                @for (chunk of doc.chunks; track chunk.chunkIndex; let i = $index) {
                  <div class="p-3 rounded-xl transition-all duration-200 cursor-pointer"
                       [style.background]="'var(--bg-surface)'"
                       [style.border]="'1px solid var(--border-secondary)'"
                       role="button" tabindex="0"
                       [attr.aria-expanded]="expandedChunks().includes(i)"
                       (click)="toggleChunk(i)"
                       (keydown.enter)="toggleChunk(i)"
                       (keydown.space)="$event.preventDefault(); toggleChunk(i)">
                    <div class="flex items-center gap-3">
                      <span class="text-xs font-mono text-indigo-400 flex-shrink-0">#{{ chunk.chunkIndex }}</span>
                      <span class="badge badge-info text-[10px] flex-shrink-0">{{ chunk.sectionName }}</span>
                      <span class="text-[10px] flex-shrink-0" [style.color]="'var(--text-muted)'">{{ chunk.tokenCount }} tokens</span>
                      <svg class="w-3.5 h-3.5 ml-auto transition-transform flex-shrink-0" [class.rotate-180]="expandedChunks().includes(i)"
                           [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                      </svg>
                    </div>
                    @if (expandedChunks().includes(i)) {
                      <p class="text-xs mt-2 leading-relaxed animate-fade-in p-2 rounded-lg" [style.color]="'var(--text-secondary)'" [style.background]="'var(--input-bg)'">{{ chunk.contentPreview }}</p>
                    }
                  </div>
                }
              </div>
            </div>
          }

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
              <div class="mt-4 p-3 rounded-xl border border-rose-500/30 bg-rose-500/10 animate-fade-in" role="alert">
                <p class="text-xs text-rose-400">{{ queryError() }}</p>
              </div>
            }

            @if (queryResult(); as qr) {
              <div class="mt-4 p-4 rounded-xl animate-fade-in" [style.background]="'var(--bg-surface)'" [style.border]="'1px solid var(--border-secondary)'">
                <p class="text-sm leading-relaxed" [style.color]="'var(--text-secondary)'">{{ qr.answer }}</p>
                <div class="flex items-center gap-2 mt-2">
                  <span class="text-[10px]" [style.color]="'var(--text-muted)'">Confidence: {{ (qr.confidence * 100).toFixed(0) }}%</span>
                  <span class="badge badge-neutral text-[9px]">{{ qr.llmProvider }}</span>
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
          <div class="glass-card-static p-6 rounded-2xl max-w-md w-full relative z-10 animate-fade-in">
            <h3 class="text-lg font-bold mb-3" [style.color]="'var(--text-primary)'">Delete Document?</h3>
            <p class="text-sm mb-6" [style.color]="'var(--text-secondary)'">
              This will permanently delete the document and all its indexed chunks. This action cannot be undone.
            </p>
            <div class="flex gap-3 justify-end">
              <button (click)="showDeleteModal.set(false)" class="btn-ghost text-sm">Cancel</button>
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

  documentId = signal<number>(0);
  document = signal<DocumentDetailResult | null>(null);
  isLoading = signal(true);
  error = signal<string | null>(null);
  expandedChunks = signal<number[]>([]);
  showDeleteModal = signal(false);
  isDeleting = signal(false);

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
