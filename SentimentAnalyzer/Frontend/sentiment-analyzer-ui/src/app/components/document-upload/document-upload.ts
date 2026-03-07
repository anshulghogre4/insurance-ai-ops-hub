import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DocumentService } from '../../services/document.service';
import { ToastService } from '../../services/toast.service';
import { DocumentUploadResult, DocumentCategory, DocumentProgressEvent } from '../../models/document.model';

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
          class="relative rounded-xl border-2 border-dashed p-8 text-center transition-all duration-200 focus-within:ring-2 focus-within:ring-indigo-500 focus-within:ring-offset-2 focus-within:ring-offset-transparent"
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
              <p class="text-sm font-medium truncate" [style.color]="'var(--text-primary)'">{{ selectedFile()?.name || 'Unknown file' }}</p>
              <p class="text-xs" [style.color]="'var(--text-muted)'">{{ selectedFile()?.size ? formatFileSize(selectedFile()!.size) : '' }}</p>
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

      <!-- SSE Progress Loader -->
      @if (isUploading() || (currentPhase() !== 'idle' && currentPhase() !== 'Error' && !uploadResult())) {
        <div class="glass-card-static p-6 sm:p-8 animate-fade-in-up" role="status" aria-live="polite">
          <div class="flex items-center gap-3 mb-6">
            <div class="w-10 h-10 rounded-xl bg-indigo-500/20 flex items-center justify-center">
              <svg class="w-5 h-5 text-indigo-400 animate-spin" fill="none" viewBox="0 0 24 24" aria-hidden="true">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
            </div>
            <div>
              <h3 class="text-sm font-bold" [style.color]="'var(--text-primary)'">Processing Document</h3>
              <p class="text-xs" [style.color]="'var(--text-muted)'">{{ progressMessage() || 'Starting...' }}</p>
            </div>
          </div>

          <!-- Phase Timeline -->
          <div class="relative mb-6">
            <!-- Vertical connector line -->
            <div class="absolute left-4 top-4 bottom-4 w-0.5 rounded-full" [style.background]="'var(--border-secondary)'"></div>

            @for (phase of phases; track phase.key; let i = $index) {
              <div class="relative flex items-start gap-4 py-3 transition-all duration-500"
                   [class.opacity-40]="!completedPhases().includes(phase.key) && currentPhase() !== phase.key"
                   [attr.aria-current]="currentPhase() === phase.key ? 'step' : null">

                <!-- Phase node -->
                <div class="relative z-10 w-8 h-8 rounded-full flex items-center justify-center shrink-0 transition-all duration-500 ring-2"
                     [class]="getPhaseNodeClass(phase.key)">
                  @if (completedPhases().includes(phase.key)) {
                    <svg class="w-4 h-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
                    </svg>
                  } @else if (currentPhase() === phase.key) {
                    <div class="w-3 h-3 rounded-full bg-indigo-400 animate-ping"></div>
                  } @else {
                    <div class="w-2 h-2 rounded-full" [style.background]="'var(--text-muted)'"></div>
                  }
                </div>

                <!-- Phase content -->
                <div class="flex-1 min-w-0 pt-0.5">
                  <div class="flex items-center gap-2">
                    <p class="text-sm font-semibold transition-colors duration-300"
                       [style.color]="currentPhase() === phase.key ? 'var(--text-primary)' : completedPhases().includes(phase.key) ? 'var(--text-secondary)' : 'var(--text-muted)'">
                      {{ phase.label }}
                    </p>
                    @if (completedPhases().includes(phase.key) && phaseDurations()[phase.key]) {
                      <span class="text-[10px] text-emerald-400 font-medium">
                        {{ (phaseDurations()[phase.key] / 1000).toFixed(1) }}s
                      </span>
                    }
                    @if (currentPhase() === phase.key) {
                      <span class="text-[10px] text-indigo-400 font-medium animate-pulse">Processing</span>
                    }
                  </div>

                  <!-- Sub-progress bar for active phase -->
                  @if (currentPhase() === phase.key && progressPercent() > 0) {
                    <div class="mt-2 w-full h-1 rounded-full overflow-hidden" [style.background]="'var(--border-secondary)'">
                      <div class="h-full rounded-full bg-gradient-to-r from-indigo-500 to-purple-500 transition-all duration-700"
                           [style.width.%]="getPhaseSubProgress(phase.key)">
                      </div>
                    </div>
                    @if (progressMessage()) {
                      <p class="text-[10px] mt-1" [style.color]="'var(--text-muted)'">{{ progressMessage() }}</p>
                    }
                  }
                </div>
              </div>
            }
          </div>

          <!-- Progress Bar -->
          <div class="progress-track rounded-full overflow-hidden">
            <div class="h-1.5 rounded-full bg-gradient-to-r from-indigo-500 via-purple-500 to-pink-500 transition-all duration-1000 ease-out"
                 [style.width.%]="progressPercent()"
                 role="progressbar"
                 aria-label="Document processing progress"
                 [attr.aria-valuenow]="progressPercent()"
                 aria-valuemin="0"
                 aria-valuemax="100">
            </div>
          </div>
          <div class="flex items-center justify-between mt-2">
            <p class="text-[10px]" [style.color]="'var(--text-muted)'">
              @if (estimatedTimeRemaining() !== null) {
                {{ formatTimeRemaining(estimatedTimeRemaining()) }}
              }
            </p>
            <p class="text-[10px]" [style.color]="'var(--text-muted)'">{{ progressPercent() }}%</p>
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
            <span class="badge badge-success ml-auto">{{ res.status || 'Unknown' }}</span>
          </div>

          @if (res.errorMessage) {
            <div class="flex items-start gap-3 p-4 mb-6 rounded-lg bg-amber-500/10 border border-amber-500/20" role="alert">
              <svg class="w-5 h-5 text-amber-400 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
              </svg>
              <p class="text-sm text-amber-400">{{ res.errorMessage }}</p>
            </div>
          }

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
              <span class="badge badge-info text-xs">{{ res.embeddingProvider || 'N/A' }}</span>
            </div>
          </div>

          <p class="text-sm mb-4" [style.color]="'var(--text-secondary)'">
            <span class="font-semibold">{{ res.fileName || 'Document' }}</span> has been processed into {{ res.chunkCount }} searchable chunks.
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
            <a routerLink="/documents" class="btn-ghost text-sm flex items-center gap-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"/>
              </svg>
              View Library
            </a>
          </div>
        </div>
      }
    </div>
  `
})
export class DocumentUploadComponent {
  private destroyRef = inject(DestroyRef);
  private documentService = inject(DocumentService);
  private toastService = inject(ToastService);

  categories: DocumentCategory[] = ['Policy', 'Claim', 'Endorsement', 'Correspondence', 'Other'];
  category: DocumentCategory = 'Other';

  selectedFile = signal<File | null>(null);
  isDragOver = signal(false);
  isUploading = signal(false);
  uploadResult = signal<DocumentUploadResult | null>(null);
  error = signal<string | null>(null);

  // SSE Progress state
  currentPhase = signal<string>('idle');
  progressPercent = signal(0);
  progressMessage = signal('');
  completedPhases = signal<string[]>([]);

  // Phase timing for ETA calculation
  phaseStartTime = signal<number>(0);
  phaseDurations = signal<Record<string, number>>({});
  estimatedTimeRemaining = computed(() => {
    const current = this.currentPhase();
    const startTime = this.phaseStartTime();
    if (!startTime || current === 'idle' || current === 'Done' || current === 'Error') return null;

    // Average phase durations (in ms) based on typical processing
    const avgDurations: Record<string, number> = {
      'Uploading': 2000,
      'OCR': 8000,
      'Chunking': 3000,
      'Embedding': 12000,
      'Safety': 5000,
    };

    const elapsed = Date.now() - startTime;
    const estimatedTotal = avgDurations[current] ?? 5000;
    const remaining = Math.max(0, estimatedTotal - elapsed);

    // Add remaining phases
    const phaseOrder = ['Uploading', 'OCR', 'Chunking', 'Embedding', 'Safety', 'Done'];
    const currentIdx = phaseOrder.indexOf(current);
    let futureTime = 0;
    for (let i = currentIdx + 1; i < phaseOrder.length - 1; i++) {
      futureTime += avgDurations[phaseOrder[i]] ?? 5000;
    }

    return remaining + futureTime;
  });

  /** Upload processing phases for the progress UI. */
  readonly phases = [
    { key: 'Uploading', label: 'Uploading Document', icon: '&#128196;' },
    { key: 'OCR', label: 'Extracting Text (OCR)', icon: '&#128269;' },
    { key: 'Chunking', label: 'Splitting into Sections', icon: '&#9986;' },
    { key: 'Embedding', label: 'Generating Embeddings', icon: '&#129504;' },
    { key: 'Safety', label: 'Indexing & Safety Check', icon: '&#128737;' },
    { key: 'Done', label: 'Ready for Queries', icon: '&#9989;' },
  ];

  getPhaseIconClass(phaseKey: string): string {
    if (this.completedPhases().includes(phaseKey)) {
      return 'bg-emerald-500/20 border border-emerald-500/30';
    }
    if (this.currentPhase() === phaseKey) {
      return 'bg-indigo-500/20 border border-indigo-500/30 animate-pulse';
    }
    return 'bg-white/5 border border-white/10';
  }

  getPhaseNodeClass(phaseKey: string): string {
    if (this.completedPhases().includes(phaseKey)) {
      return 'bg-emerald-500 ring-emerald-500/30';
    }
    if (this.currentPhase() === phaseKey) {
      return 'bg-indigo-500/20 ring-indigo-500/50';
    }
    return 'ring-[var(--border-secondary)]';
  }

  getPhaseSubProgress(phaseKey: string): number {
    // Map overall progress to per-phase sub-progress
    const phaseRanges: Record<string, [number, number]> = {
      'Uploading': [0, 15],
      'OCR': [15, 45],
      'Chunking': [45, 60],
      'Embedding': [60, 85],
      'Safety': [85, 98],
      'Done': [98, 100],
    };
    const range = phaseRanges[phaseKey];
    if (!range) return 0;
    const [start, end] = range;
    const overall = this.progressPercent();
    if (overall <= start) return 0;
    if (overall >= end) return 100;
    return ((overall - start) / (end - start)) * 100;
  }

  formatTimeRemaining(ms: number | null): string {
    if (!ms || ms <= 0) return '';
    const seconds = Math.ceil(ms / 1000);
    if (seconds < 60) return `~${seconds}s remaining`;
    const minutes = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `~${minutes}m ${secs}s remaining`;
  }

  uploadDocument(): void {
    const file = this.selectedFile();
    if (!file) return;

    this.isUploading.set(true);
    this.error.set(null);
    this.uploadResult.set(null);
    this.currentPhase.set('Uploading');
    this.progressPercent.set(0);
    this.progressMessage.set('Starting upload...');
    this.completedPhases.set([]);
    this.phaseStartTime.set(Date.now());
    this.phaseDurations.set({});

    this.documentService.uploadDocumentWithProgress(file, this.category)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (event: DocumentProgressEvent) => {
          if (event.phase === 'Error') {
            this.error.set(event.errorMessage ?? 'Processing failed.');
            this.isUploading.set(false);
            this.currentPhase.set('Error');
            this.toastService.error('Failed to upload document');
            return;
          }

          // Track completed phases
          const prevPhase = this.currentPhase();
          if (prevPhase !== event.phase && prevPhase !== 'idle' && prevPhase !== 'Error') {
            // Record duration of completed phase
            const duration = Date.now() - this.phaseStartTime();
            this.phaseDurations.update(d => ({ ...d, [prevPhase]: duration }));
            this.phaseStartTime.set(Date.now());

            this.completedPhases.update(phases =>
              phases.includes(prevPhase) ? phases : [...phases, prevPhase]
            );
          }

          this.currentPhase.set(event.phase);
          this.progressPercent.set(event.progress);
          this.progressMessage.set(event.message);

          if (event.phase === 'Done' && event.result) {
            this.uploadResult.set(event.result);
            this.isUploading.set(false);
            this.completedPhases.update(phases => [...phases, 'Done']);
            this.toastService.success('Document uploaded and indexed');
          }
        },
        error: (err) => {
          this.error.set(err?.message ?? 'Upload failed.');
          this.isUploading.set(false);
          this.currentPhase.set('Error');
          this.toastService.error('Failed to upload document');
        },
        complete: () => {
          if (this.isUploading()) {
            this.isUploading.set(false);
          }
        }
      });
  }

  reset(): void {
    this.selectedFile.set(null);
    this.uploadResult.set(null);
    this.error.set(null);
    this.category = 'Other';
    this.currentPhase.set('idle');
    this.progressPercent.set(0);
    this.progressMessage.set('');
    this.completedPhases.set([]);
    this.phaseStartTime.set(0);
    this.phaseDurations.set({});
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
}
