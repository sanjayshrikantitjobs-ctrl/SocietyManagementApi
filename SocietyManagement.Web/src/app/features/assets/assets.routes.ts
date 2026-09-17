import { Routes } from '@angular/router';

export const ASSETS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./assets-list.component').then((m) => m.AssetsListComponent) }
];
