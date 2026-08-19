import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration } from 'chart.js';
import { MatButtonModule } from '@angular/material/button';
import { AdminDashboardSummary } from '../../../core/models/dashboard.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { DashboardService } from '../dashboard.service';

/**
 * Admin dashboard — cards + charts per spec. Only the counters this
 * Foundation phase actually has data for are wired to the API
 * (Users/Flats/Parking); the rest render with clear "coming soon" framing so
 * the layout for the full spec'd dashboard exists now and each card lights
 * up in place as Maintenance/Festivals/Complaints/Visitors modules ship —
 * no layout rework needed later.
 */
@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, BaseChartDirective, PageHeaderComponent, StatCardComponent, SkeletonLoaderComponent],
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.scss'
})
export class AdminDashboardComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);

  readonly loading = signal(true);
  readonly summary = signal<AdminDashboardSummary | null>(null);

  readonly occupancyChartData: ChartConfiguration<'doughnut'>['data'] = {
    labels: ['Occupied', 'Vacant'],
    datasets: [{ data: [0, 0], backgroundColor: ['#1a56db', '#e5e9f0'] }]
  };
  readonly occupancyChartOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    plugins: { legend: { position: 'bottom' } }
  };

  readonly parkingChartData: ChartConfiguration<'bar'>['data'] = {
    labels: ['Allocated', 'Vacant'],
    datasets: [{ label: 'Parking Slots', data: [0, 0], backgroundColor: ['#1a56db', '#93c5fd'] }]
  };
  readonly parkingChartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    plugins: { legend: { display: false } },
    scales: { y: { beginAtZero: true, ticks: { stepSize: 1 } } }
  };

  ngOnInit(): void {
    this.dashboardService.getAdminSummary().subscribe((summary) => {
      this.summary.set(summary);
      this.occupancyChartData.datasets[0].data = [summary.occupiedFlats, summary.vacantFlats];
      this.parkingChartData.datasets[0].data = [summary.allocatedParkingSlots,
        summary.totalParkingSlots - summary.allocatedParkingSlots];
      this.loading.set(false);
    });
  }
}
