import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MemberDashboardSummary } from '../../../core/models/dashboard.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { DashboardService } from '../dashboard.service';

@Component({
  selector: 'app-member-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, PageHeaderComponent, StatCardComponent, SkeletonLoaderComponent],
  template: `
    <div class="app-page">
      <app-page-header title="My Dashboard" subtitle="Your maintenance, notices and events at a glance"
                        [breadcrumbs]="[{ label: 'Dashboard' }]" />

      @if (loading()) {
        <app-skeleton-loader [rows]="4" [height]="90" />
      } @else if (summary(); as s) {
        <div class="stats-grid">
          <app-stat-card label="My Maintenance Due" [value]="'₹' + (s.myMaintenanceDue | number)" icon="payments" />
          <app-stat-card label="Unread Notices" value="Coming soon" subtext="Notice Board module" icon="campaign" />
          <app-stat-card label="Upcoming Events" [value]="s.upcomingEventsCount" icon="event" iconColor="#16a34a" iconBg="#ecfdf5" />
          <app-stat-card label="Open Complaints" value="Coming soon" subtext="Complaint Management module" icon="support_agent" />
        </div>
        <div class="app-card notice-card">
          <p>View your maintenance bills, download invoices and check payment history on
          <a routerLink="/my-bills">My Bills</a>. Festival contributions and complaint history will appear here as those modules ship.</p>
        </div>
      }
    </div>
  `,
  styles: [`
    .stats-grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(220px,1fr)); gap:16px; margin-bottom:24px; }
    .notice-card { padding:20px; color: var(--app-text-muted); font-size:14px; }
  `]
})
export class MemberDashboardComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  readonly loading = signal(true);
  readonly summary = signal<MemberDashboardSummary | null>(null);

  ngOnInit(): void {
    this.dashboardService.getMemberSummary().subscribe((summary) => {
      this.summary.set(summary);
      this.loading.set(false);
    });
  }
}
