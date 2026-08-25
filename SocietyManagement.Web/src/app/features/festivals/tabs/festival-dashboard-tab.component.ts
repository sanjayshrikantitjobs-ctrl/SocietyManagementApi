import { CommonModule } from '@angular/common';
import { Component, OnInit, effect, inject, input, signal } from '@angular/core';
import { ChartConfiguration } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { SignalrService } from '../../../core/services/signalr.service';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { FestivalDashboardDto } from '../models/festival.model';
import { FestivalService } from '../services/festival.service';

/** Festival Command Center — KPI cards + the 4 charts (Budget vs Actual,
 * Collection Progress, Expense Category, Sponsor Contribution). */
@Component({
  selector: 'app-festival-dashboard-tab',
  standalone: true,
  imports: [CommonModule, BaseChartDirective, SkeletonLoaderComponent, StatCardComponent],
  template: `
    <div class="tab-content">
      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="90" />
      } @else if (data(); as d) {
        <div class="stats-grid">
          <app-stat-card label="Budget" [value]="'₹' + (d.kpis.budget | number)" icon="account_balance_wallet" />
          <app-stat-card label="Collected" [value]="'₹' + (d.kpis.collected | number)" icon="payments" iconColor="#16a34a" iconBg="#ecfdf5" />
          <app-stat-card label="Spent" [value]="'₹' + (d.kpis.spent | number)" icon="shopping_cart" iconColor="#f59e0b" iconBg="#fffbeb" />
          <app-stat-card label="Remaining" [value]="'₹' + (d.kpis.remaining | number)" icon="savings" />
          <app-stat-card label="Sponsors" [value]="d.kpis.sponsorsCount" icon="handshake" />
          <app-stat-card label="Volunteers" [value]="d.kpis.volunteersCount" icon="groups" />
          <app-stat-card label="Pending Expenses" [value]="d.kpis.pendingExpensesCount" icon="pending_actions" iconColor="#dc2626" iconBg="#fef2f2" />
          <app-stat-card label="Tasks Pending" [value]="d.kpis.tasksPendingCount" icon="checklist" />
        </div>

        <div class="charts-grid">
          <div class="app-card chart-card">
            <h3>Budget vs Actual</h3>
            <canvas baseChart [data]="budgetVsActualData()" [options]="barOptions" type="bar"></canvas>
          </div>
          <div class="app-card chart-card">
            <h3>Collection Progress</h3>
            <canvas baseChart [data]="collectionProgressData()" [options]="doughnutOptions" type="doughnut"></canvas>
          </div>
          <div class="app-card chart-card">
            <h3>Expense by Category</h3>
            <canvas baseChart [data]="expenseCategoryData()" [options]="doughnutOptions" type="doughnut"></canvas>
          </div>
          <div class="app-card chart-card">
            <h3>Sponsor Contribution</h3>
            <canvas baseChart [data]="sponsorData()" [options]="barOptions" type="bar"></canvas>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .charts-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); gap: 16px; }
    .chart-card { padding: 16px; height: 320px; display: flex; flex-direction: column; }
    .chart-card h3 { margin: 0 0 12px; font-size: 14px; }
    .chart-card canvas { flex: 1; max-height: 260px; }
  `]
})
export class FestivalDashboardTabComponent implements OnInit {
  festivalId = input.required<number>();

  private readonly festivalService = inject(FestivalService);
  private readonly signalr = inject(SignalrService);

  readonly loading = signal(true);
  readonly data = signal<FestivalDashboardDto | null>(null);

  readonly barOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } }
  };
  readonly doughnutOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } }
  };

  readonly budgetVsActualData = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });
  readonly collectionProgressData = signal<ChartConfiguration<'doughnut'>['data']>({ labels: [], datasets: [{ data: [] }] });
  readonly expenseCategoryData = signal<ChartConfiguration<'doughnut'>['data']>({ labels: [], datasets: [{ data: [] }] });
  readonly sponsorData = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });

  constructor() {
    effect(() => {
      const latest = this.signalr.notifications()[0];
      if (latest && (latest.eventName === 'FestivalContributionRecorded' || latest.eventName === 'FestivalExpenseApproved')) {
        const payload = latest.payload as { festivalId?: number };
        if (payload?.festivalId === this.festivalId()) {
          this.load();
        }
      }
    });
  }

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.festivalService.getDashboard(this.festivalId()).subscribe((data) => {
      this.data.set(data);
      this.applyChartData(data);
      this.loading.set(false);
    });
  }

  private applyChartData(d: FestivalDashboardDto): void {
    this.budgetVsActualData.set({
      labels: d.budgetVsActual.map((c) => c.categoryName),
      datasets: [
        { label: 'Estimated', data: d.budgetVsActual.map((c) => c.estimated), backgroundColor: '#93c5fd' },
        { label: 'Approved', data: d.budgetVsActual.map((c) => c.approved), backgroundColor: '#4f6ef7' },
        { label: 'Actual', data: d.budgetVsActual.map((c) => c.actual), backgroundColor: '#f59e0b' }
      ]
    });

    const remainingTarget = Math.max(d.kpis.budget - d.kpis.collected, 0);
    this.collectionProgressData.set({
      labels: ['Collected', 'Remaining Target'],
      datasets: [{ data: [d.kpis.collected, remainingTarget], backgroundColor: ['#16a34a', '#e5e9f0'] }]
    });

    this.expenseCategoryData.set({
      labels: d.expenseByCategory.map((c) => c.categoryName),
      datasets: [{
        data: d.expenseByCategory.map((c) => c.amount),
        backgroundColor: ['#4f6ef7', '#16a34a', '#f59e0b', '#dc2626', '#8b5cf6', '#ec4899', '#0891b2', '#65a30d', '#ea580c', '#475569', '#d946ef', '#0d9488']
      }]
    });

    this.sponsorData.set({
      labels: d.sponsorContributions.map((s) => s.companyName),
      datasets: [
        { label: 'Promised', data: d.sponsorContributions.map((s) => s.promised), backgroundColor: '#93c5fd' },
        { label: 'Received', data: d.sponsorContributions.map((s) => s.received), backgroundColor: '#16a34a' }
      ]
    });
  }
}
