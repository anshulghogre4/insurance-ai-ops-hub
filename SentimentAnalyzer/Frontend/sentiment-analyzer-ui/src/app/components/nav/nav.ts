import { Component, ElementRef, HostListener, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ThemeService, ThemeMode } from '../../services/theme.service';

@Component({
  selector: 'app-nav',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="sticky top-0 z-50 border-b backdrop-blur-xl transition-all duration-300"
         [style.background]="'var(--nav-bg)'"
         [style.border-color]="'var(--border-primary)'">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex items-center justify-between h-16">

          <!-- Logo -->
          <a routerLink="/" aria-label="InsureSense AI - Home" class="flex items-center gap-3 group">
            <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center shadow-lg shadow-indigo-500/20 group-hover:shadow-indigo-500/40 transition-shadow">
              <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
              </svg>
            </div>
            <div class="hidden sm:block">
              <span class="font-bold text-lg" [style.color]="'var(--text-primary)'">InsureSense</span>
              <span class="font-light text-lg text-indigo-400 ml-0.5">AI</span>
            </div>
          </a>

          <!-- Desktop Navigation -->
          <div class="hidden md:flex items-center gap-1">
            @if (!authService.authEnabled() || authService.isAuthenticated()) {

              <!-- Analyze Dropdown (combines Sentiment v1 + Insurance v2) -->
              <div class="relative" (mouseenter)="showAnalyzeMenu.set(true)" (mouseleave)="showAnalyzeMenu.set(false)">
                <button class="nav-link flex items-center gap-2"
                        [class.nav-link-active]="isAnalyzeRoute()"
                        aria-haspopup="true" [attr.aria-expanded]="showAnalyzeMenu()"
                        (click)="toggleAnalyzeMenu()">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z"/>
                  </svg>
                  Analyze
                  <svg class="w-3 h-3 transition-transform" [class.rotate-180]="showAnalyzeMenu()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                  </svg>
                </button>
                @if (showAnalyzeMenu()) {
                  <div class="absolute left-0 top-full w-56 pt-2 z-50">
                  <div class="rounded-xl shadow-xl border p-1.5 animate-fade-in-up"
                       [style.background]="'var(--bg-secondary)'" [style.border-color]="'var(--border-primary)'">
                    <a routerLink="/sentiment" (click)="showAnalyzeMenu.set(false)"
                       class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors hover:bg-[var(--bg-surface-hover)]"
                       [style.color]="'var(--text-secondary)'">
                      <svg class="w-4 h-4 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z"/>
                      </svg>
                      <div>
                        <div>Sentiment</div>
                        <div class="text-[10px] opacity-50">v1 Legacy</div>
                      </div>
                    </a>
                    <a routerLink="/insurance" (click)="showAnalyzeMenu.set(false)"
                       class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors hover:bg-[var(--bg-surface-hover)]"
                       [style.color]="'var(--text-secondary)'">
                      <svg class="w-4 h-4 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
                      </svg>
                      <div>
                        <div>Insurance</div>
                        <div class="text-[10px] opacity-50">v2 Multi-Agent</div>
                      </div>
                    </a>
                  </div>
                  </div>
                }
              </div>

              <!-- Claims Dropdown -->
              <div class="relative" (mouseenter)="showClaimsMenu.set(true)" (mouseleave)="showClaimsMenu.set(false)">
                <button class="nav-link flex items-center gap-2"
                        [class.nav-link-active]="isClaimsRoute()"
                        aria-haspopup="true" [attr.aria-expanded]="showClaimsMenu()"
                        (click)="toggleClaimsMenu()">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/>
                  </svg>
                  Claims
                  <svg class="w-3 h-3 transition-transform" [class.rotate-180]="showClaimsMenu()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                  </svg>
                </button>
                @if (showClaimsMenu()) {
                  <div class="absolute left-0 top-full w-48 pt-2 z-50">
                  <div class="rounded-xl shadow-xl border p-1.5 animate-fade-in-up"
                       [style.background]="'var(--bg-secondary)'" [style.border-color]="'var(--border-primary)'">
                    <a routerLink="/claims/triage" (click)="showClaimsMenu.set(false)"
                       class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors hover:bg-[var(--bg-surface-hover)]"
                       [style.color]="'var(--text-secondary)'">
                      <svg class="w-4 h-4 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
                      </svg>
                      New Triage
                    </a>
                    <a routerLink="/claims/history" (click)="showClaimsMenu.set(false)"
                       class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors hover:bg-[var(--bg-surface-hover)]"
                       [style.color]="'var(--text-secondary)'">
                      <svg class="w-4 h-4 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16"/>
                      </svg>
                      History
                    </a>
                  </div>
                  </div>
                }
              </div>

              <!-- Workspace Dropdown (Documents + CX Copilot) -->
              <div class="relative" (mouseenter)="showWorkspaceMenu.set(true)" (mouseleave)="showWorkspaceMenu.set(false)">
                <button class="nav-link flex items-center gap-2"
                        [class.nav-link-active]="isWorkspaceRoute()"
                        aria-haspopup="true" [attr.aria-expanded]="showWorkspaceMenu()"
                        (click)="toggleWorkspaceMenu()">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"/>
                  </svg>
                  Workspace
                  <svg class="w-3 h-3 transition-transform" [class.rotate-180]="showWorkspaceMenu()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                  </svg>
                </button>
                @if (showWorkspaceMenu()) {
                  <div class="absolute left-0 top-full w-56 pt-2 z-50">
                  <div class="rounded-xl shadow-xl border p-1.5 animate-fade-in-up"
                       [style.background]="'var(--bg-secondary)'" [style.border-color]="'var(--border-primary)'">
                    <p class="text-[10px] font-bold uppercase tracking-wider px-3 pt-1 pb-1" [style.color]="'var(--text-muted)'">Documents</p>
                    <a routerLink="/documents/upload" (click)="showWorkspaceMenu.set(false)"
                       class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors hover:bg-[var(--bg-surface-hover)]"
                       [style.color]="'var(--text-secondary)'">
                      <svg class="w-4 h-4 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"/>
                      </svg>
                      Upload
                    </a>
                    <a routerLink="/documents/query" (click)="showWorkspaceMenu.set(false)"
                       class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors hover:bg-[var(--bg-surface-hover)]"
                       [style.color]="'var(--text-secondary)'">
                      <svg class="w-4 h-4 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                      </svg>
                      Query
                    </a>
                    <div class="my-1 border-t" [style.border-color]="'var(--border-secondary)'"></div>
                    <p class="text-[10px] font-bold uppercase tracking-wider px-3 pt-1 pb-1" [style.color]="'var(--text-muted)'">AI Assistant</p>
                    <a routerLink="/cx/copilot" (click)="showWorkspaceMenu.set(false)"
                       class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors hover:bg-[var(--bg-surface-hover)]"
                       [style.color]="'var(--text-secondary)'">
                      <svg class="w-4 h-4 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z"/>
                      </svg>
                      CX Copilot
                    </a>
                  </div>
                  </div>
                }
              </div>

              <!-- Dashboard Dropdown -->
              <div class="relative" (mouseenter)="showDashMenu.set(true)" (mouseleave)="showDashMenu.set(false)">
                <button class="nav-link flex items-center gap-2"
                        [class.nav-link-active]="isDashRoute()"
                        aria-haspopup="true" [attr.aria-expanded]="showDashMenu()"
                        (click)="toggleDashMenu()">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 8v8m-4-5v5m-4-2v2m-2 4h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/>
                  </svg>
                  Dashboard
                  <svg class="w-3 h-3 transition-transform" [class.rotate-180]="showDashMenu()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                  </svg>
                </button>
                @if (showDashMenu()) {
                  <div class="absolute left-0 top-full w-48 pt-2 z-50">
                  <div class="rounded-xl shadow-xl border p-1.5 animate-fade-in-up"
                       [style.background]="'var(--bg-secondary)'" [style.border-color]="'var(--border-primary)'">
                    <a routerLink="/dashboard" (click)="showDashMenu.set(false)"
                       class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors hover:bg-[var(--bg-surface-hover)]"
                       [style.color]="'var(--text-secondary)'">
                      <svg class="w-4 h-4 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 8v8m-4-5v5m-4-2v2m-2 4h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/>
                      </svg>
                      Overview
                    </a>
                    <a routerLink="/dashboard/providers" (click)="showDashMenu.set(false)"
                       class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors hover:bg-[var(--bg-surface-hover)]"
                       [style.color]="'var(--text-secondary)'">
                      <svg class="w-4 h-4 text-teal-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01"/>
                      </svg>
                      Providers
                    </a>
                    <a routerLink="/dashboard/fraud" (click)="showDashMenu.set(false)"
                       class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors hover:bg-[var(--bg-surface-hover)]"
                       [style.color]="'var(--text-secondary)'">
                      <svg class="w-4 h-4 text-rose-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
                      </svg>
                      Fraud Alerts
                    </a>
                  </div>
                  </div>
                }
              </div>
            }
          </div>

          <!-- Right side: Theme toggle + Auth -->
          <div class="flex items-center gap-2">
            <!-- Theme Toggle -->
            <button (click)="themeService.cycleTheme()" [title]="getThemeLabel()" [attr.aria-label]="getThemeLabel()"
                    class="p-2 rounded-lg transition-all duration-200 hover:scale-105"
                    [style.color]="'var(--text-secondary)'"
                    [style.background]="'var(--bg-surface)'">
              @switch (themeService.currentTheme()) {
                @case ('dark') {
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z"/>
                  </svg>
                }
                @case ('semi-dark') {
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z"/>
                  </svg>
                }
                @default {
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z"/>
                  </svg>
                }
              }
            </button>

            @if (authService.authEnabled()) {
              <div class="hidden md:flex items-center gap-2 ml-2 pl-3 border-l" [style.border-color]="'var(--border-primary)'">
                @if (authService.isAuthenticated()) {
                  <div class="relative">
                    <button (click)="toggleUserMenu()" class="flex items-center gap-2 p-1.5 rounded-lg transition-all duration-200"
                            [style.background]="'var(--bg-surface)'" [style.color]="'var(--text-secondary)'"
                            aria-haspopup="true" [attr.aria-expanded]="showUserMenu()" aria-label="User menu">
                      <div class="w-7 h-7 rounded-full bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center text-white text-xs font-bold">
                        {{ getUserInitial() }}
                      </div>
                      <span class="text-sm max-w-[140px] truncate hidden lg:block">{{ authService.user()?.email }}</span>
                      <svg class="w-3.5 h-3.5 transition-transform" [class.rotate-180]="showUserMenu()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                      </svg>
                    </button>
                    @if (showUserMenu()) {
                      <div class="absolute right-0 top-full mt-2 w-56 rounded-xl shadow-xl border p-1.5 animate-fade-in-up"
                           [style.background]="'var(--bg-secondary)'" [style.border-color]="'var(--border-primary)'">
                        <div class="px-3 py-2 mb-1 border-b" [style.border-color]="'var(--border-secondary)'">
                          <p class="text-xs font-medium" [style.color]="'var(--text-muted)'">Signed in as</p>
                          <p class="text-sm truncate" [style.color]="'var(--text-primary)'">{{ authService.user()?.email }}</p>
                        </div>
                        <button (click)="logout()" class="w-full text-left px-3 py-2 rounded-lg text-sm transition-colors flex items-center gap-2 hover:bg-rose-500/10 text-rose-400">
                          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"/>
                          </svg>
                          Sign Out
                        </button>
                      </div>
                    }
                  </div>
                } @else {
                  <a routerLink="/login" class="btn-primary text-sm !px-4 !py-2 flex items-center gap-2">
                    Sign In
                  </a>
                }
              </div>
            }

            <!-- Mobile hamburger -->
            <button (click)="toggleMobileMenu()" aria-label="Toggle navigation menu" class="md:hidden p-2 rounded-lg transition-all"
                    [style.color]="'var(--text-secondary)'" [style.background]="'var(--bg-surface)'">
              @if (showMobileMenu()) {
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                </svg>
              } @else {
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16"/>
                </svg>
              }
            </button>
          </div>
        </div>
      </div>

      <!-- Mobile Menu -->
      @if (showMobileMenu()) {
        <div class="md:hidden border-t animate-fade-in" [style.border-color]="'var(--border-primary)'" [style.background]="'var(--nav-bg)'">
          <div class="px-4 py-3 space-y-1">
            @if (!authService.authEnabled() || authService.isAuthenticated()) {
              <!-- Analyze section -->
              <p class="text-[10px] font-bold uppercase tracking-wider px-3 mb-1" [style.color]="'var(--text-muted)'">Analyze</p>
              <a routerLink="/sentiment" routerLinkActive="nav-link-active"
                 (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                <svg class="w-4 h-4 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z"/>
                </svg>
                <div>
                  <div>Sentiment</div>
                  <div class="text-[10px] opacity-50">v1 Legacy</div>
                </div>
              </a>
              <a routerLink="/insurance" routerLinkActive="nav-link-active"
                 (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                <svg class="w-4 h-4 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
                </svg>
                <div>
                  <div>Insurance</div>
                  <div class="text-[10px] opacity-50">v2 Multi-Agent</div>
                </div>
              </a>

              <!-- Claims section -->
              <div class="pt-2 mt-1 border-t" [style.border-color]="'var(--border-secondary)'">
                <p class="text-[10px] font-bold uppercase tracking-wider px-3 mb-1" [style.color]="'var(--text-muted)'">Claims</p>
                <a routerLink="/claims/triage" routerLinkActive="nav-link-active"
                   (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                  <svg class="w-4 h-4 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
                  </svg>
                  New Triage
                </a>
                <a routerLink="/claims/history" routerLinkActive="nav-link-active"
                   (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                  <svg class="w-4 h-4 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16"/>
                  </svg>
                  History
                </a>
              </div>

              <!-- Workspace section -->
              <div class="pt-2 mt-1 border-t" [style.border-color]="'var(--border-secondary)'">
                <p class="text-[10px] font-bold uppercase tracking-wider px-3 mb-1" [style.color]="'var(--text-muted)'">Workspace</p>
                <a routerLink="/documents/upload" routerLinkActive="nav-link-active"
                   (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                  <svg class="w-4 h-4 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"/>
                  </svg>
                  Upload Document
                </a>
                <a routerLink="/documents/query" routerLinkActive="nav-link-active"
                   (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                  <svg class="w-4 h-4 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                  </svg>
                  Query Documents
                </a>
                <a routerLink="/cx/copilot" routerLinkActive="nav-link-active"
                   (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                  <svg class="w-4 h-4 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z"/>
                  </svg>
                  CX Copilot
                </a>
              </div>

              <!-- Dashboard section -->
              <div class="pt-2 mt-1 border-t" [style.border-color]="'var(--border-secondary)'">
                <p class="text-[10px] font-bold uppercase tracking-wider px-3 mb-1" [style.color]="'var(--text-muted)'">Dashboard</p>
                <a routerLink="/dashboard" routerLinkActive="nav-link-active" [routerLinkActiveOptions]="{exact: true}"
                   (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                  <svg class="w-4 h-4 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 8v8m-4-5v5m-4-2v2m-2 4h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/>
                  </svg>
                  Overview
                </a>
                <a routerLink="/dashboard/providers" routerLinkActive="nav-link-active"
                   (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                  <svg class="w-4 h-4 text-teal-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01"/>
                  </svg>
                  Providers
                </a>
                <a routerLink="/dashboard/fraud" routerLinkActive="nav-link-active"
                   (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                  <svg class="w-4 h-4 text-rose-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
                  </svg>
                  Fraud Alerts
                </a>
              </div>
            }
            @if (authService.authEnabled()) {
              <div class="pt-2 mt-2 border-t" [style.border-color]="'var(--border-secondary)'">
                @if (authService.isAuthenticated()) {
                  <button (click)="logout(); showMobileMenu.set(false)"
                          class="nav-link w-full py-3 text-rose-400 flex items-center gap-3">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"/>
                    </svg>
                    Sign Out
                  </button>
                } @else {
                  <a routerLink="/login" (click)="showMobileMenu.set(false)"
                     class="nav-link w-full py-3 text-indigo-400 flex items-center gap-3">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 16l-4-4m0 0l4-4m-4 4h14m-5 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h7a3 3 0 013 3v1"/>
                    </svg>
                    Sign In
                  </a>
                }
              </div>
            }
          </div>
        </div>
      }
    </nav>
  `
})
export class Nav {
  authService = inject(AuthService);
  themeService = inject(ThemeService);
  private router = inject(Router);
  private el = inject(ElementRef);

  @HostListener('document:click', ['$event.target'])
  onDocumentClick(target: EventTarget | null): void {
    if (this.showUserMenu() && target instanceof Node && !this.el.nativeElement.contains(target)) {
      this.showUserMenu.set(false);
    }
  }

  showMobileMenu = signal(false);
  showUserMenu = signal(false);
  showAnalyzeMenu = signal(false);
  showClaimsMenu = signal(false);
  showWorkspaceMenu = signal(false);
  showDocsMenu = signal(false);
  showDashMenu = signal(false);

  toggleMobileMenu(): void {
    this.showMobileMenu.update(v => !v);
  }

  toggleUserMenu(): void {
    this.showUserMenu.update(v => !v);
  }

  toggleAnalyzeMenu(): void {
    this.showAnalyzeMenu.update(v => !v);
  }

  toggleClaimsMenu(): void {
    this.showClaimsMenu.update(v => !v);
  }

  toggleWorkspaceMenu(): void {
    this.showWorkspaceMenu.update(v => !v);
  }

  toggleDocsMenu(): void {
    this.showDocsMenu.update(v => !v);
  }

  toggleDashMenu(): void {
    this.showDashMenu.update(v => !v);
  }

  getUserInitial(): string {
    const email = this.authService.user()?.email;
    return email ? email.charAt(0).toUpperCase() : 'U';
  }

  getThemeLabel(): string {
    const labels: Record<ThemeMode, string> = {
      'dark': 'Dark mode (click for semi-dark)',
      'semi-dark': 'Semi-dark mode (click for light)',
      'light': 'Light mode (click for dark)'
    };
    return labels[this.themeService.currentTheme()];
  }

  isAnalyzeRoute(): boolean {
    return this.router.url.startsWith('/sentiment') || this.router.url.startsWith('/insurance');
  }

  isClaimsRoute(): boolean {
    return this.router.url.startsWith('/claims');
  }

  isWorkspaceRoute(): boolean {
    return this.router.url.startsWith('/documents') || this.router.url.startsWith('/cx');
  }

  isDocsRoute(): boolean {
    return this.router.url.startsWith('/documents');
  }

  isDashRoute(): boolean {
    return this.router.url.startsWith('/dashboard');
  }

  async logout(): Promise<void> {
    this.showUserMenu.set(false);
    await this.authService.signOut();
    this.router.navigate(['/login']);
  }
}
