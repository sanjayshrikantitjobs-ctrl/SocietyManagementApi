import { Routes } from '@angular/router';

export const SOCIETIES_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./societies-list.component').then((m) => m.SocietiesListComponent) }
];
