import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

/** Admin shell for Resident Management — tab-nav over Members / Vehicles /
 * Emergency Contacts / Resale Listings, mirroring MaintenanceShellComponent. */
@Component({
  selector: 'app-residents-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, MatTabsModule, PageHeaderComponent],
  template: `
    <div class="app-page">
      <app-page-header title="Resident Management"
        subtitle="Members, occupancy, vehicles, emergency contacts, and flat resale listings."
        [breadcrumbs]="[{ label: 'Residents' }]" />

      <nav mat-tab-nav-bar [tabPanel]="tabPanel" class="residents-nav">
        <a mat-tab-link routerLink="members" routerLinkActive #m="routerLinkActive" [active]="m.isActive">Members</a>
        <a mat-tab-link routerLink="vehicles" routerLinkActive #v="routerLinkActive" [active]="v.isActive">Vehicles</a>
        <a mat-tab-link routerLink="emergency-contacts" routerLinkActive #e="routerLinkActive" [active]="e.isActive">Emergency Contacts</a>
        <a mat-tab-link routerLink="resale-listings" routerLinkActive #r="routerLinkActive" [active]="r.isActive">Resale Listings</a>
      </nav>
      <mat-tab-nav-panel #tabPanel>
        <router-outlet />
      </mat-tab-nav-panel>
    </div>
  `,
  styles: [`.residents-nav { margin-bottom: 16px; border-bottom: 1px solid var(--app-border); }`]
})
export class ResidentsShellComponent {}
