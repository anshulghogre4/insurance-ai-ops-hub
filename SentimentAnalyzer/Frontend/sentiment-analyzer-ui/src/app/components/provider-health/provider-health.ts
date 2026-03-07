import { Component, DestroyRef, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { ClaimsService } from '../../services/claims.service';
import {
  ExtendedProviderHealthResponse,
  LlmProviderHealth,
  ProviderChainHealth,
  ServiceHealth
} from '../../models/claims.model';

@Component({
  selector: 'app-provider-health',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">

      <!-- Header -->
      <div class="flex items-center justify-between mb-6 animate-fade-in-up">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-teal-500 to-cyan-600 flex items-center justify-center shadow-lg shadow-teal-500/20">
            <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01"/>
            </svg>
          </div>
          <div>
            <h1 class="text-xl sm:text-2xl font-bold" [style.color]="'var(--text-primary)'">AI Provider Health</h1>
            @if (lastChecked()) {
              <p class="text-xs" [style.color]="'var(--text-muted)'">Last checked: {{ formatTime(lastChecked()!) }} &middot; Auto-refreshes every 30s</p>
            }
          </div>
        </div>
        <button (click)="refresh()" [disabled]="isLoading()"
                class="p-2.5 rounded-xl border transition-all duration-200 hover:scale-105 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed"
                [style.background]="'var(--bg-surface-hover)'"
                [style.border-color]="'var(--border-primary)'"
                [style.color]="'var(--text-primary)'"
                aria-label="Refresh health status">
          <svg class="w-5 h-5" [class.animate-spin]="isLoading()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
          </svg>
        </button>
      </div>

      <!-- Error -->
      @if (error()) {
        <div class="glass-card-static p-4 mb-6 flex items-center gap-3 border-l-4 border-l-rose-500 animate-fade-in-up" role="alert">
          <svg class="w-5 h-5 text-rose-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
          </svg>
          <span class="text-sm text-rose-400">{{ error() }}</span>
          <button (click)="refresh()" class="btn-ghost text-xs ml-auto" aria-label="Retry loading health status">Retry</button>
        </div>
      }

      <!-- Loading Skeleton -->
      @if (isLoading() && llmProviders().length === 0) {
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 mb-8" aria-busy="true" aria-live="polite" role="status">
          <span class="sr-only">Loading provider health data...</span>
          @for (i of [1,2,3,4]; track i) {
            <div class="skeleton h-36 rounded-xl"></div>
          }
        </div>
      }

      <!-- ==================== 1. LLM Providers ==================== -->
      @if (llmProviders().length > 0) {
        <div class="mb-6 animate-fade-in-up stagger-1 isolate">
          <!-- Collapsible Header -->
          <button (click)="toggleSection('llm')" class="w-full text-left flex items-center justify-between p-4 glass-card-static no-backdrop-blur mb-0 cursor-pointer hover:bg-white/5 transition-colors !transform-none"
                  [class.rounded-b-none]="isSectionExpanded('llm')"
                  [attr.aria-expanded]="isSectionExpanded('llm')" aria-controls="section-llm" aria-label="Toggle LLM Providers section">
            <div class="flex items-center gap-3">
              <div class="w-8 h-8 rounded-lg bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center">
                <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"/></svg>
              </div>
              <h2 class="text-sm font-bold uppercase tracking-wider" [style.color]="'var(--text-primary)'">
                LLM Providers ({{ llmProviders().length }})
              </h2>
            </div>
            <svg class="w-5 h-5 transition-transform duration-200" [class.rotate-180]="isSectionExpanded('llm')" [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
            </svg>
          </button>

          @if (isSectionExpanded('llm')) {
            <div id="section-llm" class="glass-card-static no-backdrop-blur rounded-t-none border-t-0 p-5 overflow-hidden">
              <!-- LLM Fallback Chain -->
              <p class="text-xs font-bold uppercase tracking-wider mb-3" [style.color]="'var(--text-muted)'">LLM Fallback Chain</p>
              <div class="flex items-center gap-1 overflow-x-auto pb-3 mb-4 custom-scrollbar" tabindex="0" role="region" aria-label="LLM provider fallback chain">
                @for (provider of llmProviders(); track provider.name; let last = $last) {
                  <div class="flex items-center gap-1 flex-shrink-0">
                    <div class="px-3 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-all"
                         [class]="getLlmChainClass(provider)">
                      <span class="w-2 h-2 rounded-full" [class]="getLlmStatusDotClass(provider)"></span>
                      {{ provider.name }}
                    </div>
                    @if (!last) {
                      <svg class="w-4 h-4 flex-shrink-0" [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
                      </svg>
                    }
                  </div>
                }
              </div>

              <!-- LLM Provider Cards -->
              <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                @for (provider of llmProviders(); track provider.name; let i = $index) {
                  <div class="glass-card p-5 animate-fade-in-up" [class]="'stagger-' + mathMin(i + 1, 5)">
                    <div class="flex items-center justify-between mb-3">
                      <h3 class="text-sm font-bold" [style.color]="'var(--text-primary)'">{{ provider.name }}</h3>
                      <div class="flex items-center gap-1.5">
                        <span class="w-2.5 h-2.5 rounded-full" [class]="getLlmStatusDotClass(provider)"
                              [class.animate-pulse]="provider.status === 'Down'"></span>
                        <span class="text-[10px] font-semibold" [class]="getLlmStatusTextClass(provider)">{{ provider.status }}</span>
                      </div>
                    </div>
                    <div class="space-y-2">
                      <div class="flex justify-between text-xs">
                        <span [style.color]="'var(--text-muted)'">Available</span>
                        <span class="font-medium" [class]="provider.isAvailable ? 'text-emerald-400' : 'text-rose-400'">
                          {{ provider.isAvailable ? 'Yes' : 'No' }}
                        </span>
                      </div>
                      <div class="flex justify-between text-xs">
                        <span [style.color]="'var(--text-muted)'">Failures</span>
                        <span class="font-mono font-medium" [class]="provider.consecutiveFailures > 0 ? 'text-amber-400' : ''" [style.color]="provider.consecutiveFailures === 0 ? 'var(--text-secondary)' : ''">
                          {{ provider.consecutiveFailures }}
                        </span>
                      </div>
                      @if (provider.cooldownSeconds > 0) {
                        <div class="flex justify-between text-xs">
                          <span [style.color]="'var(--text-muted)'">Cooldown</span>
                          <span class="font-mono text-orange-400">{{ provider.cooldownSeconds.toFixed(0) }}s</span>
                        </div>
                        <div class="progress-track mt-1">
                          <div class="progress-fill bg-orange-500" [style.width.%]="mathMin((provider.cooldownSeconds / 300) * 100, 100)"></div>
                        </div>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      }

      <!-- ==================== 2. Embedding Providers ==================== -->
      @if (embeddingProviders().length > 0) {
        <ng-container *ngTemplateOutlet="chainSection; context: { $implicit: 'embedding', label: 'Embedding Providers', subtitle: 'Vector search & document similarity', providers: embeddingProviders(), icon: 'embedding', gradient: 'from-cyan-500 to-blue-600' }"></ng-container>
      }

      <!-- ==================== 3. OCR Providers ==================== -->
      @if (ocrProviders().length > 0) {
        <ng-container *ngTemplateOutlet="chainSection; context: { $implicit: 'ocr', label: 'OCR Providers', subtitle: 'Optical Character Recognition — text extraction from documents', providers: ocrProviders(), icon: 'document', gradient: 'from-amber-500 to-orange-600' }"></ng-container>
      }

      <!-- ==================== 4. NER Providers ==================== -->
      @if (nerProviders().length > 0) {
        <ng-container *ngTemplateOutlet="chainSection; context: { $implicit: 'ner', label: 'NER Providers', subtitle: 'Named Entity Recognition — extracting people, orgs, locations', providers: nerProviders(), icon: 'cpu', gradient: 'from-emerald-500 to-green-600' }"></ng-container>
      }

      <!-- ==================== 5. STT Providers ==================== -->
      @if (sttProviders().length > 0) {
        <ng-container *ngTemplateOutlet="chainSection; context: { $implicit: 'stt', label: 'STT Providers', subtitle: 'Speech-to-Text — audio transcription for evidence', providers: sttProviders(), icon: 'microphone', gradient: 'from-rose-500 to-pink-600' }"></ng-container>
      }

      <!-- ==================== 6. Content Safety ==================== -->
      @if (contentSafety().length > 0) {
        <ng-container *ngTemplateOutlet="serviceSection; context: { $implicit: 'contentSafety', label: 'Content Safety', services: contentSafety(), gradient: 'from-violet-500 to-purple-600' }"></ng-container>
      }

      <!-- ==================== 7. Translation ==================== -->
      @if (translation().length > 0) {
        <ng-container *ngTemplateOutlet="serviceSection; context: { $implicit: 'translation', label: 'Translation', services: translation(), gradient: 'from-sky-500 to-indigo-600' }"></ng-container>
      }

      <!-- Chain Section Template (for Embedding, OCR, NER, STT) -->
      <ng-template #chainSection let-section let-label="label" let-subtitle="subtitle" let-providers="providers" let-icon="icon" let-gradient="gradient">
        <div class="mb-6 animate-fade-in-up isolate">
          <button (click)="toggleSection(section)" class="w-full text-left flex items-center justify-between p-4 glass-card-static no-backdrop-blur mb-0 cursor-pointer hover:bg-white/5 transition-colors !transform-none"
                  [class.rounded-b-none]="isSectionExpanded(section)"
                  [attr.aria-expanded]="isSectionExpanded(section)" [attr.aria-controls]="'section-' + section" [attr.aria-label]="'Toggle ' + label + ' section'">
            <div class="flex items-center gap-3">
              <div class="w-8 h-8 rounded-lg bg-gradient-to-br flex items-center justify-center" [ngClass]="gradient">
                @switch (icon) {
                  @case ('embedding') {
                    <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 7v10c0 2.21 3.582 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.582 4 8 4s8-1.79 8-4M4 7c0-2.21 3.582-4 8-4s8 1.79 8 4"/></svg>
                  }
                  @case ('document') {
                    <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z"/></svg>
                  }
                  @case ('cpu') {
                    <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2zM9 9h6v6H9V9z"/></svg>
                  }
                  @case ('microphone') {
                    <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z"/></svg>
                  }
                  @default {
                    <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.066 2.573c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.573 1.066c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.066-2.573c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/></svg>
                  }
                }
              </div>
              <div>
                <h2 class="text-sm font-bold uppercase tracking-wider" [style.color]="'var(--text-primary)'">
                  {{ label }} ({{ providers.length }})
                </h2>
                @if (subtitle) {
                  <p class="text-[10px] font-normal normal-case tracking-normal" [style.color]="'var(--text-muted)'">{{ subtitle }}</p>
                }
              </div>
            </div>
            <svg class="w-5 h-5 transition-transform duration-200" [class.rotate-180]="isSectionExpanded(section)" [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
            </svg>
          </button>

          @if (isSectionExpanded(section)) {
            <div [id]="'section-' + section" class="glass-card-static no-backdrop-blur rounded-t-none border-t-0 p-5 overflow-hidden">
              <!-- Chain Visualization -->
              <p class="text-xs font-bold uppercase tracking-wider mb-3" [style.color]="'var(--text-muted)'">Fallback Chain</p>
              <div class="flex items-center gap-1 overflow-x-auto pb-3 mb-4 custom-scrollbar" tabindex="0" role="region" [attr.aria-label]="label + ' fallback chain'">
                @for (p of providers; track p.name; let last = $last) {
                  <div class="flex items-center gap-1 flex-shrink-0">
                    <div class="px-3 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-all"
                         [class]="getChainHealthClass(p)">
                      <span class="w-2 h-2 rounded-full" [class]="getChainStatusDotClass(p)"></span>
                      {{ p.name }}
                    </div>
                    @if (!last) {
                      <svg class="w-4 h-4 flex-shrink-0" [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
                      </svg>
                    }
                  </div>
                }
              </div>

              <!-- Provider Cards -->
              <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                @for (p of providers; track p.name; let i = $index) {
                  <div class="glass-card p-5 animate-fade-in-up" [class]="'stagger-' + mathMin(i + 1, 5)">
                    <div class="flex items-center justify-between mb-3">
                      <h3 class="text-sm font-bold" [style.color]="'var(--text-primary)'">{{ p.name }}</h3>
                      <div class="flex items-center gap-1.5">
                        <span class="w-2.5 h-2.5 rounded-full" [class]="getChainStatusDotClass(p)"
                              [class.animate-pulse]="p.status === 'Down'"></span>
                        <span class="text-[10px] font-semibold" [class]="getChainStatusTextClass(p)">{{ p.status }}</span>
                      </div>
                    </div>
                    <div class="space-y-2">
                      <div class="flex justify-between text-xs">
                        <span [style.color]="'var(--text-muted)'">Chain Order</span>
                        <span class="font-mono font-semibold px-2 py-0.5 rounded bg-indigo-500/15 text-indigo-400">#{{ p.chainOrder }}</span>
                      </div>
                      <div class="flex justify-between text-xs">
                        <span [style.color]="'var(--text-muted)'">Configured</span>
                        @if (p.isConfigured) {
                          <span class="font-medium text-emerald-400 flex items-center gap-1">
                            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>
                            Yes
                          </span>
                        } @else {
                          <span class="font-medium text-slate-400 flex items-center gap-1">
                            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>
                            No
                          </span>
                        }
                      </div>
                      <div class="flex justify-between text-xs">
                        <span [style.color]="'var(--text-muted)'">Available</span>
                        <span class="font-medium" [class]="p.isAvailable ? 'text-emerald-400' : 'text-rose-400'">
                          {{ p.isAvailable ? 'Yes' : 'No' }}
                        </span>
                      </div>
                      @if (p.freeTierLimit) {
                        <div class="flex justify-between text-xs">
                          <span [style.color]="'var(--text-muted)'">Free Tier</span>
                          <span class="font-medium text-sky-400">{{ p.freeTierLimit }}</span>
                        </div>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      </ng-template>

      <!-- Service Section Template (for Content Safety, Translation) -->
      <ng-template #serviceSection let-section let-label="label" let-services="services" let-gradient="gradient">
        <div class="mb-6 animate-fade-in-up isolate">
          <button (click)="toggleSection(section)" class="w-full text-left flex items-center justify-between p-4 glass-card-static no-backdrop-blur mb-0 cursor-pointer hover:bg-white/5 transition-colors !transform-none"
                  [class.rounded-b-none]="isSectionExpanded(section)"
                  [attr.aria-expanded]="isSectionExpanded(section)" [attr.aria-controls]="'section-' + section" [attr.aria-label]="'Toggle ' + label + ' section'">
            <div class="flex items-center gap-3">
              <div class="w-8 h-8 rounded-lg bg-gradient-to-br flex items-center justify-center" [ngClass]="gradient">
                <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/></svg>
              </div>
              <h2 class="text-sm font-bold uppercase tracking-wider" [style.color]="'var(--text-primary)'">
                {{ label }} ({{ services.length }})
              </h2>
            </div>
            <svg class="w-5 h-5 transition-transform duration-200" [class.rotate-180]="isSectionExpanded(section)" [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
            </svg>
          </button>

          @if (isSectionExpanded(section)) {
            <div [id]="'section-' + section" class="glass-card-static no-backdrop-blur rounded-t-none border-t-0 p-5 overflow-hidden">
              <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
                @for (svc of services; track svc.name; let i = $index) {
                  <div class="glass-card p-5 animate-fade-in-up" [class]="'stagger-' + mathMin(i + 1, 5)">
                    <div class="flex items-center gap-3">
                      <div class="w-10 h-10 rounded-xl flex items-center justify-center flex-shrink-0"
                           [class]="svc.isConfigured ? 'bg-emerald-500/15' : 'bg-slate-500/15'">
                        <svg class="w-5 h-5" [class]="svc.isConfigured ? 'text-emerald-400' : 'text-slate-400'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
                        </svg>
                      </div>
                      <div class="flex-1 min-w-0">
                        <p class="text-sm font-semibold truncate" [style.color]="'var(--text-primary)'">{{ svc.name }}</p>
                        <div class="flex items-center gap-1.5 mt-0.5">
                          @if (svc.isConfigured) {
                            <svg class="w-3.5 h-3.5 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
                            </svg>
                            <span class="text-[10px] font-medium text-emerald-400">Configured</span>
                          } @else {
                            <svg class="w-3.5 h-3.5 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                            </svg>
                            <span class="text-[10px] font-medium text-slate-400">Not Configured</span>
                          }
                        </div>
                      </div>
                      <span class="badge text-[10px]" [class]="svc.status === 'Available' ? 'badge-success' : 'badge-neutral'">
                        {{ svc.status }}
                      </span>
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      </ng-template>

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
export class ProviderHealthComponent implements OnInit, OnDestroy {
  private destroyRef = inject(DestroyRef);
  private claimsService = inject(ClaimsService);

  llmProviders = signal<LlmProviderHealth[]>([]);
  embeddingProviders = signal<ProviderChainHealth[]>([]);
  ocrProviders = signal<ProviderChainHealth[]>([]);
  nerProviders = signal<ProviderChainHealth[]>([]);
  sttProviders = signal<ProviderChainHealth[]>([]);
  contentSafety = signal<ServiceHealth[]>([]);
  translation = signal<ServiceHealth[]>([]);
  lastChecked = signal<string | null>(null);
  isLoading = signal(true);
  error = signal<string | null>(null);

  /** Tracks which sections are expanded. LLM is expanded by default. */
  expandedSections = signal<Set<string>>(new Set(['llm']));

  ngOnInit(): void {
    this.refresh();
    interval(30000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refresh());
  }

  ngOnDestroy(): void {}

  refresh(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.claimsService.getExtendedProviderHealth()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.llmProviders.set(res.llmProviders ?? []);
          this.embeddingProviders.set(res.embeddingProviders ?? []);
          this.ocrProviders.set(res.ocrProviders ?? []);
          this.nerProviders.set(res.nerProviders ?? []);
          this.sttProviders.set(res.sttProviders ?? []);
          this.contentSafety.set(res.contentSafety ?? []);
          this.translation.set(res.translation ?? []);
          this.lastChecked.set(res.checkedAt ?? null);
          this.isLoading.set(false);
        },
        error: () => {
          this.error.set('Failed to load provider health. Please try again.');
          this.isLoading.set(false);
        }
      });
  }

  toggleSection(section: string): void {
    const current = new Set(this.expandedSections());
    if (current.has(section)) {
      current.delete(section);
    } else {
      current.add(section);
    }
    this.expandedSections.set(current);
  }

  isSectionExpanded(section: string): boolean {
    return this.expandedSections().has(section);
  }

  mathMin(a: number, b: number): number {
    return Math.min(a, b);
  }

  formatTime(dateStr: string): string {
    return new Date(dateStr).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  }

  // ---- LLM Provider helpers ----

  getLlmStatusDotClass(provider: LlmProviderHealth): string {
    switch (provider.status) {
      case 'Healthy': return 'bg-emerald-400';
      case 'Degraded': return 'bg-amber-400';
      case 'Down': return 'bg-rose-500';
      default: return 'bg-slate-400';
    }
  }

  getLlmStatusTextClass(provider: LlmProviderHealth): string {
    switch (provider.status) {
      case 'Healthy': return 'text-emerald-400';
      case 'Degraded': return 'text-amber-400';
      case 'Down': return 'text-rose-400';
      default: return '';
    }
  }

  getLlmChainClass(provider: LlmProviderHealth): string {
    if (provider.status === 'Healthy') return 'bg-emerald-500/10 border border-emerald-500/20 text-emerald-400';
    if (provider.status === 'Degraded') return 'bg-amber-500/10 border border-amber-500/20 text-amber-400';
    return 'bg-rose-500/10 border border-rose-500/20 text-rose-400';
  }

  // ---- Chain (ProviderChainHealth) helpers ----

  getChainStatusDotClass(provider: ProviderChainHealth): string {
    switch (provider.status) {
      case 'Healthy': return 'bg-emerald-400';
      case 'Degraded': return 'bg-amber-400';
      case 'Down': return 'bg-rose-500';
      case 'NotConfigured': return 'bg-slate-400';
      default: return 'bg-slate-400';
    }
  }

  getChainStatusTextClass(provider: ProviderChainHealth): string {
    switch (provider.status) {
      case 'Healthy': return 'text-emerald-400';
      case 'Degraded': return 'text-amber-400';
      case 'Down': return 'text-rose-400';
      case 'NotConfigured': return 'text-slate-400';
      default: return '';
    }
  }

  getChainHealthClass(provider: ProviderChainHealth): string {
    if (provider.status === 'Healthy') return 'bg-emerald-500/10 border border-emerald-500/20 text-emerald-400';
    if (provider.status === 'Degraded') return 'bg-amber-500/10 border border-amber-500/20 text-amber-400';
    if (provider.status === 'NotConfigured') return 'bg-slate-500/10 border border-slate-500/20 text-slate-400';
    return 'bg-rose-500/10 border border-rose-500/20 text-rose-400';
  }
}
