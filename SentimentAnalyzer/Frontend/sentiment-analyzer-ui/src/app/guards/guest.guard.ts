import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { toObservable } from '@angular/core/rxjs-interop';
import { filter, map, take } from 'rxjs';
import { AuthService } from '../services/auth.service';

/** Allows access only to unauthenticated users; redirects logged-in users to /insurance. */
export const guestGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.authEnabled()) {
    return true;
  }

  // Auth already initialized
  if (!authService.loading()) {
    if (authService.isAuthenticated()) {
      return router.createUrlTree(['/insurance']);
    }
    return true;
  }

  // Wait for auth initialization
  return toObservable(authService.loading).pipe(
    filter(loading => !loading),
    take(1),
    map(() => {
      if (authService.isAuthenticated()) {
        return router.createUrlTree(['/insurance']);
      }
      return true;
    })
  );
};
