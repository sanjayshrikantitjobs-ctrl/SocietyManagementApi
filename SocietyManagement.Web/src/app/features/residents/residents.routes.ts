import { Routes } from '@angular/router';
import { ResidentsShellComponent } from './residents-shell.component';

export const RESIDENTS_ROUTES: Routes = [
  {
    path: '',
    component: ResidentsShellComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'overview' },
      {
        path: 'overview',
        loadComponent: () =>
          import('../occupancy/residents-overview/residents-overview.component').then((m) => m.ResidentsOverviewComponent)
      },
      {
        path: 'owners',
        loadComponent: () => import('../occupancy/owners-tab/owners-tab.component').then((m) => m.OwnersTabComponent)
      },
      {
        path: 'tenants',
        loadComponent: () => import('../occupancy/tenants-tab/tenants-tab.component').then((m) => m.TenantsTabComponent)
      },
      {
        path: 'members',
        loadComponent: () => import('./members/members-list.component').then((m) => m.MembersListComponent)
      },
      {
        path: 'vehicles',
        loadComponent: () => import('./vehicles/vehicles-list.component').then((m) => m.VehiclesListComponent)
      },
      {
        path: 'resale-listings',
        loadComponent: () =>
          import('./resale-listings/resale-listings-list.component').then((m) => m.ResaleListingsListComponent)
      }
    ]
  },
  {
    path: 'flat/:flatId',
    loadComponent: () => import('./flat-occupancy/flat-occupancy.component').then((m) => m.FlatOccupancyComponent)
  },
  {
    path: 'owners/:flatId',
    loadComponent: () => import('../occupancy/owner-flat-detail/owner-flat-detail.component').then((m) => m.OwnerFlatDetailComponent)
  },
  {
    path: 'tenants/:flatId',
    loadComponent: () => import('../occupancy/tenant-flat-detail/tenant-flat-detail.component').then((m) => m.TenantFlatDetailComponent)
  }
];
