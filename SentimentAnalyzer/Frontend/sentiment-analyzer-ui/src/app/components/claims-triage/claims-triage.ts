import { Component, DestroyRef, inject, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ClaimsService } from '../../services/claims.service';
import { ClaimTriageResponse } from '../../models/claims.model';
import { EvidenceViewerComponent } from '../evidence-viewer/evidence-viewer';
import {
  getSeverityClass,
  getUrgencyBadge,
  getFraudScoreColor,
  getFraudRiskBadge,
  getFraudGaugeGradient,
  getPriorityBadge,
  getEffectiveFraudScore
} from '../../utils/claims-display.utils';

@Component({
  selector: 'app-claims-triage',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, EvidenceViewerComponent],
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
            <h1 class="text-2xl sm:text-3xl font-bold" [style.color]="'var(--text-primary)'">Claims Triage</h1>
            <p class="text-sm" [style.color]="'var(--text-muted)'">AI-powered severity assessment & fraud detection</p>
          </div>
        </div>
      </div>

      <!-- Form Card -->
      <div class="glass-card-static p-6 sm:p-8 mb-6 animate-fade-in-up stagger-1">

        <!-- Claim Text -->
        <label for="claimText" class="block text-sm font-semibold mb-2" [style.color]="'var(--text-secondary)'">
          Claim Description
        </label>
        <textarea
          id="claimText"
          [(ngModel)]="claimText"
          class="input-field min-h-[160px] resize-y"
          [placeholder]="'Describe the claim details including dates, damages, policy information, and any relevant circumstances...'"
          [maxLength]="10000"
          aria-label="Claim description text"
          (keydown.control.enter)="submitTriage()"
          (keydown.meta.enter)="submitTriage()"
        ></textarea>
        <div class="flex justify-between items-center mt-1.5 text-xs" [style.color]="'var(--text-muted)'">
          <span>Ctrl+Enter to submit</span>
          <span [class.text-rose-400]="claimText.length > 9500">{{ claimText.length | number }} / 10,000</span>
        </div>

        <!-- Interaction Type -->
        <div class="mt-5">
          <label for="interactionType" class="block text-sm font-semibold mb-2" [style.color]="'var(--text-secondary)'">
            Interaction Type
          </label>
          <select
            id="interactionType"
            [(ngModel)]="interactionType"
            class="input-field"
            aria-label="Interaction type selector"
          >
            @for (type of interactionTypes; track type) {
              <option [value]="type">{{ type }}</option>
            }
          </select>
        </div>

        <!-- Quick Templates -->
        <div class="mt-5">
          <p class="text-xs font-semibold mb-2.5 uppercase tracking-wider" [style.color]="'var(--text-muted)'">Quick Templates</p>
          <div class="flex flex-wrap gap-2">
            @for (tmpl of templates; track tmpl.key) {
              <button
                (click)="useSample(tmpl.key)"
                class="px-3 py-1.5 rounded-lg text-xs font-medium transition-all duration-200 hover:scale-[1.03] inline-flex items-center gap-1.5"
                [style.background]="'var(--bg-surface)'"
                [style.color]="'var(--text-secondary)'"
                [style.border]="'1px solid var(--border-primary)'"
                [attr.aria-label]="'Use ' + tmpl.label + ' template'"
              >
                @switch (tmpl.icon) {
                  @case ('water') {
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 2.69l5.66 5.66a8 8 0 11-11.31 0z"/></svg>
                  }
                  @case ('auto') {
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M16 6l2 2-2 2M12 2v4M8 6L6 8l2 2M3 18h18M5 18v-2a4 4 0 014-4h6a4 4 0 014 4v2"/></svg>
                  }
                  @case ('theft') {
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
                  }
                  @case ('liability') {
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 3v18M5 8h14l-2 6H7z"/></svg>
                  }
                }
                {{ tmpl.label }}
              </button>
            }
          </div>
        </div>

        <!-- File Upload Drop Zone -->
        <div class="mt-6">
          <p class="text-xs font-semibold mb-2.5 uppercase tracking-wider" [style.color]="'var(--text-muted)'">
            Evidence Attachments <span class="font-normal">(optional)</span>
          </p>
          <div
            class="relative rounded-xl border-2 border-dashed p-6 text-center transition-all duration-200"
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
              accept="image/*,audio/*,.pdf"
              multiple
              (change)="onFilesSelected($event)"
              aria-label="Upload evidence files"
            />
            <svg class="w-8 h-8 mx-auto mb-2" [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/>
            </svg>
            <p class="text-sm font-medium" [style.color]="'var(--text-secondary)'">
              Drop files here or <span class="text-indigo-400 underline">browse</span>
            </p>
            <p class="text-xs mt-1" [style.color]="'var(--text-muted)'">Images, audio recordings, PDF documents (max 10 MB each)</p>
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

          <!-- Selected Files -->
          @if (selectedFiles().length > 0) {
            <div class="mt-3 space-y-2">
              @for (file of selectedFiles(); track $index; let i = $index) {
                <div class="flex items-center gap-3 p-2.5 rounded-lg" [style.background]="'var(--bg-surface)'" [style.border]="'1px solid var(--border-secondary)'">
                  <div class="w-8 h-8 rounded-lg flex items-center justify-center"
                       [class]="getFileIconClass(file)">
                    @if (file.type.startsWith('image/')) {
                      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg>
                    } @else if (file.type.startsWith('audio/')) {
                      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z"/></svg>
                    } @else {
                      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z"/></svg>
                    }
                  </div>
                  <div class="flex-1 min-w-0">
                    <p class="text-sm font-medium truncate" [style.color]="'var(--text-primary)'">{{ file.name }}</p>
                    <p class="text-xs" [style.color]="'var(--text-muted)'">{{ formatFileSize(file.size) }}</p>
                  </div>
                  <span class="badge badge-info text-[10px]">{{ getFileMimeLabel(file) }}</span>
                  <button (click)="removeFile(i)" class="p-1 rounded-lg transition-colors hover:bg-rose-500/10 text-rose-400" aria-label="Remove file">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                    </svg>
                  </button>
                </div>
              }
            </div>
          }
        </div>

        <!-- Submit Button -->
        <div class="mt-6 flex items-center gap-3">
          <button
            (click)="submitTriage()"
            [disabled]="!claimText.trim() || isLoading()"
            class="btn-primary flex items-center gap-2"
            [class.animate-pulse-glow]="isLoading()"
            aria-label="Submit claim for triage"
          >
            @if (isLoading()) {
              <svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
              </svg>
              Triaging...
            } @else {
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
              </svg>
              Triage Claim
            }
          </button>
          @if (claimText.trim()) {
            <button (click)="clearForm()" class="btn-ghost text-sm" aria-label="Clear form">Clear</button>
          }
        </div>
      </div>

      <!-- Loading State -->
      @if (isLoading()) {
        <div class="glass-card-static p-6 sm:p-8 mb-6 animate-fade-in" role="status" aria-live="polite">
          <div class="flex items-center gap-4 mb-5">
            <div class="w-10 h-10 rounded-xl bg-indigo-500/20 flex items-center justify-center">
              <svg class="w-5 h-5 text-indigo-400 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
              </svg>
            </div>
            <div>
              <p class="font-semibold" [style.color]="'var(--text-primary)'">{{ getTriagePhase() }}</p>
              <p class="text-xs" [style.color]="'var(--text-muted)'">{{ elapsedSeconds() }}s elapsed</p>
            </div>
          </div>
          <div class="progress-track" role="progressbar" [attr.aria-valuenow]="elapsedSeconds()" aria-valuemin="0" aria-valuemax="60">
            <div class="progress-fill bg-gradient-to-r from-indigo-500 via-purple-500 to-pink-500" [style.width.%]="Math.min((elapsedSeconds() / 45) * 100, 95)"></div>
          </div>
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 mt-5">
            @for (i of [1,2,3,4]; track i) {
              <div class="skeleton h-20 rounded-xl"></div>
            }
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

      <!-- Result Display -->
      @if (result(); as res) {
        <div class="space-y-5 animate-fade-in-up" aria-live="polite">

          <!-- Triage Summary -->
          <div class="glass-card-static p-6 sm:p-8">
            <div class="flex items-center gap-3 mb-6">
              <svg class="w-5 h-5 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
              </svg>
              <h2 class="text-lg font-bold" [style.color]="'var(--text-primary)'">Triage Complete</h2>
              <span class="badge badge-info ml-auto">Claim #{{ res.claimId }}</span>
            </div>

            <!-- Key Metrics Grid -->
            <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
              <!-- Severity -->
              <div class="metric-card flex flex-col items-center justify-center py-4">
                <p class="text-[10px] uppercase tracking-wider mb-2.5 font-semibold" [style.color]="'var(--text-muted)'">Severity</p>
                <span class="inline-block px-4 py-1.5 rounded-full text-sm font-bold text-white"
                      [class]="getSeverityClass(res.severity)"
                      [class.animate-pulse]="res.severity === 'Critical'">
                  {{ res.severity }}
                </span>
              </div>
              <!-- Urgency -->
              <div class="metric-card flex flex-col items-center justify-center py-4">
                <p class="text-[10px] uppercase tracking-wider mb-2.5 font-semibold" [style.color]="'var(--text-muted)'">Urgency</p>
                <span class="inline-block px-4 py-1.5 rounded-full text-sm font-bold" [class]="getUrgencyBadge(res.urgency)">
                  {{ res.urgency }}
                </span>
              </div>
              <!-- Claim Type -->
              <div class="metric-card flex flex-col items-center justify-center py-4">
                <p class="text-[10px] uppercase tracking-wider mb-2.5 font-semibold" [style.color]="'var(--text-muted)'">Claim Type</p>
                <span class="inline-block px-4 py-1.5 rounded-full text-sm font-bold badge-info">{{ res.claimType || 'Unclassified' }}</span>
              </div>
              <!-- Status -->
              <div class="metric-card flex flex-col items-center justify-center py-4">
                <p class="text-[10px] uppercase tracking-wider mb-2.5 font-semibold" [style.color]="'var(--text-muted)'">Status</p>
                <span class="inline-block px-4 py-1.5 rounded-full text-sm font-bold badge-neutral">{{ res.status }}</span>
              </div>
            </div>

            <!-- Fraud Score Gauge -->
            <div class="p-4 rounded-xl mb-6" [style.background]="'var(--bg-surface)'" [style.border]="'1px solid var(--border-secondary)'">
              <div class="flex items-center justify-between mb-2">
                <p class="text-sm font-semibold" [style.color]="'var(--text-secondary)'">Fraud Risk Score</p>
                <div class="flex items-center gap-2">
                  <span class="text-xl font-bold" [class]="getFraudScoreColor(getEffectiveFraudScore(res.fraudScore, res.fraudRiskLevel))">{{ getEffectiveFraudScore(res.fraudScore, res.fraudRiskLevel) }}</span>
                  <span class="text-xs" [style.color]="'var(--text-muted)'">/100</span>
                  <span class="badge text-[10px]" [class]="getFraudRiskBadge(res.fraudRiskLevel)">{{ res.fraudRiskLevel }}</span>
                </div>
              </div>
              <div class="h-3 rounded-full overflow-hidden" [style.background]="'var(--bg-surface-hover)'">
                <div class="h-full rounded-full transition-all duration-1000 ease-out"
                     [style.width.%]="getEffectiveFraudScore(res.fraudScore, res.fraudRiskLevel)"
                     [style.background]="getFraudGaugeGradient(getEffectiveFraudScore(res.fraudScore, res.fraudRiskLevel))">
                </div>
              </div>
              <div class="flex justify-between mt-1 text-[10px]" [style.color]="'var(--text-muted)'">
                <span>Low Risk</span>
                <span>High Risk</span>
              </div>
            </div>

            <!-- Estimated Loss -->
            @if (res.estimatedLossRange) {
              <div class="flex items-center gap-2 mb-4 px-4 py-3 rounded-xl" [style.background]="'var(--bg-surface)'" [style.border]="'1px solid var(--border-secondary)'">
                <svg class="w-4 h-4 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
                </svg>
                <span class="text-sm font-medium" [style.color]="'var(--text-secondary)'">Estimated Loss:</span>
                <span class="text-sm font-bold" [style.color]="'var(--text-primary)'">{{ res.estimatedLossRange }}</span>
              </div>
            }

            <!-- Fraud Flags -->
            @if (res.fraudFlags?.length) {
              <div class="mb-4">
                <p class="text-xs font-semibold uppercase tracking-wider mb-2" [style.color]="'var(--text-muted)'">Fraud Flags</p>
                <div class="flex flex-wrap gap-2">
                  @for (flag of res.fraudFlags; track flag) {
                    <span class="badge badge-danger flex items-center gap-1">
                      <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
                      </svg>
                      {{ flag }}
                    </span>
                  }
                </div>
              </div>
            }
          </div>

          <!-- Recommended Actions -->
          @if (res.recommendedActions?.length) {
            <div class="glass-card-static p-6 sm:p-8">
              <h3 class="text-sm font-bold uppercase tracking-wider mb-4" [style.color]="'var(--text-muted)'">
                Recommended Actions ({{ res.recommendedActions.length }})
              </h3>
              <div class="space-y-3">
                @for (action of res.recommendedActions; track action.action; let i = $index) {
                  <div class="p-4 rounded-xl transition-all duration-200 cursor-pointer animate-fade-in-up"
                       [class]="'stagger-' + Math.min(i + 1, 5)"
                       [style.background]="'var(--bg-surface)'"
                       [style.border]="'1px solid var(--border-secondary)'"
                       role="button" tabindex="0"
                       [attr.aria-expanded]="expandedActions().includes(i)"
                       (click)="toggleAction(i)"
                       (keydown.enter)="toggleAction(i)"
                       (keydown.space)="$event.preventDefault(); toggleAction(i)">
                    <div class="flex items-start gap-3">
                      <span class="badge text-[10px] flex-shrink-0 mt-0.5" [class]="getPriorityBadge(action.priority)">
                        {{ action.priority }}
                      </span>
                      <div class="flex-1">
                        <p class="text-sm font-semibold" [style.color]="'var(--text-primary)'">{{ action.action }}</p>
                        @if (expandedActions().includes(i)) {
                          <p class="text-xs mt-2 leading-relaxed animate-fade-in" [style.color]="'var(--text-secondary)'">{{ action.reasoning }}</p>
                        }
                      </div>
                      <svg class="w-4 h-4 transition-transform flex-shrink-0" [class.rotate-180]="expandedActions().includes(i)"
                           [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                      </svg>
                    </div>
                  </div>
                }
              </div>
            </div>
          }

          <!-- Evidence -->
          @if (res.evidence?.length) {
            <div class="glass-card-static p-6 sm:p-8">
              <h3 class="text-sm font-bold uppercase tracking-wider mb-4" [style.color]="'var(--text-muted)'">
                Processed Evidence ({{ res.evidence.length }})
              </h3>
              <div class="space-y-3">
                @for (ev of res.evidence; track ev.createdAt) {
                  <app-evidence-viewer [evidence]="ev" />
                }
              </div>
            </div>
          }

          <!-- Action Buttons -->
          <div class="flex flex-wrap items-center gap-3">
            <a [routerLink]="['/claims', res.claimId]" class="btn-primary flex items-center gap-2 text-sm">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"/>
              </svg>
              View Full Details
            </a>
            <button (click)="clearForm()" class="btn-ghost text-sm flex items-center gap-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
              </svg>
              New Triage
            </button>
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
export class ClaimsTriageComponent implements OnDestroy {
  private destroyRef = inject(DestroyRef);
  private claimsService = inject(ClaimsService);

  Math = Math;

  claimText = '';
  interactionType = 'Complaint';
  interactionTypes = ['Complaint', 'General', 'Call', 'Email', 'Review'];

  selectedFiles = signal<File[]>([]);
  fileWarning = signal<string | null>(null);
  isDragOver = signal(false);
  isLoading = signal(false);
  result = signal<ClaimTriageResponse | null>(null);
  error = signal<string | null>(null);
  elapsedSeconds = signal(0);
  expandedActions = signal<number[]>([]);

  private elapsedTimer: ReturnType<typeof setInterval> | null = null;

  templates = [
    { key: 'water', label: 'Water Damage', icon: 'water' },
    { key: 'auto', label: 'Auto Accident', icon: 'auto' },
    { key: 'theft', label: 'Theft Report', icon: 'theft' },
    { key: 'liability', label: 'Liability Claim', icon: 'liability' }
  ];

  private sampleTexts: Record<string, { text: string; type: string }> = {
    water: {
      text: 'I discovered significant water damage in my basement on February 10th after a pipe burst during the cold snap. The flooding has affected approximately 800 sq ft, damaging drywall, flooring, and several pieces of furniture. My policy number is HO-2024-789456. I have photos and a plumber\'s report documenting the damage. Estimated repair cost is $12,000-$18,000. I need an adjuster assigned immediately as mold is becoming a concern.',
      type: 'Complaint'
    },
    auto: {
      text: 'I was involved in a rear-end collision on Highway 101 on February 15th at approximately 3:30 PM. The other driver ran a red light and struck my vehicle on the passenger side. I have a police report (Report #2024-8891) and dash cam footage. My 2022 Toyota Camry sustained significant damage to the rear quarter panel and bumper. I was treated at St. Mary\'s Hospital for minor whiplash. The other driver\'s insurance is State Farm, policy AU-2024-445566.',
      type: 'Call'
    },
    theft: {
      text: 'I am reporting a home burglary that occurred between February 12-14 while I was traveling. The intruders broke in through a rear window and stole electronics, jewelry, and cash totaling approximately $25,000. I filed a police report (Case #BPD-2024-1234) and have receipts and photos of most stolen items. My home security camera captured footage of two suspects. Policy number HO-2023-556677. I need to understand the claims process and timeline for reimbursement.',
      type: 'Email'
    },
    liability: {
      text: 'A visitor to my property slipped on my icy walkway on February 8th and sustained a broken wrist. They were transported to Regional Medical Center and have since contacted me about medical expenses totaling $8,500. I have liability coverage under my homeowners policy GL-2024-112233. I had salted the walkway that morning but the ice reformed due to freezing rain. I have security camera footage and a signed incident report from my neighbor who witnessed the fall.',
      type: 'General'
    }
  };

  ngOnDestroy(): void {
    this.stopTimer();
  }

  useSample(key: string): void {
    const sample = this.sampleTexts[key];
    if (sample) {
      this.claimText = sample.text;
      this.interactionType = sample.type;
      this.result.set(null);
      this.error.set(null);
    }
  }

  submitTriage(): void {
    if (!this.claimText.trim() || this.isLoading()) return;

    this.isLoading.set(true);
    this.error.set(null);
    this.result.set(null);
    this.startTimer();

    this.claimsService.triageClaim(this.claimText, this.interactionType)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          const files = this.selectedFiles();
          if (files.length > 0) {
            this.uploadFilesSequentially(response.claimId, files, 0);
          } else {
            this.result.set(response);
            this.isLoading.set(false);
            this.stopTimer();
          }
        },
        error: (err) => {
          this.error.set(err.error?.error || 'Failed to triage claim. Please try again.');
          this.isLoading.set(false);
          this.stopTimer();
        }
      });
  }

  private uploadFilesSequentially(claimId: number, files: File[], index: number): void {
    if (index >= files.length) {
      this.claimsService.getClaimById(claimId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (fullResult) => {
            this.result.set(fullResult);
            this.isLoading.set(false);
            this.stopTimer();
          },
          error: () => {
            this.error.set('Claim submitted but failed to load result. Check claims history.');
            this.isLoading.set(false);
            this.stopTimer();
          }
        });
      return;
    }

    this.claimsService.uploadEvidence(claimId, files[index])
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.uploadFilesSequentially(claimId, files, index + 1),
        error: () => this.uploadFilesSequentially(claimId, files, index + 1)
      });
  }

  clearForm(): void {
    this.claimText = '';
    this.interactionType = 'Complaint';
    this.selectedFiles.set([]);
    this.result.set(null);
    this.error.set(null);
    this.expandedActions.set([]);
  }

  toggleAction(index: number): void {
    this.expandedActions.update(arr =>
      arr.includes(index) ? arr.filter(i => i !== index) : [...arr, index]
    );
  }

  // File handling
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
    if (files) this.addFiles(Array.from(files));
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files) {
      this.addFiles(Array.from(input.files));
      input.value = '';
    }
  }

  private addFiles(newFiles: File[]): void {
    this.fileWarning.set(null);
    const valid = newFiles.filter(f =>
      f.size <= 10 * 1024 * 1024 &&
      (f.type.startsWith('image/') || f.type.startsWith('audio/') || f.type === 'application/pdf')
    );
    const rejected = newFiles.length - valid.length;
    if (rejected > 0) {
      this.fileWarning.set(`${rejected} file${rejected > 1 ? 's' : ''} rejected (unsupported type or exceeds 10 MB)`);
    }
    this.selectedFiles.update(existing => [...existing, ...valid]);
  }

  removeFile(index: number): void {
    this.selectedFiles.update(files => files.filter((_, i) => i !== index));
  }

  getFileIconClass(file: File): string {
    if (file.type.startsWith('image/')) return 'bg-blue-500/15';
    if (file.type.startsWith('audio/')) return 'bg-purple-500/15';
    return 'bg-amber-500/15';
  }

  getFileMimeLabel(file: File): string {
    if (file.type.startsWith('image/')) return 'Image';
    if (file.type.startsWith('audio/')) return 'Audio';
    if (file.type === 'application/pdf') return 'PDF';
    return 'File';
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  // Styling helpers (delegated to shared utils)
  getSeverityClass = getSeverityClass;
  getUrgencyBadge = getUrgencyBadge;
  getFraudScoreColor = getFraudScoreColor;
  getFraudRiskBadge = getFraudRiskBadge;
  getFraudGaugeGradient = getFraudGaugeGradient;
  getPriorityBadge = getPriorityBadge;
  getEffectiveFraudScore = getEffectiveFraudScore;

  getTriagePhase(): string {
    const s = this.elapsedSeconds();
    if (s < 3) return 'Submitting claim...';
    if (s < 10) return 'Claims Triage Agent analyzing severity...';
    if (s < 20) return 'Fraud Detection Agent scoring risk...';
    if (s < 30) return 'Business Analyst validating domain rules...';
    if (s < 40) return 'QA Agent checking quality...';
    return 'Finalizing triage assessment...';
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
