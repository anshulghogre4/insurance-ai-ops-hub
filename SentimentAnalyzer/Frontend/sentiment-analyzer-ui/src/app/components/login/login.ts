import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login.html'
})
export class LoginComponent {
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  email = '';
  password = '';
  isRegisterMode = signal(false);
  isLoading = signal(false);
  error = signal<string | null>(null);
  successMessage = signal<string | null>(null);
  showPassword = signal(false);

  toggleMode(): void {
    this.isRegisterMode.update(v => !v);
    this.error.set(null);
    this.successMessage.set(null);
  }

  togglePasswordVisibility(): void {
    this.showPassword.update(v => !v);
  }

  forgotPassword(): void {
    this.error.set('Password reset is handled via Supabase. Contact your administrator if you need access.');
  }

  private getSafeReturnUrl(): string {
    const returnUrl = this.route.snapshot.queryParams['returnUrl'];
    if (!returnUrl || !returnUrl.startsWith('/') || returnUrl.startsWith('//')) {
      return '/insurance';
    }
    return returnUrl;
  }

  async submit(): Promise<void> {
    if (!this.email.trim() || !this.password.trim()) {
      this.error.set('Please enter both email and password.');
      return;
    }

    this.isLoading.set(true);
    this.error.set(null);
    this.successMessage.set(null);

    if (this.isRegisterMode()) {
      const { error } = await this.authService.signUp(this.email, this.password);
      this.isLoading.set(false);
      if (error) {
        this.error.set(error);
      } else {
        this.successMessage.set('Registration successful! Check your email to confirm, then sign in.');
        this.isRegisterMode.set(false);
      }
    } else {
      const { error } = await this.authService.signIn(this.email, this.password);
      this.isLoading.set(false);
      if (error) {
        this.error.set(error);
      } else {
        const returnUrl = this.getSafeReturnUrl();
        this.router.navigateByUrl(returnUrl);
      }
    }
  }
}
