import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Usage: { path: 'x', canActivate: [permissionGuard], data: { permission: 'members.create' } } */
export const permissionGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const requiredPermission = route.data['permission'] as string | undefined;

  if (!requiredPermission || auth.hasPermission(requiredPermission)) {
    return true;
  }

  return router.createUrlTree(['/forbidden']);
};
