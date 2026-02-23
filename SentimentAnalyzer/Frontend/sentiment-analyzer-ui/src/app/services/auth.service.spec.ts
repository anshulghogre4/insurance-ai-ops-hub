import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AuthService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should have authEnabled based on environment config', () => {
    // authEnabled depends on environment config having supabaseUrl + supabaseAnonKey
    // Angular CLI swaps environment.ts with environment.development.ts during dev builds
    expect(typeof service.authEnabled()).toBe('boolean');
  });

  it('should not be authenticated initially', () => {
    expect(service.isAuthenticated()).toBe(false);
  });

  it('should have loading signal defined', () => {
    expect(typeof service.loading()).toBe('boolean');
  });

  it('should return null user initially', () => {
    expect(service.user()).toBeNull();
  });

  it('should return null access token initially', () => {
    expect(service.getAccessToken()).toBeNull();
  });

  it('should return result object from signIn', async () => {
    const result = await service.signIn('test@test.com', 'password');
    expect(result).toHaveProperty('error');
  });

  it('should return result object from signUp', async () => {
    const result = await service.signUp('test@test.com', 'password');
    expect(result).toHaveProperty('error');
  });

  it('should not throw on signOut', async () => {
    await expect(service.signOut()).resolves.not.toThrow();
  });
});
