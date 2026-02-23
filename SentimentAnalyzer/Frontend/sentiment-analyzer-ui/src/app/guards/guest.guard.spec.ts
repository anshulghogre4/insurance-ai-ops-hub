import { vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { guestGuard } from './guest.guard';
import { AuthService } from '../services/auth.service';

describe('guestGuard', () => {
  let authService: { authEnabled: ReturnType<typeof vi.fn>; isAuthenticated: ReturnType<typeof vi.fn>; loading: ReturnType<typeof vi.fn> };
  let router: { createUrlTree: ReturnType<typeof vi.fn> };

  function runGuard(): boolean | UrlTree {
    return TestBed.runInInjectionContext(() =>
      guestGuard({} as any, {} as any)
    ) as boolean | UrlTree;
  }

  beforeEach(() => {
    authService = {
      authEnabled: vi.fn().mockReturnValue(true),
      isAuthenticated: vi.fn().mockReturnValue(false),
      loading: vi.fn().mockReturnValue(false),
    };

    router = { createUrlTree: vi.fn().mockReturnValue('insuranceUrlTree') };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router }
      ]
    });
  });

  it('should allow access when auth is disabled', () => {
    authService.authEnabled.mockReturnValue(false);
    expect(runGuard()).toBe(true);
  });

  it('should allow access when user is not authenticated', () => {
    authService.isAuthenticated.mockReturnValue(false);
    expect(runGuard()).toBe(true);
  });

  it('should redirect to /insurance when user is already authenticated', () => {
    authService.isAuthenticated.mockReturnValue(true);
    const result = runGuard();
    expect(router.createUrlTree).toHaveBeenCalledWith(['/insurance']);
    expect(result).toBe('insuranceUrlTree');
  });
});
