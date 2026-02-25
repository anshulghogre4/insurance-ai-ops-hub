import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/** Intercepts HTTP error responses with user-friendly messages and auth handling. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Only force logout on 401 if the request was to Supabase (actual auth failure).
      // Don't logout on 401 from the .NET backend — it may not validate Supabase tokens.
      if (error.status === 401 && authService.authEnabled() && error.url?.includes('supabase')) {
        authService.signOut().catch(() => {});
        router.navigate(['/login'], {
          queryParams: { returnUrl: router.url }
        });
      }

      // Enrich error with user-friendly message for common status codes
      let userMessage = error.error?.error || error.message;
      switch (error.status) {
        case 429:
          userMessage = 'Rate limit reached. Free tier allows 30 requests per minute. Please wait a moment and try again.';
          break;
        case 502:
          userMessage = 'AI provider temporarily unavailable. The system will automatically try another provider on your next request.';
          break;
        case 503:
          userMessage = 'All AI services are currently down. Please try again later.';
          break;
      }

      const enrichedError = new HttpErrorResponse({
        error: { error: userMessage, status: error.status },
        headers: error.headers,
        status: error.status,
        statusText: error.statusText,
        url: error.url ?? undefined
      });

      return throwError(() => enrichedError);
    })
  );
};
