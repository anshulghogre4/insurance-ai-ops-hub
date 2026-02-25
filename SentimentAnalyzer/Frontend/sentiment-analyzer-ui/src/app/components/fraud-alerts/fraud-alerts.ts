import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ClaimsService } from '../../services/claims.service';
import { ClaimTriageResponse } from '../../models/claims.model';

@Component({
  selector: 'app-fraud-alerts',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">

      <!-- Header -->
      <div class="flex items-center justify-between mb-6 animate-fade-in-up">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-rose-500 to-orange-600 flex items-center justify-center shadow-lg shadow-rose-500/20">
            <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
            </svg>
          </div>
          <div>
            <h1 class="text-xl sm:text-2xl font-bold" [style.color]="'var(--text-primary)'">Fraud Alerts</h1>
            <p class="text-xs" [style.color]="'var(--text-muted)'">Claims with fraud score above 55 &middot; Sorted by risk</p>
          </div>
        </div>
        <div class="flex items-center gap-3">
          @if (alerts().length > 0) {
            <span class="badge badge-danger text-xs font-bold">{{ alerts().length }} alert{{ alerts().length !== 1 ? 's' : '' }}</span>
          }
          <button (click)="refresh()" class="btn-ghost text-sm p-2.5" aria-label="Refresh fraud alerts">
            <svg class="w-4 h-4" [class.animate-spin]="isLoading()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
          </button>
        </div>
      </div>

      <!-- Summary Stats -->
      @if (alerts().length > 0) {
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-6 animate-fade-in-up stagger-1">
          <div class="glass-card-static p-4 text-center">
            <p class="text-2xl font-bold text-rose-400">{{ getCriticalCount() }}</p>
            <p class="text-[10px] font-medium uppercase tracking-wider mt-1" [style.color]="'var(--text-muted)'">Critical Risk</p>
          </div>
          <div class="glass-card-static p-4 text-center">
            <p class="text-2xl font-bold text-orange-400">{{ getHighCount() }}</p>
            <p class="text-[10px] font-medium uppercase tracking-wider mt-1" [style.color]="'var(--text-muted)'">High Risk</p>
          </div>
          <div class="glass-card-static p-4 text-center">
            <p class="text-2xl font-bold text-amber-400">{{ getAvgScore() }}</p>
            <p class="text-[10px] font-medium uppercase tracking-wider mt-1" [style.color]="'var(--text-muted)'">Avg Score</p>
          </div>
          <div class="glass-card-static p-4 text-center">
            <p class="text-2xl font-bold" [style.color]="'var(--text-primary)'">{{ getSIUReferralCount() }}</p>
            <p class="text-[10px] font-medium uppercase tracking-wider mt-1" [style.color]="'var(--text-muted)'">SIU Referrals</p>
          </div>
        </div>
      }

      <!-- Loading -->
      @if (isLoading() && alerts().length === 0) {
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          @for (i of [1,2,3,4]; track i) {
            <div class="skeleton h-48 rounded-xl"></div>
          }
        </div>
      }

      <!-- Error -->
      @if (error()) {
        <div class="glass-card-static p-4 mb-6 flex items-center gap-3 border-l-4 border-l-rose-500 animate-fade-in-up" role="alert">
          <svg class="w-5 h-5 text-rose-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
          </svg>
          <span class="text-sm text-rose-400">{{ error() }}</span>
        </div>
      }

      <!-- Alert Cards -->
      @if (!isLoading() && alerts().length > 0) {
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          @for (alert of alerts(); track alert.claimId; let i = $index) {
            <div class="glass-card p-5 animate-fade-in-up border-l-4" [class]="'stagger-' + Math.min(i + 1, 5)"
                 [class.border-l-rose-500]="alert.fraudScore >= 75"
                 [class.border-l-orange-500]="alert.fraudScore >= 55 && alert.fraudScore < 75"
                 [class.border-l-amber-500]="alert.fraudScore < 55">

              <!-- Card Header -->
              <div class="flex items-start justify-between mb-3">
                <div>
                  <div class="flex items-center gap-2 mb-1">
                    <a [routerLink]="'/claims/' + alert.claimId"
                       class="text-sm font-bold hover:text-indigo-400 transition-colors" [style.color]="'var(--text-primary)'">
                      Claim #{{ alert.claimId }}
                    </a>
                    @if (isSIUReferral(alert)) {
                      <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-rose-500/15 text-rose-400 text-[10px] font-bold animate-pulse"
                            role="status" aria-label="Flagged for Special Investigations Unit referral">
                        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 21v-4m0 0V5a2 2 0 012-2h6.5l1 1H21l-3 6 3 6h-8.5l-1-1H5a2 2 0 00-2 2zm9-13.5V9"/>
                        </svg>
                        SIU Referral
                      </span>
                    }
                  </div>
                  <p class="text-[10px]" [style.color]="'var(--text-muted)'">{{ formatDate(alert.createdAt) }}</p>
                </div>
                <span class="badge text-[10px] font-bold" [class]="getRiskBadgeClass(alert.fraudRiskLevel)">
                  {{ alert.fraudRiskLevel }}
                </span>
              </div>

              <!-- Fraud Score Gauge -->
              <div class="mb-4">
                <div class="flex items-center justify-between mb-1.5">
                  <span class="text-xs font-medium" [style.color]="'var(--text-muted)'">Fraud Score</span>
                  <span class="text-lg font-bold font-mono" [class]="getFraudScoreColor(alert.fraudScore)">{{ alert.fraudScore }}</span>
                </div>
                <div class="progress-track h-2.5 rounded-full"
                     role="progressbar"
                     [attr.aria-valuenow]="alert.fraudScore"
                     aria-valuemin="0"
                     aria-valuemax="100"
                     [attr.aria-label]="'Fraud score: ' + alert.fraudScore + ' out of 100'">
                  <div class="h-full rounded-full transition-all duration-500"
                       [style.width.%]="alert.fraudScore"
                       [class]="getFraudBarClass(alert.fraudScore)">
                  </div>
                </div>
              </div>

              <!-- Claim Info -->
              <div class="grid grid-cols-3 gap-2 mb-3">
                <div class="text-center p-2 rounded-lg" [style.background]="'var(--bg-surface)'">
                  <p class="text-[10px] font-medium" [style.color]="'var(--text-muted)'">Severity</p>
                  <p class="text-xs font-bold" [class]="getSeverityColor(alert.severity)">{{ alert.severity }}</p>
                </div>
                <div class="text-center p-2 rounded-lg" [style.background]="'var(--bg-surface)'">
                  <p class="text-[10px] font-medium" [style.color]="'var(--text-muted)'">Type</p>
                  <p class="text-xs font-semibold truncate" [style.color]="'var(--text-primary)'">{{ alert.claimType }}</p>
                </div>
                <div class="text-center p-2 rounded-lg" [style.background]="'var(--bg-surface)'">
                  <p class="text-[10px] font-medium" [style.color]="'var(--text-muted)'">Est. Loss</p>
                  <p class="text-xs font-semibold truncate" [style.color]="'var(--text-primary)'">{{ alert.estimatedLossRange || 'N/A' }}</p>
                </div>
              </div>

              <!-- Fraud Flags -->
              @if (alert.fraudFlags && alert.fraudFlags.length > 0) {
                <div class="mb-3">
                  <p class="text-[10px] font-bold uppercase tracking-wider mb-1.5" [style.color]="'var(--text-muted)'">Fraud Flags</p>
                  <div class="flex flex-wrap gap-1">
                    @for (flag of alert.fraudFlags.slice(0, 4); track flag) {
                      <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-medium"
                            [class]="getFlagCategoryClass(flag)">
                        <svg class="w-2.5 h-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01"/>
                        </svg>
                        {{ flag }}
                      </span>
                    }
                    @if (alert.fraudFlags.length > 4) {
                      <span class="text-[10px] font-medium px-2 py-0.5" [style.color]="'var(--text-muted)'">
                        +{{ alert.fraudFlags.length - 4 }} more
                      </span>
                    }
                  </div>
                </div>
              }

              <!-- Actions -->
              <div class="flex items-center gap-2 pt-3 border-t" [style.border-color]="'var(--border-secondary)'">
                <a [routerLink]="'/claims/' + alert.claimId"
                   class="btn-ghost text-xs flex items-center gap-1.5 flex-1 justify-center">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"/>
                  </svg>
                  View Claim
                </a>
                <button (click)="runDeepAnalysis(alert.claimId)"
                        class="btn-primary text-xs flex items-center gap-1.5 flex-1 justify-center !py-2"
                        [disabled]="analyzingClaimIds().has(alert.claimId)"
                        [attr.aria-label]="analyzingClaimIds().has(alert.claimId) ? 'Analyzing claim ' + alert.claimId : 'Run deep fraud analysis for claim ' + alert.claimId">
                  @if (analyzingClaimIds().has(alert.claimId)) {
                    <svg class="w-3.5 h-3.5 animate-spin" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
                    </svg>
                    Analyzing...
                  } @else {
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                    </svg>
                    Deep Analysis
                  }
                </button>
              </div>
            </div>
          }
        </div>
      }

      <!-- Empty State -->
      @if (!isLoading() && alerts().length === 0 && !error()) {
        <div class="glass-card-static p-12 text-center animate-fade-in-up">
          <div class="w-20 h-20 rounded-2xl bg-emerald-500/10 flex items-center justify-center mx-auto mb-5">
            <svg class="w-10 h-10 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
            </svg>
          </div>
          <h3 class="text-lg font-bold mb-2" [style.color]="'var(--text-primary)'">All Clear</h3>
          <p class="text-sm mb-1" [style.color]="'var(--text-secondary)'">No fraud alerts detected</p>
          <p class="text-xs mb-6" [style.color]="'var(--text-muted)'">Claims with a fraud score above 55 will appear here for review</p>
          <a routerLink="/claims/triage" class="btn-primary text-sm inline-flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
            </svg>
            Submit a Claim
          </a>
        </div>
      }

      <!-- Back Link -->
      <div class="mt-8">
        <a routerLink="/dashboard" class="btn-ghost text-sm inline-flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"/>
          </svg>
          Back to Dashboard
        </a>
      </div>
    </div>
  `
})
export class FraudAlertsComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private claimsService = inject(ClaimsService);

  /** SIU referral threshold (fraud score >= 75). */
  private static readonly SIU_THRESHOLD = 75;
  /** Critical risk threshold (fraud score >= 85) — higher bar than SIU referral. */
  private static readonly CRITICAL_THRESHOLD = 85;

  Math = Math;

  alerts = signal<ClaimTriageResponse[]>([]);
  isLoading = signal(true);
  error = signal<string | null>(null);
  analyzingClaimIds = signal<Set<number>>(new Set());

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.claimsService.getFraudAlerts(55, 50)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (alerts) => {
          this.alerts.set(alerts.sort((a, b) => b.fraudScore - a.fraudScore));
          this.isLoading.set(false);
        },
        error: () => {
          this.error.set('Failed to load fraud alerts. Please try again.');
          this.isLoading.set(false);
        }
      });
  }

  runDeepAnalysis(claimId: number): void {
    this.analyzingClaimIds.update(ids => { const next = new Set(ids); next.add(claimId); return next; });
    this.claimsService.analyzeFraud(claimId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.analyzingClaimIds.update(ids => { const next = new Set(ids); next.delete(claimId); return next; });
          this.refresh();
        },
        error: () => {
          this.analyzingClaimIds.update(ids => { const next = new Set(ids); next.delete(claimId); return next; });
          this.error.set(`Deep analysis failed for claim #${claimId}. Please try again.`);
        }
      });
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit'
    });
  }

  isSIUReferral(alert: ClaimTriageResponse): boolean {
    return alert.fraudScore >= FraudAlertsComponent.SIU_THRESHOLD;
  }

  getCriticalCount(): number {
    return this.alerts().filter(a => a.fraudScore >= FraudAlertsComponent.CRITICAL_THRESHOLD).length;
  }

  getHighCount(): number {
    return this.alerts().filter(a => a.fraudScore >= 55 && a.fraudScore < 75).length;
  }

  getAvgScore(): number {
    const a = this.alerts();
    if (a.length === 0) return 0;
    return Math.round(a.reduce((sum, x) => sum + x.fraudScore, 0) / a.length);
  }

  getSIUReferralCount(): number {
    return this.alerts().filter(a => a.fraudScore >= FraudAlertsComponent.SIU_THRESHOLD).length;
  }

  getFraudScoreColor(score: number): string {
    if (score >= 75) return 'text-rose-400';
    if (score >= 55) return 'text-orange-400';
    return 'text-amber-400';
  }

  getFraudBarClass(score: number): string {
    if (score >= 75) return 'bg-gradient-to-r from-orange-500 to-rose-500';
    if (score >= 55) return 'bg-gradient-to-r from-amber-500 to-orange-500';
    return 'bg-gradient-to-r from-yellow-500 to-amber-500';
  }

  getRiskBadgeClass(riskLevel: string): string {
    switch (riskLevel?.toLowerCase()) {
      case 'veryhigh': case 'very high': return 'badge-danger';
      case 'high': return 'bg-orange-500/15 text-orange-400 border border-orange-500/20';
      case 'medium': return 'badge-warning';
      default: return 'badge-neutral';
    }
  }

  getSeverityColor(severity: string): string {
    switch (severity?.toLowerCase()) {
      case 'critical': return 'text-rose-400';
      case 'high': return 'text-orange-400';
      case 'medium': return 'text-amber-400';
      case 'low': return 'text-emerald-400';
      default: return '';
    }
  }

  getFlagCategoryClass(flag: string): string {
    const lowerFlag = flag.toLowerCase();
    if (lowerFlag.includes('timing') || lowerFlag.includes('time')) return 'bg-blue-500/10 text-blue-400 border border-blue-500/20';
    if (lowerFlag.includes('behavior') || lowerFlag.includes('pattern')) return 'bg-purple-500/10 text-purple-400 border border-purple-500/20';
    if (lowerFlag.includes('financial') || lowerFlag.includes('amount') || lowerFlag.includes('money')) return 'bg-rose-500/10 text-rose-400 border border-rose-500/20';
    if (lowerFlag.includes('document') || lowerFlag.includes('doc')) return 'bg-slate-500/10 text-slate-400 border border-slate-500/20';
    return 'bg-amber-500/10 text-amber-400 border border-amber-500/20';
  }
}
