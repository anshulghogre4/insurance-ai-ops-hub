import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { InsuranceService } from '../../services/insurance.service';
import {
  DashboardMetrics,
  SentimentDistribution,
  PersonaCount,
  AnalysisHistoryItem
} from '../../models/insurance.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class DashboardComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private insuranceService = inject(InsuranceService);

  isLoading = signal(true);
  error = signal<string | null>(null);

  metrics: DashboardMetrics = {
    totalAnalyses: 0,
    avgPurchaseIntent: 0,
    avgSentimentScore: 0,
    highRiskCount: 0
  };

  topPersonas: PersonaCount[] = [];

  sentimentDistribution: SentimentDistribution = {
    positive: 0,
    negative: 0,
    neutral: 0,
    mixed: 0
  };

  recentHistory: AnalysisHistoryItem[] = [];

  ngOnInit(): void {
    this.loadDashboard();
    this.loadHistory();
  }

  loadDashboard(): void {
    this.isLoading.set(true);
    this.insuranceService.getDashboard()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.metrics = data.metrics;
          this.sentimentDistribution = data.sentimentDistribution;
          this.topPersonas = data.topPersonas;
          this.isLoading.set(false);
        },
        error: (err) => {
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
          this.recentHistory = items;
        },
        error: () => {}
      });
  }

  refresh(): void {
    this.error.set(null);
    this.loadDashboard();
    this.loadHistory();
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
    switch (sentiment?.toLowerCase()) {
      case 'positive': return 'badge-success';
      case 'negative': return 'badge-danger';
      case 'mixed': return 'badge-warning';
      default: return 'badge-info';
    }
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
