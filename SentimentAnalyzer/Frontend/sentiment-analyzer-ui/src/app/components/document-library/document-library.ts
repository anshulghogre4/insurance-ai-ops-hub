import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, of } from 'rxjs';
import { switchMap, catchError } from 'rxjs/operators';
import { DocumentService } from '../../services/document.service';
import { DocumentSummary, DocumentCategory } from '../../models/document.model';

@Component({
  selector: 'app-document-library',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">

      <!-- Header -->
      <div class="flex items-center justify-between mb-6 animate-fade-in-up">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 flex items-center justify-center shadow-lg shadow-emerald-500/20">
            <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"/>
            </svg>
          </div>
          <div>
            <h1 class="text-xl sm:text-2xl font-bold" [style.color]="'var(--text-primary)'">Document Library</h1>
            <p class="text-xs" [style.color]="'var(--text-muted)'">Browse and manage your uploaded documents</p>
          </div>
        </div>
        <a routerLink="/documents/upload" class="btn-primary text-sm flex items-center gap-2" aria-label="Upload document">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"/>
          </svg>
          Upload
        </a>
      </div>

      <!-- Category Filter -->
      <div class="glass-card-static p-4 mb-5 animate-fade-in-up stagger-1">
        <div class="flex items-center gap-3">
          <label class="text-[10px] uppercase tracking-wider font-semibold" [style.color]="'var(--text-muted)'">Category</label>
          <select [value]="selectedCategory()" (change)="onCategoryChange($event)" class="input-field !py-2 text-sm w-48" aria-label="Filter by category">
            <option value="All">All Categories</option>
            <option value="Policy">Policy</option>
            <option value="Claim">Claim</option>
            <option value="Endorsement">Endorsement</option>
            <option value="Correspondence">Correspondence</option>
            <option value="Other">Other</option>
          </select>
          <button (click)="toggleSort()" class="btn-ghost !py-2 text-xs flex items-center gap-1.5" aria-label="Toggle sort order">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 4h13M3 8h9m-9 4h6m4 0l4-4m0 0l4 4m-4-4v12"/>
            </svg>
            {{ sortOrder() === 'newest' ? 'Newest First' : 'Oldest First' }}
          </button>
          <span class="text-xs ml-auto" [style.color]="'var(--text-muted)'">{{ totalCount() }} document{{ totalCount() === 1 ? '' : 's' }}</span>
        </div>
      </div>

      <!-- Loading -->
      @if (isLoading()) {
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4" aria-busy="true" aria-live="polite" role="status">
          <span class="sr-only">Loading documents...</span>
          @for (i of [1,2,3,4,5,6]; track i) {
            <div class="skeleton h-48 rounded-xl"></div>
          }
        </div>
      }

      <!-- Error State -->
      @if (!isLoading() && error()) {
        <div class="glass-card-static p-4 mb-6 flex items-center gap-3 border-l-4 border-l-rose-500 animate-fade-in-up" role="alert">
          <svg class="w-5 h-5 text-rose-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
          </svg>
          <span class="text-sm text-rose-400">{{ error() }}</span>
          <button (click)="loadDocuments()" class="btn-ghost text-xs ml-auto" aria-label="Retry loading documents">Retry</button>
        </div>
      }

      <!-- Document Cards Grid -->
      @if (!isLoading() && !error() && documents().length > 0) {
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 animate-fade-in-up stagger-2" aria-live="polite">
          @for (doc of documents(); track doc.id; let i = $index) {
            <div class="glass-card-static p-5 cursor-pointer transition-all duration-200 hover:scale-[1.02] hover:shadow-lg group"
                 tabindex="0" role="button"
                 [attr.aria-label]="'View document ' + (doc.fileName || 'Unknown')"
                 (click)="viewDocument(doc.id)"
                 (keydown.enter)="viewDocument(doc.id)">

              <!-- File Icon + Name -->
              <div class="flex items-start gap-3 mb-3">
                <div class="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0"
                     [class]="getFileIconBg(doc.mimeType)">
                  @if (isImageMime(doc.mimeType)) {
                    <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/>
                    </svg>
                  } @else {
                    <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z"/>
                    </svg>
                  }
                </div>
                <div class="min-w-0 flex-1">
                  <p class="text-sm font-medium truncate group-hover:text-indigo-400 transition-colors" [style.color]="'var(--text-primary)'" [title]="doc.fileName || 'Unknown'">
                    {{ doc.fileName || 'Unknown' }}
                  </p>
                  <p class="text-[10px] mt-0.5" [style.color]="'var(--text-muted)'">{{ doc.mimeType || 'N/A' }}</p>
                </div>
              </div>

              <!-- Badges -->
              <div class="flex items-center gap-2 mb-3">
                <span class="inline-block px-2 py-0.5 rounded-full text-[10px] font-bold text-white" [class]="getCategoryBadge(doc.category)">
                  {{ doc.category || 'N/A' }}
                </span>
                <span class="inline-block px-2 py-0.5 rounded-full text-[10px] font-bold text-white" [class]="getStatusBadge(doc.status)">
                  {{ doc.status || 'N/A' }}
                </span>
              </div>

              <!-- Metrics -->
              <div class="flex items-center gap-3 text-[11px] mb-3" [style.color]="'var(--text-muted)'">
                <span class="flex items-center gap-1">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
                  </svg>
                  {{ doc.pageCount ?? 0 }} pages
                </span>
                <span class="flex items-center gap-1">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 7v10c0 2.21 3.582 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.582 4 8 4s8-1.79 8-4M4 7c0-2.21 3.582-4 8-4s8 1.79 8 4"/>
                  </svg>
                  {{ doc.chunkCount ?? 0 }} chunks
                </span>
              </div>

              <!-- Footer: Date + Query button -->
              <div class="flex items-center justify-between pt-3 border-t" [style.border-color]="'var(--border-secondary)'">
                <span class="text-[10px]" [style.color]="'var(--text-muted)'">{{ doc.createdAt ? formatDate(doc.createdAt) : 'Date unavailable' }}</span>
                <a [routerLink]="'/documents/query'" [queryParams]="{documentId: doc.id}"
                   class="p-1.5 rounded-lg transition-colors hover:bg-indigo-500/10 focus-visible:outline focus-visible:outline-2 focus-visible:outline-indigo-500"
                   [attr.aria-label]="'Query document ' + (doc.fileName || 'Unknown')"
                   (click)="$event.stopPropagation()">
                  <svg class="w-4 h-4 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                  </svg>
                </a>
              </div>
            </div>
          }
        </div>

        <!-- Pagination -->
        @if (totalPages() > 1) {
          <div class="flex items-center justify-between mt-6 glass-card-static p-4 animate-fade-in-up stagger-3">
            <button (click)="prevPage()" [disabled]="currentPage() <= 1"
                    class="btn-ghost !py-2 !px-4 text-sm disabled:opacity-40 disabled:cursor-not-allowed" aria-label="Previous page">
              Previous
            </button>
            <span class="text-xs" [style.color]="'var(--text-muted)'">
              Page {{ currentPage() }} of {{ totalPages() }} ({{ totalCount() }} documents)
            </span>
            <button (click)="nextPage()" [disabled]="currentPage() >= totalPages()"
                    class="btn-ghost !py-2 !px-4 text-sm disabled:opacity-40 disabled:cursor-not-allowed" aria-label="Next page">
              Next
            </button>
          </div>
        }
      }

      <!-- Empty State -->
      @if (!isLoading() && !error() && documents().length === 0) {
        <div class="glass-card-static p-12 text-center animate-fade-in" aria-live="polite">
          <svg class="w-16 h-16 mx-auto mb-4" [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"/>
          </svg>
          <p class="text-sm font-medium mb-1" [style.color]="'var(--text-primary)'">No documents uploaded yet</p>
          <p class="text-xs mb-4" [style.color]="'var(--text-muted)'">Upload a document to start querying with AI</p>
          <a routerLink="/documents/upload" class="btn-primary text-sm inline-flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"/>
            </svg>
            Upload Document
          </a>
        </div>
      }
    </div>
  `
})
export class DocumentLibraryComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private documentService = inject(DocumentService);
  private router = inject(Router);
  private loadSubject = new Subject<void>();

  documents = signal<DocumentSummary[]>([]);
  isLoading = signal(true);
  error = signal<string | null>(null);
  selectedCategory = signal<string>('All');
  sortOrder = signal<'newest' | 'oldest'>('newest');
  currentPage = signal(1);
  totalPages = signal(0);
  totalCount = signal(0);

  ngOnInit(): void {
    this.loadSubject.pipe(
      switchMap(() => {
        this.isLoading.set(true);
        this.error.set(null);
        const filters: { category?: DocumentCategory; page?: number; pageSize?: number } = {
          page: this.currentPage(),
          pageSize: 12
        };
        if (this.selectedCategory() !== 'All') {
          filters.category = this.selectedCategory() as DocumentCategory;
        }
        return this.documentService.getDocumentHistory(filters).pipe(
          catchError(() => {
            this.error.set('Failed to load document library. Please try again.');
            this.documents.set([]);
            this.isLoading.set(false);
            return of(null);
          })
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((res) => {
      if (res) {
        this.documents.set(res.items);
        this.sortDocuments();
        this.totalCount.set(res.totalCount);
        this.totalPages.set(res.totalPages);
        this.isLoading.set(false);
      }
    });

    this.loadSubject.next();
  }

  loadDocuments(): void {
    this.loadSubject.next();
  }

  onCategoryChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedCategory.set(select.value);
    this.currentPage.set(1);
    this.loadDocuments();
  }

  viewDocument(id: number): void {
    this.router.navigate(['/documents', id]);
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update(p => p - 1);
      this.loadDocuments();
    }
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(p => p + 1);
      this.loadDocuments();
    }
  }

  toggleSort(): void {
    this.sortOrder.update(s => s === 'newest' ? 'oldest' : 'newest');
    this.sortDocuments();
  }

  private sortDocuments(): void {
    this.documents.update(docs => [...docs].sort((a, b) => {
      const dateA = a.createdAt ? new Date(a.createdAt).getTime() : 0;
      const dateB = b.createdAt ? new Date(b.createdAt).getTime() : 0;
      return this.sortOrder() === 'newest' ? dateB - dateA : dateA - dateB;
    }));
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  isImageMime(mimeType: string): boolean {
    return (mimeType ?? '').startsWith('image/');
  }

  getFileIconBg(mimeType: string): string {
    return this.isImageMime(mimeType) ? 'bg-purple-500' : 'bg-indigo-500';
  }

  getCategoryBadge(category: string): string {
    const map: Record<string, string> = {
      Policy: 'bg-indigo-500',
      Claim: 'bg-amber-500',
      Endorsement: 'bg-emerald-500',
      Correspondence: 'bg-purple-500',
      Other: 'bg-slate-500'
    };
    return map[category] || 'bg-slate-500';
  }

  getStatusBadge(status: string): string {
    const map: Record<string, string> = {
      Processed: 'bg-emerald-500',
      Processing: 'bg-amber-500',
      Error: 'bg-rose-500'
    };
    return map[status] || 'bg-slate-500';
  }
}
