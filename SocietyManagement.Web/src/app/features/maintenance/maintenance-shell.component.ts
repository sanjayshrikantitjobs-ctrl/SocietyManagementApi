import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

/** Admin shell for the Maintenance module — a router-link tab bar over the
 * six config/operational screens, plus a router-outlet for the active one.
 * Mirrors the Festival detail page's tab structure, but as routes (each
 * screen has its own URL/breadcrumb) rather than mat-tab-group panels,
 * since these are independent admin pages, not facets of one entity. */
@Component({
  selector: 'app-maintenance-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, MatTabsModule, PageHeaderComponent],
  template: `
    <div class="app-page">
      <app-page-header title="Maintenance Management"
        subtitle="Billing configuration, special charges, fines, and monthly bills."
        [breadcrumbs]="[{ label: 'Maintenance' }]" />

      <nav mat-tab-nav-bar [tabPanel]="tabPanel" class="maintenance-nav">
        <a mat-tab-link routerLink="dashboard" routerLinkActive #d="routerLinkActive" [active]="d.isActive">Dashboard</a>
        <a mat-tab-link routerLink="bills" routerLinkActive #b="routerLinkActive" [active]="b.isActive">Bills</a>
        <a mat-tab-link routerLink="categories" routerLinkActive #c="routerLinkActive" [active]="c.isActive">Categories</a>
        <a mat-tab-link routerLink="special-charges" routerLinkActive #sc="routerLinkActive" [active]="sc.isActive">Special Charges</a>
        <a mat-tab-link routerLink="fines" routerLinkActive #f="routerLinkActive" [active]="f.isActive">Fines</a>
        <a mat-tab-link routerLink="settings" routerLinkActive #s="routerLinkActive" [active]="s.isActive">Settings</a>
      </nav>
      <mat-tab-nav-panel #tabPanel>
        <router-outlet />
      </mat-tab-nav-panel>
    </div>
  `,
  styles: [`.maintenance-nav { margin-bottom: 16px; border-bottom: 1px solid var(--app-border); }`]
})
export class MaintenanceShellComponent {}
