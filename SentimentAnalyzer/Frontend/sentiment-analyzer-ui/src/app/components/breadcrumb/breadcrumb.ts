import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BreadcrumbService } from '../../services/breadcrumb.service';

/**
 * Breadcrumb navigation component.
 * Renders an accessible breadcrumb trail between the nav and main content.
 * Hidden when depth <= 1 (e.g., on /dashboard alone or the landing page).
 */
@Component({
  selector: 'app-breadcrumb',
  standalone: true,
  imports: [RouterLink],
  template: `
    @if (breadcrumbService.isVisible()) {
      <nav aria-label="Breadcrumb"
           class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-9 flex items-center">
        <ol class="flex items-center gap-1 text-xs">
          @for (crumb of breadcrumbService.breadcrumbs(); track i; let i = $index) {
            <li class="flex items-center gap-1 animate-fade-in"
                [style.animation-delay]="(i * 50) + 'ms'"
                [style.opacity]="0">
              @if (i > 0) {
                <span class="select-none" style="color: var(--text-muted)" aria-hidden="true">/</span>
              }
              @if (crumb.isCurrentPage) {
                <span aria-current="page"
                      class="font-medium"
                      style="color: var(--text-primary)">
                  {{ crumb.label }}
                </span>
              } @else {
                <a [routerLink]="crumb.url"
                   class="transition-colors duration-150 hover:underline"
                   style="color: var(--text-secondary)">
                  {{ crumb.label }}
                </a>
              }
            </li>
          }
        </ol>
      </nav>
    }
  `
})
export class BreadcrumbComponent {
  breadcrumbService = inject(BreadcrumbService);
}
