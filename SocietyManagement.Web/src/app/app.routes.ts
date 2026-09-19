import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';
import { permissionGuard } from './core/guards/permission.guard';
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
        path: 'announcements',
        loadChildren: () => import('./features/announcements/announcements.routes').then((m) => m.ANNOUNCEMENTS_ROUTES)
      },
      {
        path: 'facilities',
        loadChildren: () => import('./features/facilities/facilities.routes').then((m) => m.FACILITIES_ROUTES)
      },
      {
        path: 'facility-bookings',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () => import('./features/facilities/facility-bookings-admin.component').then((m) => m.FacilityBookingsAdminComponent)
      },
      {
        path: 'my-facility-bookings',
        loadComponent: () => import('./features/facilities/my-facility-bookings.component').then((m) => m.MyFacilityBookingsComponent)
      },
      {
        path: 'assets',
        loadChildren: () => import('./features/assets/assets.routes').then((m) => m.ASSETS_ROUTES)
      },
      {
        path: 'asset-rentals',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () => import('./features/assets/asset-rentals-admin.component').then((m) => m.AssetRentalsAdminComponent)
      },
      {
        path: 'asset-returns',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () => import('./features/assets/asset-returns.component').then((m) => m.AssetReturnsComponent)
      },
      {
        path: 'my-rentals',
        loadComponent: () => import('./features/assets/my-asset-rentals.component').then((m) => m.MyAssetRentalsComponent)
      },
      {
        path: 'inventory',
        canActivate: [permissionGuard],
        data: { permission: 'inventory.view' },
        loadComponent: () => import('./features/inventory/inventory.component').then((m) => m.InventoryComponent)
      },
      {
        path: 'purchases',
        canActivate: [permissionGuard],
        data: { permission: 'purchases.view' },
        loadComponent: () => import('./features/purchases/purchases-list.component').then((m) => m.PurchasesListComponent)
      },
      {
        path: 'budgets',
        canActivate: [permissionGuard],
        data: { permission: 'expenses.manage' },
        loadComponent: () => import('./features/budgets/budgets.component').then((m) => m.BudgetsComponent)
      },
      {
        path: 'pets',
        canActivate: [permissionGuard],
        data: { permission: 'pets.view' },
        loadComponent: () => import('./features/pets/pets-list.component').then((m) => m.PetsListComponent)
      },
      {
        path: 'vendors',
        canActivate: [permissionGuard],
        data: { permission: 'vendors.view' },
        loadComponent: () => import('./features/vendors/vendors-list.component').then((m) => m.VendorsListComponent)
      },
      {
        path: 'documents',
        canActivate: [permissionGuard],
        data: { permission: 'documents.view' },
        loadComponent: () => import('./features/documents/documents-list.component').then((m) => m.DocumentsListComponent)
      },
      {
        path: 'staff-attendance',
        canActivate: [permissionGuard],
        data: { permission: 'staff.view' },
        loadComponent: () => import('./features/attendance/attendance.component').then((m) => m.AttendanceComponent)
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
        // Was role-literal ['Admin'] despite staff.view/staff.manage already
        // existing as granular permissions — meant no custom role (e.g. a
        // Security Head preset) could ever reach this page no matter what
        // was granted via Roles & Permissions. Permission-gated like the
        // feature's own actions already are.
        path: 'staff',
        canActivate: [permissionGuard],
        data: { permission: 'staff.view' },
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
        // Same fix as 'staff' below — was role-literal ['Admin'] despite
        // complaints.view/manage already existing, which silently broke the
        // Committee Member preset's Complaints access (it grants the
        // permission, but the route itself never checked it).
        path: 'complaints',
        canActivate: [permissionGuard],
        data: { permission: 'complaints.view' },
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
        path: 'support',
        loadComponent: () => import('./features/support/my-tickets.component').then((m) => m.MyTicketsComponent)
      },
      {
        path: 'admin/support-tickets',
        canActivate: [roleGuard],
        data: { roles: ['SuperAdmin'] },
        loadComponent: () => import('./features/support/support-tickets-admin.component').then((m) => m.SupportTicketsAdminComponent)
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
