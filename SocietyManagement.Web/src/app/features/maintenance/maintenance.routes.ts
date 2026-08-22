import { Routes } from '@angular/router';
import { MaintenanceShellComponent } from './maintenance-shell.component';

export const MAINTENANCE_ROUTES: Routes = [
  {
    path: '',
    component: MaintenanceShellComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () => import('./dashboard/maintenance-dashboard.component').then((m) => m.MaintenanceDashboardComponent)
      },
      {
        path: 'bills',
        loadComponent: () => import('./bills/maintenance-bills-list.component').then((m) => m.MaintenanceBillsListComponent)
      },
      {
        path: 'bills/:id',
        loadComponent: () => import('./bills/maintenance-bill-detail.component').then((m) => m.MaintenanceBillDetailComponent)
      },
      {
        path: 'categories',
        loadComponent: () => import('./categories/maintenance-categories.component').then((m) => m.MaintenanceCategoriesComponent)
      },
      {
        path: 'special-charges',
        loadComponent: () => import('./special-charges/special-charges.component').then((m) => m.SpecialChargesComponent)
      },
      {
        path: 'fines',
        loadComponent: () => import('./fines/fines.component').then((m) => m.FinesComponent)
      },
      {
        path: 'settings',
        loadComponent: () => import('./settings/maintenance-settings.component').then((m) => m.MaintenanceSettingsComponent)
      },
      {
        path: 'water-tanker',
        loadComponent: () => import('./water-tanker/water-tanker.component').then((m) => m.WaterTankerComponent)
      }
    ]
  }
];
