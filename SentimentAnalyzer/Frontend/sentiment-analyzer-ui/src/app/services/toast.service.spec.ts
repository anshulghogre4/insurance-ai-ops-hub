import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  let service: ToastService;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({});
    service = TestBed.inject(ToastService);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should add a success toast with 5 second timeout', () => {
    service.success('Claim triaged successfully');

    const toasts = service.toasts();
    expect(toasts.length).toBe(1);
    expect(toasts[0].type).toBe('success');
    expect(toasts[0].message).toBe('Claim triaged successfully');
    expect(toasts[0].timeout).toBe(5000);
  });

  it('should add an error toast with 8 second timeout', () => {
    service.error('Failed to process water damage claim');

    const toasts = service.toasts();
    expect(toasts.length).toBe(1);
    expect(toasts[0].type).toBe('error');
    expect(toasts[0].message).toBe('Failed to process water damage claim');
    expect(toasts[0].timeout).toBe(8000);
  });

  it('should add a warning toast with 6 second timeout', () => {
    service.warning('Claim submitted but evidence upload pending');

    const toasts = service.toasts();
    expect(toasts.length).toBe(1);
    expect(toasts[0].type).toBe('warning');
    expect(toasts[0].message).toBe('Claim submitted but evidence upload pending');
    expect(toasts[0].timeout).toBe(6000);
  });

  it('should add an info toast with 5 second timeout', () => {
    service.info('Correlation dismissed by fraud analyst');

    const toasts = service.toasts();
    expect(toasts.length).toBe(1);
    expect(toasts[0].type).toBe('info');
    expect(toasts[0].message).toBe('Correlation dismissed by fraud analyst');
    expect(toasts[0].timeout).toBe(5000);
  });

  it('should dismiss a toast by id', () => {
    service.success('Policy document uploaded');
    service.error('Fraud analysis timed out');

    expect(service.toasts().length).toBe(2);

    const firstId = service.toasts()[0].id;
    service.dismiss(firstId);

    expect(service.toasts().length).toBe(1);
    expect(service.toasts()[0].message).toBe('Fraud analysis timed out');
  });

  it('should auto-dismiss success toast after 5 seconds', () => {
    service.success('Document indexed into vector store');

    expect(service.toasts().length).toBe(1);

    vi.advanceTimersByTime(4999);
    expect(service.toasts().length).toBe(1);

    vi.advanceTimersByTime(1);
    expect(service.toasts().length).toBe(0);
  });

  it('should auto-dismiss error toast after 8 seconds', () => {
    service.error('All AI providers unavailable');

    expect(service.toasts().length).toBe(1);

    vi.advanceTimersByTime(7999);
    expect(service.toasts().length).toBe(1);

    vi.advanceTimersByTime(1);
    expect(service.toasts().length).toBe(0);
  });

  it('should support multiple concurrent toasts', () => {
    service.success('Claim #101 triaged');
    service.warning('Adjuster assignment delayed');
    service.error('SIU referral failed');
    service.info('New fraud correlation detected');

    expect(service.toasts().length).toBe(4);
    expect(service.toasts()[0].type).toBe('success');
    expect(service.toasts()[1].type).toBe('warning');
    expect(service.toasts()[2].type).toBe('error');
    expect(service.toasts()[3].type).toBe('info');
  });

  it('should assign unique incremental ids to each toast', () => {
    service.success('First notification');
    service.error('Second notification');
    service.info('Third notification');

    const ids = service.toasts().map(t => t.id);
    expect(ids[0]).toBeLessThan(ids[1]);
    expect(ids[1]).toBeLessThan(ids[2]);
  });

  it('should cap visible toasts at 5 by removing oldest', () => {
    service.success('Claim #1 processed');
    service.success('Claim #2 processed');
    service.success('Claim #3 processed');
    service.success('Claim #4 processed');
    service.success('Claim #5 processed');
    service.success('Claim #6 processed');

    expect(service.toasts().length).toBe(5);
    expect(service.toasts()[0].message).toBe('Claim #2 processed');
    expect(service.toasts()[4].message).toBe('Claim #6 processed');
  });

  it('should handle dismissing a non-existent id gracefully', () => {
    service.success('Active notification');
    service.dismiss(9999);

    expect(service.toasts().length).toBe(1);
  });
});
