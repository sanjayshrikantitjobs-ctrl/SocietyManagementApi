import { Routes } from '@angular/router';

export const COMMITTEE_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./committee-list.component').then((m) => m.CommitteeListComponent) }
];
