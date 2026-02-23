import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { toObservable } from '@angular/core/rxjs-interop';
import { filter, map, take } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // Auth not configured — allow all access (dev mode)
  if (!authService.authEnabled()) {
    return true;
  }

  // Auth already initialized — check synchronously
  if (!authService.loading()) {
    if (authService.isAuthenticated()) {
      return true;
    }
    return router.createUrlTree(['/login'], {
      queryParams: { returnUrl: state.url }
    });
  }

  // Auth still loading — wait for session restoration to complete
  return toObservable(authService.loading).pipe(
    filter(loading => !loading),
    take(1),
    map(() => {
      if (authService.isAuthenticated()) {
        return true;
      }
      return router.createUrlTree(['/login'], {
        queryParams: { returnUrl: state.url }
      });
    })
  );
};
