import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { ToastService } from '../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import {
  BILL_ITEM_TYPE_LABELS, BILL_STATUS_LABELS, MaintenanceBillDetailDto, PAYMENT_MODE_LABELS
} from '../models/maintenance.model';
import { MaintenanceService } from '../services/maintenance.service';

@Component({
  selector: 'app-maintenance-bill-detail',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatProgressSpinnerModule, MatTableModule,
    PageHeaderComponent
  ],
  template: `
    @if (loading()) {
      <div class="loading"><mat-spinner diameter="36" /></div>
    } @else if (bill(); as b) {
      <app-page-header [title]="b.invoiceNumber"
        [breadcrumbs]="[{ label: 'Maintenance', link: '/maintenance/bills' }, { label: 'Bills', link: '/maintenance/bills' }, { label: b.invoiceNumber }]">
        <span class="badge" [class]="'status-' + b.status">{{ statusLabels[b.status] }}</span>
        <button mat-stroked-button (click)="downloadPdf()"><mat-icon>download</mat-icon> Download PDF</button>
        @if (b.status !== 3) {
          <button mat-flat-button color="primary" (click)="recordPayment()"><mat-icon>payments</mat-icon> Record Payment</button>
        }
      </app-page-header>

      <div class="summary-grid">
        <div class="app-card summary-card"><span class="label">Flat</span><span class="value">{{ b.flatNumber }}</span><span class="muted">{{ b.buildingName }} / {{ b.wingName }}</span></div>
        <div class="app-card summary-card"><span class="label">Owner</span><span class="value">{{ b.ownerNameSnapshot || '—' }}</span></div>
        <div class="app-card summary-card"><span class="label">Bill Month</span><span class="value">{{ b.billMonth | date: 'MMMM yyyy' }}</span></div>
        <div class="app-card summary-card"><span class="label">Due Date</span><span class="value">{{ b.dueDate | date: 'mediumDate' }}</span></div>
        <div class="app-card summary-card"><span class="label">Total</span><span class="value">₹{{ b.totalAmount | number }}</span></div>
        <div class="app-card summary-card"><span class="label">Paid</span><span class="value paid">₹{{ b.amountPaid | number }}</span></div>
        <div class="app-card summary-card">
          <span class="label">Balance</span>
          @if (b.balance < 0) {
            <span class="value credit">Credit ₹{{ -b.balance | number }}</span>
          } @else {
            <span class="value" [class.overdue]="b.balance > 0">₹{{ b.balance | number }}</span>
          }
        </div>
      </div>

      <div class="app-card section">
        <h3>Bill Items</h3>
        <table mat-table [dataSource]="b.items">
          <ng-container matColumnDef="description">
            <th mat-header-cell *matHeaderCellDef>Description</th>
            <td mat-cell *matCellDef="let i">{{ i.description }}<span class="muted"> — {{ itemTypeLabels[i.itemType] }}</span></td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef>Amount</th>
            <td mat-cell *matCellDef="let i">₹{{ i.amount | number }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="['description', 'amount']"></tr>
          <tr mat-row *matRowDef="let row; columns: ['description', 'amount'];"></tr>
        </table>
        @if (b.previousBalance > 0) {
          <div class="extra-row"><span>Previous Balance</span><span>₹{{ b.previousBalance | number }}</span></div>
        }
        <div class="extra-row total"><span>Grand Total</span><span>₹{{ b.totalAmount | number }}</span></div>
      </div>

      <div class="app-card section">
        <h3>Payment History</h3>
        @if (b.payments.length === 0) {
          <p class="muted">No payments recorded yet.</p>
        } @else {
          <table mat-table [dataSource]="b.payments">
            <ng-container matColumnDef="date">
              <th mat-header-cell *matHeaderCellDef>Date</th>
              <td mat-cell *matCellDef="let p">{{ p.paymentDate | date: 'mediumDate' }}</td>
            </ng-container>
            <ng-container matColumnDef="amount">
              <th mat-header-cell *matHeaderCellDef>Amount</th>
              <td mat-cell *matCellDef="let p">₹{{ p.amount | number }}</td>
            </ng-container>
            <ng-container matColumnDef="mode">
              <th mat-header-cell *matHeaderCellDef>Mode</th>
              <td mat-cell *matCellDef="let p"><mat-chip-set><mat-chip>{{ paymentModeLabels[p.paymentMode] }}</mat-chip></mat-chip-set></td>
            </ng-container>
            <ng-container matColumnDef="reference">
              <th mat-header-cell *matHeaderCellDef>Reference</th>
              <td mat-cell *matCellDef="let p">{{ p.transactionReference || '—' }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="['date', 'amount', 'mode', 'reference']"></tr>
            <tr mat-row *matRowDef="let row; columns: ['date', 'amount', 'mode', 'reference'];"></tr>
          </table>
        }
      </div>
    }
  `,
  styles: [`
    .loading { display: flex; justify-content: center; padding: 60px; }
    .badge { padding: 4px 12px; border-radius: 12px; font-size: 12px; font-weight: 700; margin-right: 8px; }
    .status-1 { background: #e2e8f0; color: #475569; }
    .status-2 { background: #fef3c7; color: #b45309; }
    .status-3 { background: #dcfce7; color: #15803d; }
    .status-4 { background: #fee2e2; color: #dc2626; }
    .summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 12px; margin-bottom: 20px; }
    .summary-card { padding: 14px; display: flex; flex-direction: column; gap: 2px; }
    .summary-card .label { font-size: 11px; color: var(--app-text-muted); text-transform: uppercase; }
    .summary-card .value { font-size: 16px; font-weight: 700; }
    .summary-card .value.paid { color: #15803d; }
    .summary-card .value.overdue { color: #dc2626; }
    .summary-card .value.credit, .credit { color: #15803d; }
    .summary-card .muted { font-size: 11px; color: var(--app-text-muted); }
    .section { padding: 20px; margin-bottom: 16px; }
    .section h3 { margin: 0 0 12px; font-size: 14px; }
    table { width: 100%; }
    .extra-row { display: flex; justify-content: space-between; padding: 10px 8px; border-top: 1px solid var(--app-border); font-size: 13px; }
    .extra-row.total { font-weight: 700; font-size: 15px; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
  `]
})
export class MaintenanceBillDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly bill = signal<MaintenanceBillDetailDto | null>(null);
  readonly statusLabels: Record<number, string> = BILL_STATUS_LABELS;
  readonly itemTypeLabels: Record<number, string> = BILL_ITEM_TYPE_LABELS;
  readonly paymentModeLabels: Record<number, string> = PAYMENT_MODE_LABELS;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loading.set(true);
    this.maintenanceService.getBillById(id).subscribe((bill) => {
      this.bill.set(bill);
      this.loading.set(false);
    });
  }

  downloadPdf(): void {
    const b = this.bill();
    if (!b) return;
    this.maintenanceService.downloadBillPdf(b.id).subscribe((blob) => {
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${b.invoiceNumber}.pdf`;
      link.click();
      window.URL.revokeObjectURL(url);
    });
  }

  recordPayment(): void {
    const b = this.bill();
    if (!b) return;

    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: {
        title: `Record Payment — ${b.invoiceNumber}`, submitLabel: 'Save',
        fields: [
          { key: 'amount', label: `Amount (balance: ₹${b.balance})`, type: 'number', defaultValue: b.balance },
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
        maintenanceBillId: b.id, ...result, amount: Number(result.amount), paymentMode: Number(result.paymentMode)
      }).subscribe(() => {
        this.toast.success('Payment recorded.');
        this.load();
      });
    });
  }
}
