import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Usage: { path: 'x', canActivate: [roleGuard], data: { roles: ['Admin'] } } */
export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const allowedRoles = (route.data['roles'] as string[] | undefined) ?? [];

  if (allowedRoles.length === 0 || allowedRoles.includes(auth.roleName() ?? '')) {
    return true;
  }

  return router.createUrlTree(['/forbidden']);
};
