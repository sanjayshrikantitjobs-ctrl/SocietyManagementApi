import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';

export const VEHICLE_SECURITY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./vehicle-security-landing.component').then((m) => m.VehicleSecurityLandingComponent)
  },
  {
    path: 'scan',
    canActivate: [permissionGuard],
    data: { permission: 'vehicles.scan' },
    loadComponent: () => import('./vehicle-scan.component').then((m) => m.VehicleScanComponent)
  },
  {
    path: 'search',
    canActivate: [permissionGuard],
    data: { permission: 'vehicles.search' },
    loadComponent: () => import('./vehicle-search.component').then((m) => m.VehicleSearchComponent)
  },
  {
    path: 'history',
    canActivate: [permissionGuard],
    data: { permission: 'vehicles.scan' },
    loadComponent: () => import('./vehicle-scan-history.component').then((m) => m.VehicleScanHistoryComponent)
  },
  {
    path: 'parking-fines',
    canActivate: [permissionGuard],
    data: { permission: 'parking_fines.view' },
    loadComponent: () => import('./parking-fines/parking-fines-list.component').then((m) => m.ParkingFinesListComponent)
  }
];
