import { Routes } from '@angular/router';

// Services (vendor/AMC contracts) are simple enough that inline edit
// covers everything — no separate detail page needed, unlike Staff.
export const SERVICES_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./services-list.component').then((m) => m.ServicesListComponent) }
];
