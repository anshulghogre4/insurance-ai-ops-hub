import { vi, describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: { authEnabled: ReturnType<typeof vi.fn>; isAuthenticated: ReturnType<typeof vi.fn>; getAccessToken: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    authService = {
      authEnabled: vi.fn().mockReturnValue(true),
      isAuthenticated: vi.fn().mockReturnValue(true),
      getAccessToken: vi.fn().mockReturnValue('mock-jwt-token'),
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authService },
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should add Authorization header when authenticated', () => {
    httpClient.get('/api/test').subscribe();
    const req = httpMock.expectOne('/api/test');
    expect(req.request.headers.get('Authorization')).toBe('Bearer mock-jwt-token');
    req.flush({});
  });

  it('should NOT add header when auth is disabled', () => {
    authService.authEnabled.mockReturnValue(false);
    httpClient.get('/api/test').subscribe();
    const req = httpMock.expectOne('/api/test');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('should NOT add header when not authenticated', () => {
    authService.isAuthenticated.mockReturnValue(false);
    httpClient.get('/api/test').subscribe();
    const req = httpMock.expectOne('/api/test');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('should NOT add header when token is null', () => {
    authService.getAccessToken.mockReturnValue(null);
    httpClient.get('/api/test').subscribe();
    const req = httpMock.expectOne('/api/test');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });
});
