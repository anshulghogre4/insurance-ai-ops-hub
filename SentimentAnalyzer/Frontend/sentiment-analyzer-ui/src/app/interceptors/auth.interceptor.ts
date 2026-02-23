import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  if (!authService.authEnabled() || !authService.isAuthenticated()) {
    return next(req);
  }

  const token = authService.getAccessToken();
  if (!token) {
    return next(req);
  }

  const clonedRequest = req.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
  });

  return next(clonedRequest);
};
