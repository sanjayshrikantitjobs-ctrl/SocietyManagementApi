import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import {
  BILL_ITEM_TYPE_LABELS, BILL_STATUS_LABELS, MaintenanceBillDetailDto, PAYMENT_MODE_LABELS
} from '../models/maintenance.model';
import { MaintenanceService } from '../services/maintenance.service';

/** Read-only bill breakdown + payment history for Members — the admin
 * MaintenanceBillDetailComponent lives behind the Admin-only /maintenance
 * route guard, so this is a lighter dialog-based equivalent reachable from
 * the Member-visible /my-bills page. */
@Component({
  selector: 'app-my-bill-detail-dialog',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatChipsModule, MatDialogModule, MatProgressSpinnerModule, MatTableModule],
  template: `
    <h2 mat-dialog-title>Bill Details</h2>
    <mat-dialog-content>
      @if (loading()) {
        <div class="loading"><mat-spinner diameter="28" /></div>
      } @else if (bill(); as b) {
        <div class="header-row">
          <div><span class="label">Invoice</span><span class="value">{{ b.invoiceNumber }}</span></div>
          <div><span class="label">Month</span><span class="value">{{ b.billMonth | date: 'MMMM yyyy' }}</span></div>
          <div><span class="label">Status</span><span class="badge" [class]="'status-' + b.status">{{ statusLabels[b.status] }}</span></div>
        </div>

        <h4>Items</h4>
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
        <div class="extra-row total"><span>Grand Total</span><span>₹{{ b.totalAmount | number }}</span></div>

        <h4>Payment History</h4>
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
            <tr mat-header-row *matHeaderRowDef="['date', 'amount', 'mode']"></tr>
            <tr mat-row *matRowDef="let row; columns: ['date', 'amount', 'mode'];"></tr>
          </table>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end"><button mat-button mat-dialog-close>Close</button></mat-dialog-actions>
  `,
  styles: [`
    .loading { display: flex; justify-content: center; padding: 40px; }
    .header-row { display: flex; gap: 24px; margin-bottom: 16px; }
    .header-row .label { display: block; font-size: 11px; color: var(--app-text-muted); text-transform: uppercase; }
    .header-row .value { display: block; font-size: 14px; font-weight: 700; }
    h4 { margin: 16px 0 8px; font-size: 13px; }
    table { width: 100%; }
    .extra-row { display: flex; justify-content: space-between; padding: 8px; font-weight: 700; border-top: 1px solid var(--app-border); }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .status-1 { background: #e2e8f0; color: #475569; }
    .status-2 { background: #fef3c7; color: #b45309; }
    .status-3 { background: #dcfce7; color: #15803d; }
    .status-4 { background: #fee2e2; color: #dc2626; }
  `]
})
export class MyBillDetailDialogComponent implements OnInit {
  private readonly billId = inject<number>(MAT_DIALOG_DATA);
  private readonly maintenanceService = inject(MaintenanceService);

  readonly loading = signal(true);
  readonly bill = signal<MaintenanceBillDetailDto | null>(null);
  readonly statusLabels: Record<number, string> = BILL_STATUS_LABELS;
  readonly itemTypeLabels: Record<number, string> = BILL_ITEM_TYPE_LABELS;
  readonly paymentModeLabels: Record<number, string> = PAYMENT_MODE_LABELS;

  ngOnInit(): void {
    this.maintenanceService.getBillById(this.billId).subscribe((bill) => {
      this.bill.set(bill);
      this.loading.set(false);
    });
  }
}
