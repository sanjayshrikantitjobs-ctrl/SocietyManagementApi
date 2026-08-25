import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';

/** Festivals and Events are two independent top-level route trees (Events
 * keeps its own /events/... paths, including check-in/:qrToken links that
 * may already be printed/shared — restructuring those under /festivals
 * would break them). This is purely a shared UI header giving the two
 * pages a common tab bar, mirroring MaintenanceShellComponent's look,
 * without touching routing at all. */
@Component({
  selector: 'app-festivals-events-tabs',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, MatTabsModule],
  template: `
    <nav mat-tab-nav-bar [tabPanel]="tabPanel" class="fe-nav">
      <a mat-tab-link routerLink="/festivals" routerLinkActive #f="routerLinkActive" [active]="f.isActive">Festivals</a>
      <a mat-tab-link routerLink="/events" routerLinkActive #e="routerLinkActive" [active]="e.isActive">Events</a>
    </nav>
    <mat-tab-nav-panel #tabPanel />
  `,
  styles: [`.fe-nav { margin-bottom: 16px; border-bottom: 1px solid var(--app-border); }`]
})
export class FestivalsEventsTabsComponent {}
