import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { roleGuard } from '../../core/guards/role.guard';
import { AuthService } from '../../core/services/auth.service';

export const DASHBOARD_ROUTES: Routes = [
  {
    path: '', pathMatch: 'full',
    // A hardcoded redirectTo: 'admin' here sent every non-Admin straight
    // into the Admin-only 'admin' route, which roleGuard then bounced to
    // /forbidden — every Member/Watchman hit this on first login, since
    // that's exactly where the login flow lands (see login.component.ts).
    redirectTo: () => (inject(AuthService).isAdmin() ? 'admin' : 'member')
  },
  {
    path: 'admin',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
    loadComponent: () => import('./admin-dashboard/admin-dashboard.component').then((m) => m.AdminDashboardComponent)
  },
  {
    path: 'member',
    loadComponent: () =>
      import('./member-dashboard/member-dashboard.component').then((m) => m.MemberDashboardComponent)
  }
];
