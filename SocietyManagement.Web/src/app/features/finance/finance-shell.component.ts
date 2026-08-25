import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

/** Admin shell for the Finance module — mirrors MaintenanceShellComponent's
 * route-tab-bar pattern. "Payments" and "Ledger" (both named in the
 * original request) are combined into one Ledger tab — a chronological
 * running-balance view of every transaction — since nothing distinguishes
 * a separate "Payments" view once Income and Expenses each have their own
 * tab. */
@Component({
  selector: 'app-finance-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, MatTabsModule, PageHeaderComponent],
  template: `
    <div class="app-page">
      <app-page-header title="Finance"
        subtitle="Income, expenses, outstanding dues, and financial reports across the whole society."
        [breadcrumbs]="[{ label: 'Finance' }]" />

      <nav mat-tab-nav-bar [tabPanel]="tabPanel" class="finance-nav">
        <a mat-tab-link routerLink="overview" routerLinkActive #o="routerLinkActive" [active]="o.isActive">Overview</a>
        <a mat-tab-link routerLink="income" routerLinkActive #i="routerLinkActive" [active]="i.isActive">Income</a>
        <a mat-tab-link routerLink="expenses" routerLinkActive #e="routerLinkActive" [active]="e.isActive">Expenses</a>
        <a mat-tab-link routerLink="outstanding" routerLinkActive #ou="routerLinkActive" [active]="ou.isActive">Outstanding</a>
        <a mat-tab-link routerLink="ledger" routerLinkActive #l="routerLinkActive" [active]="l.isActive">Ledger</a>
        <a mat-tab-link routerLink="receipts" routerLinkActive #r="routerLinkActive" [active]="r.isActive">Receipts</a>
        <a mat-tab-link routerLink="reports" routerLinkActive #rp="routerLinkActive" [active]="rp.isActive">Reports</a>
      </nav>
      <mat-tab-nav-panel #tabPanel>
        <router-outlet />
      </mat-tab-nav-panel>
    </div>
  `,
  styles: [`.finance-nav { margin-bottom: 16px; border-bottom: 1px solid var(--app-border); }`]
})
export class FinanceShellComponent {}
