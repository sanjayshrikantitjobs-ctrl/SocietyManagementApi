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
        path: 'events',
        loadChildren: () => import('./features/events/events.routes').then((m) => m.EVENTS_ROUTES)
      },
      {
        path: 'visitors',
        loadChildren: () => import('./features/visitors/visitors.routes').then((m) => m.VISITORS_ROUTES)
      },
      {
        path: 'vehicle-security',
        loadChildren: () => import('./features/vehicle-security/vehicle-security.routes').then((m) => m.VEHICLE_SECURITY_ROUTES)
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
        path: 'staff',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadChildren: () => import('./features/staff/staff.routes').then((m) => m.STAFF_ROUTES)
      },
      {
        path: 'services',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadChildren: () => import('./features/services/services.routes').then((m) => m.SERVICES_ROUTES)
      },
      {
        path: 'finance',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadChildren: () => import('./features/finance/finance.routes').then((m) => m.FINANCE_ROUTES)
      },
      {
        path: 'complaints',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadChildren: () => import('./features/complaints/complaints.routes').then((m) => m.COMPLAINTS_ROUTES)
      },
      {
        path: 'committee',
        loadChildren: () => import('./features/committee/committee.routes').then((m) => m.COMMITTEE_ROUTES)
      },
      {
        path: 'my-family',
        loadChildren: () => import('./features/occupancy/my-family/my-family.routes').then((m) => m.MY_FAMILY_ROUTES)
      },
      {
        path: 'my-complaints',
        loadComponent: () =>
          import('./features/complaints/my-complaints/my-complaints.component').then((m) => m.MyComplaintsComponent)
      },
      {
        path: 'my-bills',
        loadComponent: () => import('./features/maintenance/my-bills/my-bills.component').then((m) => m.MyBillsComponent)
      },
      {
        path: 'my-water-tanker',
        loadComponent: () => import('./features/maintenance/my-water-tanker/my-water-tanker.component').then((m) => m.MyWaterTankerComponent)
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
  { path: 'subscription-expired', loadComponent: () => import('./shared/pages/subscription-expired.component').then(m => m.SubscriptionExpiredComponent) },
  { path: '**', loadComponent: () => import('./shared/pages/not-found.component').then(m => m.NotFoundComponent) }
];
