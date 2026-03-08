import { TestBed } from '@angular/core/testing';
import { SignalRService } from './signalr.service';
import { describe, it, expect, beforeEach } from 'vitest';

describe('SignalRService', () => {
  let service: SignalRService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(SignalRService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should start in disconnected state', () => {
    expect(service.connectionState()).toBe('disconnected');
  });

  it('should report not connected initially', () => {
    expect(service.isConnected()).toBe(false);
  });

  it('should return observable from on() even before connection', () => {
    const obs = service.on('/hubs/claims', 'ClaimTriaged');
    expect(obs).toBeTruthy();
    expect(obs.subscribe).toBeInstanceOf(Function);
  });

  it('should handle disconnect gracefully when no connections', async () => {
    await expect(service.disconnect()).resolves.toBeUndefined();
  });

  it('should handle disconnect for specific hub when not connected', async () => {
    await expect(service.disconnect('/hubs/claims')).resolves.toBeUndefined();
  });
});
