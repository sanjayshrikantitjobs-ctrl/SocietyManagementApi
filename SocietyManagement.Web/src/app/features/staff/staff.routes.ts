import { Routes } from '@angular/router';

export const STAFF_ROUTES: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./staff-list.component').then((m) => m.StaffListComponent) },
  { path: ':id', loadComponent: () => import('./staff-detail.component').then((m) => m.StaffDetailComponent) }
];
