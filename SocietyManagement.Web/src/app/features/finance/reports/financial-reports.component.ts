import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { SocietyService } from '../../society-setup/services/society.service';
import { FinanceReportSummaryDto } from '../models/finance.model';
import { FinanceService } from '../services/finance.service';

@Component({
  selector: 'app-financial-reports',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatTableModule, SkeletonLoaderComponent, StatCardComponent],
  template: `
    <div class="tab-content">
      <div class="filters">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>From</mat-label>
          <input matInput type="date" [(ngModel)]="dateFrom" (change)="load()" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>To</mat-label>
          <input matInput type="date" [(ngModel)]="dateTo" (change)="load()" />
        </mat-form-field>
        <span class="spacer"></span>
        <button mat-stroked-button (click)="exportPdf()"><mat-icon>picture_as_pdf</mat-icon> Export PDF</button>
        <button mat-stroked-button (click)="exportExcel()"><mat-icon>grid_on</mat-icon> Export Excel</button>
      </div>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="90" />
      } @else if (summary(); as s) {
        <div class="stats-grid">
          <app-stat-card label="Total Income" [value]="'₹' + (s.totalIncome | number)" icon="trending_up" iconColor="#16a34a" iconBg="#ecfdf5" />
          <app-stat-card label="Total Expense" [value]="'₹' + (s.totalExpense | number)" icon="trending_down" iconColor="#dc2626" iconBg="#fef2f2" />
          <app-stat-card label="Net Balance" [value]="'₹' + (s.netBalance | number)" icon="account_balance" />
        </div>

        <div class="tables-grid">
          <div class="app-card section">
            <h3>Income by Source</h3>
            <table mat-table [dataSource]="s.incomeBySource">
              <ng-container matColumnDef="label">
                <th mat-header-cell *matHeaderCellDef>Source</th>
                <td mat-cell *matCellDef="let l">{{ l.label }}</td>
              </ng-container>
              <ng-container matColumnDef="amount">
                <th mat-header-cell *matHeaderCellDef>Amount</th>
                <td mat-cell *matCellDef="let l">₹{{ l.amount | number }}</td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="['label', 'amount']"></tr>
              <tr mat-row *matRowDef="let row; columns: ['label', 'amount'];"></tr>
            </table>
          </div>

          <div class="app-card section">
            <h3>Expense by Category</h3>
            @if (s.expenseByCategory.length === 0) {
              <p class="muted">No expenses in this period.</p>
            } @else {
              <table mat-table [dataSource]="s.expenseByCategory">
                <ng-container matColumnDef="label">
                  <th mat-header-cell *matHeaderCellDef>Category</th>
                  <td mat-cell *matCellDef="let l">{{ l.label }}</td>
                </ng-container>
                <ng-container matColumnDef="amount">
                  <th mat-header-cell *matHeaderCellDef>Amount</th>
                  <td mat-cell *matCellDef="let l">₹{{ l.amount | number }}</td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="['label', 'amount']"></tr>
                <tr mat-row *matRowDef="let row; columns: ['label', 'amount'];"></tr>
              </table>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .filters { display: flex; gap: 12px; align-items: center; margin-bottom: 20px; flex-wrap: wrap; }
    .filters mat-form-field { width: 180px; }
    .spacer { flex: 1; }
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .tables-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; }
    .section { padding: 20px; }
    .section h3 { margin: 0 0 12px; font-size: 14px; }
    table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 13px; }
  `]
})
export class FinancialReportsComponent implements OnInit {
  private readonly financeService = inject(FinanceService);
  private readonly societyService = inject(SocietyService);

  readonly loading = signal(true);
  readonly summary = signal<FinanceReportSummaryDto | null>(null);

  dateFrom = '';
  dateTo = '';

  private societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.financeService.getReportSummary(this.societyId, this.dateFrom || undefined, this.dateTo || undefined)
      .subscribe((result) => {
        this.summary.set(result);
        this.loading.set(false);
      });
  }

  exportPdf(): void {
    this.financeService.exportReportPdf(this.societyId, this.dateFrom || undefined, this.dateTo || undefined)
      .subscribe((blob) => this.downloadBlob(blob, 'financial-report.pdf'));
  }

  exportExcel(): void {
    this.financeService.exportReportExcel(this.societyId, this.dateFrom || undefined, this.dateTo || undefined)
      .subscribe((blob) => this.downloadBlob(blob, 'financial-report.xlsx'));
  }

  private downloadBlob(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    window.URL.revokeObjectURL(url);
  }
}
