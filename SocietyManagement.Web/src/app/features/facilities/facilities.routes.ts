import { Routes } from '@angular/router';

export const FACILITIES_ROUTES: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./facilities-list.component').then((m) => m.FacilitiesListComponent) },
  { path: ':id', loadComponent: () => import('./facility-detail.component').then((m) => m.FacilityDetailComponent) }
];
