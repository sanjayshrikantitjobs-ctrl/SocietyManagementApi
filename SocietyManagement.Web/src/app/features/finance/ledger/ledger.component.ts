import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { SocietyService } from '../../society-setup/services/society.service';
import { FinanceLedgerPageDto } from '../models/finance.model';
import { FinanceService } from '../services/finance.service';

@Component({
  selector: 'app-ledger',
  standalone: true,
  imports: [CommonModule, FormsModule, MatChipsModule, MatFormFieldModule, MatInputModule, MatTableModule, DataTableComponent, SkeletonLoaderComponent],
  template: `
    <div class="tab-content">
      <div class="filters">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>From</mat-label>
          <input matInput type="date" [(ngModel)]="dateFrom" (change)="onFilterChange()" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>To</mat-label>
          <input matInput type="date" [(ngModel)]="dateTo" (change)="onFilterChange()" />
        </mat-form-field>
        @if (page(); as p) {
          <div class="opening-balance">Opening Balance: <strong>₹{{ p.openingBalance | number }}</strong></div>
        }
      </div>

      @if (loading()) {
        <app-skeleton-loader [rows]="5" />
      } @else {
        <app-data-table
          [loading]="false" [totalCount]="page()?.totalCount ?? 0" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
          [showSearch]="false" emptyIcon="account_balance" emptyTitle="No transactions"
          emptyMessage="Income and expenses will appear here as they're recorded."
          (page)="onPage($event)">
          <table mat-table [dataSource]="page()?.items ?? []" table>
            <ng-container matColumnDef="date">
              <th mat-header-cell *matHeaderCellDef>Date</th>
              <td mat-cell *matCellDef="let r">{{ r.date | date: 'mediumDate' }}</td>
            </ng-container>
            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef>Type</th>
              <td mat-cell *matCellDef="let r">
                <mat-chip-set><mat-chip [class.income]="r.type === 'Income'" [class.expense]="r.type === 'Expense'">{{ r.type }}</mat-chip></mat-chip-set>
              </td>
            </ng-container>
            <ng-container matColumnDef="source">
              <th mat-header-cell *matHeaderCellDef>Source</th>
              <td mat-cell *matCellDef="let r">{{ r.source }}</td>
            </ng-container>
            <ng-container matColumnDef="description">
              <th mat-header-cell *matHeaderCellDef>Description</th>
              <td mat-cell *matCellDef="let r">{{ r.description }}</td>
            </ng-container>
            <ng-container matColumnDef="amount">
              <th mat-header-cell *matHeaderCellDef>Amount</th>
              <td mat-cell *matCellDef="let r" [class.income-amount]="r.amount >= 0" [class.expense-amount]="r.amount < 0">
                {{ r.amount >= 0 ? '+' : '' }}₹{{ r.amount | number }}
              </td>
            </ng-container>
            <ng-container matColumnDef="balance">
              <th mat-header-cell *matHeaderCellDef>Running Balance</th>
              <td mat-cell *matCellDef="let r"><strong>₹{{ r.runningBalance | number }}</strong></td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
          </table>
        </app-data-table>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .filters { display: flex; gap: 12px; margin-bottom: 16px; align-items: center; flex-wrap: wrap; }
    .filters mat-form-field { width: 180px; }
    .opening-balance { font-size: 13px; color: var(--app-text-muted); margin-left: auto; }
    table { width: 100%; }
    .income { background: #dcfce7 !important; color: #15803d !important; }
    .expense { background: #fee2e2 !important; color: #b91c1c !important; }
    .income-amount { color: #15803d; font-weight: 600; }
    .expense-amount { color: #b91c1c; font-weight: 600; }
  `]
})
export class LedgerComponent implements OnInit {
  private readonly financeService = inject(FinanceService);
  private readonly societyService = inject(SocietyService);

  readonly loading = signal(true);
  readonly page = signal<FinanceLedgerPageDto | null>(null);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);
  readonly displayedColumns = ['date', 'type', 'source', 'description', 'amount', 'balance'];

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
    this.financeService.getLedger({
      societyId: this.societyId, dateFrom: this.dateFrom || undefined, dateTo: this.dateTo || undefined,
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.page.set(result);
      this.loading.set(false);
    });
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  onFilterChange(): void {
    this.pageIndex.set(0);
    this.load();
  }
}
