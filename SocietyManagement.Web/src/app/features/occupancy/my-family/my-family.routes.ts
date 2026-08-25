import { Routes } from '@angular/router';

export const MY_FAMILY_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./my-family.component').then((m) => m.MyFamilyComponent) }
];
