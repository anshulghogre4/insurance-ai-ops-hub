import { Component, DestroyRef, ElementRef, HostListener, inject, OnDestroy, OnInit, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { InsuranceService } from '../../services/insurance.service';
import { AnalysisStateService } from '../../services/analysis-state.service';
import { InsuranceAnalysisResponse, AnalysisHistoryItem } from '../../models/insurance.model';
import { HistoryPanelComponent } from '../history-panel/history-panel';

@Component({
  selector: 'app-insurance-analyzer',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, HistoryPanelComponent],
  templateUrl: './insurance-analyzer.html',
  styleUrl: './insurance-analyzer.css'
})
export class InsuranceAnalyzerComponent implements OnInit, OnDestroy {
  private destroyRef = inject(DestroyRef);
  private insuranceService = inject(InsuranceService);
  private stateService = inject(AnalysisStateService);

  inputText = '';
  interactionType = 'General';
  isLoading = signal(false);
  result = signal<InsuranceAnalysisResponse | null>(null);
  error = signal<string | null>(null);

  // History panel state
  historyItems = signal<AnalysisHistoryItem[]>([]);
  historyLoading = signal(false);
  showHistory = signal(false);
  isRestoredFromCache = signal(false);
  loadingFromHistory = signal(false);

  /** Tracks elapsed seconds during analysis for progress feedback. */
  elapsedSeconds = signal(0);
  private elapsedTimer: ReturnType<typeof setInterval> | null = null;

