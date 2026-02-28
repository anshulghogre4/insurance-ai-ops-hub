import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ClaimsService } from '../../services/claims.service';
import { ToastService } from '../../services/toast.service';
import { BatchClaimUploadResult, BatchClaimItemResult, BatchClaimError } from '../../models/claims.model';
import { getSeverityClass, getFraudScoreColor } from '../../utils/claims-display.utils';

@Component({
  selector: 'app-batch-upload',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">

      <!-- Header -->
      <div class="text-center mb-8 animate-fade-in-up">
        <div class="inline-flex items-center gap-3 mb-3">
          <div class="w-12 h-12 rounded-2xl bg-gradient-to-br from-cyan-500 via-indigo-500 to-purple-500 flex items-center justify-center shadow-lg shadow-indigo-500/25">
            <svg class="w-6 h-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
            </svg>
          </div>
          <div>
            <h1 class="text-2xl sm:text-3xl font-bold" [style.color]="'var(--text-primary)'">Batch Claims Upload</h1>
            <p class="text-sm" [style.color]="'var(--text-muted)'">Upload CSV to process multiple claims at once</p>
          </div>
        </div>
      </div>

      <!-- Upload Card -->
      <div class="glass-card-static p-6 sm:p-8 mb-6 animate-fade-in-up stagger-1">

        <!-- CSV Format Info -->
        <div class="mb-6 p-4 rounded-xl" [style.background]="'var(--bg-surface)'" [style.border]="'1px solid var(--border-secondary)'">
          <p class="text-xs font-semibold uppercase tracking-wider mb-2" [style.color]="'var(--text-muted)'">Expected CSV Format</p>
          <code class="text-xs block p-3 rounded-lg font-mono" [style.background]="'var(--bg-primary)'" [style.color]="'var(--text-secondary)'">
            ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate
          </code>
          <p class="text-xs mt-2" [style.color]="'var(--text-muted)'">
            Required columns: ClaimId, ClaimType, Description. Optional: EstimatedAmount, IncidentDate. Max file size: 5 MB.
          </p>
        </div>

        <!-- File Drop Zone -->
        <div
          class="relative rounded-xl border-2 border-dashed p-8 text-center transition-all duration-200 cursor-pointer"
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
            id="csvFileUpload"
            class="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
            accept=".csv"
            (change)="onFileSelected($event)"
            aria-label="Upload CSV file for batch claims"
          />
          <svg class="w-10 h-10 mx-auto mb-3" [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/>
          </svg>
          <p class="text-sm font-medium" [style.color]="'var(--text-secondary)'">
            Drop your CSV file here or <span class="text-indigo-400 underline">browse</span>
          </p>
          <p class="text-xs mt-1" [style.color]="'var(--text-muted)'">CSV files only (max 5 MB)</p>
        </div>

        <!-- File Warning -->
        @if (fileWarning()) {
          <p class="text-xs text-amber-400 mt-2 flex items-center gap-1" role="alert">
            <svg class="w-3.5 h-3.5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
            </svg>
            {{ fileWarning() }}
          </p>
        }

        <!-- Selected File Info -->
        @if (selectedFile()) {
          <div class="mt-4 flex items-center gap-3 p-3 rounded-lg" [style.background]="'var(--bg-surface)'" [style.border]="'1px solid var(--border-secondary)'">
            <div class="w-9 h-9 rounded-lg bg-emerald-500/15 flex items-center justify-center">
              <svg class="w-5 h-5 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
              </svg>
            </div>
            <div class="flex-1 min-w-0">
              <p class="text-sm font-medium truncate" [style.color]="'var(--text-primary)'">{{ selectedFile()!.name }}</p>
              <p class="text-xs" [style.color]="'var(--text-muted)'">{{ formatFileSize(selectedFile()!.size) }}</p>
            </div>
            <span class="badge badge-info text-[10px]">CSV</span>
            <button (click)="removeFile()" class="p-1 rounded-lg transition-colors hover:bg-rose-500/10 text-rose-400" aria-label="Remove selected file">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>
        }

        <!-- CSV Preview -->
        @if (csvPreview().length > 0) {
          <div class="mt-4">
            <p class="text-xs font-semibold uppercase tracking-wider mb-2" [style.color]="'var(--text-muted)'">
              Preview (first {{ csvPreview().length }} rows)
            </p>
            <div class="overflow-x-auto rounded-lg" [style.border]="'1px solid var(--border-secondary)'">
              <table class="w-full text-xs" aria-label="CSV preview table">
                <thead>
                  <tr [style.background]="'var(--bg-surface)'">
                    @for (header of csvHeaders(); track header) {
                      <th class="px-3 py-2 text-left font-semibold" [style.color]="'var(--text-secondary)'">{{ header }}</th>
                    }
                  </tr>
                </thead>
                <tbody>
                  @for (row of csvPreview(); track $index) {
                    <tr [style.border-top]="'1px solid var(--border-secondary)'">
                      @for (cell of row; track $index) {
                        <td class="px-3 py-2 max-w-[200px] truncate" [style.color]="'var(--text-primary)'">{{ cell || 'N/A' }}</td>
                      }
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <!-- Submit Button -->
        <div class="mt-6 flex items-center gap-3">
          <button
            (click)="submitBatch()"
            [disabled]="!selectedFile() || isLoading()"
            class="btn-primary flex items-center gap-2"
            [class.animate-pulse-glow]="isLoading()"
            aria-label="Process batch claims"
          >
            @if (isLoading()) {
              <svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
              </svg>
              Processing...
            } @else {
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"/>
              </svg>
              Process Batch
            }
          </button>
          @if (selectedFile()) {
            <button (click)="clearAll()" class="btn-ghost text-sm" aria-label="Clear selection">Clear</button>
          }
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

      <!-- Results Display -->
      @if (result(); as res) {
        <div class="space-y-5 animate-fade-in-up" aria-live="polite">

          <!-- Summary Card -->
          <div class="glass-card-static p-6 sm:p-8">
            <div class="flex items-center gap-3 mb-6">
              <svg class="w-5 h-5 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
              </svg>
              <h2 class="text-lg font-bold" [style.color]="'var(--text-primary)'">Batch Processing Complete</h2>
              <span class="badge badge-info ml-auto text-[10px]">{{ res.batchId || 'N/A' }}</span>
            </div>

            <!-- Summary Metrics -->
            <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-4">
              <div class="metric-card flex flex-col items-center justify-center py-4">
                <p class="text-[10px] uppercase tracking-wider mb-2 font-semibold" [style.color]="'var(--text-muted)'">Total</p>
                <span class="text-2xl font-bold" [style.color]="'var(--text-primary)'">{{ res.totalCount ?? 0 }}</span>
              </div>
              <div class="metric-card flex flex-col items-center justify-center py-4">
                <p class="text-[10px] uppercase tracking-wider mb-2 font-semibold" [style.color]="'var(--text-muted)'">Processed</p>
                <span class="text-2xl font-bold" [style.color]="'var(--text-primary)'">{{ res.processedCount ?? 0 }}</span>
              </div>
              <div class="metric-card flex flex-col items-center justify-center py-4">
                <p class="text-[10px] uppercase tracking-wider mb-2 font-semibold" [style.color]="'var(--text-muted)'">Success</p>
                <span class="text-2xl font-bold text-emerald-400">{{ res.successCount ?? 0 }}</span>
              </div>
              <div class="metric-card flex flex-col items-center justify-center py-4">
                <p class="text-[10px] uppercase tracking-wider mb-2 font-semibold" [style.color]="'var(--text-muted)'">Errors</p>
                <span class="text-2xl font-bold" [class]="(res.errorCount ?? 0) > 0 ? 'text-rose-400' : 'text-emerald-400'">{{ res.errorCount ?? 0 }}</span>
              </div>
            </div>

            <!-- Status Badge -->
            <div class="flex items-center gap-2">
              <span class="text-xs font-semibold" [style.color]="'var(--text-muted)'">Status:</span>
              <span class="badge text-[10px]"
                    [class]="res.status === 'Completed' ? 'badge-success' : res.status === 'Failed' ? 'badge-danger' : 'badge-warning'">
                {{ res.status || 'Unknown' }}
              </span>
            </div>
          </div>

          <!-- Results Table -->
          @if (res.results && res.results.length > 0) {
            <div class="glass-card-static p-6 sm:p-8">
              <h3 class="text-sm font-bold uppercase tracking-wider mb-4" [style.color]="'var(--text-muted)'">
                Triage Results ({{ res.results.length }})
              </h3>
              <div class="overflow-x-auto rounded-lg" [style.border]="'1px solid var(--border-secondary)'">
                <table class="w-full text-sm" aria-label="Batch triage results">
                  <thead>
                    <tr [style.background]="'var(--bg-surface)'">
                      <th class="px-4 py-3 text-left font-semibold text-xs uppercase tracking-wider" [style.color]="'var(--text-muted)'">Row</th>
                      <th class="px-4 py-3 text-left font-semibold text-xs uppercase tracking-wider" [style.color]="'var(--text-muted)'">Claim ID</th>
                      <th class="px-4 py-3 text-left font-semibold text-xs uppercase tracking-wider" [style.color]="'var(--text-muted)'">Severity</th>
                      <th class="px-4 py-3 text-left font-semibold text-xs uppercase tracking-wider" [style.color]="'var(--text-muted)'">Fraud Score</th>
                      <th class="px-4 py-3 text-left font-semibold text-xs uppercase tracking-wider" [style.color]="'var(--text-muted)'">Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (item of res.results; track item.rowNumber) {
                      <tr [style.border-top]="'1px solid var(--border-secondary)'" class="transition-colors hover:bg-[var(--bg-surface-hover)]">
                        <td class="px-4 py-3 text-xs" [style.color]="'var(--text-muted)'">{{ item.rowNumber ?? 'N/A' }}</td>
                        <td class="px-4 py-3 font-medium" [style.color]="'var(--text-primary)'">{{ item.claimId || 'N/A' }}</td>
                        <td class="px-4 py-3">
                          <span class="inline-block px-3 py-1 rounded-full text-xs font-bold text-white"
                                [class]="getSeverityClass(item.severity)">
                            {{ item.severity || 'N/A' }}
                          </span>
                        </td>
                        <td class="px-4 py-3">
                          <span class="font-bold" [class]="getFraudScoreColor(item.fraudScore ?? 0)">{{ item.fraudScore ?? 0 }}</span>
                          <span class="text-xs" [style.color]="'var(--text-muted)'">/100</span>
                        </td>
                        <td class="px-4 py-3">
                          <span class="badge badge-info text-[10px]">{{ item.status || 'N/A' }}</span>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- Error Rows -->
          @if (res.errors && res.errors.length > 0) {
            <div class="glass-card-static p-6 sm:p-8 border-l-4 border-amber-500">
              <h3 class="text-sm font-bold uppercase tracking-wider mb-4 text-amber-400">
                Validation Errors ({{ res.errors.length }})
              </h3>
              <div class="space-y-2">
                @for (err of res.errors; track err.rowNumber + err.field) {
                  <div class="flex items-start gap-3 p-3 rounded-lg bg-amber-500/5 border border-amber-500/20">
                    <svg class="w-4 h-4 text-amber-400 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
                    </svg>
                    <div>
                      <p class="text-xs font-semibold text-amber-400">
                        Row {{ err.rowNumber ?? 'N/A' }} — {{ err.field || 'Unknown' }}
                      </p>
                      <p class="text-xs mt-0.5" [style.color]="'var(--text-secondary)'">{{ err.errorMessage || 'Unknown error' }}</p>
                    </div>
                  </div>
                }
              </div>
            </div>
          }

          <!-- Action Buttons -->
          <div class="flex flex-wrap items-center gap-3">
            <button (click)="clearAll()" class="btn-primary flex items-center gap-2 text-sm">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"/>
              </svg>
              Upload Another Batch
            </button>
            <a routerLink="/claims/triage" class="btn-ghost text-sm flex items-center gap-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
              </svg>
              Single Triage
            </a>
            <a routerLink="/claims/history" class="btn-ghost text-sm flex items-center gap-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
              </svg>
              View History
            </a>
          </div>
        </div>
      }
    </div>
  `
})
export class BatchUploadComponent {
  private destroyRef = inject(DestroyRef);
  private claimsService = inject(ClaimsService);
  private toastService = inject(ToastService);

  selectedFile = signal<File | null>(null);
  fileWarning = signal<string | null>(null);
  isDragOver = signal(false);
  isLoading = signal(false);
  result = signal<BatchClaimUploadResult | null>(null);
  error = signal<string | null>(null);
  csvHeaders = signal<string[]>([]);
  csvPreview = signal<string[][]>([]);

  // Styling helpers from shared utils
  getSeverityClass = getSeverityClass;
  getFraudScoreColor = getFraudScoreColor;

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
    if (files && files.length > 0) {
      this.selectFile(files[0]);
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.selectFile(input.files[0]);
      input.value = '';
    }
  }

  private selectFile(file: File): void {
    this.fileWarning.set(null);
    this.error.set(null);

    // Validate file type
    if (!file.name.endsWith('.csv') && !file.type.includes('csv') && !file.type.includes('text/plain')) {
      this.fileWarning.set('Only CSV files are accepted. Please select a .csv file.');
      return;
    }

    // Validate file size (5 MB)
    if (file.size > 5 * 1024 * 1024) {
      this.fileWarning.set('File size exceeds the 5 MB limit.');
      return;
    }

    this.selectedFile.set(file);
    this.result.set(null);
    this.parsePreview(file);
  }

  private parsePreview(file: File): void {
    const reader = new FileReader();
    reader.onload = () => {
      const text = reader.result as string;
      const lines = text.split('\n').filter(l => l.trim().length > 0);

      if (lines.length > 0) {
        this.csvHeaders.set(this.parseCsvLine(lines[0]));
      }

      const previewRows: string[][] = [];
      const maxPreview = Math.min(lines.length, 6); // header + 5 data rows
      for (let i = 1; i < maxPreview; i++) {
        previewRows.push(this.parseCsvLine(lines[i]));
      }
      this.csvPreview.set(previewRows);
    };
    reader.readAsText(file);
  }

  /** Simple CSV line parser that handles quoted fields with commas. */
  private parseCsvLine(line: string): string[] {
    const fields: string[] = [];
    let current = '';
    let inQuotes = false;

    for (let i = 0; i < line.length; i++) {
      const ch = line[i];
      if (inQuotes) {
        if (ch === '"') {
          if (i + 1 < line.length && line[i + 1] === '"') {
            current += '"';
            i++;
          } else {
            inQuotes = false;
          }
        } else {
          current += ch;
        }
      } else {
        if (ch === '"') {
          inQuotes = true;
        } else if (ch === ',') {
          fields.push(current.trim());
          current = '';
        } else {
          current += ch;
        }
      }
    }
    fields.push(current.trim());
    return fields;
  }

  removeFile(): void {
    this.selectedFile.set(null);
    this.csvHeaders.set([]);
    this.csvPreview.set([]);
    this.fileWarning.set(null);
  }

  submitBatch(): void {
    const file = this.selectedFile();
    if (!file || this.isLoading()) return;

    this.isLoading.set(true);
    this.error.set(null);
    this.result.set(null);

    this.claimsService.uploadBatch(file)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.result.set(response);
          this.isLoading.set(false);
          const msg = `Batch complete: ${response.successCount ?? 0} succeeded, ${response.errorCount ?? 0} errors`;
          if ((response.errorCount ?? 0) > 0) {
            this.toastService.warning(msg);
          } else {
            this.toastService.success(msg);
          }
        },
        error: (err) => {
          this.error.set(err.error?.error || 'Failed to process batch. Please try again.');
          this.isLoading.set(false);
          this.toastService.error('Batch processing failed');
        }
      });
  }

  clearAll(): void {
    this.selectedFile.set(null);
    this.csvHeaders.set([]);
    this.csvPreview.set([]);
    this.result.set(null);
    this.error.set(null);
    this.fileWarning.set(null);
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }
}
