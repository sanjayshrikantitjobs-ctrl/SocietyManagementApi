import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ChartConfiguration } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { MatChipsModule } from '@angular/material/chips';
import { MatTableModule } from '@angular/material/table';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { SocietyService } from '../../society-setup/services/society.service';
import { FinanceOverviewDto } from '../models/finance.model';
import { FinanceService } from '../services/finance.service';

@Component({
  selector: 'app-finance-overview',
  standalone: true,
  imports: [CommonModule, BaseChartDirective, MatChipsModule, MatTableModule, SkeletonLoaderComponent, StatCardComponent],
  template: `
    <div class="tab-content">
      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="90" />
      } @else if (data(); as d) {
        <div class="stats-grid">
          <app-stat-card label="Total Income" [value]="'₹' + (d.totalIncome | number)" icon="trending_up" iconColor="#16a34a" iconBg="#ecfdf5" />
          <app-stat-card label="Total Expense" [value]="'₹' + (d.totalExpense | number)" icon="trending_down" iconColor="#dc2626" iconBg="#fef2f2" />
          <app-stat-card label="Available Balance" [value]="'₹' + (d.availableBalance | number)" icon="account_balance_wallet" />
          <app-stat-card label="Pending Collection" [value]="'₹' + (d.pendingCollection | number)" icon="pending_actions" iconColor="#b45309" iconBg="#fffbeb" />
        </div>

        <div class="charts-grid">
          <div class="app-card chart-card">
            <h3>Income vs Expense (6 months)</h3>
            <canvas baseChart [data]="trendData()" [options]="lineOptions" type="line"></canvas>
          </div>
          <div class="app-card chart-card">
            <h3>Income by Source</h3>
            <canvas baseChart [data]="incomeBySourceData()" [options]="doughnutOptions" type="doughnut"></canvas>
          </div>
          <div class="app-card chart-card">
            <h3>Expense by Category</h3>
            <canvas baseChart [data]="expenseByCategoryData()" [options]="barOptions" type="bar"></canvas>
          </div>
        </div>

        <div class="app-card section">
          <h3>Recent Transactions</h3>
          @if (d.recentTransactions.length === 0) {
            <p class="muted">No transactions recorded yet.</p>
          } @else {
            <table mat-table [dataSource]="d.recentTransactions">
              <ng-container matColumnDef="date">
                <th mat-header-cell *matHeaderCellDef>Date</th>
                <td mat-cell *matCellDef="let t">{{ t.date | date: 'mediumDate' }}</td>
              </ng-container>
              <ng-container matColumnDef="type">
                <th mat-header-cell *matHeaderCellDef>Type</th>
                <td mat-cell *matCellDef="let t">
                  <mat-chip-set><mat-chip [class.income]="t.type === 'Income'" [class.expense]="t.type === 'Expense'">{{ t.type }}</mat-chip></mat-chip-set>
                </td>
              </ng-container>
              <ng-container matColumnDef="source">
                <th mat-header-cell *matHeaderCellDef>Source</th>
                <td mat-cell *matCellDef="let t">{{ t.source }}</td>
              </ng-container>
              <ng-container matColumnDef="description">
                <th mat-header-cell *matHeaderCellDef>Description</th>
                <td mat-cell *matCellDef="let t">{{ t.description }}</td>
              </ng-container>
              <ng-container matColumnDef="amount">
                <th mat-header-cell *matHeaderCellDef>Amount</th>
                <td mat-cell *matCellDef="let t" [class.income-amount]="t.type === 'Income'" [class.expense-amount]="t.type === 'Expense'">
                  {{ t.type === 'Income' ? '+' : '-' }}₹{{ t.amount | number }}
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="['date', 'type', 'source', 'description', 'amount']"></tr>
              <tr mat-row *matRowDef="let row; columns: ['date', 'type', 'source', 'description', 'amount'];"></tr>
            </table>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .charts-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .chart-card { padding: 16px; height: 300px; display: flex; flex-direction: column; }
    .chart-card h3 { margin: 0 0 12px; font-size: 14px; }
    .chart-card canvas { flex: 1; max-height: 240px; }
    .section { padding: 20px; }
    .section h3 { margin: 0 0 12px; font-size: 14px; }
    table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 13px; }
    .income { background: #dcfce7 !important; color: #15803d !important; }
    .expense { background: #fee2e2 !important; color: #b91c1c !important; }
    .income-amount { color: #15803d; font-weight: 600; }
    .expense-amount { color: #b91c1c; font-weight: 600; }
  `]
})
export class FinanceOverviewComponent implements OnInit {
  private readonly financeService = inject(FinanceService);
  private readonly societyService = inject(SocietyService);

  readonly loading = signal(true);
  readonly data = signal<FinanceOverviewDto | null>(null);

  readonly lineOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } }
  };
  readonly doughnutOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } }
  };
  readonly barOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }
  };

  readonly trendData = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });
  readonly incomeBySourceData = signal<ChartConfiguration<'doughnut'>['data']>({ labels: [], datasets: [{ data: [] }] });
  readonly expenseByCategoryData = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.financeService.getOverview(societies[0].id).subscribe((data) => {
        this.data.set(data);
        this.applyChartData(data);
        this.loading.set(false);
      });
    });
  }

  private applyChartData(d: FinanceOverviewDto): void {
    this.trendData.set({
      labels: d.monthlyTrend.map((m) => m.monthLabel),
      datasets: [
        { label: 'Income', data: d.monthlyTrend.map((m) => m.income), borderColor: '#16a34a', backgroundColor: '#bbf7d0', tension: 0.3, fill: true },
        { label: 'Expense', data: d.monthlyTrend.map((m) => m.expense), borderColor: '#dc2626', backgroundColor: '#fecaca', tension: 0.3, fill: true }
      ]
    });

    this.incomeBySourceData.set({
      labels: d.incomeBySource.map((s) => s.label),
      datasets: [{ data: d.incomeBySource.map((s) => s.amount), backgroundColor: ['#4f6ef7', '#7c3aed', '#0891b2'] }]
    });

    this.expenseByCategoryData.set({
      labels: d.expenseByCategory.map((c) => c.label),
      datasets: [{ label: 'Expense', data: d.expenseByCategory.map((c) => c.amount), backgroundColor: '#f59e0b' }]
    });
  }
}
