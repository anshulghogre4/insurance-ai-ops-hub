import { vi, describe, it, expect, beforeEach } from 'vitest';
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

  it('should redirect to login on 401 from Supabase', () => {
    httpClient.get('https://project.supabase.co/rest/v1/test').subscribe({ error: () => {} });
    const req = httpMock.expectOne('https://project.supabase.co/rest/v1/test');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(authService.signOut).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/insurance' }
    });
  });

  it('should NOT redirect to login on 401 from backend API', () => {
    httpClient.get('/api/test').subscribe({ error: () => {} });
    const req = httpMock.expectOne('/api/test');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(authService.signOut).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
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

  it('should enrich 429 error with rate limit message', () => {
    const errorSpy = vi.fn();
    httpClient.post('/api/insurance/analyze', {}).subscribe({ error: errorSpy });
    const req = httpMock.expectOne('/api/insurance/analyze');
    req.flush({ error: 'Too many requests' }, { status: 429, statusText: 'Too Many Requests' });

    expect(errorSpy).toHaveBeenCalled();
    const enrichedError = errorSpy.mock.calls[0][0];
    expect(enrichedError.status).toBe(429);
    expect(enrichedError.error.error).toContain('Rate limit reached');
    expect(enrichedError.error.error).toContain('30 requests per minute');
  });

  it('should enrich 502 error with provider unavailable message', () => {
    const errorSpy = vi.fn();
    httpClient.post('/api/insurance/analyze', {}).subscribe({ error: errorSpy });
    const req = httpMock.expectOne('/api/insurance/analyze');
    req.flush({ error: 'Bad Gateway' }, { status: 502, statusText: 'Bad Gateway' });

    expect(errorSpy).toHaveBeenCalled();
    const enrichedError = errorSpy.mock.calls[0][0];
    expect(enrichedError.status).toBe(502);
    expect(enrichedError.error.error).toContain('AI provider temporarily unavailable');
  });

  it('should enrich 503 error with all services down message', () => {
    const errorSpy = vi.fn();
    httpClient.post('/api/insurance/analyze', {}).subscribe({ error: errorSpy });
    const req = httpMock.expectOne('/api/insurance/analyze');
    req.flush({ error: 'Service Unavailable' }, { status: 503, statusText: 'Service Unavailable' });

    expect(errorSpy).toHaveBeenCalled();
    const enrichedError = errorSpy.mock.calls[0][0];
    expect(enrichedError.status).toBe(503);
    expect(enrichedError.error.error).toContain('All AI services are currently down');
  });
});
