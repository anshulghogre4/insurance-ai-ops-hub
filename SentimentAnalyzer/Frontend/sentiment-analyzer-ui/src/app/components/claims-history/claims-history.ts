import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, of } from 'rxjs';
import { switchMap, catchError } from 'rxjs/operators';
import { ClaimsService } from '../../services/claims.service';
import { ClaimTriageResponse } from '../../models/claims.model';

@Component({
  selector: 'app-claims-history',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">

      <!-- Header -->
      <div class="flex items-center justify-between mb-6 animate-fade-in-up">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-cyan-500 to-blue-600 flex items-center justify-center shadow-lg shadow-cyan-500/20">
            <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
            </svg>
          </div>
          <div>
            <h1 class="text-xl sm:text-2xl font-bold" [style.color]="'var(--text-primary)'">Claims History</h1>
            <p class="text-xs" [style.color]="'var(--text-muted)'">{{ totalCount() }} total claims</p>
          </div>
        </div>
        <div class="flex items-center gap-2">
          <a routerLink="/claims/triage" class="btn-primary text-sm flex items-center gap-2" aria-label="New triage">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
            </svg>
            New Triage
          </a>
          <button (click)="loadClaims()" class="btn-ghost text-sm p-2.5" aria-label="Refresh claims">
            <svg class="w-4 h-4" [class.animate-spin]="isLoading()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
          </button>
        </div>
      </div>

      <!-- Search -->
      <div class="glass-card-static p-4 mb-4 animate-fade-in-up stagger-1">
        <div class="relative">
          <svg class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4" [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
          </svg>
          <input type="text" [(ngModel)]="searchText" (keydown.enter)="applyFilters()"
                 class="input-field !py-2.5 !pl-10 text-sm w-full"
                 placeholder="Search claims by type, status, or ID..."
                 aria-label="Search claims" />
        </div>
      </div>

      <!-- Filters -->
      <div class="glass-card-static p-4 mb-5 animate-fade-in-up stagger-1">
        <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
          <div>
            <label class="text-[10px] uppercase tracking-wider font-semibold block mb-1" [style.color]="'var(--text-muted)'">Severity</label>
            <select [(ngModel)]="filterSeverity" class="input-field !py-2 text-sm" aria-label="Filter by severity">
              <option value="">All</option>
              <option value="Critical">Critical</option>
              <option value="High">High</option>
              <option value="Medium">Medium</option>
              <option value="Low">Low</option>
            </select>
          </div>
          <div>
            <label class="text-[10px] uppercase tracking-wider font-semibold block mb-1" [style.color]="'var(--text-muted)'">Status</label>
            <select [(ngModel)]="filterStatus" class="input-field !py-2 text-sm" aria-label="Filter by status">
              <option value="">All</option>
              <option value="Submitted">Submitted</option>
              <option value="Triaging">Triaging</option>
              <option value="Triaged">Triaged</option>
              <option value="UnderReview">Under Review</option>
              <option value="Resolved">Resolved</option>
            </select>
          </div>
          <div>
            <label class="text-[10px] uppercase tracking-wider font-semibold block mb-1" [style.color]="'var(--text-muted)'">From Date</label>
            <input type="date" [(ngModel)]="filterFromDate" class="input-field !py-2 text-sm" aria-label="From date filter" />
          </div>
          <div>
            <label class="text-[10px] uppercase tracking-wider font-semibold block mb-1" [style.color]="'var(--text-muted)'">To Date</label>
            <input type="date" [(ngModel)]="filterToDate" class="input-field !py-2 text-sm" aria-label="To date filter" />
          </div>
          <div class="flex items-end gap-2">
            <button (click)="applyFilters()" class="btn-primary !py-2 text-sm flex-1" aria-label="Apply filters">Apply</button>
          </div>
          <div class="flex items-end">
            <button (click)="clearFilters()" class="btn-ghost !py-2 text-sm w-full" aria-label="Clear filters">Clear</button>
          </div>
        </div>
      </div>

      <!-- Loading -->
      @if (isLoading()) {
        <div class="space-y-3">
          @for (i of [1,2,3,4,5]; track i) {
            <div class="skeleton h-16 rounded-xl"></div>
          }
        </div>
      }

      <!-- Table -->
      @if (!isLoading() && claims().length > 0) {
        <div class="glass-card-static overflow-hidden animate-fade-in-up stagger-2">
          <div class="overflow-x-auto">
            <table class="w-full text-sm" role="table">
              <thead>
                <tr [style.background]="'var(--bg-surface-hover)'">
                  <th class="px-4 py-3 text-left text-[10px] uppercase tracking-wider font-semibold" [style.color]="'var(--text-muted)'">#</th>
                  <th class="px-4 py-3 text-left text-[10px] uppercase tracking-wider font-semibold" [style.color]="'var(--text-muted)'">Date</th>
                  <th class="px-4 py-3 text-left text-[10px] uppercase tracking-wider font-semibold hidden sm:table-cell" [style.color]="'var(--text-muted)'">Type</th>
                  <th class="px-4 py-3 text-center text-[10px] uppercase tracking-wider font-semibold" [style.color]="'var(--text-muted)'">Severity</th>
                  <th class="px-4 py-3 text-center text-[10px] uppercase tracking-wider font-semibold hidden md:table-cell" [style.color]="'var(--text-muted)'">Urgency</th>
                  <th class="px-4 py-3 text-center text-[10px] uppercase tracking-wider font-semibold" [style.color]="'var(--text-muted)'">Fraud</th>
                  <th class="px-4 py-3 text-center text-[10px] uppercase tracking-wider font-semibold hidden lg:table-cell" [style.color]="'var(--text-muted)'">Status</th>
                </tr>
              </thead>
              <tbody>
                @for (claim of claims(); track claim.claimId; let i = $index) {
                  <tr class="cursor-pointer transition-all duration-150 border-t"
                      [style.border-color]="'var(--border-secondary)'"
                      [class.hover:bg-indigo-500/5]="true"
                      tabindex="0"
                      role="row"
                      (click)="viewClaim(claim.claimId)"
                      (keydown.enter)="viewClaim(claim.claimId)"
                      [attr.aria-label]="'View claim ' + claim.claimId">
                    <td class="px-4 py-3 font-mono text-xs" [style.color]="'var(--text-muted)'">#{{ claim.claimId }}</td>
                    <td class="px-4 py-3 text-xs" [style.color]="'var(--text-secondary)'">{{ formatDate(claim.createdAt) }}</td>
                    <td class="px-4 py-3 hidden sm:table-cell"><span class="badge badge-info text-[10px]">{{ claim.claimType }}</span></td>
                    <td class="px-4 py-3 text-center">
                      <span class="inline-block px-2.5 py-0.5 rounded-full text-[10px] font-bold text-white" [class]="getSeverityClass(claim.severity)">{{ claim.severity }}</span>
                    </td>
                    <td class="px-4 py-3 text-center hidden md:table-cell">
                      <span class="badge text-[10px]" [class]="getUrgencyBadge(claim.urgency)">{{ claim.urgency }}</span>
                    </td>
                    <td class="px-4 py-3 text-center">
                      <span class="font-bold text-xs" [class]="getFraudColor(claim.fraudScore)">{{ claim.fraudScore }}</span>
                    </td>
                    <td class="px-4 py-3 text-center hidden lg:table-cell">
                      <span class="badge badge-neutral text-[10px]">{{ claim.status }}</span>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Pagination -->
          @if (totalPages() > 1) {
            <div class="flex items-center justify-between px-4 py-3 border-t" [style.border-color]="'var(--border-secondary)'" [style.background]="'var(--bg-surface)'">
              <span class="text-xs" [style.color]="'var(--text-muted)'">Page {{ currentPage() }} of {{ totalPages() }}</span>
              <div class="flex items-center gap-2">
                <select [(ngModel)]="pageSizeValue" (ngModelChange)="onPageSizeChange()" class="input-field !py-1.5 !px-2 text-xs w-20" aria-label="Page size">
                  <option [value]="10">10</option>
                  <option [value]="20">20</option>
                  <option [value]="50">50</option>
                </select>
                <button (click)="prevPage()" [disabled]="currentPage() <= 1" class="btn-ghost !py-1.5 !px-3 text-xs" aria-label="Previous page">Prev</button>
                <button (click)="nextPage()" [disabled]="currentPage() >= totalPages()" class="btn-ghost !py-1.5 !px-3 text-xs" aria-label="Next page">Next</button>
              </div>
            </div>
          }
        </div>
      }

      <!-- Error State -->
      @if (!isLoading() && error()) {
        <div class="glass-card-static p-4 mb-6 flex items-center gap-3 border-l-4 border-l-rose-500 animate-fade-in-up" role="alert">
          <svg class="w-5 h-5 text-rose-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
          </svg>
          <span class="text-sm text-rose-400">{{ error() }}</span>
          <button (click)="loadClaims()" class="btn-ghost text-xs ml-auto" aria-label="Retry loading claims">Retry</button>
        </div>
      }

      <!-- Empty State -->
      @if (!isLoading() && !error() && claims().length === 0) {
        <div class="glass-card-static p-12 text-center animate-fade-in">
          <svg class="w-16 h-16 mx-auto mb-4" [style.color]="'var(--text-muted)'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
          </svg>
          <p class="text-sm font-medium mb-1" [style.color]="'var(--text-primary)'">No claims found</p>
          <p class="text-xs mb-4" [style.color]="'var(--text-muted)'">Submit a claim for AI-powered triage assessment</p>
          <a routerLink="/claims/triage" class="btn-primary text-sm inline-flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
            </svg>
            Start Triage
          </a>
        </div>
      }
    </div>
  `
})
export class ClaimsHistoryComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private claimsService = inject(ClaimsService);
  private router = inject(Router);
  private loadSubject = new Subject<void>();

  claims = signal<ClaimTriageResponse[]>([]);
  totalCount = signal(0);
  currentPage = signal(1);
  totalPages = signal(0);
  isLoading = signal(true);
  error = signal<string | null>(null);
  pageSizeValue = 20;

  searchText = '';
  filterSeverity = '';
  filterStatus = '';
  filterFromDate = '';
  filterToDate = '';

  ngOnInit(): void {
    this.loadSubject.pipe(
      switchMap(() => {
        this.isLoading.set(true);
        this.error.set(null);
        return this.claimsService.getClaimsHistory({
          severity: this.filterSeverity || undefined,
          status: this.filterStatus || undefined,
          fromDate: this.filterFromDate || undefined,
          toDate: this.filterToDate || undefined,
          pageSize: this.pageSizeValue,
          page: this.currentPage()
        }).pipe(
          catchError(() => {
            this.error.set('Failed to load claims history. Please try again.');
            this.claims.set([]);
            this.isLoading.set(false);
            return of(null);
          })
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((res) => {
      if (res) {
        const q = this.searchText.trim().toLowerCase();
        const filtered = q
          ? res.items.filter(c =>
              c.claimType.toLowerCase().includes(q) ||
              c.status.toLowerCase().includes(q) ||
              c.severity.toLowerCase().includes(q) ||
              String(c.claimId).includes(q))
          : res.items;
        this.claims.set(filtered);
        this.totalCount.set(q ? filtered.length : res.totalCount);
        this.totalPages.set(q ? 1 : res.totalPages);
        this.isLoading.set(false);
      }
    });

    this.loadSubject.next();
  }

  loadClaims(): void {
    this.loadSubject.next();
  }

  applyFilters(): void {
    this.currentPage.set(1);
    this.loadClaims();
  }

  clearFilters(): void {
    this.searchText = '';
    this.filterSeverity = '';
    this.filterStatus = '';
    this.filterFromDate = '';
    this.filterToDate = '';
    this.currentPage.set(1);
    this.loadClaims();
  }

  viewClaim(id: number): void {
    this.router.navigate(['/claims', id]);
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update(p => p - 1);
      this.loadClaims();
    }
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(p => p + 1);
      this.loadClaims();
    }
  }

  onPageSizeChange(): void {
    this.currentPage.set(1);
    this.loadClaims();
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  getSeverityClass(s: string): string {
    const map: Record<string, string> = { Critical: 'bg-rose-500', High: 'bg-orange-500', Medium: 'bg-amber-500', Low: 'bg-emerald-500' };
    return map[s] || 'bg-slate-500';
  }

  getUrgencyBadge(u: string): string {
    const map: Record<string, string> = { Immediate: 'badge-danger', Urgent: 'badge-warning', Standard: 'badge-info', Low: 'badge-success' };
    return map[u] || 'badge-neutral';
  }

  getFraudColor(score: number): string {
    if (score >= 75) return 'text-rose-400';
    if (score >= 55) return 'text-orange-400';
    if (score >= 30) return 'text-amber-400';
    return 'text-emerald-400';
  }
}
