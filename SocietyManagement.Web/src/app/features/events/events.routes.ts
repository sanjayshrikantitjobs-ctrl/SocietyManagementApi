import { Routes } from '@angular/router';
import { roleGuard } from '../../core/guards/role.guard';

export const EVENTS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./events-list.component').then((m) => m.EventsListComponent)
  },
  {
    path: 'check-in/:qrToken',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
    loadComponent: () => import('./event-checkin-confirm.component').then((m) => m.EventCheckinConfirmComponent)
  },
  {
    path: ':id/rsvp',
    loadComponent: () => import('./event-rsvp.component').then((m) => m.EventRsvpComponent)
  },
  {
    path: ':id/check-in',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
    loadComponent: () => import('./event-checkin.component').then((m) => m.EventCheckinComponent)
  }
];
