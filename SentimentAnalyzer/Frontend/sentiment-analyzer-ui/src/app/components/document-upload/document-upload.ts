import { Component, DestroyRef, inject, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DocumentService } from '../../services/document.service';
import { DocumentUploadResult, DocumentCategory } from '../../models/document.model';

@Component({
  selector: 'app-document-upload',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-8">

      <!-- Header -->
      <div class="text-center mb-8 animate-fade-in-up">
        <div class="inline-flex items-center gap-3 mb-3">
          <div class="w-12 h-12 rounded-2xl bg-gradient-to-br from-indigo-500 via-purple-500 to-pink-500 flex items-center justify-center shadow-lg shadow-indigo-500/25">
            <svg class="w-6 h-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/>
            </svg>
          </div>
          <div>
            <h1 class="text-2xl sm:text-3xl font-bold" [style.color]="'var(--text-primary)'">Document Upload</h1>
            <p class="text-sm" [style.color]="'var(--text-muted)'">Upload insurance documents for RAG-powered intelligence</p>
          </div>
        </div>
      </div>

      <!-- Upload Card -->
      <div class="glass-card-static p-6 sm:p-8 mb-6 animate-fade-in-up stagger-1">

        <!-- Category Selector -->
        <label for="category" class="block text-sm font-semibold mb-2" [style.color]="'var(--text-secondary)'">
          Document Category
        </label>
        <select
          id="category"
          [(ngModel)]="category"
          class="input-field mb-5"
          aria-label="Document category selector"
        >
          @for (cat of categories; track cat) {
            <option [value]="cat">{{ cat }}</option>
          }
        </select>

        <!-- Drop Zone -->
        <div
          class="relative rounded-xl border-2 border-dashed p-8 text-center transition-all duration-200"
          [class.border-indigo-500]="isDragOver()"
          [class.bg-indigo-500/5]="isDragOver()"
          [style.border-color]="isDragOver() ? '' : 'var(--border-primary)'"
          [style.background]="isDragOver() ? '' : 'var(--input-bg)'"
          (dragover)="onDragOver($event)"
          (dragleave)="isDragOver.set(false)"
          (drop)="onDrop($event)"
        >
          <input
            type="file"
            id="fileUpload"
            class="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
            accept=".pdf,.png,.jpg,.jpeg,.tiff,.tif"
            (change)="onFileSelected($event)"
            aria-label="Upload document file"
          />
          <svg class="w-10 h-10 mx-auto mb-3" [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/>
          </svg>
          <p class="text-sm font-medium" [style.color]="'var(--text-secondary)'">
            Drop file here or <span class="text-indigo-400 underline">browse</span>
          </p>
          <p class="text-xs mt-1" [style.color]="'var(--text-muted)'">PDF, PNG, JPEG, TIFF (max 5 MB)</p>
        </div>

        <!-- Selected File -->
        @if (selectedFile()) {
          <div class="mt-4 flex items-center gap-3 p-3 rounded-lg" [style.background]="'var(--bg-surface)'" [style.border]="'1px solid var(--border-secondary)'">
            <div class="w-8 h-8 rounded-lg bg-indigo-500/15 flex items-center justify-center">
              <svg class="w-4 h-4 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z"/>
              </svg>
            </div>
            <div class="flex-1 min-w-0">
              <p class="text-sm font-medium truncate" [style.color]="'var(--text-primary)'">{{ selectedFile()!.name }}</p>
              <p class="text-xs" [style.color]="'var(--text-muted)'">{{ formatFileSize(selectedFile()!.size) }}</p>
            </div>
            <span class="badge badge-info text-[10px]">{{ category }}</span>
            <button (click)="selectedFile.set(null)" class="p-1 rounded-lg transition-colors hover:bg-rose-500/10 text-rose-400" aria-label="Remove file">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>
        }

        <!-- Upload Button -->
        <div class="mt-6">
          <button
            (click)="uploadDocument()"
            [disabled]="!selectedFile() || isUploading()"
            class="btn-primary flex items-center gap-2"
            aria-label="Upload document for processing"
          >
            @if (isUploading()) {
              <svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
              </svg>
              Processing...
            } @else {
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/>
              </svg>
              Upload & Process
            }
          </button>
        </div>
      </div>

      <!-- Loading State -->
      @if (isUploading()) {
        <div class="glass-card-static p-6 sm:p-8 mb-6 animate-fade-in" role="status" aria-live="polite">
          <div class="flex items-center gap-4 mb-5">
            <div class="w-10 h-10 rounded-xl bg-indigo-500/20 flex items-center justify-center">
              <svg class="w-5 h-5 text-indigo-400 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
              </svg>
            </div>
            <div>
              <p class="font-semibold" [style.color]="'var(--text-primary)'">{{ getUploadPhase() }}</p>
              <p class="text-xs" [style.color]="'var(--text-muted)'">{{ elapsedSeconds() }}s elapsed</p>
            </div>
          </div>
          <div class="progress-track" role="progressbar" [attr.aria-valuenow]="elapsedSeconds()" aria-valuemin="0" aria-valuemax="30">
            <div class="progress-fill bg-gradient-to-r from-indigo-500 via-purple-500 to-pink-500" [style.width.%]="Math.min((elapsedSeconds() / 25) * 100, 95)"></div>
          </div>
        </div>
      }

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

      <!-- Upload Result -->
      @if (uploadResult(); as res) {
        <div class="glass-card-static p-6 sm:p-8 mb-6 animate-fade-in-up" aria-live="polite">
          <div class="flex items-center gap-3 mb-6">
            <svg class="w-5 h-5 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
            </svg>
            <h2 class="text-lg font-bold" [style.color]="'var(--text-primary)'">Document Processed</h2>
            <span class="badge badge-success ml-auto">{{ res.status }}</span>
          </div>

          <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
            <div class="metric-card text-center">
              <p class="text-[10px] uppercase tracking-wider mb-1.5 font-semibold" [style.color]="'var(--text-muted)'">Document ID</p>
              <span class="text-lg font-bold text-indigo-400">#{{ res.documentId }}</span>
            </div>
            <div class="metric-card text-center">
              <p class="text-[10px] uppercase tracking-wider mb-1.5 font-semibold" [style.color]="'var(--text-muted)'">Pages</p>
              <span class="text-lg font-bold" [style.color]="'var(--text-primary)'">{{ res.pageCount }}</span>
            </div>
            <div class="metric-card text-center">
              <p class="text-[10px] uppercase tracking-wider mb-1.5 font-semibold" [style.color]="'var(--text-muted)'">Chunks</p>
              <span class="text-lg font-bold" [style.color]="'var(--text-primary)'">{{ res.chunkCount }}</span>
            </div>
            <div class="metric-card text-center">
              <p class="text-[10px] uppercase tracking-wider mb-1.5 font-semibold" [style.color]="'var(--text-muted)'">Embeddings</p>
              <span class="badge badge-info text-xs">{{ res.embeddingProvider }}</span>
            </div>
          </div>

          <p class="text-sm mb-4" [style.color]="'var(--text-secondary)'">
            <span class="font-semibold">{{ res.fileName }}</span> has been processed into {{ res.chunkCount }} searchable chunks.
          </p>

          <!-- Action Buttons -->
          <div class="flex flex-wrap gap-3">
            <a [routerLink]="['/documents/query']" [queryParams]="{ documentId: res.documentId }" class="btn-primary flex items-center gap-2 text-sm">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
              </svg>
              Query This Document
            </a>
            <a [routerLink]="['/documents', res.documentId]" class="btn-ghost text-sm flex items-center gap-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"/>
              </svg>
              View Details
            </a>
            <button (click)="reset()" class="btn-ghost text-sm flex items-center gap-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
              </svg>
              Upload Another
            </button>
          </div>
        </div>
      }
    </div>
  `
})
export class DocumentUploadComponent implements OnDestroy {
  private destroyRef = inject(DestroyRef);
  private documentService = inject(DocumentService);

  Math = Math;

  categories: DocumentCategory[] = ['Policy', 'Claim', 'Endorsement', 'Correspondence', 'Other'];
  category: DocumentCategory = 'Other';

  selectedFile = signal<File | null>(null);
  isDragOver = signal(false);
  isUploading = signal(false);
  uploadResult = signal<DocumentUploadResult | null>(null);
  error = signal<string | null>(null);
  elapsedSeconds = signal(0);

  private elapsedTimer: ReturnType<typeof setInterval> | null = null;

  ngOnDestroy(): void {
    this.stopTimer();
  }

  uploadDocument(): void {
    const file = this.selectedFile();
    if (!file || this.isUploading()) return;

    this.isUploading.set(true);
    this.error.set(null);
    this.uploadResult.set(null);
    this.startTimer();

    this.documentService.uploadDocument(file, this.category)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.uploadResult.set(result);
          this.isUploading.set(false);
          this.stopTimer();
        },
        error: (err) => {
          const status = err.status;
          if (status === 429) {
            this.error.set(err.error?.error || 'Document limit reached. Delete existing documents to upload more.');
          } else if (status === 422) {
            this.error.set(err.error?.error || 'Document processing failed. The file may be corrupted or unsupported.');
          } else {
            this.error.set(err.error?.error || 'Failed to upload document. Please try again.');
          }
          this.isUploading.set(false);
          this.stopTimer();
        }
      });
  }

  reset(): void {
    this.selectedFile.set(null);
    this.uploadResult.set(null);
    this.error.set(null);
    this.category = 'Other';
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(true);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
    const files = event.dataTransfer?.files;
    if (files?.length) this.validateAndSetFile(files[0]);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      this.validateAndSetFile(input.files[0]);
      input.value = '';
    }
  }

  private validateAndSetFile(file: File): void {
    this.error.set(null);
    const allowedTypes = ['application/pdf', 'image/png', 'image/jpeg', 'image/tiff'];
    if (!allowedTypes.includes(file.type)) {
      this.error.set('Unsupported file type. Please upload PDF, PNG, JPEG, or TIFF files.');
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      this.error.set('File exceeds 5 MB limit. Please upload a smaller file.');
      return;
    }
    this.selectedFile.set(file);
    this.uploadResult.set(null);
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  getUploadPhase(): string {
    const s = this.elapsedSeconds();
    if (s < 3) return 'Uploading document...';
    if (s < 8) return 'OCR extracting text...';
    if (s < 15) return 'Chunking into insurance sections...';
    if (s < 22) return 'Generating vector embeddings...';
    return 'Finalizing document index...';
  }

  private startTimer(): void {
    this.stopTimer();
    this.elapsedSeconds.set(0);
    this.elapsedTimer = setInterval(() => this.elapsedSeconds.update(v => v + 1), 1000);
  }

  private stopTimer(): void {
    if (this.elapsedTimer) {
      clearInterval(this.elapsedTimer);
      this.elapsedTimer = null;
    }
  }
}
