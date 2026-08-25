import { Routes } from '@angular/router';

export const COMPLAINTS_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./complaints-board.component').then((m) => m.ComplaintsBoardComponent) }
];
