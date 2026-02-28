import {
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  ViewChild,
  computed,
  effect,
  inject,
  signal
} from '@angular/core';
import { Router } from '@angular/router';
import { CommandRegistryService, PaletteCommand } from '../../services/command-registry.service';

/**
 * Command palette overlay that enables keyboard-driven quick navigation.
 * Opens with Ctrl+K (Windows) or Cmd+K (Mac), supports fuzzy search,
 * arrow key navigation, and Enter to select.
 */
@Component({
  selector: 'app-command-palette',
  standalone: true,
  template: `
    @if (isOpen()) {
      <!-- Backdrop -->
      <div
        class="fixed inset-0 z-[60] transition-opacity duration-200"
        [class.opacity-60]="isAnimatingIn()"
        [class.opacity-0]="!isAnimatingIn()"
        style="background: rgba(0, 0, 0, 0.7); backdrop-filter: blur(4px);"
        (click)="close()"
        data-testid="command-palette-backdrop"
        aria-hidden="true"
      ></div>

      <!-- Dialog -->
      <div
        class="fixed inset-0 z-[61] flex items-start justify-center pt-[15vh] px-4"
        role="dialog"
        aria-modal="true"
        aria-label="Command palette"
        (click)="onDialogContainerClick($event)"
      >
        <div
          #paletteDialog
          class="w-full max-w-lg rounded-2xl shadow-2xl border overflow-hidden transition-all duration-200"
          [class.scale-100]="isAnimatingIn()"
          [class.opacity-100]="isAnimatingIn()"
          [class.scale-95]="!isAnimatingIn()"
          [class.opacity-0]="!isAnimatingIn()"
          [style.background]="'var(--bg-secondary)'"
          [style.border-color]="'var(--border-primary)'"
          style="transform-origin: top center;"
          data-testid="command-palette-dialog"
        >
          <!-- Search Input -->
          <div class="relative border-b" [style.border-color]="'var(--border-primary)'">
            <svg
              class="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5"
              [style.color]="'var(--text-muted)'"
              fill="none" stroke="currentColor" viewBox="0 0 24 24"
              aria-hidden="true"
            >
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
            </svg>
            <input
              #searchInput
              type="text"
              placeholder="Search or jump to..."
              class="w-full pl-12 pr-4 py-4 text-base bg-transparent outline-none"
              [style.color]="'var(--text-primary)'"
              [value]="registry.searchQuery()"
              (input)="onSearchInput($event)"
              (keydown)="onSearchKeydown($event)"
              autocomplete="off"
              spellcheck="false"
              role="combobox"
              aria-expanded="true"
              aria-controls="command-palette-results"
              aria-activedescendant="{{ activeDescendantId() }}"
              aria-autocomplete="list"
              data-testid="command-palette-search"
            />
            <kbd
              class="absolute right-4 top-1/2 -translate-y-1/2 px-2 py-0.5 text-xs rounded-md border"
              [style.color]="'var(--text-muted)'"
              [style.border-color]="'var(--border-primary)'"
              [style.background]="'var(--bg-surface)'"
              aria-hidden="true"
            >ESC</kbd>
          </div>

          <!-- Results Count (screen reader) -->
          <div class="sr-only" aria-live="polite" data-testid="command-palette-live">
            {{ resultCountText() }}
          </div>

          <!-- Results List -->
          <div
            id="command-palette-results"
            class="max-h-80 overflow-y-auto custom-scrollbar"
            role="listbox"
            aria-label="Command results"
            data-testid="command-palette-results"
          >
            @if (flatResults().length === 0) {
              <div class="px-4 py-8 text-center" [style.color]="'var(--text-muted)'">
                <svg class="w-8 h-8 mx-auto mb-2 opacity-40" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M9.172 16.172a4 4 0 015.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
                </svg>
                <p class="text-sm">No commands found</p>
                <p class="text-xs mt-1 opacity-60">Try a different search term</p>
              </div>
            } @else {
              @for (category of categoryNames(); track category) {
                <!-- Category Header -->
                <div
                  class="px-4 pt-3 pb-1 text-[10px] font-bold uppercase tracking-wider"
                  [style.color]="'var(--text-muted)'"
                  aria-hidden="true"
                >{{ category }}</div>

                @for (command of groupedResults()[category]; track command.id) {
                  <div
                    class="mx-2 px-3 py-2.5 rounded-xl flex items-center gap-3 cursor-pointer transition-all duration-150 group"
                    [class.bg-indigo-500]="selectedIndex() === getFlatIndex(command)"
                    [class.bg-opacity-10]="selectedIndex() === getFlatIndex(command)"
                    [class.border-l-2]="selectedIndex() === getFlatIndex(command)"
                    [class.border-indigo-400]="selectedIndex() === getFlatIndex(command)"
                    [class.border-l-2]="selectedIndex() !== getFlatIndex(command)"
                    [class.border-transparent]="selectedIndex() !== getFlatIndex(command)"
                    [style.background]="selectedIndex() === getFlatIndex(command) ? 'rgba(99, 102, 241, 0.1)' : ''"
                    (click)="executeCommand(command)"
                    (mouseenter)="selectedIndex.set(getFlatIndex(command))"
                    role="option"
                    [attr.id]="'cmd-' + command.id"
                    [attr.aria-selected]="selectedIndex() === getFlatIndex(command)"
                    [attr.data-testid]="'command-' + command.id"
                  >
                    <!-- Icon -->
                    <div
                      class="w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0"
                      [style.background]="'var(--bg-surface)'"
                    >
                      <svg
                        class="w-4 h-4 text-indigo-400"
                        fill="none" stroke="currentColor" viewBox="0 0 24 24"
                        aria-hidden="true"
                      >
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                              [attr.d]="command.icon"/>
                      </svg>
                    </div>

                    <!-- Label + Description -->
                    <div class="flex-1 min-w-0">
                      <div class="text-sm font-medium" [style.color]="'var(--text-primary)'">
                        {{ command.label }}
                      </div>
                      <div class="text-xs truncate" [style.color]="'var(--text-muted)'">
                        {{ command.description }}
                      </div>
                    </div>

                    <!-- Route Hint -->
                    <span
                      class="text-[10px] px-2 py-0.5 rounded-md flex-shrink-0 hidden sm:block"
                      [style.color]="'var(--text-muted)'"
                      [style.background]="'var(--bg-surface)'"
                    >{{ command.route }}</span>

                    <!-- Arrow indicator on selected -->
                    @if (selectedIndex() === getFlatIndex(command)) {
                      <svg
                        class="w-4 h-4 flex-shrink-0 text-indigo-400"
                        fill="none" stroke="currentColor" viewBox="0 0 24 24"
                        aria-hidden="true"
                      >
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
                      </svg>
                    }
                  </div>
                }
              }
            }
          </div>

          <!-- Footer -->
          <div
            class="px-4 py-2.5 border-t flex items-center gap-4 text-[11px]"
            [style.border-color]="'var(--border-primary)'"
            [style.color]="'var(--text-muted)'"
            aria-hidden="true"
          >
            <span class="flex items-center gap-1">
              <kbd class="px-1.5 py-0.5 rounded border text-[10px]"
                   [style.border-color]="'var(--border-primary)'"
                   [style.background]="'var(--bg-surface)'"
              >&uarr;&darr;</kbd>
              navigate
            </span>
            <span class="flex items-center gap-1">
              <kbd class="px-1.5 py-0.5 rounded border text-[10px]"
                   [style.border-color]="'var(--border-primary)'"
                   [style.background]="'var(--bg-surface)'"
              >&crarr;</kbd>
              select
            </span>
            <span class="flex items-center gap-1">
              <kbd class="px-1.5 py-0.5 rounded border text-[10px]"
                   [style.border-color]="'var(--border-primary)'"
                   [style.background]="'var(--bg-surface)'"
              >esc</kbd>
              close
            </span>
          </div>
        </div>
      </div>
    }
  `
})
export class CommandPaletteComponent implements OnDestroy {
  readonly registry = inject(CommandRegistryService);
  private readonly router = inject(Router);

