import { Routes } from '@angular/router';
import { ResidentsShellComponent } from './residents-shell.component';

export const RESIDENTS_ROUTES: Routes = [
  {
    path: '',
    component: ResidentsShellComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'members' },
      {
        path: 'members',
        loadComponent: () => import('./members/members-list.component').then((m) => m.MembersListComponent)
      },
      {
        path: 'vehicles',
        loadComponent: () => import('./vehicles/vehicles-list.component').then((m) => m.VehiclesListComponent)
      },
      {
        path: 'emergency-contacts',
        loadComponent: () =>
          import('./emergency-contacts/emergency-contacts-list.component').then((m) => m.EmergencyContactsListComponent)
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
    path: 'occupancy/:flatId',
    loadComponent: () =>
      import('../occupancy/occupancy-overview/occupancy-overview.component').then((m) => m.OccupancyOverviewComponent)
  }
];

