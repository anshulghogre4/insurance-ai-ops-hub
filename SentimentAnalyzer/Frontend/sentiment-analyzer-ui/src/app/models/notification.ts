export type NotificationType = 'claim' | 'fraud' | 'provider' | 'system';

export interface NotificationEvent {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  severity: 'info' | 'warning' | 'critical';
  createdAt: string;
  isRead: boolean;
  routerLink?: string;
}
