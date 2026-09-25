import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { AdminAuthService } from './admin-auth.service';

/**
 * Convenience only: sends an unauthenticated visitor to the sign-in gate instead of an empty page.
 * The control is [Authorize(Roles = "Admin")] on the API — this guard can be bypassed and it doesn't matter.
 */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AdminAuthService);
  const router = inject(Router);

  return auth.me().pipe(
    map(() => true),
    catchError(() => of(router.createUrlTree(['/admin/login']))),
  );
};
