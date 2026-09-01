import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { SelectionModel } from '@angular/cdk/collections';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { Society } from '../../../core/models/society.model';
import { SocietyService } from '../../society-setup/services/society.service';
import { BILL_STATUS_LABELS, BillStatus, MaintenanceBillDto } from '../models/maintenance.model';
import { MaintenanceService } from '../services/maintenance.service';

@Component({
  selector: 'app-maintenance-bills-list',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatCheckboxModule, MatChipsModule, MatDatepickerModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatMenuModule, MatSelectModule, MatTableModule, DataTableComponent
  ],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <div class="filters">
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="month-field">
            <mat-label>Month</mat-label>
            <input matInput [matDatepicker]="picker" [value]="monthFilterDate()" readonly (click)="picker.open()" />
            @if (monthFilterDate()) {
              <button mat-icon-button matSuffix (click)="$event.stopPropagation(); clearMonthFilter()"><mat-icon>close</mat-icon></button>
            } @else {
              <mat-datepicker-toggle matIconSuffix [for]="picker" />
            }
            <mat-datepicker #picker startView="year" (monthSelected)="onMonthSelected($event, picker)" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="status-select">
            <mat-label>Status</mat-label>
            <mat-select [value]="statusFilter()" (selectionChange)="onStatusFilterChange($event.value)">
              <mat-option [value]="null">All</mat-option>
              @for (s of statusOptions; track s.value) { <mat-option [value]="s.value">{{ s.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
        </div>
        <button mat-flat-button color="primary" (click)="generateNow()"><mat-icon>bolt</mat-icon> Generate Bills</button>
      </div>

      @if (selection.selected.length > 0) {
        <div class="bulk-toolbar">
          <span>{{ selection.selected.length }} bill(s) selected</span>
          <button mat-flat-button color="primary" (click)="bulkMarkPaid()"><mat-icon>payments</mat-icon> Mark as Paid</button>
        </div>
      }

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        [showSearch]="false" emptyTitle="No bills yet" emptyMessage="Click 'Generate Bills' to create this month's invoices."
        (page)="onPage($event)">
        <table mat-table [dataSource]="bills()" table>
          <ng-container matColumnDef="select">
            <th mat-header-cell *matHeaderCellDef>
              <mat-checkbox (change)="$event ? toggleAll() : null" [checked]="allSelected()" [indeterminate]="someSelected()" />
            </th>
            <td mat-cell *matCellDef="let b">
              @if (b.status !== 3) {
                <mat-checkbox (click)="$event.stopPropagation()" (change)="selection.toggle(b.id)" [checked]="selection.isSelected(b.id)" />
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="flat">
            <th mat-header-cell *matHeaderCellDef>Flat</th>
            <td mat-cell *matCellDef="let b">
              <strong>{{ b.flatNumber }}</strong><br /><span class="muted">{{ b.buildingName }} / {{ b.wingName }}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="invoice">
            <th mat-header-cell *matHeaderCellDef>Invoice</th>
            <td mat-cell *matCellDef="let b">{{ b.invoiceNumber }}<br /><span class="muted">{{ b.billMonth | date: 'MMMM yyyy' }}</span></td>
          </ng-container>
          <ng-container matColumnDef="total">
            <th mat-header-cell *matHeaderCellDef>Total</th>
            <td mat-cell *matCellDef="let b">₹{{ b.totalAmount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="balance">
            <th mat-header-cell *matHeaderCellDef>Balance</th>
            <td mat-cell *matCellDef="let b">₹{{ b.balance | number }}</td>
          </ng-container>
          <ng-container matColumnDef="dueDate">
            <th mat-header-cell *matHeaderCellDef>Due Date</th>
            <td mat-cell *matCellDef="let b">{{ b.dueDate | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let b"><span class="badge" [class]="'status-' + b.status">{{ statusLabels[b.status] }}</span></td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let b">
              <button mat-icon-button [matMenuTriggerFor]="menu"><mat-icon>more_vert</mat-icon></button>
              <mat-menu #menu="matMenu">
                <button mat-menu-item (click)="viewDetail(b)"><mat-icon>visibility</mat-icon><span>View Detail</span></button>
                <button mat-menu-item (click)="downloadPdf(b)"><mat-icon>download</mat-icon><span>Download PDF</span></button>
                @if (b.status !== 3) {
                  <button mat-menu-item (click)="recordPayment(b)"><mat-icon>payments</mat-icon><span>Record Payment</span></button>
                }
                <button mat-menu-item (click)="resendWhatsApp(b)"><mat-icon>send</mat-icon><span>Resend WhatsApp</span></button>
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
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; gap: 12px; flex-wrap: wrap; }
    .filters { display: flex; align-items: center; gap: 12px; }
    .bulk-toolbar { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; padding: 10px 14px; background: var(--app-primary-light); border-radius: 8px; }
    .bulk-toolbar span { font-size: 13px; font-weight: 600; color: var(--app-primary); }
    .month-field { width: 160px; }
    .status-select { width: 200px; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .status-1 { background: #e2e8f0; color: #475569; }
    .status-2 { background: #fef3c7; color: #b45309; }
    .status-3 { background: #dcfce7; color: #15803d; }
    .status-4 { background: #fee2e2; color: #dc2626; }
  `]
})
export class MaintenanceBillsListComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly bills = signal<MaintenanceBillDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly statusFilter = signal<BillStatus | null>(null);
  readonly monthFilterDate = signal<Date | null>(null);
  readonly displayedColumns = ['select', 'flat', 'invoice', 'total', 'balance', 'dueDate', 'status', 'actions'];
  readonly statusLabels: Record<number, string> = BILL_STATUS_LABELS;
  readonly statusOptions = Object.entries(BILL_STATUS_LABELS).map(([value, label]) => ({ value: Number(value), label }));
  readonly selection = new SelectionModel<number>(true, []);

  private societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies: Society[]) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  private monthFilterAsString(): string | undefined {
    const date = this.monthFilterDate();
    if (!date) return undefined;
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-01`;
  }

  load(): void {
    this.loading.set(true);
    this.selection.clear();
    this.maintenanceService.getBills({
      societyId: this.societyId, status: this.statusFilter() ?? undefined, billMonth: this.monthFilterAsString(),
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.bills.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  private payableBills(): MaintenanceBillDto[] {
    return this.bills().filter((b) => b.status !== 3);
  }

  allSelected(): boolean {
    const payable = this.payableBills();
    return payable.length > 0 && payable.every((b) => this.selection.isSelected(b.id));
  }

  someSelected(): boolean {
    return this.payableBills().some((b) => this.selection.isSelected(b.id)) && !this.allSelected();
  }

  toggleAll(): void {
    if (this.allSelected()) {
      this.payableBills().forEach((b) => this.selection.deselect(b.id));
    } else {
      this.payableBills().forEach((b) => this.selection.select(b.id));
    }
  }

  bulkMarkPaid(): void {
    const billIds = [...this.selection.selected];
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: {
        title: `Mark ${billIds.length} Bill(s) as Paid`, submitLabel: 'Mark as Paid',
        fields: [
          { key: 'paymentDate', label: 'Payment Date', type: 'date', defaultValue: new Date().toISOString().substring(0, 10) },
          { key: 'paymentMode', label: 'Payment Mode', type: 'select', options: [{ value: 1, label: 'Cash' }, { value: 2, label: 'UPI' }, { value: 3, label: 'Bank Transfer' }, { value: 4, label: 'Cheque' }] },
          { key: 'transactionReference', label: 'Transaction Reference', type: 'text', required: false },
          { key: 'notes', label: 'Notes', type: 'textarea', required: false }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.maintenanceService.bulkRecordPayment(billIds, {
        paymentDate: result.paymentDate, paymentMode: Number(result.paymentMode),
        transactionReference: result.transactionReference || null, notes: result.notes || null
      }).subscribe((results) => {
        const recordedCount = results.filter((r) => r.recorded).length;
        this.toast.success(
          recordedCount === results.length
            ? `${recordedCount} bill(s) marked paid.`
            : `${recordedCount} of ${results.length} bill(s) marked paid — the rest were already paid or not found.`
        );
        this.selection.clear();
        this.load();
      });
    });
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  onStatusFilterChange(status: BillStatus | null): void {
    this.statusFilter.set(status);
    this.pageIndex.set(0);
    this.load();
  }

  onMonthSelected(date: Date, picker: { close: () => void }): void {
    this.monthFilterDate.set(date);
    picker.close();
    this.pageIndex.set(0);
    this.load();
  }

  clearMonthFilter(): void {
    this.monthFilterDate.set(null);
    this.pageIndex.set(0);
    this.load();
  }

  generateNow(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: 'Generate Bills', submitLabel: 'Generate',
        fields: [{ key: 'billMonth', label: 'Bill Month', type: 'date', defaultValue: new Date().toISOString().substring(0, 10) }]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.maintenanceService.generateBills(this.societyId, result.billMonth).subscribe((count) => {
        this.toast.success(`${count} bill(s) generated.`);
        this.load();
      });
    });
  }

  viewDetail(bill: MaintenanceBillDto): void {
    this.router.navigate(['/maintenance/bills', bill.id]);
  }

  downloadPdf(bill: MaintenanceBillDto): void {
    this.maintenanceService.downloadBillPdf(bill.id).subscribe((blob) => {
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${bill.invoiceNumber}.pdf`;
      link.click();
      window.URL.revokeObjectURL(url);
    });
  }

  recordPayment(bill: MaintenanceBillDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: {
        title: `Record Payment — ${bill.invoiceNumber}`, submitLabel: 'Save',
        fields: [
          { key: 'amount', label: `Amount (balance: ₹${bill.balance})`, type: 'number', defaultValue: bill.balance },
          { key: 'paymentDate', label: 'Payment Date', type: 'date', defaultValue: new Date().toISOString().substring(0, 10) },
          { key: 'paymentMode', label: 'Payment Mode', type: 'select', options: [{ value: 1, label: 'Cash' }, { value: 2, label: 'UPI' }, { value: 3, label: 'Bank Transfer' }, { value: 4, label: 'Cheque' }] },
          { key: 'transactionReference', label: 'Transaction Reference', type: 'text', required: false },
          { key: 'notes', label: 'Notes', type: 'textarea', required: false }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.maintenanceService.recordPayment({
        maintenanceBillId: bill.id, ...result, amount: Number(result.amount), paymentMode: Number(result.paymentMode)
      }).subscribe(() => {
        this.toast.success('Payment recorded.');
        this.load();
      });
    });
  }

  resendWhatsApp(bill: MaintenanceBillDto): void {
    this.maintenanceService.resendWhatsApp(bill.id).subscribe(() => this.toast.success('Bill resent via WhatsApp.'));
  }
}
