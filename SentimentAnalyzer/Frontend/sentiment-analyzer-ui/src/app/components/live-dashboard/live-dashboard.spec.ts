import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { LiveDashboardComponent } from './live-dashboard';
import { SignalRService } from '../../services/signalr.service';
import { NotificationService } from '../../services/notification.service';
import { Router } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { describe, it, expect, beforeEach, vi } from 'vitest';

describe('LiveDashboardComponent', () => {
  let component: LiveDashboardComponent;
  let fixture: ComponentFixture<LiveDashboardComponent>;

  const mockSignalR = {
    connect: vi.fn().mockResolvedValue(undefined),
    on: vi.fn().mockReturnValue(of()),
    disconnect: vi.fn().mockResolvedValue(undefined),  // Accepts optional hubPath arg
    joinGroup: vi.fn().mockResolvedValue(undefined),
    leaveGroup: vi.fn().mockResolvedValue(undefined),
    connectionState: signal('disconnected' as const),
    isConnected: signal(false)
  };

  const mockNotification = {
    addNotification: vi.fn(),
    notifications: signal([]),
    unreadCount: signal(0),
    markAsRead: vi.fn(),
    markAllAsRead: vi.fn(),
    clearAll: vi.fn()
  };

  const mockRouter = {
    navigate: vi.fn()
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LiveDashboardComponent, RouterTestingModule],
      providers: [
        { provide: SignalRService, useValue: mockSignalR },
        { provide: NotificationService, useValue: mockNotification }
      ]
    }).compileComponents();

    mockRouter.navigate = TestBed.inject(Router).navigate = vi.fn();

    fixture = TestBed.createComponent(LiveDashboardComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should start in loading state', () => {
    expect(component.isLoading()).toBe(true);
  });

  it('should render metric cards with default values', () => {
    component.isLoading.set(false);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Claims / Hour');
    expect(compiled.textContent).toContain('Avg Triage Time');
    expect(compiled.textContent).toContain('Fraud Detection Rate');
    expect(compiled.textContent).toContain('Doc Queries / Hour');
  });

  it('should show empty state for claims feed', () => {
    component.isLoading.set(false);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No recent claims');
  });

  it('should show empty state for fraud alerts', () => {
    component.isLoading.set(false);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No fraud alerts');
  });

  it('should return correct severity colors', () => {
    expect(component.getSeverityColor('Critical')).toContain('rose');
    expect(component.getSeverityColor('High')).toContain('amber');
    expect(component.getSeverityColor('Low')).toContain('emerald');
  });

  it('should format time correctly', () => {
    const now = new Date().toISOString();
    expect(component.formatTime(now)).toBe('just now');
    expect(component.formatTime('')).toBe('');
  });

  it('should navigate to claim on click', () => {
    component.navigateToClaim(1042);
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/claims', 1042]);
  });
});
