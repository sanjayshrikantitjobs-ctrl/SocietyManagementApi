import { Routes } from '@angular/router';
import { roleGuard } from '../../core/guards/role.guard';

export const VISITORS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./visitors-landing.component').then((m) => m.VisitorsLandingComponent)
  },
  {
    path: 'new',
    canActivate: [roleGuard],
    data: { roles: ['Admin', 'Watchman'] },
    loadComponent: () => import('./new-visitor.component').then((m) => m.NewVisitorComponent)
  },
  {
    path: 'currently-inside',
    canActivate: [roleGuard],
    data: { roles: ['Admin', 'Watchman'] },
    loadComponent: () => import('./currently-inside.component').then((m) => m.CurrentlyInsideComponent)
  },
  {
    path: 'gates',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
    loadComponent: () => import('./gates.component').then((m) => m.GatesComponent)
  },
  {
    path: 'purposes',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
    loadComponent: () => import('./purposes.component').then((m) => m.PurposesComponent)
  }
];
