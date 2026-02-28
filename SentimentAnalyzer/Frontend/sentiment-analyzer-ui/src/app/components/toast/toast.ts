import { Component, inject } from '@angular/core';
import { ToastService, Toast } from '../../services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  template: `
    <div class="fixed bottom-6 right-6 z-50 flex flex-col gap-3 max-w-sm w-full pointer-events-none"
         aria-live="polite" aria-atomic="false">
      @for (toast of toastService.toasts(); track toast.id) {
        <div
          class="glass-card-static p-4 flex items-start gap-3 animate-slide-in-right pointer-events-auto shadow-lg relative overflow-hidden"
          [class.border-l-4]="true"
          [class.border-emerald-500]="toast.type === 'success'"
          [class.border-rose-500]="toast.type === 'error'"
          [class.border-amber-500]="toast.type === 'warning'"
          [class.border-cyan-500]="toast.type === 'info'"
          role="alert"
          [attr.data-toast-type]="toast.type"
          [attr.data-toast-id]="toast.id"
        >
          <!-- Icon -->
          @switch (toast.type) {
            @case ('success') {
              <svg class="w-5 h-5 text-emerald-400 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
              </svg>
            }
            @case ('error') {
              <svg class="w-5 h-5 text-rose-400 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
              </svg>
            }
            @case ('warning') {
              <svg class="w-5 h-5 text-amber-400 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"/>
              </svg>
            }
            @case ('info') {
              <svg class="w-5 h-5 text-cyan-400 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
              </svg>
            }
          }

          <!-- Message -->
          <p class="text-sm flex-1" [style.color]="'var(--text-primary)'">{{ toast.message }}</p>

          <!-- Dismiss Button -->
          <button
            (click)="dismiss(toast.id)"
            class="p-1 rounded-lg transition-colors flex-shrink-0"
            [style.color]="'var(--text-muted)'"
            aria-label="Dismiss notification"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>

          <!-- Progress bar countdown -->
          <div class="toast-progress-bar"
               [class.toast-progress-success]="toast.type === 'success'"
               [class.toast-progress-error]="toast.type === 'error'"
               [class.toast-progress-warning]="toast.type === 'warning'"
               [class.toast-progress-info]="toast.type === 'info'"
               [style.animation-duration.ms]="toast.timeout"
               data-testid="toast-progress"
               aria-hidden="true">
          </div>
        </div>
      }
    </div>
  `
})
export class ToastComponent {
  toastService = inject(ToastService);

  dismiss(id: number): void {
    this.toastService.dismiss(id);
  }
}
