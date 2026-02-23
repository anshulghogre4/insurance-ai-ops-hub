import { Injectable, signal, effect } from '@angular/core';

export type ThemeMode = 'dark' | 'semi-dark' | 'light';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly STORAGE_KEY = 'insuresense-theme';

  currentTheme = signal<ThemeMode>(this.loadTheme());

  constructor() {
    effect(() => {
      const theme = this.currentTheme();
      this.applyTheme(theme);
      localStorage.setItem(this.STORAGE_KEY, theme);
    });
  }

  setTheme(theme: ThemeMode): void {
    this.currentTheme.set(theme);
  }

  cycleTheme(): void {
    const order: ThemeMode[] = ['dark', 'semi-dark', 'light'];
    const idx = order.indexOf(this.currentTheme());
    this.currentTheme.set(order[(idx + 1) % order.length]);
  }

  private loadTheme(): ThemeMode {
    const stored = localStorage.getItem(this.STORAGE_KEY) as ThemeMode | null;
    if (stored && ['dark', 'semi-dark', 'light'].includes(stored)) {
      return stored;
    }
    return 'dark';
  }

  private applyTheme(theme: ThemeMode): void {
    const root = document.documentElement;
    root.classList.remove('theme-dark', 'theme-semi-dark', 'theme-light');
    root.classList.add(`theme-${theme}`);
  }
}
