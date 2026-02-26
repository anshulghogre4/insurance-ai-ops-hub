import { Component, computed, DestroyRef, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../services/auth.service';
import { FraudCorrelationService } from '../../services/fraud-correlation.service';
import { FraudCorrelationResponse, ReviewCorrelationRequest } from '../../models/document.model';

@Component({
  selector: 'app-fraud-correlation',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 animate-fade-in-up">

      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-8">
        <div class="flex items-center gap-3">
          <div class="w-12 h-12 rounded-2xl bg-gradient-to-br from-orange-500 via-red-500 to-pink-500 flex items-center justify-center shadow-lg shadow-orange-500/25">
            <svg class="w-6 h-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1"/>
            </svg>
          </div>
          <div>
            <h1 class="text-2xl sm:text-3xl font-bold" [style.color]="'var(--text-primary)'">Fraud Correlations</h1>
            <p class="text-sm" [style.color]="'var(--text-muted)'">Claim #{{ claimId() }} — Cross-claim pattern analysis</p>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="runAnalysis()" [disabled]="isAnalyzing()" class="btn-primary flex items-center gap-2" aria-label="Run new correlation analysis">
            @if (isAnalyzing()) {
              <svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/></svg>
              Analyzing...
            } @else {
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/></svg>
              Run New Analysis
            }
          </button>
          <a routerLink="/dashboard/fraud" class="btn-secondary text-xs" aria-label="Back to fraud alerts">Back to Alerts</a>
        </div>
      </div>

      <!-- Loading State -->
      @if (isLoading()) {
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-8">
          @for (i of [1, 2, 3]; track i) {
            <div class="glass-card-static p-6 animate-pulse"><div class="h-8 rounded-lg" [style.background]="'var(--border-secondary)'"></div><div class="h-4 rounded-lg mt-2 w-2/3" [style.background]="'var(--border-secondary)'"></div></div>
          }
        </div>
        <div class="space-y-4">
          @for (i of [1, 2]; track i) {
            <div class="glass-card-static p-6 animate-pulse"><div class="h-24 rounded-lg" [style.background]="'var(--border-secondary)'"></div></div>
          }
        </div>
      }

      <!-- Error State -->
      @if (error()) {
        <div class="glass-card-static p-6 border-l-4 border-rose-500 mb-8 animate-fade-in" role="alert">
          <div class="flex items-center gap-3">
            <svg class="w-5 h-5 text-rose-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
            </svg>
            <div>
              <p class="text-sm font-semibold" [style.color]="'var(--text-primary)'">Failed to load correlations</p>
              <p class="text-xs mt-0.5" [style.color]="'var(--text-muted)'">{{ error() }}</p>
            </div>
          </div>
        </div>
      }

      @if (!isLoading()) {
        <!-- Summary Stats -->
        @if (correlations().length > 0) {
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8 animate-fade-in">
            <div class="glass-card-static p-4 text-center">
              <p class="text-2xl font-bold" [style.color]="'var(--text-primary)'">{{ correlations().length }}</p>
              <p class="text-xs" [style.color]="'var(--text-muted)'">Total Correlations</p>
            </div>
            <div class="glass-card-static p-4 text-center">
              <p class="text-2xl font-bold text-orange-400">{{ getAverageScore() | number:'1.0-0' }}%</p>
              <p class="text-xs" [style.color]="'var(--text-muted)'">Avg Score</p>
            </div>
            <div class="glass-card-static p-4 text-center">
              <p class="text-2xl font-bold text-yellow-400">{{ getStatusCount('Pending') }}</p>
              <p class="text-xs" [style.color]="'var(--text-muted)'">Pending Review</p>
            </div>
            <div class="glass-card-static p-4 text-center">
              <p class="text-2xl font-bold text-rose-400">{{ getStatusCount('Confirmed') }}</p>
              <p class="text-xs" [style.color]="'var(--text-muted)'">Confirmed Fraud</p>
            </div>
          </div>

          <!-- Strategy Breakdown -->
          <div class="glass-card-static p-4 mb-8 animate-fade-in">
            <p class="text-xs font-semibold mb-3" [style.color]="'var(--text-secondary)'">Strategy Breakdown</p>
            <div class="flex flex-wrap gap-2">
              @for (s of getStrategyBreakdown(); track s.name) {
                <span class="badge text-xs flex items-center gap-1" [class]="getStrategyBadgeClass(s.name)">
                  {{ s.name }} <span class="opacity-60">({{ s.count }})</span>
                </span>
              }
            </div>
          </div>
        }

        <!-- Status Filter Tabs -->
        <div class="flex items-center gap-1 mb-6 p-1 glass-card-static rounded-xl inline-flex">
          @for (tab of statusTabs; track tab) {
            <button
              (click)="activeTab.set(tab)"
              [class]="activeTab() === tab ? 'bg-gradient-to-r from-indigo-500 to-purple-500 text-white shadow-md' : ''"
              class="px-4 py-1.5 rounded-lg text-xs font-medium transition-all"
              [style.color]="activeTab() !== tab ? 'var(--text-muted)' : undefined"
              [attr.aria-label]="'Filter by ' + tab + ' status'"
            >
              {{ tab }}
              @if (tab !== 'All') {
                <span class="ml-1 opacity-60">({{ getStatusCount(tab) }})</span>
              }
            </button>
          }
        </div>

        <!-- Correlations List -->
        @if (filteredCorrelations().length === 0) {
          <div class="text-center py-16 animate-fade-in">
            <svg class="w-16 h-16 mx-auto mb-4 text-orange-500/30" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1" d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1"/>
            </svg>
            @if (correlations().length === 0) {
              <p class="text-lg font-semibold mb-2" [style.color]="'var(--text-secondary)'">No correlations found</p>
              <p class="text-sm mb-4" [style.color]="'var(--text-muted)'">Run a new analysis to detect cross-claim fraud patterns.</p>
            } @else {
              <p class="text-lg font-semibold mb-2" [style.color]="'var(--text-secondary)'">No {{ activeTab() | lowercase }} correlations</p>
              <p class="text-sm" [style.color]="'var(--text-muted)'">Try a different status filter above.</p>
            }
          </div>
        }

        <div class="space-y-4">
          @for (corr of filteredCorrelations(); track corr.id) {
            <div class="glass-card-static rounded-2xl overflow-hidden animate-fade-in" [class.border-l-4]="true" [class.border-rose-500]="corr.status === 'Confirmed'" [class.border-yellow-500]="corr.status === 'Pending'" [class.border-slate-500]="corr.status === 'Dismissed'">
              <!-- Card Header -->
              <div class="p-4 flex items-center justify-between">
                <div class="flex items-center gap-3">
                  <!-- Score Gauge -->
                  <div class="relative w-12 h-12 flex-shrink-0">
                    <svg class="w-12 h-12 -rotate-90" viewBox="0 0 36 36">
                      <path d="M18 2.0845a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" fill="none" [attr.stroke]="'var(--border-secondary)'" stroke-width="3"/>
                      <path d="M18 2.0845a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" fill="none" [attr.stroke]="getScoreColor(corr.correlationScore)" stroke-width="3" [attr.stroke-dasharray]="(corr.correlationScore * 100) + ', 100'" stroke-linecap="round"/>
                    </svg>
                    <span class="absolute inset-0 flex items-center justify-center text-[10px] font-bold" [style.color]="'var(--text-primary)'">{{ (corr.correlationScore * 100) | number:'1.0-0' }}</span>
                  </div>
                  <div>
                    <p class="text-sm font-semibold" [style.color]="'var(--text-primary)'">Correlation #{{ corr.id }}</p>
                    <p class="text-xs" [style.color]="'var(--text-muted)'">Detected {{ corr.detectedAt | date:'MMM d, yyyy h:mm a' }}</p>
                  </div>
                </div>
                <div class="flex items-center gap-2">
                  <!-- Status Badge -->
                  <span class="badge text-[10px]" [class]="getStatusBadgeClass(corr.status)">{{ corr.status }}</span>
                </div>
              </div>

              <!-- Split Card: Source vs Correlated -->
              <div class="grid grid-cols-1 md:grid-cols-2 divide-y md:divide-y-0 md:divide-x" [style.border-top]="'1px solid var(--border-secondary)'" [style.--tw-divide-opacity]="1" style="--tw-divide-color: var(--border-secondary);">
                <!-- Source Claim -->
                <div class="p-4">
                  <p class="text-[10px] font-semibold uppercase tracking-wider mb-2" [style.color]="'var(--text-muted)'">Source Claim</p>
                  <p class="text-lg font-bold" [style.color]="'var(--text-primary)'">#{{ corr.sourceClaimId }}</p>
                  <div class="flex flex-wrap gap-1.5 mt-2">
                    @if (corr.sourceClaimType) {
                      <span class="badge badge-info text-[10px]">{{ corr.sourceClaimType }}</span>
                    }
                    @if (corr.sourceClaimSeverity) {
                      <span class="badge text-[10px]" [class]="getSeverityClass(corr.sourceClaimSeverity)">{{ corr.sourceClaimSeverity }}</span>
                    }
                    @if (corr.sourceFraudScore != null) {
                      <span class="badge badge-neutral text-[10px]">Fraud: {{ (corr.sourceFraudScore * 100) | number:'1.0-0' }}%</span>
                    }
                  </div>
                </div>

                <!-- Correlated Claim -->
                <div class="p-4">
                  <p class="text-[10px] font-semibold uppercase tracking-wider mb-2" [style.color]="'var(--text-muted)'">Correlated Claim</p>
                  <p class="text-lg font-bold" [style.color]="'var(--text-primary)'">#{{ corr.correlatedClaimId }}</p>
                  <div class="flex flex-wrap gap-1.5 mt-2">
                    @if (corr.correlatedClaimType) {
                      <span class="badge badge-info text-[10px]">{{ corr.correlatedClaimType }}</span>
                    }
                    @if (corr.correlatedClaimSeverity) {
                      <span class="badge text-[10px]" [class]="getSeverityClass(corr.correlatedClaimSeverity)">{{ corr.correlatedClaimSeverity }}</span>
                    }
                    @if (corr.correlatedFraudScore != null) {
                      <span class="badge badge-neutral text-[10px]">Fraud: {{ (corr.correlatedFraudScore * 100) | number:'1.0-0' }}%</span>
                    }
                  </div>
                </div>
              </div>

              <!-- Strategy Badges + Details -->
              <div class="p-4" [style.border-top]="'1px solid var(--border-secondary)'">
                <div class="flex flex-wrap gap-1.5 mb-2">
                  @for (strategy of corr.correlationTypes; track strategy) {
                    <span class="badge text-[10px]" [class]="getStrategyBadgeClass(strategy)">{{ strategy }}</span>
                  }
                </div>
                <p class="text-xs leading-relaxed" [style.color]="'var(--text-secondary)'">{{ corr.details }}</p>
              </div>

              <!-- Review Actions -->
              @if (corr.status === 'Pending') {
                <div class="p-4 flex items-center gap-3" [style.border-top]="'1px solid var(--border-secondary)'">
                  <button
                    (click)="confirmCorrelation(corr)"
                    [disabled]="reviewingId() != null"
                    class="btn-primary text-xs flex items-center gap-1.5"
                    aria-label="Confirm as fraud"
                  >
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>
                    Confirm Fraud
                  </button>
                  <button
                    (click)="openDismissModal(corr)"
                    [disabled]="reviewingId() != null"
                    class="btn-secondary text-xs flex items-center gap-1.5"
                    aria-label="Dismiss correlation"
                  >
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>
                    Dismiss
                  </button>
                </div>
              }

              <!-- Review Info (for reviewed items) -->
              @if (corr.reviewedBy) {
                <div class="p-3 text-[10px] flex items-center gap-2" [style.border-top]="'1px solid var(--border-secondary)'" [style.color]="'var(--text-muted)'">
                  <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"/></svg>
                  Reviewed by {{ corr.reviewedBy }} on {{ corr.reviewedAt | date:'MMM d, yyyy' }}
                  @if (corr.dismissalReason) {
                    <span> — Reason: {{ corr.dismissalReason }}</span>
                  }
                </div>
              }
            </div>
          }
        </div>
      }

      <!-- Dismiss Modal -->
      @if (dismissTarget()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-label="Dismiss correlation reason" (keydown.escape)="closeDismissModal()">
          <div class="absolute inset-0 bg-black/50 backdrop-blur-sm" (click)="closeDismissModal()"></div>
          <div class="glass-card-static rounded-2xl p-6 w-full max-w-md relative z-10 animate-fade-in-up">
            <h3 class="text-lg font-bold mb-1" [style.color]="'var(--text-primary)'">Dismiss Correlation</h3>
            <p class="text-xs mb-4" [style.color]="'var(--text-muted)'">Provide a reason for dismissing correlation #{{ dismissTarget()!.id }}</p>
            <textarea
              [(ngModel)]="dismissReason"
              class="input-field min-h-[80px] resize-none mb-4"
              placeholder="e.g., Claims are from the same household — legitimate duplicate."
              maxlength="500"
              aria-label="Dismissal reason"
            ></textarea>
            <div class="flex justify-end gap-3">
              <button (click)="closeDismissModal()" class="btn-secondary text-xs">Cancel</button>
              <button
                (click)="submitDismissal()"
                [disabled]="!dismissReason.trim() || reviewingId() != null"
                class="btn-primary text-xs flex items-center gap-1.5"
              >
                @if (reviewingId() != null) {
                  <svg class="w-3.5 h-3.5 animate-spin" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/></svg>
                }
                Dismiss Correlation
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class FraudCorrelationComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private authService = inject(AuthService);
  private fraudService = inject(FraudCorrelationService);

  claimId = signal<number>(0);
  correlations = signal<FraudCorrelationResponse[]>([]);
  isLoading = signal(false);
  isAnalyzing = signal(false);
  reviewingId = signal<number | null>(null);
  error = signal<string | null>(null);
  activeTab = signal<string>('All');
  dismissTarget = signal<FraudCorrelationResponse | null>(null);
  dismissReason = '';

  statusTabs = ['All', 'Pending', 'Confirmed', 'Dismissed'];

  filteredCorrelations = computed(() => {
    const tab = this.activeTab();
    if (tab === 'All') return this.correlations();
    return this.correlations().filter(c => c.status === tab);
  });

  ngOnInit(): void {
    this.route.params
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        const id = Number(params['claimId']);
        if (id) {
          this.claimId.set(id);
          this.loadCorrelations();
        }
      });
  }

  loadCorrelations(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.fraudService.getCorrelations(this.claimId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.correlations.set(response.items ?? []);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.error.set(err.error?.message || err.message || 'Failed to load correlations.');
          this.isLoading.set(false);
        }
      });
  }

  runAnalysis(): void {
    this.isAnalyzing.set(true);
    this.error.set(null);

    this.fraudService.correlate(this.claimId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.correlations.set(result.correlations);
          this.isAnalyzing.set(false);
        },
        error: (err) => {
          this.error.set(err.error?.message || err.message || 'Correlation analysis failed.');
          this.isAnalyzing.set(false);
        }
      });
  }

  getAverageScore(): number {
    const items = this.correlations();
    if (items.length === 0) return 0;
    return (items.reduce((sum, c) => sum + c.correlationScore, 0) / items.length) * 100;
  }

  getStatusCount(status: string): number {
    return this.correlations().filter(c => c.status === status).length;
  }

  getStrategyBreakdown(): { name: string; count: number }[] {
    const map = new Map<string, number>();
    for (const c of this.correlations()) {
      for (const s of c.correlationTypes) {
        map.set(s, (map.get(s) || 0) + 1);
      }
    }
    return Array.from(map.entries()).map(([name, count]) => ({ name, count })).sort((a, b) => b.count - a.count);
  }

  getScoreColor(score: number): string {
    if (score >= 0.7) return '#ef4444';
    if (score >= 0.4) return '#f59e0b';
    return '#22c55e';
  }

  getStrategyBadgeClass(strategy: string): string {
    switch (strategy) {
      case 'DateProximity': return 'bg-blue-500/15 text-blue-400 border border-blue-500/30';
      case 'SimilarNarrative': return 'bg-purple-500/15 text-purple-400 border border-purple-500/30';
      case 'SharedFlags': return 'bg-orange-500/15 text-orange-400 border border-orange-500/30';
      case 'SameSeverity': return 'bg-rose-500/15 text-rose-400 border border-rose-500/30';
      default: return 'badge-neutral';
    }
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Pending': return 'bg-yellow-500/15 text-yellow-400 border border-yellow-500/30';
      case 'Confirmed': return 'badge-danger';
      case 'Dismissed': return 'badge-neutral';
      default: return 'badge-neutral';
    }
  }

  getSeverityClass(severity: string): string {
    switch (severity) {
      case 'Critical': return 'badge-danger';
      case 'High': return 'bg-orange-500/15 text-orange-400 border border-orange-500/30';
      case 'Medium': return 'bg-yellow-500/15 text-yellow-400 border border-yellow-500/30';
      case 'Low': return 'bg-green-500/15 text-green-400 border border-green-500/30';
      default: return 'badge-neutral';
    }
  }

  confirmCorrelation(corr: FraudCorrelationResponse): void {
    this.reviewingId.set(corr.id);
    const request: ReviewCorrelationRequest = { status: 'Confirmed', reviewedBy: this.authService.user()?.email ?? 'Analyst' };

    this.fraudService.reviewCorrelation(corr.id, request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.correlations.update(items =>
            items.map(c => c.id === corr.id ? { ...c, status: 'Confirmed', reviewedBy: this.authService.user()?.email ?? 'Analyst', reviewedAt: new Date().toISOString() } : c)
          );
          this.reviewingId.set(null);
        },
        error: (err) => {
          this.error.set(err.error?.message || 'Failed to confirm correlation.');
          this.reviewingId.set(null);
        }
      });
  }

  openDismissModal(corr: FraudCorrelationResponse): void {
    this.dismissTarget.set(corr);
    this.dismissReason = '';
  }

  closeDismissModal(): void {
    this.dismissTarget.set(null);
    this.dismissReason = '';
  }

  submitDismissal(): void {
    const target = this.dismissTarget();
    if (!target || !this.dismissReason.trim()) return;

    this.reviewingId.set(target.id);
    const request: ReviewCorrelationRequest = {
      status: 'Dismissed',
      reviewedBy: this.authService.user()?.email ?? 'Analyst',
      dismissalReason: this.dismissReason.trim()
    };

    this.fraudService.reviewCorrelation(target.id, request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.correlations.update(items =>
            items.map(c => c.id === target.id ? {
              ...c,
              status: 'Dismissed',
              reviewedBy: this.authService.user()?.email ?? 'Analyst',
              reviewedAt: new Date().toISOString(),
              dismissalReason: this.dismissReason.trim()
            } : c)
          );
          this.reviewingId.set(null);
          this.closeDismissModal();
        },
        error: (err) => {
          this.error.set(err.error?.message || 'Failed to dismiss correlation.');
          this.reviewingId.set(null);
        }
      });
  }
}
