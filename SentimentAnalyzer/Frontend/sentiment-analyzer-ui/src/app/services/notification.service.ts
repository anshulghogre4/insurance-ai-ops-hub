import { Injectable, signal, computed } from '@angular/core';
import { NotificationEvent, NotificationType } from '../models/notification';

const STORAGE_KEY = 'insurance-hub-notifications';
const MAX_NOTIFICATIONS = 50;

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private _notifications = signal<NotificationEvent[]>(this.loadFromStorage());
  readonly notifications = this._notifications.asReadonly();
  readonly unreadCount = computed(() => this._notifications().filter(n => !n.isRead).length);

  private idCounter = 0;

  addNotification(type: NotificationType, title: string, message: string, severity: 'info' | 'warning' | 'critical', routerLink?: string): void {
    const notification: NotificationEvent = {
      id: `notif-${Date.now()}-${++this.idCounter}`,
      type,
      title,
      message,
      severity,
      createdAt: new Date().toISOString(),
      isRead: false,
      routerLink
    };

    this._notifications.update(list => {
      const updated = [notification, ...list];
      return updated.slice(0, MAX_NOTIFICATIONS);
    });

    this.saveToStorage();
  }

  markAsRead(id: string): void {
    this._notifications.update(list =>
      list.map(n => n.id === id ? { ...n, isRead: true } : n)
    );
    this.saveToStorage();
  }

  markAllAsRead(): void {
    this._notifications.update(list =>
      list.map(n => ({ ...n, isRead: true }))
    );
    this.saveToStorage();
  }

  clearAll(): void {
    this._notifications.set([]);
    this.saveToStorage();
  }

  private loadFromStorage(): NotificationEvent[] {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      return stored ? JSON.parse(stored) : [];
    } catch {
      return [];
    }
  }

  private saveToStorage(): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(this._notifications()));
    } catch {
      // localStorage may be unavailable in SSR
    }
  }
}
