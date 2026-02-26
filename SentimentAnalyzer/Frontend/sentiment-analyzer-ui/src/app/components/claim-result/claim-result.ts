import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ClaimsService } from '../../services/claims.service';
import { ClaimTriageResponse, FraudAnalysisResponse } from '../../models/claims.model';
import { EvidenceViewerComponent } from '../evidence-viewer/evidence-viewer';
import {
  getSeverityClass,
  getUrgencyBadge,
  getFraudScoreColor,
  getFraudRiskBadge,
  getFraudGaugeGradient,
  getPriorityBadge,
  getCategoryBadge,
  formatClaimDate,
  getEffectiveFraudScore
} from '../../utils/claims-display.utils';

@Component({
  selector: 'app-claim-result',
  standalone: true,
  imports: [CommonModule, RouterLink, EvidenceViewerComponent],
  template: `
    <div class="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-8">

      <!-- Back + Header -->
      <div class="flex items-center gap-3 mb-6 animate-fade-in-up">
        <a routerLink="/claims/history" class="p-2 rounded-lg transition-all hover:scale-105"
           [style.background]="'var(--bg-surface)'" [style.color]="'var(--text-secondary)'"
           aria-label="Back to claims history">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"/>
          </svg>
        </a>
        <div class="flex-1">
          <h1 class="text-xl sm:text-2xl font-bold" [style.color]="'var(--text-primary)'">
            Claim #{{ claimId }}
          </h1>
        </div>
        @if (claim(); as c) {
          <span class="badge badge-neutral">{{ c.status }}</span>
          <span class="text-xs" [style.color]="'var(--text-muted)'">{{ formatDate(c.createdAt) }}</span>
        }
      </div>

      <!-- Loading -->
      @if (isLoading()) {
        <div class="space-y-4">
          @for (i of [1,2,3]; track i) {
            <div class="skeleton h-32 rounded-xl"></div>
          }
        </div>
      }

      <!-- Error -->
      @if (error()) {
        <div class="glass-card-static p-6 text-center animate-fade-in" role="alert">
          <svg class="w-12 h-12 mx-auto mb-3 text-rose-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9.172 16.172a4 4 0 015.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
          </svg>
          <p class="text-sm font-medium" [style.color]="'var(--text-primary)'">{{ error() }}</p>
          <a routerLink="/claims/history" class="btn-ghost text-sm mt-4 inline-block">Back to History</a>
        </div>
      }

      <!-- Claim Detail -->
      @if (claim(); as c) {
        <div class="space-y-5 animate-fade-in-up">

          <!-- Triage Summary -->
          <div class="glass-card-static p-6 sm:p-8">
            <h2 class="text-sm font-bold uppercase tracking-wider mb-5" [style.color]="'var(--text-muted)'">Triage Assessment</h2>

            <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
              <div class="metric-card flex flex-col items-center justify-center py-4">
                <p class="text-[10px] uppercase tracking-wider mb-2.5 font-semibold" [style.color]="'var(--text-muted)'">Severity</p>
                <span class="inline-block px-4 py-1.5 rounded-full text-sm font-bold text-white" [class]="getSeverityClass(c.severity)"
                      [class.animate-pulse]="c.severity === 'Critical'">{{ c.severity }}</span>
              </div>
              <div class="metric-card flex flex-col items-center justify-center py-4">
                <p class="text-[10px] uppercase tracking-wider mb-2.5 font-semibold" [style.color]="'var(--text-muted)'">Urgency</p>
                <span class="inline-block px-4 py-1.5 rounded-full text-sm font-bold" [class]="getUrgencyBadge(c.urgency)">{{ c.urgency }}</span>
              </div>
              <div class="metric-card flex flex-col items-center justify-center py-4">
                <p class="text-[10px] uppercase tracking-wider mb-2.5 font-semibold" [style.color]="'var(--text-muted)'">Claim Type</p>
                <span class="inline-block px-4 py-1.5 rounded-full text-sm font-bold badge-info">{{ c.claimType || 'Unclassified' }}</span>
              </div>
              <div class="metric-card flex flex-col items-center justify-center py-4">
                <p class="text-[10px] uppercase tracking-wider mb-2.5 font-semibold" [style.color]="'var(--text-muted)'">Est. Loss</p>
                <span class="inline-block px-4 py-1.5 rounded-full text-sm font-bold" [style.color]="'var(--text-primary)'">{{ c.estimatedLossRange || 'N/A' }}</span>
              </div>
            </div>

            <!-- Fraud Score Gauge -->
            <div class="p-4 rounded-xl" [style.background]="'var(--bg-surface)'" [style.border]="'1px solid var(--border-secondary)'">
              <div class="flex items-center justify-between mb-2">
                <span class="text-sm font-semibold" [style.color]="'var(--text-secondary)'">Fraud Risk Score</span>
                <div class="flex items-center gap-2">
                  <span class="text-2xl font-bold" [class]="getFraudScoreColor(getEffectiveFraudScore(c.fraudScore, c.fraudRiskLevel))">{{ getEffectiveFraudScore(c.fraudScore, c.fraudRiskLevel) }}</span>
                  <span class="text-xs" [style.color]="'var(--text-muted)'">/100</span>
                  <span class="badge text-[10px]" [class]="getFraudBadge(c.fraudRiskLevel)">{{ c.fraudRiskLevel }}</span>
                </div>
              </div>
              <div class="h-3 rounded-full overflow-hidden" [style.background]="'var(--bg-surface-hover)'">
                <div class="h-full rounded-full transition-all duration-1000"
                     [style.width.%]="getEffectiveFraudScore(c.fraudScore, c.fraudRiskLevel)"
                     [style.background]="getGaugeGradient(getEffectiveFraudScore(c.fraudScore, c.fraudRiskLevel))"></div>
              </div>
              <div class="flex justify-between mt-1 text-[10px]" [style.color]="'var(--text-muted)'">
                <span>Low Risk</span><span>High Risk</span>
              </div>
            </div>
          </div>

          <!-- Fraud Flags -->
          @if (c.fraudFlags?.length) {
            <div class="glass-card-static p-6">
              <h3 class="text-sm font-bold uppercase tracking-wider mb-3" [style.color]="'var(--text-muted)'">Fraud Flags</h3>
              <div class="flex flex-wrap gap-2">
                @for (flag of c.fraudFlags; track flag) {
                  <span class="badge badge-danger flex items-center gap-1">
                    <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01"/>
                    </svg>
                    {{ flag }}
                  </span>
                }
              </div>
            </div>
          }

          <!-- Recommended Actions -->
          @if (c.recommendedActions?.length) {
            <div class="glass-card-static p-6 sm:p-8">
              <h3 class="text-sm font-bold uppercase tracking-wider mb-4" [style.color]="'var(--text-muted)'">Recommended Actions</h3>
              <div class="space-y-3">
                @for (action of c.recommendedActions; track action.action; let i = $index) {
                  <div class="p-4 rounded-xl cursor-pointer transition-all"
                       [style.background]="'var(--bg-surface)'" [style.border]="'1px solid var(--border-secondary)'"
                       role="button" tabindex="0"
                       [attr.aria-expanded]="expandedActions().includes(i)"
                       (click)="toggleAction(i)"
                       (keydown.enter)="toggleAction(i)"
                       (keydown.space)="$event.preventDefault(); toggleAction(i)">
                    <div class="flex items-start gap-3">
                      <span class="badge text-[10px] flex-shrink-0 mt-0.5" [class]="getPriorityBadge(action.priority)">{{ action.priority }}</span>
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
          @if (c.evidence?.length) {
            <div class="glass-card-static p-6 sm:p-8">
              <h3 class="text-sm font-bold uppercase tracking-wider mb-4" [style.color]="'var(--text-muted)'">
                Processed Evidence ({{ c.evidence.length }})
              </h3>
              <div class="space-y-3">
                @for (ev of c.evidence; track ev.createdAt) {
                  <app-evidence-viewer [evidence]="ev" />
                }
              </div>
            </div>
          }

          <!-- Fraud Analysis (Deep) -->
          @if (fraudResult()) {
            <div class="glass-card-static p-6 sm:p-8 border-l-4 border-orange-500 animate-fade-in-up">
              <h3 class="text-sm font-bold uppercase tracking-wider mb-4" [style.color]="'var(--text-muted)'">Deep Fraud Analysis</h3>
              <div class="grid grid-cols-2 sm:grid-cols-3 gap-4 mb-4">
                <div class="metric-card text-center">
                  <p class="text-[10px] uppercase tracking-wider mb-1" [style.color]="'var(--text-muted)'">Fraud Score</p>
                  <span class="text-xl font-bold" [class]="getFraudScoreColor(fraudResult()!.fraudScore)">{{ fraudResult()!.fraudScore }}</span>
                </div>
                <div class="metric-card text-center">
                  <p class="text-[10px] uppercase tracking-wider mb-1" [style.color]="'var(--text-muted)'">Confidence</p>
                  <span class="text-xl font-bold text-indigo-400">{{ (fraudResult()!.confidence * 100).toFixed(0) }}%</span>
                </div>
                <div class="metric-card text-center">
                  <p class="text-[10px] uppercase tracking-wider mb-1" [style.color]="'var(--text-muted)'">SIU Referral</p>
                  <span class="badge text-sm" [class]="fraudResult()!.referToSIU ? 'badge-danger' : 'badge-success'">
                    {{ fraudResult()!.referToSIU ? 'Recommended' : 'Not Required' }}
                  </span>
                </div>
              </div>
              @if (fraudResult()!.indicators?.length) {
                <div class="space-y-2">
                  @for (ind of fraudResult()!.indicators; track ind.description) {
                    <div class="flex items-center gap-2 p-2 rounded-lg" [style.background]="'var(--bg-surface)'">
                      <span class="badge text-[10px]" [class]="getCategoryBadge(ind.category)">{{ ind.category }}</span>
                      <span class="text-xs flex-1" [style.color]="'var(--text-secondary)'">{{ ind.description }}</span>
                      <span class="badge text-[10px]" [class]="getSeverityBadge(ind.severity)">{{ ind.severity }}</span>
                    </div>
                  }
                </div>
              }
            </div>
          }

          <!-- Fraud Error -->
          @if (fraudError()) {
            <div class="glass-card-static p-4 flex items-center gap-3 border-l-4 border-l-rose-500 animate-fade-in" role="alert">
              <svg class="w-5 h-5 text-rose-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
              </svg>
              <span class="text-sm text-rose-400">{{ fraudError() }}</span>
            </div>
          }

          <!-- Action Footer -->
          <div class="flex flex-wrap items-center gap-3">
            @if (!fraudResult()) {
              <button (click)="runFraudAnalysis()" [disabled]="fraudLoading()" class="btn-primary flex items-center gap-2 text-sm"
                      aria-label="Run deep fraud analysis">
                @if (fraudLoading()) {
                  <svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
                  </svg>
                  Analyzing...
                } @else {
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                  </svg>
                  Run Deep Fraud Analysis
                }
              </button>
            }
            <a routerLink="/claims/triage" class="btn-ghost text-sm flex items-center gap-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
              </svg>
              New Triage
            </a>
          </div>
        </div>
      }
    </div>
  `
})
export class ClaimResultComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private route = inject(ActivatedRoute);
  private claimsService = inject(ClaimsService);

  claimId = 0;
  claim = signal<ClaimTriageResponse | null>(null);
  fraudResult = signal<FraudAnalysisResponse | null>(null);
  isLoading = signal(true);
  fraudLoading = signal(false);
  error = signal<string | null>(null);
  expandedActions = signal<number[]>([]);

  ngOnInit(): void {
    this.claimId = Number(this.route.snapshot.paramMap.get('id'));
    if (isNaN(this.claimId)) {
      this.error.set('Invalid claim ID.');
      this.isLoading.set(false);
      return;
    }
    this.loadClaim();
  }

  private loadClaim(): void {
    this.claimsService.getClaimById(this.claimId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (c) => { this.claim.set(c); this.isLoading.set(false); },
        error: () => { this.error.set('Claim not found.'); this.isLoading.set(false); }
      });
  }

  fraudError = signal<string | null>(null);

  runFraudAnalysis(): void {
    this.fraudLoading.set(true);
    this.fraudError.set(null);
    this.claimsService.analyzeFraud(this.claimId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => { this.fraudResult.set(res); this.fraudLoading.set(false); },
        error: () => { this.fraudError.set('Fraud analysis failed. Please try again.'); this.fraudLoading.set(false); }
      });
  }

  toggleAction(i: number): void {
    this.expandedActions.update(arr => arr.includes(i) ? arr.filter(x => x !== i) : [...arr, i]);
  }

  formatDate = (dateStr: string): string => formatClaimDate(dateStr, true);
  getSeverityClass = getSeverityClass;
  getUrgencyBadge = getUrgencyBadge;
  getFraudScoreColor = getFraudScoreColor;
  getFraudBadge = getFraudRiskBadge;
  getGaugeGradient = getFraudGaugeGradient;
  getPriorityBadge = getPriorityBadge;
  getCategoryBadge = getCategoryBadge;
  getEffectiveFraudScore = getEffectiveFraudScore;

  getSeverityBadge(s: string): string {
    const map: Record<string, string> = { High: 'badge-danger', Medium: 'badge-warning', Low: 'badge-success' };
    return map[s] || 'badge-neutral';
  }
}