  @ViewChild('analysisInput') private analysisInputRef?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('historyToggleBtn') private historyToggleBtnRef?: ElementRef<HTMLButtonElement>;

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.showHistory()) {
      this.toggleHistory();
    }
  }

  interactionTypes: readonly string[] = ['General', 'Email', 'Call', 'Chat', 'Review', 'Complaint'];

  ngOnInit(): void {
    // Restore cached analysis state
    const cachedResult = this.stateService.insuranceResult();
    if (cachedResult) {
      this.result.set(cachedResult);
      this.inputText = this.stateService.insuranceInputText();
      this.interactionType = this.stateService.insuranceInteractionType();
      this.isRestoredFromCache.set(true);
    }
    this.loadHistory();
  }

  ngOnDestroy(): void {
    this.stopElapsedTimer();
  }

  analyze(): void {
    if (!this.inputText.trim()) {
      this.error.set('Please enter text to analyze.');
      return;
    }

    this.isLoading.set(true);
    this.loadingFromHistory.set(false);
    this.error.set(null);
    this.result.set(null);
    this.startElapsedTimer();

    this.insuranceService.analyzeInsurance(this.inputText, this.interactionType)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.result.set(response);
          this.isLoading.set(false);
          this.isRestoredFromCache.set(false);
          this.stopElapsedTimer();
          this.stateService.saveInsuranceState(this.inputText, this.interactionType, response);
          this.loadHistory();
        },
        error: (err) => {
          this.error.set(err.error?.error || 'An error occurred during analysis. Please try again.');
          this.isLoading.set(false);
          this.stopElapsedTimer();
        }
      });
  }

  clearAll(): void {
    this.inputText = '';
    this.interactionType = 'General';
    this.result.set(null);
    this.error.set(null);
    this.isRestoredFromCache.set(false);
    this.stateService.clearInsuranceState();
  }

  getEmotionEntries(): [string, number][] {
    const emotions = this.result()?.emotionBreakdown;
    if (!emotions) return [];
    return Object.entries(emotions).sort((a, b) => b[1] - a[1]);
  }

  getSentimentClass(): string {
    return (this.result()?.sentiment || '').toLowerCase();
  }

  getIntentColor(): string {
    const score = this.result()?.insuranceAnalysis?.purchaseIntentScore ?? 0;
    if (score >= 70) return 'text-green-400';
    if (score >= 40) return 'text-yellow-400';
    return 'text-red-400';
  }

  getIntentBarColor(): string {
    const score = this.result()?.insuranceAnalysis?.purchaseIntentScore ?? 0;
    if (score >= 70) return 'bg-green-500';
    if (score >= 40) return 'bg-yellow-500';
    return 'bg-red-500';
  }

  private readonly sampleTexts: Record<string, { text: string; type: string }> = {
    claim: {
      text: 'I reported water damage to my basement on January 15th and it has been over 3 weeks with no response from the claims department. My policy number is HO-2024-789456. I am extremely frustrated and considering switching to another provider. If I do not hear back within 48 hours, I will be filing a complaint with the department of insurance.',
      type: 'Complaint'
    },
    renewal: {
      text: 'Hi, my auto policy is coming up for renewal next month and I wanted to check if there are any discounts available. I have been a loyal customer for 5 years with no claims. My current premium seems a bit high compared to what I have seen from other providers. Can you help me understand my options?',
      type: 'Call'
    },
    positive: {
      text: 'I just wanted to reach out and say how impressed I am with the claims process. My adjuster Sarah was incredibly helpful and professional. The entire claim was settled within a week and the communication was excellent throughout. This is exactly why I have stayed with your company for over a decade.',
      type: 'Review'
    },
    billing: {
      text: 'I noticed my premium increased by 15% this month without any prior notice. I have not had any claims or changes to my policy. I need someone to explain these charges immediately. If this cannot be resolved, I will need to cancel my policy and find coverage elsewhere.',
      type: 'Email'
    }
  };

  useSample(key: string): void {
    const sample = this.sampleTexts[key];
    if (sample) {
      this.inputText = sample.text;
      this.interactionType = sample.type;
      this.result.set(null);
      this.error.set(null);
    }
  }

  /** Returns a descriptive phase based on elapsed time to give users feedback on what's happening. */
  getAnalysisPhase(): string {
    const s = this.elapsedSeconds();
    if (s < 5) return 'Sending to AI agents...';
    if (s < 15) return 'Business Analyst reviewing...';
    if (s < 25) return 'Developer formatting output...';
    if (s < 35) return 'QA validating consistency...';
    if (s < 45) return 'Architect evaluating...';
    return 'Finalizing analysis...';
  }

  /** Returns a Tailwind color class for emotion bars based on positive/negative valence. */
  getEmotionBarColor(emotion: string): string {
    const positive = ['trust', 'satisfaction', 'relief'];
    const negative = ['frustration', 'anger', 'anxiety'];
    if (positive.includes(emotion.toLowerCase())) return 'bg-emerald-500';
    if (negative.includes(emotion.toLowerCase())) return 'bg-rose-500';
    return 'bg-amber-500'; // neutral emotions like confusion, urgency
  }

  /** Scrolls the viewport to the input textarea for quick re-analysis. */
  scrollToInput(): void {
    this.analysisInputRef?.nativeElement?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  getRiskBadge(level: string): string {
    switch (level?.toLowerCase()) {
      case 'high': return 'badge-danger';
      case 'medium': return 'badge-warning';
      case 'low': return 'badge-success';
      default: return 'badge-neutral';
    }
  }

  getPersonaIcon(): string {
    switch (this.result()?.insuranceAnalysis?.customerPersona) {
      case 'PriceSensitive': return '💰';
      case 'CoverageFocused': return '🛡️';
      case 'ClaimFrustrated': return '😤';
      case 'NewBuyer': return '🆕';
      case 'RenewalRisk': return '⚠️';
      case 'UpsellReady': return '📈';
      default: return '👤';
    }
  }

  getJourneyIcon(): string {
    switch (this.result()?.insuranceAnalysis?.journeyStage) {
      case 'Awareness': return '👁️';
      case 'Consideration': return '🤔';
      case 'Decision': return '✅';
      case 'Onboarding': return '📋';
      case 'ActiveClaim': return '📝';
      case 'Renewal': return '🔄';
      default: return '📍';
    }
  }

  toggleHistory(): void {
    const wasOpen = this.showHistory();
    this.showHistory.update(v => !v);

    if (this.showHistory()) {
      // Opening: load history if empty, focus will be handled by the sidebar
      if (this.historyItems().length === 0) {
        this.loadHistory();
      }
      // Focus the sidebar's close button after render
      setTimeout(() => {
        const closeBtn = document.querySelector<HTMLElement>('#history-sidebar button[aria-label="Close history panel"]');
        closeBtn?.focus();
      });
    } else if (wasOpen) {
      // Closing: return focus to the toggle button
      setTimeout(() => this.historyToggleBtnRef?.nativeElement?.focus());
    }
  }

  loadHistory(): void {
    this.historyLoading.set(true);
    this.insuranceService.getHistory(20)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          this.historyItems.set(items);
          this.historyLoading.set(false);
        },
        error: () => {
          this.historyLoading.set(false);
        }
      });
  }

  onHistoryItemSelect(item: AnalysisHistoryItem): void {
    this.showHistory.set(false);
    this.error.set(null);
    this.result.set(null);
    this.isLoading.set(true);
    this.loadingFromHistory.set(true);
    this.isRestoredFromCache.set(false);

    this.insuranceService.getAnalysisById(item.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.result.set(response);
          this.inputText = item.inputTextPreview;
          this.interactionType = item.interactionType || 'General';
          this.isLoading.set(false);
          this.loadingFromHistory.set(false);
          this.isRestoredFromCache.set(true);
        },
        error: () => {
          this.error.set('Failed to load analysis. Please try again.');
          this.inputText = item.inputTextPreview;
          this.interactionType = item.interactionType || 'General';
          this.isLoading.set(false);
          this.loadingFromHistory.set(false);
        }
      });
  }

  getSentimentBadgeClass(sentiment: string): string {
    switch (sentiment?.toLowerCase()) {
      case 'positive': return 'badge-success';
      case 'negative': return 'badge-danger';
      case 'mixed': return 'badge-warning';
      default: return 'badge-info';
    }
  }

  getChurnBadgeClass(risk: string): string {
    switch (risk?.toLowerCase()) {
      case 'high': return 'badge-danger';
      case 'medium': return 'badge-warning';
      default: return 'badge-success';
    }
  }

  formatRelativeTime(dateStr: string): string {
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    const diffDays = Math.floor(diffHours / 24);
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString();
  }

  private startElapsedTimer(): void {
    this.stopElapsedTimer();
    this.elapsedSeconds.set(0);
    this.elapsedTimer = setInterval(() => {
      this.elapsedSeconds.update(v => v + 1);
    }, 1000);
  }

  private stopElapsedTimer(): void {
    if (this.elapsedTimer) {
      clearInterval(this.elapsedTimer);
      this.elapsedTimer = null;
    }
  }
}
