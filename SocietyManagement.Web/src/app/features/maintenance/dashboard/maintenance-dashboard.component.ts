import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ChartConfiguration } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { MatChipsModule } from '@angular/material/chips';
import { MatTableModule } from '@angular/material/table';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { Society } from '../../../core/models/society.model';
import { SocietyService } from '../../society-setup/services/society.service';
import { MaintenanceDashboardDto, PAYMENT_MODE_LABELS } from '../models/maintenance.model';
import { MaintenanceService } from '../services/maintenance.service';

@Component({
  selector: 'app-maintenance-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, BaseChartDirective, MatChipsModule, MatTableModule, SkeletonLoaderComponent, StatCardComponent],
  template: `
    <div class="tab-content">
      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="90" />
      } @else if (data(); as d) {
        <div class="stats-grid">
          <app-stat-card label="Total Flats" [value]="d.kpis.totalFlats" icon="apartment" />
          <app-stat-card label="Bills Generated" [value]="d.kpis.billsGenerated" icon="receipt_long" subtext="this month" />
          <app-stat-card label="Paid" [value]="d.kpis.paid" icon="check_circle" iconColor="#16a34a" iconBg="#ecfdf5" />
          <app-stat-card label="Pending" [value]="d.kpis.pending" icon="pending_actions" iconColor="#b45309" iconBg="#fffbeb" />
          <app-stat-card label="Overdue" [value]="d.kpis.overdue" icon="warning" iconColor="#dc2626" iconBg="#fef2f2" />
          <app-stat-card label="Total Collection" [value]="'₹' + (d.kpis.totalCollection | number)" icon="payments" />
          <app-stat-card label="Outstanding" [value]="'₹' + (d.kpis.outstanding | number)" icon="account_balance_wallet" iconColor="#dc2626" iconBg="#fef2f2" />
        </div>

        <div class="charts-grid">
          <div class="app-card chart-card">
            <h3>Monthly Collection Trend</h3>
            <canvas baseChart [data]="collectionTrendData()" [options]="lineOptions" type="line"></canvas>
          </div>
          <div class="app-card chart-card">
            <h3>Paid vs Pending</h3>
            <canvas baseChart [data]="paidVsPendingData()" [options]="doughnutOptions" type="doughnut"></canvas>
          </div>
          <div class="app-card chart-card">
            <h3>Outstanding by Wing</h3>
            <canvas baseChart [data]="outstandingByWingData()" [options]="barOptions" type="bar"></canvas>
          </div>
        </div>

        <div class="tables-grid">
          <div class="app-card section">
            <h3>Recent Payments</h3>
            @if (d.recentPayments.length === 0) {
              <p class="muted">No payments recorded yet.</p>
            } @else {
              <table mat-table [dataSource]="d.recentPayments">
                <ng-container matColumnDef="flat">
                  <th mat-header-cell *matHeaderCellDef>Flat</th>
                  <td mat-cell *matCellDef="let p">{{ p.flatNumber }}</td>
                </ng-container>
                <ng-container matColumnDef="amount">
                  <th mat-header-cell *matHeaderCellDef>Amount</th>
                  <td mat-cell *matCellDef="let p">₹{{ p.amount | number }}</td>
                </ng-container>
                <ng-container matColumnDef="mode">
                  <th mat-header-cell *matHeaderCellDef>Mode</th>
                  <td mat-cell *matCellDef="let p"><mat-chip-set><mat-chip>{{ paymentModeLabels[p.paymentMode] }}</mat-chip></mat-chip-set></td>
                </ng-container>
                <ng-container matColumnDef="date">
                  <th mat-header-cell *matHeaderCellDef>Date</th>
                  <td mat-cell *matCellDef="let p">{{ p.paymentDate | date: 'mediumDate' }}</td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="['flat', 'amount', 'mode', 'date']"></tr>
                <tr mat-row *matRowDef="let row; columns: ['flat', 'amount', 'mode', 'date'];"></tr>
              </table>
            }
          </div>

          <div class="app-card section">
            <h3>Overdue Flats</h3>
            @if (d.overdueFlats.length === 0) {
              <p class="muted">No overdue bills. 🎉</p>
            } @else {
              <table mat-table [dataSource]="d.overdueFlats">
                <ng-container matColumnDef="flat">
                  <th mat-header-cell *matHeaderCellDef>Flat</th>
                  <td mat-cell *matCellDef="let o"><a [routerLink]="['/maintenance/bills', o.billId]">{{ o.flatNumber }}</a></td>
                </ng-container>
                <ng-container matColumnDef="invoice">
                  <th mat-header-cell *matHeaderCellDef>Invoice</th>
                  <td mat-cell *matCellDef="let o">{{ o.invoiceNumber }}</td>
                </ng-container>
                <ng-container matColumnDef="balance">
                  <th mat-header-cell *matHeaderCellDef>Balance</th>
                  <td mat-cell *matCellDef="let o">₹{{ o.balance | number }}</td>
                </ng-container>
                <ng-container matColumnDef="days">
                  <th mat-header-cell *matHeaderCellDef>Days Overdue</th>
                  <td mat-cell *matCellDef="let o"><span class="overdue-days">{{ o.daysOverdue }}</span></td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="['flat', 'invoice', 'balance', 'days']"></tr>
                <tr mat-row *matRowDef="let row; columns: ['flat', 'invoice', 'balance', 'days'];"></tr>
              </table>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .charts-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .chart-card { padding: 16px; height: 300px; display: flex; flex-direction: column; }
    .chart-card h3 { margin: 0 0 12px; font-size: 14px; }
    .chart-card canvas { flex: 1; max-height: 240px; }
    .tables-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(360px, 1fr)); gap: 16px; }
    .section { padding: 20px; }
    .section h3 { margin: 0 0 12px; font-size: 14px; }
    table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 13px; }
    .overdue-days { color: #dc2626; font-weight: 700; }
    a { color: var(--app-primary); text-decoration: none; }
  `]
})
export class MaintenanceDashboardComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly societyService = inject(SocietyService);

  readonly loading = signal(true);
  readonly data = signal<MaintenanceDashboardDto | null>(null);
  readonly paymentModeLabels: Record<number, string> = PAYMENT_MODE_LABELS;

  readonly lineOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }
  };
  readonly doughnutOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } }
  };
  readonly barOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }
  };

  readonly collectionTrendData = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });
  readonly paidVsPendingData = signal<ChartConfiguration<'doughnut'>['data']>({ labels: [], datasets: [{ data: [] }] });
  readonly outstandingByWingData = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies: Society[]) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.maintenanceService.getDashboard(societies[0].id).subscribe((data) => {
        this.data.set(data);
        this.applyChartData(data);
        this.loading.set(false);
      });
    });
  }

  private applyChartData(d: MaintenanceDashboardDto): void {
    this.collectionTrendData.set({
      labels: d.monthlyCollectionTrend.map((m) => m.monthLabel),
      datasets: [{ label: 'Collected', data: d.monthlyCollectionTrend.map((m) => m.amount), borderColor: '#4f6ef7', backgroundColor: '#93c5fd', tension: 0.3, fill: true }]
    });

    this.paidVsPendingData.set({
      labels: ['Paid', 'Outstanding'],
      datasets: [{ data: [d.paidVsPending.paidAmount, d.paidVsPending.outstandingAmount], backgroundColor: ['#16a34a', '#e5e9f0'] }]
    });

    this.outstandingByWingData.set({
      labels: d.outstandingByWing.map((w) => w.wingName),
      datasets: [{ label: 'Outstanding', data: d.outstandingByWing.map((w) => w.outstanding), backgroundColor: '#f59e0b' }]
    });
  }
}
