import { Injectable, signal } from '@angular/core';
import { InsuranceAnalysisResponse } from '../models/insurance.model';
import { SentimentResponse } from '../models/sentiment.model';

@Injectable({ providedIn: 'root' })
export class AnalysisStateService {
  private readonly INSURANCE_KEY = 'insuresense-insurance-state';
  private readonly SENTIMENT_KEY = 'insuresense-sentiment-state';

  // Insurance analyzer state
  private _insuranceResult = signal<InsuranceAnalysisResponse | null>(null);
  private _insuranceInputText = signal<string>('');
  private _insuranceInteractionType = signal<string>('General');

  // v1 sentiment analyzer state
  private _sentimentResult = signal<SentimentResponse | null>(null);
  private _sentimentInputText = signal<string>('');

  // Public readonly signals
  readonly insuranceResult = this._insuranceResult.asReadonly();
  readonly insuranceInputText = this._insuranceInputText.asReadonly();
  readonly insuranceInteractionType = this._insuranceInteractionType.asReadonly();
  readonly sentimentResult = this._sentimentResult.asReadonly();
  readonly sentimentInputText = this._sentimentInputText.asReadonly();

  constructor() {
    this.restoreFromStorage();
  }

  saveInsuranceState(inputText: string, interactionType: string, result: InsuranceAnalysisResponse): void {
    this._insuranceInputText.set(inputText);
    this._insuranceInteractionType.set(interactionType);
    this._insuranceResult.set(result);
    try {
      sessionStorage.setItem(this.INSURANCE_KEY, JSON.stringify({ inputText, interactionType, result }));
    } catch { /* quota exceeded — signal cache still works */ }
  }

  clearInsuranceState(): void {
    this._insuranceInputText.set('');
    this._insuranceInteractionType.set('General');
    this._insuranceResult.set(null);
    sessionStorage.removeItem(this.INSURANCE_KEY);
  }

  saveSentimentState(inputText: string, result: SentimentResponse): void {
    this._sentimentInputText.set(inputText);
    this._sentimentResult.set(result);
    try {
      sessionStorage.setItem(this.SENTIMENT_KEY, JSON.stringify({ inputText, result }));
    } catch { /* quota exceeded — signal cache still works */ }
  }

  clearSentimentState(): void {
    this._sentimentInputText.set('');
    this._sentimentResult.set(null);
    sessionStorage.removeItem(this.SENTIMENT_KEY);
  }

  private restoreFromStorage(): void {
    try {
      const raw = sessionStorage.getItem(this.INSURANCE_KEY);
      if (raw) {
        const state = JSON.parse(raw);
        this._insuranceInputText.set(state.inputText ?? '');
        this._insuranceInteractionType.set(state.interactionType ?? 'General');
        this._insuranceResult.set(state.result ?? null);
      }
    } catch { /* corrupted storage — ignore */ }

    try {
      const raw = sessionStorage.getItem(this.SENTIMENT_KEY);
      if (raw) {
        const state = JSON.parse(raw);
        this._sentimentInputText.set(state.inputText ?? '');
        this._sentimentResult.set(state.result ?? null);
      }
    } catch { /* corrupted storage — ignore */ }
  }
}
