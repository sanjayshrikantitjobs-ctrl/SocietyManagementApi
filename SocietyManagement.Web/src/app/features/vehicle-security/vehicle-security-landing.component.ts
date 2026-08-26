import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

/** Index page — big tap targets for a phone in hand at the gate, not a
 * data table. Scan/Search visibility mirrors the route guards
 * (vehicles.scan / vehicles.search); History is visible to anyone who can
 * reach either (the query itself narrows Watchman down to their own rows). */
@Component({
  selector: 'app-vehicle-security-landing',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule, PageHeaderComponent],
  template: `
    <div class="app-page landing-page">
      <app-page-header title="Vehicle Security" subtitle="Scan a plate, search registered vehicles, or review scan history." />
      <div class="action-grid">
        @if (auth.hasPermission('vehicles.scan')) {
          <a routerLink="/vehicle-security/scan" class="app-card action-card">
            <mat-icon>photo_camera</mat-icon>
            <h3>Scan Plate</h3>
            <p class="muted">Capture and recognize a number plate.</p>
          </a>
        }
        @if (auth.hasPermission('vehicles.search')) {
          <a routerLink="/vehicle-security/search" class="app-card action-card">
            <mat-icon>search</mat-icon>
            <h3>Search Vehicles</h3>
            <p class="muted">Look up by reg. no., owner, or flat.</p>
          </a>
        }
        @if (auth.hasPermission('vehicles.scan')) {
          <a routerLink="/vehicle-security/history" class="app-card action-card">
            <mat-icon>history</mat-icon>
            <h3>Scan History</h3>
            <p class="muted">Review past scans and searches.</p>
          </a>
        }
      </div>
    </div>
  `,
  styles: [`
    .landing-page { max-width: 720px; margin: 0 auto; }
    .action-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; }
    .action-card { display: flex; flex-direction: column; align-items: center; text-align: center; gap: 8px;
      padding: 28px 16px; text-decoration: none; color: inherit; transition: box-shadow 0.15s ease; }
    .action-card:hover { box-shadow: var(--app-shadow-hover); }
    .action-card mat-icon { font-size: 36px; width: 36px; height: 36px; color: var(--app-primary); }
    .action-card h3 { margin: 0; font-size: 15px; }
    .muted { margin: 0; font-size: 12px; color: var(--app-text-muted); }
  `]
})
export class VehicleSecurityLandingComponent {
  readonly auth = inject(AuthService);
}