  /** Whether the palette is currently open. */
  readonly isOpen = signal(false);

  /** Whether the animation is showing (in vs out). */
  readonly isAnimatingIn = signal(false);

  /** Currently highlighted result index in the flat list. */
  readonly selectedIndex = signal(0);

  /** Timer handle for close animation delay. */
  private closeTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    // React to external open requests (e.g., from nav button)
    effect(() => {
      if (this.registry.openRequested()) {
        this.registry.acknowledgeOpen();
        this.open();
      }
    });
  }

  @ViewChild('searchInput') searchInput!: ElementRef<HTMLInputElement>;
  @ViewChild('paletteDialog') paletteDialog!: ElementRef<HTMLDivElement>;

  /** Flat array of all filtered results for keyboard navigation. */
  readonly flatResults = computed(() => this.registry.filteredCommands());

  /** Grouped results by category for display. */
  readonly groupedResults = computed(() => this.registry.groupedCommands());

  /** Category names from grouped results. */
  readonly categoryNames = computed(() => Object.keys(this.groupedResults()));

  /** Accessible result count text for screen readers. */
  readonly resultCountText = computed(() => {
    const count = this.flatResults().length;
    return count === 0 ? 'No results found' : `${count} result${count === 1 ? '' : 's'} available`;
  });

  /** ID of the currently active descendant for aria-activedescendant. */
  readonly activeDescendantId = computed(() => {
    const results = this.flatResults();
    const idx = this.selectedIndex();
    if (idx >= 0 && idx < results.length) {
      return 'cmd-' + results[idx].id;
    }
    return '';
  });

  /** Gets the flat index of a command for comparison with selectedIndex. */
  getFlatIndex(command: PaletteCommand): number {
    return this.flatResults().indexOf(command);
  }

  /**
   * Global keyboard listener for opening the palette with Ctrl+K or Cmd+K.
   */
  @HostListener('document:keydown', ['$event'])
  onGlobalKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key === 'k') {
      event.preventDefault();
      event.stopPropagation();
      if (this.isOpen()) {
        this.close();
      } else {
        this.open();
      }
    }

    if (event.key === 'Escape' && this.isOpen()) {
      event.preventDefault();
      this.close();
    }
  }

  /** Opens the command palette. */
  open(): void {
    if (this.closeTimer) {
      clearTimeout(this.closeTimer);
      this.closeTimer = null;
    }
    this.registry.clearSearch();
    this.selectedIndex.set(0);
    this.isOpen.set(true);

    // Trigger animation in on next frame
    requestAnimationFrame(() => {
      this.isAnimatingIn.set(true);
      // Focus the search input after render
      setTimeout(() => {
        this.searchInput?.nativeElement?.focus();
      }, 50);
    });
  }

  /** Closes the command palette with animation. */
  close(): void {
    this.isAnimatingIn.set(false);
    this.closeTimer = setTimeout(() => {
      this.isOpen.set(false);
      this.registry.clearSearch();
      this.selectedIndex.set(0);
      this.closeTimer = null;
    }, 150);
  }

  /** Handles search input changes. */
  onSearchInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.registry.setSearchQuery(target.value);
    this.selectedIndex.set(0);
  }

  /** Handles keyboard navigation within the search input. */
  onSearchKeydown(event: KeyboardEvent): void {
    const results = this.flatResults();
    const maxIndex = results.length - 1;

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.selectedIndex.update(idx => (idx >= maxIndex ? 0 : idx + 1));
        this.scrollSelectedIntoView();
        break;

      case 'ArrowUp':
        event.preventDefault();
        this.selectedIndex.update(idx => (idx <= 0 ? maxIndex : idx - 1));
        this.scrollSelectedIntoView();
        break;

      case 'Enter':
        event.preventDefault();
        if (results.length > 0) {
          const selected = results[this.selectedIndex()];
          if (selected) {
            this.executeCommand(selected);
          }
        }
        break;

      case 'Tab':
        // Trap focus within palette
        event.preventDefault();
        break;
    }
  }

  /** Executes a command (navigates to its route) and closes the palette. */
  executeCommand(command: PaletteCommand): void {
    this.close();
    // Navigate after close animation starts
    setTimeout(() => {
      this.router.navigateByUrl(command.route);
    }, 50);
  }

  /** Handles clicks on the dialog container to close when clicking outside the dialog. */
  onDialogContainerClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (this.paletteDialog && !this.paletteDialog.nativeElement.contains(target)) {
      this.close();
    }
  }

  /** Scrolls the currently selected item into view. */
  private scrollSelectedIntoView(): void {
    requestAnimationFrame(() => {
      const results = this.flatResults();
      const idx = this.selectedIndex();
      if (idx >= 0 && idx < results.length) {
        const element = document.getElementById('cmd-' + results[idx].id);
        element?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
      }
    });
  }

  ngOnDestroy(): void {
    if (this.closeTimer) {
      clearTimeout(this.closeTimer);
    }
  }
}
