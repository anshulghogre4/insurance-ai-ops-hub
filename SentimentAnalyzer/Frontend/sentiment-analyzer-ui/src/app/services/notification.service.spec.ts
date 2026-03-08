import { TestBed } from '@angular/core/testing';
import { NotificationService } from './notification.service';
import { describe, it, expect, beforeEach, vi } from 'vitest';

describe('NotificationService', () => {
  let service: NotificationService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    service = TestBed.inject(NotificationService);
  });

  it('should be created with empty notifications', () => {
    expect(service).toBeTruthy();
    expect(service.notifications().length).toBe(0);
  });

  it('should add a notification and increment unread count', () => {
    service.addNotification('claim', 'Claim Triaged', 'Claim #1042 triaged as Critical severity', 'critical', '/claims/1042');
    expect(service.notifications().length).toBe(1);
    expect(service.unreadCount()).toBe(1);
  });

  it('should mark notification as read', () => {
    service.addNotification('fraud', 'Fraud Alert', 'High fraud score detected on claim #2087', 'warning');
    const id = service.notifications()[0].id;
    service.markAsRead(id);
    expect(service.unreadCount()).toBe(0);
    expect(service.notifications()[0].isRead).toBe(true);
  });

  it('should clear all notifications', () => {
    service.addNotification('provider', 'Provider Down', 'Groq entered cooldown for 180s', 'info');
    service.addNotification('claim', 'Claim Triaged', 'Claim #3015 triaged', 'info');
    service.clearAll();
    expect(service.notifications().length).toBe(0);
    expect(service.unreadCount()).toBe(0);
  });

  it('should cap notifications at 50', () => {
    for (let i = 0; i < 55; i++) {
      service.addNotification('claim', `Claim #${i}`, `Auto-liability claim ${i} triaged`, 'info');
    }
    expect(service.notifications().length).toBe(50);
  });

  it('should persist to localStorage', () => {
    service.addNotification('fraud', 'Fraud Alert', 'Staged accident indicators on claim #4201', 'critical');
    const stored = localStorage.getItem('insurance-hub-notifications');
    expect(stored).toBeTruthy();
    const parsed = JSON.parse(stored!);
    expect(parsed.length).toBe(1);
    expect(parsed[0].title).toBe('Fraud Alert');
  });
});
