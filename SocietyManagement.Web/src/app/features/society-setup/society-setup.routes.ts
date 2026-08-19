import { Routes } from '@angular/router';

export const SOCIETY_SETUP_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'societies' },
  {
    path: 'societies',
    loadComponent: () => import('./societies/societies-list.component').then((m) => m.SocietiesListComponent)
  },
  {
    path: 'societies/:societyId/structure',
    loadComponent: () => import('./buildings/structure.component').then((m) => m.StructureComponent)
  },
  {
    path: 'societies/:societyId/flats',
    loadComponent: () => import('./flats/flats-list.component').then((m) => m.FlatsListComponent)
  },
  {
    path: 'societies/:societyId/parking',
    loadComponent: () => import('./flats/parking-list.component').then((m) => m.ParkingListComponent)
  }
];
