import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { LoginComponent } from './login';
import { AuthService } from '../../services/auth.service';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authService: AuthService;
  let router: Router;

  function setup(queryParams: Record<string, string> = {}): void {
    TestBed.configureTestingModule({
      imports: [LoginComponent, FormsModule],
      providers: [
        {
          provide: AuthService,
          useValue: {
            signIn: vi.fn().mockResolvedValue({ error: null }),
            signUp: vi.fn().mockResolvedValue({ error: null }),
            authEnabled: () => true,
            isAuthenticated: () => false,
          }
        },
        {
          provide: Router,
          useValue: { navigate: vi.fn(), navigateByUrl: vi.fn() }
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParams } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    setup();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should start in login mode', () => {
    expect(component.isRegisterMode()).toBe(false);
  });

  it('should toggle between login and register', () => {
    component.toggleMode();
    expect(component.isRegisterMode()).toBe(true);
    component.toggleMode();
    expect(component.isRegisterMode()).toBe(false);
  });

  it('should show error for empty fields', async () => {
    component.email = '';
    component.password = '';
    await component.submit();
    expect(component.error()).toBe('Please enter both email and password.');
  });

  it('should show error for whitespace-only fields', async () => {
    component.email = '  ';
    component.password = '  ';
    await component.submit();
    expect(component.error()).toBe('Please enter both email and password.');
  });

  it('should call signIn and navigate to default route on success', async () => {
    component.email = 'test@test.com';
    component.password = 'password123';
    await component.submit();
    expect(authService.signIn).toHaveBeenCalledWith('test@test.com', 'password123');
    expect(router.navigateByUrl).toHaveBeenCalledWith('/insurance');
  });

  it('should navigate to returnUrl after successful signIn', async () => {
    TestBed.resetTestingModule();
    setup({ returnUrl: '/dashboard' });
    component.email = 'test@test.com';
    component.password = 'password123';
    await component.submit();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });

  it('should show error on signIn failure', async () => {
    vi.spyOn(authService, 'signIn').mockResolvedValue({ error: 'Invalid credentials' });
    component.email = 'test@test.com';
    component.password = 'wrong';
    await component.submit();
    expect(component.error()).toBe('Invalid credentials');
  });

  it('should call signUp in register mode', async () => {
    component.isRegisterMode.set(true);
    component.email = 'new@test.com';
    component.password = 'password123';
    await component.submit();
    expect(authService.signUp).toHaveBeenCalledWith('new@test.com', 'password123');
    expect(component.successMessage()).toContain('Registration successful');
  });

  it('should show error on signUp failure', async () => {
    vi.spyOn(authService, 'signUp').mockResolvedValue({ error: 'Email already registered' });
    component.isRegisterMode.set(true);
    component.email = 'existing@test.com';
    component.password = 'password123';
    await component.submit();
    expect(component.error()).toBe('Email already registered');
  });

  it('should switch to login mode after successful registration', async () => {
    component.isRegisterMode.set(true);
    component.email = 'new@test.com';
    component.password = 'password123';
    await component.submit();
    expect(component.isRegisterMode()).toBe(false);
  });

  it('should clear error and success on mode toggle', () => {
    component.error.set('Some error');
    component.successMessage.set('Some success');
    component.toggleMode();
    expect(component.error()).toBeNull();
    expect(component.successMessage()).toBeNull();
  });

  it('should set loading during submission', async () => {
    component.email = 'test@test.com';
    component.password = 'password123';
    // isLoading should be false before and after
    expect(component.isLoading()).toBe(false);
    await component.submit();
    expect(component.isLoading()).toBe(false);
  });

  it('should block open redirect with external returnUrl', async () => {
    TestBed.resetTestingModule();
    setup({ returnUrl: 'https://evil.com' });
    component.email = 'test@test.com';
    component.password = 'password123';
    await component.submit();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/insurance');
  });

  it('should block open redirect with protocol-relative returnUrl', async () => {
    TestBed.resetTestingModule();
    setup({ returnUrl: '//evil.com' });
    component.email = 'test@test.com';
    component.password = 'password123';
    await component.submit();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/insurance');
  });

  it('should toggle password visibility', () => {
    expect(component.showPassword()).toBe(false);
    component.togglePasswordVisibility();
    expect(component.showPassword()).toBe(true);
    component.togglePasswordVisibility();
    expect(component.showPassword()).toBe(false);
  });

  it('should show message on forgot password', () => {
    component.forgotPassword();
    expect(component.error()).toBeTruthy();
  });
});
