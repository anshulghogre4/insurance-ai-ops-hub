import { vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';

describe('authGuard', () => {
  let authService: { authEnabled: ReturnType<typeof vi.fn>; isAuthenticated: ReturnType<typeof vi.fn>; loading: ReturnType<typeof vi.fn> };
  let router: { createUrlTree: ReturnType<typeof vi.fn> };

  function runGuard(url = '/insurance'): boolean | UrlTree {
    return TestBed.runInInjectionContext(() =>
      authGuard({} as any, { url } as any)
    ) as boolean | UrlTree;
  }

  beforeEach(() => {
    authService = {
      authEnabled: vi.fn().mockReturnValue(true),
      isAuthenticated: vi.fn().mockReturnValue(false),
      loading: vi.fn().mockReturnValue(false),
    };

    router = { createUrlTree: vi.fn().mockReturnValue('loginUrlTree') };

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

  it('should allow access when user is authenticated', () => {
    authService.isAuthenticated.mockReturnValue(true);
    expect(runGuard()).toBe(true);
  });

  it('should redirect to /login with returnUrl when not authenticated', () => {
    authService.isAuthenticated.mockReturnValue(false);
    const result = runGuard('/dashboard');
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/dashboard' }
    });
    expect(result).toBe('loginUrlTree');
  });

  it('should not check authentication when auth is disabled', () => {
    authService.authEnabled.mockReturnValue(false);
    runGuard();
    expect(authService.isAuthenticated).not.toHaveBeenCalled();
  });
});
