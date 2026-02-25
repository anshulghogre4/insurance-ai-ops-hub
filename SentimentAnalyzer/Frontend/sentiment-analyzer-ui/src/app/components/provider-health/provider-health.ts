import { Component, DestroyRef, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { ClaimsService } from '../../services/claims.service';
import { ProviderHealthResponse, LlmProviderHealth, ServiceHealth } from '../../models/claims.model';

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
        <button (click)="refresh()" class="btn-ghost text-sm p-2.5" aria-label="Refresh health status">
          <svg class="w-4 h-4" [class.animate-spin]="isLoading()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
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

      <!-- Fallback Chain Visualization -->
      @if (llmProviders().length > 0) {
        <div class="glass-card-static p-5 mb-6 animate-fade-in-up stagger-1">
          <p class="text-xs font-bold uppercase tracking-wider mb-3" [style.color]="'var(--text-muted)'">LLM Fallback Chain</p>
          <div class="flex items-center gap-1 overflow-x-auto pb-2 custom-scrollbar" tabindex="0" role="region" aria-label="LLM provider fallback chain">
            @for (provider of llmProviders(); track provider.name; let last = $last) {
              <div class="flex items-center gap-1 flex-shrink-0">
                <div class="px-3 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-all"
                     [class]="getChainClass(provider)">
                  <span class="w-2 h-2 rounded-full" [class]="getStatusDotClass(provider)"></span>
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
        </div>
      }

      <!-- LLM Providers Grid -->
      <div class="mb-8">
        <h2 class="text-sm font-bold uppercase tracking-wider mb-4" [style.color]="'var(--text-muted)'">
          LLM Providers ({{ llmProviders().length }})
        </h2>

        @if (isLoading() && llmProviders().length === 0) {
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
            @for (i of [1,2,3,4]; track i) {
              <div class="skeleton h-36 rounded-xl"></div>
            }
          </div>
        }

        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          @for (provider of llmProviders(); track provider.name; let i = $index) {
            <div class="glass-card p-5 animate-fade-in-up" [class]="'stagger-' + Math.min(i + 1, 5)">
              <div class="flex items-center justify-between mb-3">
                <h3 class="text-sm font-bold" [style.color]="'var(--text-primary)'">{{ provider.name }}</h3>
                <div class="flex items-center gap-1.5">
                  <span class="w-2.5 h-2.5 rounded-full" [class]="getStatusDotClass(provider)"
                        [class.animate-pulse]="provider.status === 'Down'"></span>
                  <span class="text-[10px] font-semibold" [class]="getStatusTextClass(provider)">{{ provider.status }}</span>
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
                    <div class="progress-fill bg-orange-500" [style.width.%]="Math.min((provider.cooldownSeconds / 300) * 100, 100)"></div>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      </div>

      <!-- Multimodal Services Grid -->
      <div>
        <h2 class="text-sm font-bold uppercase tracking-wider mb-4" [style.color]="'var(--text-muted)'">
          Multimodal Services ({{ multimodalServices().length }})
        </h2>

        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          @for (svc of multimodalServices(); track svc.name; let i = $index) {
            <div class="glass-card p-5 animate-fade-in-up" [class]="'stagger-' + Math.min(i + 1, 5)">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-xl flex items-center justify-center flex-shrink-0"
                     [class]="svc.isConfigured ? 'bg-emerald-500/15' : 'bg-slate-500/15'">
                  @switch (getServiceIcon(svc)) {
                    @case ('microphone') {
                      <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z"/></svg>
                    }
                    @case ('eye') {
                      <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/><path stroke-linecap="round" stroke-linejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"/></svg>
                    }
                    @case ('cloud') {
                      <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M3 15a4 4 0 004 4h9a5 5 0 10-.1-9.999 5.002 5.002 0 10-9.78 2.096A4.001 4.001 0 003 15z"/></svg>
                    }
                    @case ('document') {
                      <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z"/></svg>
                    }
                    @case ('cpu') {
                      <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2zM9 9h6v6H9V9z"/></svg>
                    }
                    @case ('compass') {
                      <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 2a10 10 0 110 20 10 10 0 010-20zm0 0l3.5 6.5L12 12l-3.5-3.5L12 2z"/><polygon stroke-linecap="round" stroke-linejoin="round" points="16.24 7.76 14.12 14.12 7.76 16.24 9.88 9.88"/></svg>
                    }
                    @default {
                      <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.066 2.573c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.573 1.066c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.066-2.573c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"/><path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/></svg>
                    }
                  }
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

  Math = Math;

  llmProviders = signal<LlmProviderHealth[]>([]);
  multimodalServices = signal<ServiceHealth[]>([]);
  lastChecked = signal<string | null>(null);
  isLoading = signal(true);
  error = signal<string | null>(null);

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
    this.claimsService.getProviderHealth()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.llmProviders.set(res.llmProviders);
          this.multimodalServices.set(res.multimodalServices);
          this.lastChecked.set(res.checkedAt);
          this.isLoading.set(false);
        },
        error: () => {
          this.error.set('Failed to load provider health. Please try again.');
          this.isLoading.set(false);
        }
      });
  }

  formatTime(dateStr: string): string {
    return new Date(dateStr).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  }

  getStatusDotClass(provider: LlmProviderHealth): string {
    switch (provider.status) {
      case 'Healthy': return 'bg-emerald-400';
      case 'Degraded': return 'bg-amber-400';
      case 'Down': return 'bg-rose-500';
      default: return 'bg-slate-400';
    }
  }

  getStatusTextClass(provider: LlmProviderHealth): string {
    switch (provider.status) {
      case 'Healthy': return 'text-emerald-400';
      case 'Degraded': return 'text-amber-400';
      case 'Down': return 'text-rose-400';
      default: return '';
    }
  }

  getChainClass(provider: LlmProviderHealth): string {
    if (provider.status === 'Healthy') return 'bg-emerald-500/10 border border-emerald-500/20 text-emerald-400';
    if (provider.status === 'Degraded') return 'bg-amber-500/10 border border-amber-500/20 text-amber-400';
    return 'bg-rose-500/10 border border-rose-500/20 text-rose-400';
  }

  getServiceIcon(svc: ServiceHealth): string {
    if (svc.name.includes('Deepgram')) return 'microphone';
    if (svc.name.includes('Azure')) return 'eye';
    if (svc.name.includes('Cloudflare')) return 'cloud';
    if (svc.name.includes('Ocr') || svc.name.includes('OCR')) return 'document';
    if (svc.name.includes('Hugging')) return 'cpu';
    if (svc.name.includes('Voyage')) return 'compass';
    return 'gear';
  }
}
