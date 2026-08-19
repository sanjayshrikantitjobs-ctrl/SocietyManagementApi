import { Routes } from '@angular/router';

export const FESTIVALS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./festivals-list.component').then((m) => m.FestivalsListComponent) },
  { path: ':id', loadComponent: () => import('./festival-detail.component').then((m) => m.FestivalDetailComponent) }
];
