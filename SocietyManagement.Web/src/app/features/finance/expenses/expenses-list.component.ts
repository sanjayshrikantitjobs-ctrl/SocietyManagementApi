import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { SocietyService } from '../../society-setup/services/society.service';
import { ExpenseFormDialogComponent } from './expense-form-dialog.component';
import { EXPENSE_CATEGORY_LABELS, FINANCE_SOURCE_LABELS, FinanceExpenseRowDto } from '../models/finance.model';
import { FinanceService } from '../services/finance.service';

@Component({
  selector: 'app-expenses-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatChipsModule, MatFormFieldModule, MatIconModule,
    MatSelectModule, MatTableModule, MatTooltipModule, DataTableComponent
  ],
  template: `
    <div class="tab-content">
      <div class="toolbar-row">
        <div class="filters">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Category</mat-label>
            <mat-select [(ngModel)]="categoryFilter" (selectionChange)="onFilterChange()">
              <mat-option [value]="null">All</mat-option>
              @for (opt of categoryOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
        </div>
        <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> Add Expense</button>
      </div>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search title or paid to..." emptyIcon="receipt_long" emptyTitle="No expenses recorded"
        emptyMessage="Add electricity, repairs, vendor payments, or staff salary payouts to get started."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="rows()" table>
          <ng-container matColumnDef="date">
            <th mat-header-cell *matHeaderCellDef>Date</th>
            <td mat-cell *matCellDef="let r">{{ r.expenseDate | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="category">
            <th mat-header-cell *matHeaderCellDef>Category</th>
            <td mat-cell *matCellDef="let r"><mat-chip-set><mat-chip>{{ r.categoryLabel }}</mat-chip></mat-chip-set></td>
          </ng-container>
          <ng-container matColumnDef="title">
            <th mat-header-cell *matHeaderCellDef>Title</th>
            <td mat-cell *matCellDef="let r">
              {{ r.title }}
              @if (r.source === 2) { <span class="muted">({{ sourceLabels[r.source] }})</span> }
            </td>
          </ng-container>
          <ng-container matColumnDef="paidTo">
            <th mat-header-cell *matHeaderCellDef>Paid To</th>
            <td mat-cell *matCellDef="let r">{{ r.paidTo ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="method">
            <th mat-header-cell *matHeaderCellDef>Method</th>
            <td mat-cell *matCellDef="let r">{{ r.paymentMethod }}</td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef>Amount</th>
            <td mat-cell *matCellDef="let r" class="expense-amount">-₹{{ r.amount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let r">
              @if (r.source === 4) {
                <button mat-icon-button matTooltip="Edit" (click)="edit(r)"><mat-icon>edit</mat-icon></button>
                <button mat-icon-button matTooltip="Delete" (click)="remove(r)"><mat-icon>delete_outline</mat-icon></button>
              } @else {
                <button mat-icon-button matTooltip="View in Festival" (click)="viewFestival(r)"><mat-icon>open_in_new</mat-icon></button>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .toolbar-row { display: flex; justify-content: space-between; align-items: flex-end; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; }
    .filters mat-form-field { width: 200px; }
    table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .expense-amount { color: #b91c1c; font-weight: 600; }
  `]
})
export class ExpensesListComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly financeService = inject(FinanceService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly rows = signal<FinanceExpenseRowDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly displayedColumns = ['date', 'category', 'title', 'paidTo', 'method', 'amount', 'actions'];
  readonly sourceLabels: Record<number, string> = FINANCE_SOURCE_LABELS;
  readonly categoryOptions = Object.entries(EXPENSE_CATEGORY_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  categoryFilter: number | null = null;

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
    this.financeService.getExpenses({
      societyId: this.societyId, category: this.categoryFilter ?? undefined, search: this.searchTerm() || undefined,
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.rows.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.pageIndex.set(0);
    this.load();
  }

  onFilterChange(): void {
    this.pageIndex.set(0);
    this.load();
  }

  add(): void {
    const ref = this.dialog.open(ExpenseFormDialogComponent, { data: { expense: null, societyId: this.societyId } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.financeService.createExpense({ ...result, societyId: this.societyId }).subscribe(() => {
        this.toast.success('Expense recorded.');
        this.load();
      });
    });
  }

  edit(row: FinanceExpenseRowDto): void {
    this.financeService.getExpenseById(row.id).subscribe((expense) => {
      const ref = this.dialog.open(ExpenseFormDialogComponent, { data: { expense, societyId: this.societyId } });
      ref.afterClosed().subscribe((result) => {
        if (!result) return;
        this.financeService.updateExpense(row.id, result).subscribe(() => {
          this.toast.success('Expense updated.');
          this.load();
        });
      });
    });
  }

  remove(row: FinanceExpenseRowDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Expense', destructive: true, message: `Delete "${row.title}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.financeService.deleteExpense(row.id).subscribe(() => {
        this.toast.success('Expense deleted.');
        this.load();
      });
    });
  }

  viewFestival(row: FinanceExpenseRowDto): void {
    if (row.festivalId) this.router.navigate(['/festivals', row.festivalId]);
  }
}
