import { Injectable, signal } from '@angular/core';

/** Represents a single toast notification displayed to the user. */
export interface Toast {
  id: number;
  type: 'success' | 'error' | 'warning' | 'info';
  message: string;
  timeout: number;
}

/** Signal-based toast notification service. Manages a stack of toast messages. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  /** Reactive list of active toasts. */
  toasts = signal<Toast[]>([]);
  private nextId = 0;

  /** Show a success toast (auto-dismisses in 5s). */
  success(message: string): void {
    this.add('success', message, 5000);
  }

  /** Show an error toast (auto-dismisses in 8s). */
  error(message: string): void {
    this.add('error', message, 8000);
  }

  /** Show a warning toast (auto-dismisses in 6s). */
  warning(message: string): void {
    this.add('warning', message, 6000);
  }

  /** Show an info toast (auto-dismisses in 5s). */
  info(message: string): void {
    this.add('info', message, 5000);
  }

  /** Dismiss a toast by its unique id. */
  dismiss(id: number): void {
    this.toasts.update(t => t.filter(toast => toast.id !== id));
  }

  private add(type: Toast['type'], message: string, timeout: number): void {
    const id = this.nextId++;
    this.toasts.update(t => {
      const updated = [...t, { id, type, message, timeout }];
      // Keep max 5 visible toasts — remove oldest if exceeding
      return updated.length > 5 ? updated.slice(updated.length - 5) : updated;
    });
    setTimeout(() => this.dismiss(id), timeout);
  }
}
