import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ToastComponent } from './toast';
import { ToastService } from '../../services/toast.service';

describe('ToastComponent', () => {
  let component: ToastComponent;
  let fixture: ComponentFixture<ToastComponent>;
  let toastService: ToastService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ToastComponent],
    }).compileComponents();

    toastService = TestBed.inject(ToastService);
    fixture = TestBed.createComponent(ToastComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render toast messages from the service', () => {
    toastService.success('Claim triaged successfully');
    toastService.error('Fraud analysis failed');
    fixture.detectChanges();

    const toastElements = fixture.nativeElement.querySelectorAll('[role="alert"]');
    expect(toastElements.length).toBe(2);
    expect(toastElements[0].textContent).toContain('Claim triaged successfully');
    expect(toastElements[1].textContent).toContain('Fraud analysis failed');
  });

  it('should apply correct border class for success toast', () => {
    toastService.success('Document uploaded and indexed');
    fixture.detectChanges();

    const toast = fixture.nativeElement.querySelector('[data-toast-type="success"]');
    expect(toast).toBeTruthy();
    expect(toast.classList.contains('border-emerald-500')).toBe(true);
  });

  it('should apply correct border class for error toast', () => {
    toastService.error('Failed to upload insurance document');
    fixture.detectChanges();

    const toast = fixture.nativeElement.querySelector('[data-toast-type="error"]');
    expect(toast).toBeTruthy();
    expect(toast.classList.contains('border-rose-500')).toBe(true);
  });

  it('should apply correct border class for warning toast', () => {
    toastService.warning('Provider health degraded');
    fixture.detectChanges();

    const toast = fixture.nativeElement.querySelector('[data-toast-type="warning"]');
    expect(toast).toBeTruthy();
    expect(toast.classList.contains('border-amber-500')).toBe(true);
  });

  it('should apply correct border class for info toast', () => {
    toastService.info('Correlation dismissed by analyst');
    fixture.detectChanges();

    const toast = fixture.nativeElement.querySelector('[data-toast-type="info"]');
    expect(toast).toBeTruthy();
    expect(toast.classList.contains('border-cyan-500')).toBe(true);
  });

  it('should dismiss toast when X button is clicked', () => {
    toastService.success('Claim processed');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('[role="alert"]').length).toBe(1);

    const dismissBtn = fixture.nativeElement.querySelector('button[aria-label="Dismiss notification"]');
    expect(dismissBtn).toBeTruthy();
    dismissBtn.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('[role="alert"]').length).toBe(0);
  });

  it('should have aria-live attribute for accessibility', () => {
    const container = fixture.nativeElement.querySelector('[aria-live="polite"]');
    expect(container).toBeTruthy();
  });

  it('should render correct icon for each toast type', () => {
    toastService.success('Success message');
    toastService.info('Info message');
    fixture.detectChanges();

    const successToast = fixture.nativeElement.querySelector('[data-toast-type="success"]');
    const successIcon = successToast.querySelector('svg');
    expect(successIcon).toBeTruthy();
    expect(successIcon.classList.contains('text-emerald-400')).toBe(true);

    const infoToast = fixture.nativeElement.querySelector('[data-toast-type="info"]');
    const infoIcon = infoToast.querySelector('svg');
    expect(infoIcon).toBeTruthy();
    expect(infoIcon.classList.contains('text-cyan-400')).toBe(true);
  });

  it('should show no toasts when service has none', () => {
    fixture.detectChanges();
    const toastElements = fixture.nativeElement.querySelectorAll('[role="alert"]');
    expect(toastElements.length).toBe(0);
  });

  it('should apply animate-slide-in-right class to each toast', () => {
    toastService.success('Animated notification');
    fixture.detectChanges();

    const toast = fixture.nativeElement.querySelector('[role="alert"]');
    expect(toast.classList.contains('animate-slide-in-right')).toBe(true);
  });

  it('should render a progress bar inside each toast', () => {
    toastService.success('Claim processed with progress bar');
    fixture.detectChanges();

    const progressBar = fixture.nativeElement.querySelector('[data-testid="toast-progress"]');
    expect(progressBar).toBeTruthy();
    expect(progressBar.classList.contains('toast-progress-bar')).toBe(true);
  });

  it('should apply correct progress bar color for success toast', () => {
    toastService.success('Success with progress');
    fixture.detectChanges();

    const progressBar = fixture.nativeElement.querySelector('[data-testid="toast-progress"]');
    expect(progressBar.classList.contains('toast-progress-success')).toBe(true);
  });

  it('should apply correct progress bar color for error toast', () => {
    toastService.error('Error with progress');
    fixture.detectChanges();

    const progressBar = fixture.nativeElement.querySelector('[data-testid="toast-progress"]');
    expect(progressBar.classList.contains('toast-progress-error')).toBe(true);
  });

  it('should apply correct progress bar color for warning toast', () => {
    toastService.warning('Warning with progress');
    fixture.detectChanges();

    const progressBar = fixture.nativeElement.querySelector('[data-testid="toast-progress"]');
    expect(progressBar.classList.contains('toast-progress-warning')).toBe(true);
  });

  it('should apply correct progress bar color for info toast', () => {
    toastService.info('Info with progress');
    fixture.detectChanges();

    const progressBar = fixture.nativeElement.querySelector('[data-testid="toast-progress"]');
    expect(progressBar.classList.contains('toast-progress-info')).toBe(true);
  });

  it('should set animation-duration matching toast timeout', () => {
    toastService.success('Timed progress bar');
    fixture.detectChanges();

    const progressBar = fixture.nativeElement.querySelector('[data-testid="toast-progress"]');
    // Success toast has 5000ms timeout
    expect(progressBar.style.animationDuration).toBe('5000ms');
  });
});
