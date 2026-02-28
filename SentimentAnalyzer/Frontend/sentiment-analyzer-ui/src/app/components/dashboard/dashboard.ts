import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BaseChartDirective } from 'ng2-charts';
import { Chart, DoughnutController, ArcElement, Tooltip, Legend, BarController, BarElement, CategoryScale, LinearScale } from 'chart.js';
import { InsuranceService } from '../../services/insurance.service';
import { ClaimsService } from '../../services/claims.service';
import {
  DashboardMetrics,
  SentimentDistribution,
  PersonaCount,
  AnalysisHistoryItem
} from '../../models/insurance.model';

Chart.register(DoughnutController, ArcElement, Tooltip, Legend, BarController, BarElement, CategoryScale, LinearScale);

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, BaseChartDirective],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class DashboardComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private insuranceService = inject(InsuranceService);
  private claimsService = inject(ClaimsService);

  claimsTotal = signal(0);
  claimsCritical = signal(0);
  claimsAvgFraud = signal(0);

  isLoading = signal(true);
  error = signal<string | null>(null);
  historyError = signal<string | null>(null);
  claimsError = signal<string | null>(null);

  metrics = signal<DashboardMetrics>({
    totalAnalyses: 0,
    avgPurchaseIntent: 0,
    avgSentimentScore: 0,
    highRiskCount: 0
  });

  topPersonas = signal<PersonaCount[]>([]);

  sentimentDistribution = signal<SentimentDistribution>({
    positive: 0,
    negative: 0,
    neutral: 0,
    mixed: 0
  });

  recentHistory = signal<AnalysisHistoryItem[]>([]);

  sentimentChartData = {
    labels: ['Positive', 'Negative', 'Neutral', 'Mixed'],
    datasets: [{
      data: [0, 0, 0, 0],
      backgroundColor: [
        'rgba(16, 185, 129, 0.8)',
        'rgba(244, 63, 94, 0.8)',
        'rgba(6, 182, 212, 0.8)',
        'rgba(245, 158, 11, 0.8)'
      ],
      borderColor: [
        'rgba(16, 185, 129, 1)',
        'rgba(244, 63, 94, 1)',
        'rgba(6, 182, 212, 1)',
        'rgba(245, 158, 11, 1)'
      ],
      borderWidth: 2,
      hoverOffset: 8
    }]
  };

  sentimentChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '65%',
    animation: {
      duration: 1000,
      easing: 'easeInOutQuart' as const
    },
    plugins: {
      legend: {
        position: 'bottom' as const,
        labels: {
          color: 'rgba(148, 163, 184, 0.8)',
          padding: 16,
          usePointStyle: true,
          pointStyleWidth: 10,
          font: { size: 11, family: "'Inter', sans-serif" }
        }
      },
      tooltip: {
        backgroundColor: 'rgba(15, 23, 42, 0.9)',
        titleColor: '#e2e8f0',
        bodyColor: '#94a3b8',
        borderColor: 'rgba(99, 102, 241, 0.3)',
        borderWidth: 1,
        cornerRadius: 8,
        padding: 10,
        callbacks: {
          label: (ctx: { label: string; raw: unknown }) => ` ${ctx.label}: ${ctx.raw}%`
        }
      }
    }
  };

  personaChartData = {
    labels: [] as string[],
    datasets: [{
      data: [] as number[],
      backgroundColor: [
        'rgba(99, 102, 241, 0.7)',
        'rgba(168, 85, 247, 0.7)',
        'rgba(236, 72, 153, 0.7)',
        'rgba(14, 165, 233, 0.7)',
        'rgba(20, 184, 166, 0.7)',
        'rgba(245, 158, 11, 0.7)'
      ],
      borderColor: [
        'rgba(99, 102, 241, 1)',
        'rgba(168, 85, 247, 1)',
        'rgba(236, 72, 153, 1)',
        'rgba(14, 165, 233, 1)',
        'rgba(20, 184, 166, 1)',
        'rgba(245, 158, 11, 1)'
      ],
      borderWidth: 1,
      borderRadius: 6,
      barPercentage: 0.7
    }]
  };

  personaChartOptions = {
    indexAxis: 'y' as const,
    responsive: true,
    maintainAspectRatio: false,
    animation: {
      duration: 1000,
      easing: 'easeInOutQuart' as const
    },
    scales: {
      x: {
        grid: { color: 'rgba(148, 163, 184, 0.08)' },
        ticks: { color: 'rgba(148, 163, 184, 0.6)', font: { size: 10 } },
        border: { display: false }
      },
      y: {
        grid: { display: false },
        ticks: { color: 'rgba(148, 163, 184, 0.8)', font: { size: 11, family: "'Inter', sans-serif" } },
        border: { display: false }
      }
    },
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: 'rgba(15, 23, 42, 0.9)',
        titleColor: '#e2e8f0',
        bodyColor: '#94a3b8',
        borderColor: 'rgba(99, 102, 241, 0.3)',
        borderWidth: 1,
        cornerRadius: 8,
        padding: 10,
        callbacks: {
          label: (ctx: { raw: unknown }) => ` Count: ${ctx.raw}`
        }
      }
    }
  };

  ngOnInit(): void {
    this.loadDashboard();
    this.loadHistory();
    this.loadClaimsKpis();
  }

  loadDashboard(): void {
    this.isLoading.set(true);
    this.insuranceService.getDashboard()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.metrics.set(data.metrics);
          this.sentimentDistribution.set(data.sentimentDistribution);
          this.topPersonas.set(data.topPersonas);
          this.updateCharts();
          this.isLoading.set(false);
        },
        error: () => {
          this.error.set('Failed to load dashboard data.');
          this.isLoading.set(false);
        }
      });
  }

  loadHistory(): void {
    this.insuranceService.getHistory(10)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          this.recentHistory.set(items);
        },
        error: () => {
          this.historyError.set('Failed to load recent analyses.');
        }
      });
  }

  private updateCharts(): void {
    const dist = this.sentimentDistribution();
    this.sentimentChartData = {
      ...this.sentimentChartData,
      datasets: [{
        ...this.sentimentChartData.datasets[0],
        data: [
          dist.positive,
          dist.negative,
          dist.neutral,
          dist.mixed
        ]
      }]
    };

    const personas = this.topPersonas();
    if (personas.length > 0) {
      this.personaChartData = {
        ...this.personaChartData,
        labels: personas.map(p => p.name),
        datasets: [{
          ...this.personaChartData.datasets[0],
          data: personas.map(p => p.count)
        }]
      };
    }
  }

  loadClaimsKpis(): void {
    this.claimsService.getClaimsHistory({ pageSize: 100 })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.claimsTotal.set(res.totalCount);
          this.claimsCritical.set(res.items.filter(c => c.severity === 'Critical').length);
          const avg = res.items.length > 0
            ? Math.round(res.items.reduce((s, c) => s + c.fraudScore, 0) / res.items.length)
            : 0;
          this.claimsAvgFraud.set(avg);
        },
        error: () => {
          this.claimsError.set('Failed to load claims data.');
        }
      });
  }

  refresh(): void {
    this.error.set(null);
    this.historyError.set(null);
    this.claimsError.set(null);
    this.loadDashboard();
    this.loadHistory();
    this.loadClaimsKpis();
  }

  getSentimentColor(sentiment: string): string {
    switch (sentiment?.toLowerCase()) {
      case 'positive': return 'text-emerald-400';
      case 'negative': return 'text-rose-400';
      case 'mixed': return 'text-amber-400';
      default: return 'text-cyan-400';
    }
  }

  getSentimentBadge(sentiment: string): string {
    const raw = sentiment?.toLowerCase() || '';
    if (['positive', 'happy', 'satisfied', 'pleased', 'grateful', 'delighted', 'content', 'impressed'].includes(raw)) return 'badge-success';
    if (['negative', 'angry', 'frustrated', 'upset', 'furious', 'dissatisfied', 'annoyed', 'hostile', 'bitter'].includes(raw)) return 'badge-danger';
    if (['mixed', 'ambivalent', 'conflicted'].includes(raw)) return 'badge-warning';
    return 'badge-info';
  }

  getRiskColor(risk: string): string {
    switch (risk?.toLowerCase()) {
      case 'high': return 'text-rose-400';
      case 'medium': return 'text-amber-400';
      default: return 'text-emerald-400';
    }
  }

  getRiskBadge(risk: string): string {
    switch (risk?.toLowerCase()) {
      case 'high': return 'badge-danger';
      case 'medium': return 'badge-warning';
      default: return 'badge-success';
    }
  }
}
