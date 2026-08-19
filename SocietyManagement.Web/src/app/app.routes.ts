import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },

  {
    path: 'auth',
    canActivate: [guestGuard],
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES)
  },

  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./shared/layout/main-layout/main-layout.component').then((m) => m.MainLayoutComponent),
    children: [
      {
        path: 'dashboard',
        loadChildren: () => import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES)
      },
      {
        path: 'festivals',
        loadChildren: () => import('./features/festivals/festivals.routes').then((m) => m.FESTIVALS_ROUTES)
      },
      {
        path: 'maintenance',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadChildren: () => import('./features/maintenance/maintenance.routes').then((m) => m.MAINTENANCE_ROUTES)
      },
      {
        path: 'residents',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadChildren: () => import('./features/residents/residents.routes').then((m) => m.RESIDENTS_ROUTES)
      },
      {
        path: 'my-bills',
        loadComponent: () => import('./features/maintenance/my-bills/my-bills.component').then((m) => m.MyBillsComponent)
      },
      {
        path: 'society-setup',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadChildren: () =>
          import('./features/society-setup/society-setup.routes').then((m) => m.SOCIETY_SETUP_ROUTES)
      },
      {
        path: 'users',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadChildren: () => import('./features/users/users.routes').then((m) => m.USERS_ROUTES)
      },
      {
        path: 'roles',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadChildren: () => import('./features/roles/roles.routes').then((m) => m.ROLES_ROUTES)
      },
      {
        path: 'profile',
        loadComponent: () =>
          import('./features/auth/change-password/change-password.component')
            .then((m) => m.ChangePasswordComponent)
      }
    ]
  },

  { path: 'forbidden', loadComponent: () => import('./shared/pages/forbidden.component').then(m => m.ForbiddenComponent) },
  { path: '**', loadComponent: () => import('./shared/pages/not-found.component').then(m => m.NotFoundComponent) }
];
