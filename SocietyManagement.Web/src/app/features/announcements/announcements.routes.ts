import { Routes } from '@angular/router';

export const ANNOUNCEMENTS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./announcements-list.component').then((m) => m.AnnouncementsListComponent) },
  { path: ':id', loadComponent: () => import('./announcement-detail.component').then((m) => m.AnnouncementDetailComponent) }
];
