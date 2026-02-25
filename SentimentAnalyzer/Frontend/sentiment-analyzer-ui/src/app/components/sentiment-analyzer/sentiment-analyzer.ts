import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SentimentService } from '../../services/sentiment.service';
import { AnalysisStateService } from '../../services/analysis-state.service';
import { SentimentResponse } from '../../models/sentiment.model';

@Component({
  selector: 'app-sentiment-analyzer',
  imports: [CommonModule, FormsModule],
  templateUrl: './sentiment-analyzer.html',
  styleUrl: './sentiment-analyzer.css',
})
export class SentimentAnalyzer implements OnInit {
  private destroyRef = inject(DestroyRef);
  private sentimentService = inject(SentimentService);
  private stateService = inject(AnalysisStateService);

  inputText = signal('');
  isLoading = signal(false);
  result = signal<SentimentResponse | null>(null);
  error = signal<string | null>(null);

  ngOnInit(): void {
    const cachedResult = this.stateService.sentimentResult();
    if (cachedResult) {
      this.result.set(cachedResult);
      this.inputText.set(this.stateService.sentimentInputText());
    }
  }

  analyzeSentiment(): void {
    if (!this.inputText().trim()) {
      this.error.set('Please enter some text to analyze');
      return;
    }

    this.isLoading.set(true);
    this.error.set(null);
    this.result.set(null);

    this.sentimentService.analyzeSentiment(this.inputText())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
      next: (response) => {
        this.result.set(response);
        this.isLoading.set(false);
        this.stateService.saveSentimentState(this.inputText(), response);
      },
      error: (err) => {
        this.error.set(err.error?.error || 'Error analyzing sentiment. Please try again.');
        this.isLoading.set(false);
      }
    });
  }

  clearAll(): void {
    this.inputText.set('');
    this.result.set(null);
    this.error.set(null);
    this.stateService.clearSentimentState();
  }

  getEmotionEntries(): [string, number][] {
    if (!this.result()?.emotionBreakdown) return [];
    return Object.entries(this.result()!.emotionBreakdown).sort((a, b) => b[1] - a[1]);
  }

  getSentimentClass(): string {
    if (!this.result()) return '';
    return this.result()!.sentiment.toLowerCase();
  }
}
