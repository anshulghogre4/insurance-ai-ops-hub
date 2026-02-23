import { Component, inject, signal } from '@angular/core';
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
              <a routerLink="/" routerLinkActive="nav-link-active" [routerLinkActiveOptions]="{exact: true}"
                 class="nav-link flex items-center gap-2">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z"/>
                </svg>
                Sentiment v1
              </a>
              <a routerLink="/insurance" routerLinkActive="nav-link-active"
                 class="nav-link flex items-center gap-2">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
                </svg>
                Insurance Analysis
              </a>
              <a routerLink="/dashboard" routerLinkActive="nav-link-active"
                 class="nav-link flex items-center gap-2">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 8v8m-4-5v5m-4-2v2m-2 4h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/>
                </svg>
                Dashboard
              </a>
            }
          </div>

          <!-- Right side: Theme toggle + Auth -->
          <div class="flex items-center gap-2">
            <!-- Theme Toggle -->
            <button (click)="themeService.cycleTheme()" [title]="getThemeLabel()"
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
                            [style.background]="'var(--bg-surface)'" [style.color]="'var(--text-secondary)'">
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
              <a routerLink="/" routerLinkActive="nav-link-active" [routerLinkActiveOptions]="{exact: true}"
                 (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                Sentiment v1
              </a>
              <a routerLink="/insurance" routerLinkActive="nav-link-active"
                 (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                Insurance Analysis
              </a>
              <a routerLink="/dashboard" routerLinkActive="nav-link-active"
                 (click)="showMobileMenu.set(false)" class="nav-link w-full py-3 flex items-center gap-3">
                Dashboard
              </a>
            }
            @if (authService.authEnabled()) {
              <div class="pt-2 mt-2 border-t" [style.border-color]="'var(--border-secondary)'">
                @if (authService.isAuthenticated()) {
                  <button (click)="logout(); showMobileMenu.set(false)"
                          class="nav-link w-full py-3 text-rose-400 flex items-center gap-3">
                    Sign Out
                  </button>
                } @else {
                  <a routerLink="/login" (click)="showMobileMenu.set(false)"
                     class="nav-link w-full py-3 text-indigo-400 flex items-center gap-3">
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

  showMobileMenu = signal(false);
  showUserMenu = signal(false);

  toggleMobileMenu(): void {
    this.showMobileMenu.update(v => !v);
  }

  toggleUserMenu(): void {
    this.showUserMenu.update(v => !v);
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

  async logout(): Promise<void> {
    this.showUserMenu.set(false);
    await this.authService.signOut();
    this.router.navigate(['/login']);
  }
}
