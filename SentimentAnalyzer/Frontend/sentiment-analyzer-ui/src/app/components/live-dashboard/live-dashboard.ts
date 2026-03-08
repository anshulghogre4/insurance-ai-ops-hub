import { Component, OnInit, OnDestroy, signal, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SignalRService } from '../../services/signalr.service';
import { NotificationService } from '../../services/notification.service';
import { AnalyticsMetrics, ClaimTriagedEvent, FraudAlertEvent, ProviderStatusEvent, HealthSnapshotEvent } from '../../models/analytics';

@Component({
  selector: 'app-live-dashboard',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './live-dashboard.html'
})
export class LiveDashboardComponent implements OnInit, OnDestroy {
  private signalR = inject(SignalRService);
  private notificationService = inject(NotificationService);
  private router = inject(Router);
  private subscriptions: Subscription[] = [];

  // Signals for reactive state
  metrics = signal<AnalyticsMetrics | null>(null);
  recentClaims = signal<ClaimTriagedEvent[]>([]);
  recentFraudAlerts = signal<FraudAlertEvent[]>([]);
  providerStatuses = signal<ProviderStatusEvent[]>([]);
  isConnected = computed(() => this.signalR.isConnected());
  connectionState = computed(() => this.signalR.connectionState());

  // UI state
  isLoading = signal(true);
  error = signal<string | null>(null);

  private maxFeedItems = 20;
  private readonly hubPaths = ['/hubs/claims', '/hubs/provider-health', '/hubs/analytics'] as const;

  async ngOnInit(): Promise<void> {
    try {
      // Connect to all 3 hubs — use allSettled for partial connection resilience
      const results = await Promise.allSettled(
        this.hubPaths.map(hub => this.signalR.connect(hub))
      );

      const failures = results.filter(r => r.status === 'rejected');
      if (failures.length === this.hubPaths.length) {
        throw new Error('All hub connections failed');
      }
      if (failures.length > 0) {
        this.error.set(`${failures.length} of ${this.hubPaths.length} real-time connections failed. Some data may be delayed.`);
      }

      // Subscribe to events
      this.subscriptions.push(
        this.signalR.on<AnalyticsMetrics>('/hubs/analytics', 'MetricsUpdate').subscribe(m => {
          this.metrics.set(m);
          this.isLoading.set(false);
        }),

        this.signalR.on<ClaimTriagedEvent>('/hubs/claims', 'ClaimTriaged').subscribe(evt => {
          this.recentClaims.update(list => [evt, ...list].slice(0, this.maxFeedItems));
          this.notificationService.addNotification(
            'claim', `Claim #${evt.claimId} Triaged`,
            `${evt.severity} severity ${evt.claimType} claim`,
            evt.severity === 'Critical' ? 'critical' : 'info',
            `/claims/${evt.claimId}`
          );
        }),

        this.signalR.on<FraudAlertEvent>('/hubs/claims', 'FraudAlertRaised').subscribe(evt => {
          this.recentFraudAlerts.update(list => [evt, ...list].slice(0, this.maxFeedItems));
          this.notificationService.addNotification(
            'fraud', `Fraud Alert: Claim #${evt.claimId}`,
            `Score ${evt.fraudScore} — ${evt.riskLevel}`,
            evt.fraudScore >= 80 ? 'critical' : 'warning',
            `/claims/${evt.claimId}`
          );
        }),

        this.signalR.on<HealthSnapshotEvent>('/hubs/provider-health', 'HealthSnapshot').subscribe(snap => {
          this.providerStatuses.set(snap.providers);
          this.isLoading.set(false);
        }),

        this.signalR.on<ProviderStatusEvent>('/hubs/provider-health', 'ProviderStatusChanged').subscribe(evt => {
          if (evt.newStatus === 'Cooldown') {
            this.notificationService.addNotification(
              'provider', `${evt.providerName} Down`,
              `Entered cooldown${evt.cooldownSeconds ? ` for ${evt.cooldownSeconds}s` : ''}`,
              'warning', '/dashboard/providers'
            );
          }
        })
      );

      this.isLoading.set(false);
    } catch (err) {
      this.error.set('Failed to connect to real-time services. Using static data.');
      this.isLoading.set(false);
    }
  }

  async reconnect(): Promise<void> {
    this.error.set(null);
    await this.ngOnInit();
  }

  navigateToClaim(claimId: number): void {
    this.router.navigate(['/claims', claimId]);
  }

  getSeverityColor(severity: string): string {
    const colors: Record<string, string> = {
      'Critical': 'text-rose-400',
      'High': 'text-amber-400',
      'Medium': 'text-yellow-300',
      'Low': 'text-emerald-400'
    };
    return colors[severity] ?? 'text-slate-400';
  }

  getSeverityBgColor(severity: string): string {
    const colors: Record<string, string> = {
      'Critical': 'bg-rose-500/10 border-rose-500/20',
      'High': 'bg-amber-500/10 border-amber-500/20',
      'Medium': 'bg-yellow-500/10 border-yellow-500/20',
      'Low': 'bg-emerald-500/10 border-emerald-500/20'
    };
    return colors[severity] ?? 'bg-slate-500/10 border-slate-500/20';
  }

  getProviderStatusColor(status: string): string {
    return status === 'Available' ? 'bg-emerald-400' : status === 'Cooldown' ? 'bg-rose-400' : 'bg-amber-400';
  }

  getProviderStatusPulse(status: string): string {
    return status === 'Available' ? 'animate-pulse' : '';
  }

  formatTime(isoDate: string): string {
    if (!isoDate) return '';
    const date = new Date(isoDate);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 1) return 'just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    const diffHrs = Math.floor(diffMins / 60);
    return `${diffHrs}h ago`;
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(s => s.unsubscribe());
    // Disconnect only the hubs this component owns, not all globally
    for (const hub of this.hubPaths) {
      this.signalR.disconnect(hub);
    }
  }
}
