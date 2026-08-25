import { CommonModule } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration } from 'chart.js';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AdminDashboardSummary, MonthlyCollectionPoint, RecentActivityItem, UpcomingItems } from '../../../core/models/dashboard.model';
import { ComplaintKpisDto } from '../../complaints/models/complaint.model';
import { WaterTankerMonthSummaryDto } from '../../maintenance/models/maintenance.model';
import { CurrentSocietyService } from '../../../core/services/current-society.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { ComplaintService } from '../../complaints/services/complaint.service';
import { MaintenanceService } from '../../maintenance/services/maintenance.service';
import { DashboardService } from '../dashboard.service';

/** Admin dashboard — every number scoped to the caller's own society (see
 * GetAdminDashboardSummaryQuery's doc comment: this used to be a
 * system-wide count across every society, a real cross-tenant leak for a
 * scoped Admin, fixed alongside this redesign). */
@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    CommonModule, RouterLink, MatButtonModule, MatIconModule, BaseChartDirective,
    PageHeaderComponent, StatCardComponent, SkeletonLoaderComponent
  ],
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.scss'
})
export class AdminDashboardComponent {
  private readonly dashboardService = inject(DashboardService);
  private readonly complaintService = inject(ComplaintService);
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly currentSociety = inject(CurrentSocietyService);

  readonly loading = signal(true);
  readonly summary = signal<AdminDashboardSummary | null>(null);
  readonly upcoming = signal<UpcomingItems | null>(null);
  readonly recentActivity = signal<RecentActivityItem[]>([]);
  readonly complaintKpis = signal<ComplaintKpisDto | null>(null);
  readonly waterTanker = signal<WaterTankerMonthSummaryDto | null>(null);

  readonly occupancyChartData: ChartConfiguration<'doughnut'>['data'] = {
    labels: ['Occupied', 'Vacant'],
    datasets: [{ data: [0, 0], backgroundColor: ['#4f6ef7', '#e5e9f0'] }]
  };
  readonly occupancyChartOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    plugins: { legend: { position: 'bottom' } }
  };

  readonly parkingChartData: ChartConfiguration<'bar'>['data'] = {
    labels: ['Allocated', 'Vacant'],
    datasets: [{ label: 'Parking Slots', data: [0, 0], backgroundColor: ['#4f6ef7', '#93c5fd'] }]
  };
  readonly parkingChartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    plugins: { legend: { display: false } },
    scales: { y: { beginAtZero: true, ticks: { stepSize: 1 } } }
  };

  readonly collectionChartData: ChartConfiguration<'bar'>['data'] = {
    labels: [],
    datasets: [
      { label: 'Collected', data: [], backgroundColor: '#16a34a' },
      { label: 'Pending', data: [], backgroundColor: '#f59e0b' }
    ]
  };
  readonly collectionChartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    plugins: { legend: { position: 'bottom' } },
    scales: { y: { beginAtZero: true } }
  };

  readonly complaintChartData: ChartConfiguration<'doughnut'>['data'] = {
    labels: ['Open', 'Assigned', 'In Progress', 'Resolved', 'Closed'],
    datasets: [{ data: [0, 0, 0, 0, 0], backgroundColor: ['#94a3b8', '#f59e0b', '#4f6ef7', '#16a34a', '#e5e9f0'] }]
  };
  readonly complaintChartOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    plugins: { legend: { position: 'bottom' } }
  };

  private loadedForSocietyId: number | null = null;

  constructor() {
    effect(() => {
      const societyId = this.currentSociety.society()?.id;
      if (societyId && societyId !== this.loadedForSocietyId) {
        this.loadedForSocietyId = societyId;
        this.load(societyId);
      }
    });
  }

  private load(societyId: number): void {
    this.loading.set(true);

    this.dashboardService.getAdminSummary(societyId).subscribe((summary) => {
      this.summary.set(summary);
      this.occupancyChartData.datasets[0].data = [summary.occupiedFlats, summary.totalFlats - summary.occupiedFlats];
      this.loading.set(false);
    });

    this.dashboardService.getMonthlyCollectionTrend(societyId).subscribe((points) => {
      this.collectionChartData.labels = points.map((p) => p.monthLabel);
      this.collectionChartData.datasets[0].data = points.map((p) => p.collected);
      this.collectionChartData.datasets[1].data = points.map((p) => p.pending);
    });

    this.dashboardService.getUpcoming(societyId).subscribe((upcoming) => this.upcoming.set(upcoming));
    this.dashboardService.getRecentActivity(societyId).subscribe((items) => this.recentActivity.set(items));

    this.complaintService.getKpis(societyId).subscribe((kpis) => {
      this.complaintKpis.set(kpis);
      this.complaintChartData.datasets[0].data = [kpis.open, kpis.assigned, kpis.inProgress, kpis.resolved, kpis.closed];
    });

    const currentMonth = new Date().toISOString().substring(0, 7) + '-01';
    this.maintenanceService.getWaterTankerSummary(societyId, currentMonth).subscribe((summary) => this.waterTanker.set(summary));
  }

  activityIcon(type: string): string {
    switch (type) {
      case 'payment': return 'payments';
      case 'complaint': return 'report_problem';
      case 'visitor': return 'badge';
      case 'watertanker': return 'water_drop';
      default: return 'circle';
    }
  }
}
