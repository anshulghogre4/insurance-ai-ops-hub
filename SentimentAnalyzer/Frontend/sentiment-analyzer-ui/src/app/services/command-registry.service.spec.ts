import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { CommandRegistryService, PaletteCommand } from './command-registry.service';

describe('CommandRegistryService', () => {
  let service: CommandRegistryService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(CommandRegistryService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should have all 10 default commands registered', () => {
    const commands = service.commands();
    expect(commands.length).toBe(10);
  });

  it('should include Dashboard Overview route', () => {
    const commands = service.commands();
    const dashboard = commands.find(c => c.id === 'nav-dashboard');
    expect(dashboard).toBeTruthy();
    expect(dashboard!.label).toBe('Dashboard Overview');
    expect(dashboard!.route).toBe('/dashboard');
    expect(dashboard!.category).toBe('Navigate');
  });

  it('should include Claims - New Triage route', () => {
    const commands = service.commands();
    const triage = commands.find(c => c.id === 'nav-claims-triage');
    expect(triage).toBeTruthy();
    expect(triage!.route).toBe('/claims/triage');
  });

  it('should include Claims - History route', () => {
    const commands = service.commands();
    const history = commands.find(c => c.id === 'nav-claims-history');
    expect(history).toBeTruthy();
    expect(history!.route).toBe('/claims/history');
  });

  it('should include Fraud Alerts route', () => {
    const commands = service.commands();
    const fraud = commands.find(c => c.id === 'nav-fraud-alerts');
    expect(fraud).toBeTruthy();
    expect(fraud!.route).toBe('/dashboard/fraud');
  });

  it('should include Provider Health route', () => {
    const commands = service.commands();
    const provider = commands.find(c => c.id === 'nav-provider-health');
    expect(provider).toBeTruthy();
    expect(provider!.route).toBe('/dashboard/providers');
  });

  it('should include Document Upload route', () => {
    const commands = service.commands();
    const upload = commands.find(c => c.id === 'nav-doc-upload');
    expect(upload).toBeTruthy();
    expect(upload!.route).toBe('/documents/upload');
  });

  it('should include Document Query route', () => {
    const commands = service.commands();
    const query = commands.find(c => c.id === 'nav-doc-query');
    expect(query).toBeTruthy();
    expect(query!.route).toBe('/documents/query');
  });

  it('should include CX Copilot route', () => {
    const commands = service.commands();
    const cx = commands.find(c => c.id === 'nav-cx-copilot');
    expect(cx).toBeTruthy();
    expect(cx!.route).toBe('/cx/copilot');
  });

  it('should include Sentiment Analysis route', () => {
    const commands = service.commands();
    const sentiment = commands.find(c => c.id === 'nav-sentiment');
    expect(sentiment).toBeTruthy();
    expect(sentiment!.route).toBe('/sentiment');
  });

  it('should include Insurance Analysis route', () => {
    const commands = service.commands();
    const insurance = commands.find(c => c.id === 'nav-insurance');
    expect(insurance).toBeTruthy();
    expect(insurance!.route).toBe('/insurance');
  });

  it('should return all commands when search query is empty', () => {
    const results = service.search('');
    expect(results.length).toBe(10);
  });

  it('should return all commands when search query is whitespace', () => {
    const results = service.search('   ');
    expect(results.length).toBe(10);
  });

  it('should filter commands by label match', () => {
    const results = service.search('Dashboard');
    expect(results.length).toBeGreaterThanOrEqual(1);
    expect(results.some(r => r.label === 'Dashboard Overview')).toBe(true);
  });

  it('should filter commands by description match', () => {
    const results = service.search('triage');
    expect(results.length).toBeGreaterThanOrEqual(1);
    expect(results.some(r => r.id === 'nav-claims-triage')).toBe(true);
  });

  it('should perform case-insensitive search on labels', () => {
    const resultsLower = service.search('dashboard');
    const resultsUpper = service.search('DASHBOARD');
    const resultsMixed = service.search('DaShBoArD');

    expect(resultsLower.length).toBe(resultsUpper.length);
    expect(resultsLower.length).toBe(resultsMixed.length);
    expect(resultsLower.length).toBeGreaterThanOrEqual(1);
  });

  it('should perform case-insensitive search on descriptions', () => {
    const resultsLower = service.search('streaming');
    const resultsUpper = service.search('STREAMING');

    expect(resultsLower.length).toBe(resultsUpper.length);
    expect(resultsLower.length).toBeGreaterThanOrEqual(1);
  });

  it('should return empty array when no commands match', () => {
    const results = service.search('xyznonexistentcommand');
    expect(results.length).toBe(0);
  });

  it('should match partial strings in labels', () => {
    const results = service.search('Cop');
    expect(results.some(r => r.label.includes('Copilot'))).toBe(true);
  });

  it('should match partial strings in descriptions', () => {
    const results = service.search('RAG');
    expect(results.some(r => r.id === 'nav-doc-upload')).toBe(true);
  });

  it('should update filteredCommands when search query changes', () => {
    expect(service.filteredCommands().length).toBe(10);

    service.setSearchQuery('fraud');
    expect(service.filteredCommands().length).toBeGreaterThanOrEqual(1);
    expect(service.filteredCommands().some(c => c.id === 'nav-fraud-alerts')).toBe(true);
  });

  it('should reset filteredCommands when search is cleared', () => {
    service.setSearchQuery('fraud');
    expect(service.filteredCommands().length).toBeLessThan(10);

    service.clearSearch();
    expect(service.filteredCommands().length).toBe(10);
  });

  it('should group commands by category', () => {
    const grouped = service.groupedCommands();
    expect(grouped['Navigate']).toBeTruthy();
    expect(grouped['Navigate'].length).toBe(10);
  });

  it('should register a new command', () => {
    const newCommand: PaletteCommand = {
      id: 'action-test',
      label: 'Run Claims Triage sample',
      description: 'Execute a sample claims triage workflow',
      route: '/claims/triage',
      icon: 'M13 10V3L4 14h7v7l9-11h-7z',
      category: 'Action'
    };

    service.register(newCommand);
    expect(service.commands().length).toBe(11);
    expect(service.commands().some(c => c.id === 'action-test')).toBe(true);
  });

  it('should include registered commands in search results', () => {
    const newCommand: PaletteCommand = {
      id: 'action-sample',
      label: 'Run Fraud Detection sample',
      description: 'Execute a sample fraud detection workflow',
      route: '/dashboard/fraud',
      icon: 'M13 10V3L4 14h7v7l9-11h-7z',
      category: 'Action'
    };

    service.register(newCommand);
    const results = service.search('fraud detection workflow');
    expect(results.some(c => c.id === 'action-sample')).toBe(true);
  });

  it('should have all commands with required fields populated', () => {
    for (const cmd of service.commands()) {
      expect(cmd.id).toBeTruthy();
      expect(cmd.label).toBeTruthy();
      expect(cmd.description).toBeTruthy();
      expect(cmd.route).toBeTruthy();
      expect(cmd.icon).toBeTruthy();
      expect(['Navigate', 'Action']).toContain(cmd.category);
    }
  });

  it('should handle openRequested signal', () => {
    expect(service.openRequested()).toBe(false);

    service.requestOpen();
    expect(service.openRequested()).toBe(true);

    service.acknowledgeOpen();
    expect(service.openRequested()).toBe(false);
  });
});
