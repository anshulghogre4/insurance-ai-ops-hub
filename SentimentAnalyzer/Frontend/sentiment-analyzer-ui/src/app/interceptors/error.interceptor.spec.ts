import { vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { errorInterceptor } from './error.interceptor';
import { AuthService } from '../services/auth.service';

describe('errorInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: { authEnabled: ReturnType<typeof vi.fn>; signOut: ReturnType<typeof vi.fn> };
  let router: { navigate: ReturnType<typeof vi.fn>; url: string };

  beforeEach(() => {
    authService = {
      authEnabled: vi.fn().mockReturnValue(true),
      signOut: vi.fn().mockResolvedValue(undefined),
    };

    router = {
      navigate: vi.fn(),
      url: '/insurance',
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router },
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should redirect to login on 401 response', () => {
    httpClient.get('/api/test').subscribe({ error: () => {} });
    const req = httpMock.expectOne('/api/test');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(authService.signOut).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/insurance' }
    });
  });

  it('should redirect to login on 403 response', () => {
    httpClient.get('/api/test').subscribe({ error: () => {} });
    const req = httpMock.expectOne('/api/test');
    req.flush('Forbidden', { status: 403, statusText: 'Forbidden' });

    expect(authService.signOut).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/insurance' }
    });
  });

  it('should NOT redirect on 401 when auth is disabled', () => {
    authService.authEnabled.mockReturnValue(false);
    httpClient.get('/api/test').subscribe({ error: () => {} });
    const req = httpMock.expectOne('/api/test');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(authService.signOut).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('should NOT redirect on non-auth errors like 500', () => {
    httpClient.get('/api/test').subscribe({ error: () => {} });
    const req = httpMock.expectOne('/api/test');
    req.flush('Server Error', { status: 500, statusText: 'Internal Server Error' });

    expect(authService.signOut).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('should still propagate the error to subscribers', () => {
    const errorSpy = vi.fn();
    httpClient.get('/api/test').subscribe({ error: errorSpy });
    const req = httpMock.expectOne('/api/test');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(errorSpy).toHaveBeenCalled();
  });
});
