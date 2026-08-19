import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { BUDGET_CATEGORY_LABELS, EXPENSE_STATUS_LABELS, ExpenseApprovalStatus, FestivalExpenseDto } from '../models/festival.model';
import { FestivalService } from '../services/festival.service';
import { ExpenseFormDialogComponent } from './expense-form-dialog.component';

const EDITABLE_STATUSES: ExpenseApprovalStatus[] = [1, 4];

@Component({
  selector: 'app-festival-expenses-tab',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatMenuModule, MatTableModule, DataTableComponent
  ],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Expenses</h3>
        @if (canManage()) {
          <button mat-flat-button color="primary" (click)="addExpense()"><mat-icon>add</mat-icon> Record Expense</button>
        }
      </div>
      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        [showSearch]="false" emptyTitle="No expenses yet" emptyMessage="Record expenses as they occur, then submit them for approval."
        (page)="onPage($event)">
        <table mat-table [dataSource]="expenses()" table>
          <ng-container matColumnDef="category">
            <th mat-header-cell *matHeaderCellDef>Category</th>
            <td mat-cell *matCellDef="let e">
              {{ categoryLabel(e) }}
              @if (e.vendorName) { <br /><span class="muted">{{ e.vendorName }}</span> }
            </td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef>Amount</th>
            <td mat-cell *matCellDef="let e">₹{{ e.amount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="date">
            <th mat-header-cell *matHeaderCellDef>Date</th>
            <td mat-cell *matCellDef="let e">{{ e.expenseDate | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let e">
              <mat-chip-set><mat-chip [class]="'status-' + e.approvalStatus">{{ statusLabel(e.approvalStatus) }}</mat-chip></mat-chip-set>
              @if (e.approvalStatus === 4 && e.rejectionReason) { <div class="muted reason">{{ e.rejectionReason }}</div> }
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let e">
              <button mat-icon-button [matMenuTriggerFor]="menu"><mat-icon>more_vert</mat-icon></button>
              <mat-menu #menu="matMenu">
                @if (canManage() && isEditable(e)) {
                  <button mat-menu-item (click)="editExpense(e)"><mat-icon>edit</mat-icon><span>Edit</span></button>
                  <button mat-menu-item (click)="removeExpense(e)"><mat-icon>delete</mat-icon><span>Delete</span></button>
                }
                @if (canManage() && e.approvalStatus === 1) {
                  <button mat-menu-item (click)="submitExpense(e)"><mat-icon>send</mat-icon><span>Submit for Approval</span></button>
                }
                @if (canApprove() && e.approvalStatus === 2) {
                  <button mat-menu-item (click)="approveExpense(e)"><mat-icon>check_circle</mat-icon><span>Approve</span></button>
                  <button mat-menu-item (click)="rejectExpense(e)"><mat-icon>cancel</mat-icon><span>Reject</span></button>
                }
                @if (canApprove() && e.approvalStatus === 3) {
                  <button mat-menu-item (click)="markPaid(e)"><mat-icon>paid</mat-icon><span>Mark as Paid</span></button>
                }
              </mat-menu>
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
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
    .toolbar h3 { margin: 0; font-size: 15px; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .reason { max-width: 200px; }
    mat-chip.status-1 { --mdc-chip-elevated-container-color: #e2e8f0; }
    mat-chip.status-2 { --mdc-chip-elevated-container-color: #fef3c7; }
    mat-chip.status-3 { --mdc-chip-elevated-container-color: #dbeafe; }
    mat-chip.status-4 { --mdc-chip-elevated-container-color: #fee2e2; }
    mat-chip.status-5 { --mdc-chip-elevated-container-color: #dcfce7; }
  `]
})
export class FestivalExpensesTabComponent implements OnInit {
  festivalId = input.required<number>();
  societyId = input.required<number>();
  canManage = input(false);
  canApprove = input(false);

  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly expenses = signal<FestivalExpenseDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly displayedColumns = ['category', 'amount', 'date', 'status', 'actions'];

  categoryLabel(e: FestivalExpenseDto): string {
    return BUDGET_CATEGORY_LABELS[e.category];
  }

  statusLabel(status: ExpenseApprovalStatus): string {
    return EXPENSE_STATUS_LABELS[status];
  }

  isEditable(e: FestivalExpenseDto): boolean {
    return EDITABLE_STATUSES.includes(e.approvalStatus);
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.festivalService.getExpenses({
      festivalId: this.festivalId(), pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.expenses.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  addExpense(): void {
    const ref = this.dialog.open(ExpenseFormDialogComponent, {
      width: '620px', data: { festivalId: this.festivalId(), societyId: this.societyId(), expense: null }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.createExpense(result).subscribe(() => {
        this.toast.success('Expense recorded.');
        this.load();
      });
    });
  }

  editExpense(expense: FestivalExpenseDto): void {
    const ref = this.dialog.open(ExpenseFormDialogComponent, {
      width: '620px', data: { festivalId: this.festivalId(), societyId: this.societyId(), expense }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.updateExpense(expense.id, result).subscribe(() => {
        this.toast.success('Expense updated.');
        this.load();
      });
    });
  }

  removeExpense(expense: FestivalExpenseDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Expense', destructive: true, message: `Delete this expense of ₹${expense.amount}?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.festivalService.deleteExpense(expense.id).subscribe(() => {
        this.toast.success('Expense deleted.');
        this.load();
      });
    });
  }

  submitExpense(expense: FestivalExpenseDto): void {
    this.festivalService.submitExpense(expense.id).subscribe(() => {
      this.toast.success('Expense submitted for approval.');
      this.load();
    });
  }

  approveExpense(expense: FestivalExpenseDto): void {
    this.festivalService.approveExpense(expense.id).subscribe(() => {
      this.toast.success('Expense approved.');
      this.load();
    });
  }

  rejectExpense(expense: FestivalExpenseDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: { title: 'Reject Expense', submitLabel: 'Reject', fields: [{ key: 'reason', label: 'Reason', type: 'textarea' }] }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.rejectExpense(expense.id, result.reason).subscribe(() => {
        this.toast.success('Expense rejected.');
        this.load();
      });
    });
  }

  markPaid(expense: FestivalExpenseDto): void {
    this.festivalService.markExpensePaid(expense.id).subscribe(() => {
      this.toast.success('Expense marked as paid.');
      this.load();
    });
  }
}
