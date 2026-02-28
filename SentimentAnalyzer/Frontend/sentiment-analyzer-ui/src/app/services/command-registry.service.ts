import { Injectable, computed, signal } from '@angular/core';

/** Represents a single command in the command palette. */
export interface PaletteCommand {
  /** Unique identifier for the command. */
  id: string;
  /** Display label shown in the palette. */
  label: string;
  /** Short description of what the command does. */
  description: string;
  /** Angular route to navigate to. */
  route: string;
  /** SVG icon path(s) for the command. */
  icon: string;
  /** Category grouping for display. */
  category: 'Navigate' | 'Action';
}

/** Default commands registered in the palette, covering all navigable routes. */
const DEFAULT_COMMANDS: PaletteCommand[] = [
  {
    id: 'nav-dashboard',
    label: 'Dashboard Overview',
    description: 'Analytics dashboard with sentiment distribution and key metrics',
    route: '/dashboard',
    icon: 'M16 8v8m-4-5v5m-4-2v2m-2 4h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z',
    category: 'Navigate'
  },
  {
    id: 'nav-claims-triage',
    label: 'Claims \u2014 New Triage',
    description: 'Submit a new insurance claim for AI-powered triage and severity assessment',
    route: '/claims/triage',
    icon: 'M12 4v16m8-8H4',
    category: 'Navigate'
  },
  {
    id: 'nav-claims-history',
    label: 'Claims \u2014 History',
    description: 'Browse and search all previously triaged insurance claims',
    route: '/claims/history',
    icon: 'M4 6h16M4 10h16M4 14h16M4 18h16',
    category: 'Navigate'
  },
  {
    id: 'nav-fraud-alerts',
    label: 'Fraud Alerts',
    description: 'View high-risk fraud alerts and SIU referral recommendations',
    route: '/dashboard/fraud',
    icon: 'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z',
    category: 'Navigate'
  },
  {
    id: 'nav-provider-health',
    label: 'Provider Health',
    description: 'Monitor AI provider status, fallback chain, and service availability',
    route: '/dashboard/providers',
    icon: 'M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01',
    category: 'Navigate'
  },
  {
    id: 'nav-doc-upload',
    label: 'Document Upload',
    description: 'Upload insurance documents for RAG-powered intelligence extraction',
    route: '/documents/upload',
    icon: 'M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12',
    category: 'Navigate'
  },
  {
    id: 'nav-doc-query',
    label: 'Document Query',
    description: 'Ask natural language questions about uploaded insurance documents',
    route: '/documents/query',
    icon: 'M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z',
    category: 'Navigate'
  },
  {
    id: 'nav-cx-copilot',
    label: 'CX Copilot',
    description: 'AI-powered customer experience assistant with streaming responses',
    route: '/cx/copilot',
    icon: 'M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z',
    category: 'Navigate'
  },
  {
    id: 'nav-sentiment',
    label: 'Sentiment Analysis',
    description: 'Analyze text sentiment using the v1 legacy AI engine',
    route: '/sentiment',
    icon: 'M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z',
    category: 'Navigate'
  },
  {
    id: 'nav-insurance',
    label: 'Insurance Analysis',
    description: 'Multi-agent insurance sentiment analysis with persona detection',
    route: '/insurance',
    icon: 'M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z',
    category: 'Navigate'
  }
];

/**
 * Service that manages the command palette registry.
 * Provides signal-based search and filtering over all registered commands.
 */
@Injectable({ providedIn: 'root' })
export class CommandRegistryService {
  /** Internal signal holding all registered commands. */
  private readonly _commands = signal<PaletteCommand[]>([...DEFAULT_COMMANDS]);

  /** Current search query entered by the user. */
  private readonly _searchQuery = signal<string>('');

  /** Read-only signal exposing all registered commands. */
  readonly commands = this._commands.asReadonly();

  /** Read-only signal exposing the current search query. */
  readonly searchQuery = this._searchQuery.asReadonly();

  /** Computed signal that filters commands based on the current search query. */
  readonly filteredCommands = computed(() => {
    const query = this._searchQuery();
    if (!query.trim()) {
      return this._commands();
    }
    return this.search(query);
  });

  /** Computed signal that groups filtered commands by category. */
  readonly groupedCommands = computed(() => {
    const filtered = this.filteredCommands();
    const groups: Record<string, PaletteCommand[]> = {};
    for (const cmd of filtered) {
      if (!groups[cmd.category]) {
        groups[cmd.category] = [];
      }
      groups[cmd.category].push(cmd);
    }
    return groups;
  });

  /**
   * Updates the search query signal, triggering re-filtering.
   * @param query The search string to filter commands by.
   */
  setSearchQuery(query: string): void {
    this._searchQuery.set(query);
  }

  /**
   * Filters commands by matching query against label and description (case-insensitive).
   * @param query The search string to filter commands by.
   * @returns Array of matching commands.
   */
  search(query: string): PaletteCommand[] {
    const lowerQuery = query.toLowerCase().trim();
    if (!lowerQuery) {
      return this._commands();
    }
    return this._commands().filter(cmd =>
      cmd.label.toLowerCase().includes(lowerQuery) ||
      cmd.description.toLowerCase().includes(lowerQuery)
    );
  }

  /**
   * Registers a new command in the palette.
   * @param command The command to add.
   */
  register(command: PaletteCommand): void {
    this._commands.update(cmds => [...cmds, command]);
  }

  /** Resets the search query to empty string. */
  clearSearch(): void {
    this._searchQuery.set('');
  }

  /** Signal to request the palette to open (used by external triggers like nav buttons). */
  private readonly _openRequested = signal(false);

  /** Read-only signal indicating an external open request. */
  readonly openRequested = this._openRequested.asReadonly();

  /** Request the palette to open from an external component. */
  requestOpen(): void {
    this._openRequested.set(true);
  }

  /** Acknowledge the open request (called by the palette after opening). */
  acknowledgeOpen(): void {
    this._openRequested.set(false);
  }
}
