import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { ToastService } from '../../../core/services/toast.service';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { MatDialog } from '@angular/material/dialog';
import {
  FLAT_CONTRIBUTION_STATUS_LABELS, FestivalContributionDto, FlatContributionDto, PAYMENT_METHOD_LABELS
} from '../models/festival.model';
import { FestivalService } from '../services/festival.service';
import { MOBILE_PATTERN, MOBILE_PATTERN_ERROR } from '../../../shared/validators/mobile.validator';

export interface FlatContributionDetailDialogData {
  festivalId: number;
  flat: FlatContributionDto;
  canManage: boolean;
  canContribute: boolean;
}

/** One flat's target + full contribution history, opened by clicking a row
 * in the "By Flat" view — target amount (editable) and payment history
 * (date/time, amount, method, receipt) together, per the merged Contribution
 * page's requirements. Closes with `true` if anything changed, so the
 * parent list/KPIs reload. */
@Component({
  selector: 'app-flat-contribution-detail-dialog',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatChipsModule, MatDialogModule, MatIconModule, MatProgressSpinnerModule, MatTableModule
  ],
  template: `
    <h2 mat-dialog-title>Flat {{ data.flat.flatNumber }}</h2>
    <mat-dialog-content class="content">
      <div class="target-row">
        <div>
          <span class="label">Target</span>
          <span class="value">₹{{ target() | number }}</span>
        </div>
        <div>
          <span class="label">Paid</span>
          <span class="value">₹{{ paid() | number }}</span>
        </div>
        <div>
          <span class="label">Outstanding</span>
          <span class="value">₹{{ target() - paid() | number }}</span>
        </div>
        <div>
          <span class="label">Status</span>
          <mat-chip-set><mat-chip [class]="'status-' + data.flat.status">{{ statusLabels[data.flat.status] }}</mat-chip></mat-chip-set>
        </div>
        @if (data.canManage) {
          <button mat-icon-button (click)="editTarget()" matTooltip="Edit target"><mat-icon>edit</mat-icon></button>
        }
      </div>

      @if (declineReason()) {
        <div class="decline-banner">
          <mat-icon inline>info</mat-icon>
          <span>Declined to contribute: {{ declineReason() }}</span>
          @if (data.canContribute) {
            <button mat-button (click)="setDeclineReason()">Edit</button>
            <button mat-button (click)="clearDeclineReason()">Clear</button>
          }
        </div>
      } @else if (data.canContribute) {
        <button mat-stroked-button class="decline-btn" (click)="setDeclineReason()">
          <mat-icon>block</mat-icon> Mark as Declined to Pay
        </button>
      }

      <div class="history-header">
        <h3>Contribution History</h3>
        @if (data.canContribute) {
          <button mat-stroked-button (click)="addContribution()"><mat-icon>add</mat-icon> Add Contribution</button>
        }
      </div>

      @if (loading()) {
        <div class="loading"><mat-spinner diameter="28" /></div>
      } @else if (history().length === 0) {
        <p class="empty">No contributions recorded for this flat yet.</p>
      } @else {
        <table mat-table [dataSource]="history()">
          <ng-container matColumnDef="date">
            <th mat-header-cell *matHeaderCellDef>Date</th>
            <td mat-cell *matCellDef="let c">{{ c.paymentDate | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef>Amount</th>
            <td mat-cell *matCellDef="let c">₹{{ c.amount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="method">
            <th mat-header-cell *matHeaderCellDef>Method</th>
            <td mat-cell *matCellDef="let c">{{ methodLabels[c.paymentMethod] }}</td>
          </ng-container>
          <ng-container matColumnDef="receipt">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let c">
              <button mat-icon-button (click)="downloadReceipt(c)" matTooltip="Download PDF receipt"><mat-icon>download</mat-icon></button>
              @if (data.canContribute) {
                <button mat-icon-button (click)="editContribution(c)" matTooltip="Edit"><mat-icon>edit</mat-icon></button>
                <button mat-icon-button (click)="deleteContribution(c)" matTooltip="Remove (mark unpaid)"><mat-icon>delete</mat-icon></button>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns;"></tr>
        </table>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="close()">Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .content { min-width: 480px; }
    .target-row { display: flex; align-items: center; gap: 24px; padding: 12px 0; border-bottom: 1px solid var(--app-border); margin-bottom: 16px; }
    .target-row .label { display: block; font-size: 11px; color: var(--app-text-muted); }
    .target-row .value { font-weight: 600; }
    .decline-banner { display: flex; align-items: center; gap: 8px; background: #fef2f2; color: #b91c1c; border-radius: 8px; padding: 8px 12px; margin-bottom: 16px; font-size: 13px; }
    .decline-banner span { flex: 1; }
    .decline-btn { margin-bottom: 16px; color: #b91c1c; }
    .history-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; }
    .history-header h3 { margin: 0; font-size: 14px; }
    .loading { display: flex; justify-content: center; padding: 24px; }
    .empty { color: var(--app-text-muted); font-size: 13px; }
    table { width: 100%; }
    .status-0 { background: #f1f5f9 !important; }
    .status-1 { background: #fef2f2 !important; color: #b91c1c !important; }
    .status-2 { background: #fffbeb !important; color: #b45309 !important; }
    .status-3 { background: #ecfdf5 !important; color: #15803d !important; }
  `]
})
export class FlatContributionDetailDialogComponent implements OnInit {
  dialogRef = inject(MatDialogRef<FlatContributionDetailDialogComponent>);
  data = inject<FlatContributionDetailDialogData>(MAT_DIALOG_DATA);
  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  readonly loading = signal(true);
  readonly history = signal<FestivalContributionDto[]>([]);
  readonly target = signal(0);
  readonly paid = signal(0);
  readonly declineReason = signal<string | null>(null);
  readonly columns = ['date', 'amount', 'method', 'receipt'];
  // Widened to a numeric index signature — the row type from [dataSource]
  // doesn't narrow to the literal union in the template context.
  readonly statusLabels: Record<number, string> = FLAT_CONTRIBUTION_STATUS_LABELS;
  readonly methodLabels: Record<number, string> = PAYMENT_METHOD_LABELS;

  private changed = false;

  ngOnInit(): void {
    this.target.set(this.data.flat.targetAmount);
    this.paid.set(this.data.flat.paidAmount);
    this.declineReason.set(this.data.flat.declineReason ?? null);
    this.loadHistory();
  }

  loadHistory(): void {
    this.loading.set(true);
    this.festivalService.getContributions({
      festivalId: this.data.festivalId, flatId: this.data.flat.flatId, pageSize: 100
    }).subscribe((result) => {
      this.history.set(result.items);
      this.loading.set(false);
    });
  }

  editTarget(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: `Edit Target — ${this.data.flat.flatNumber}`,
        submitLabel: 'Save',
        fields: [{ key: 'targetAmount', label: 'Target Amount (₹)', type: 'number', defaultValue: this.target() }]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.updateFlatContributionTarget(this.data.festivalId, this.data.flat.flatId, Number(result.targetAmount)).subscribe(() => {
        this.target.set(Number(result.targetAmount));
        this.toast.success('Target updated.');
        this.changed = true;
      });
    });
  }

  addContribution(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '460px',
      data: {
        title: 'Record Contribution',
        submitLabel: 'Save',
        fields: [
          { key: 'memberName', label: 'Donor Name', type: 'text' },
          { key: 'amount', label: 'Amount', type: 'number' },
          {
            key: 'paymentMethod', label: 'Payment Method', type: 'select',
            options: [{ value: 1, label: 'Cash' }, { value: 2, label: 'UPI' }, { value: 3, label: 'Bank Transfer' }]
          },
          { key: 'paymentDate', label: 'Payment Date', type: 'date' },
          { key: 'transactionId', label: 'Transaction ID', type: 'text', required: false },
          { key: 'whatsAppNumber', label: 'WhatsApp Number (optional — defaults to the flat\'s number on file)', type: 'text', required: false, pattern: MOBILE_PATTERN, patternError: MOBILE_PATTERN_ERROR, maxLength: 10 },
          { key: 'isAnonymous', label: 'Keep donor anonymous on public displays', type: 'checkbox' }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.createContribution({
        festivalId: this.data.festivalId, flatId: this.data.flat.flatId,
        memberName: result.memberName, amount: Number(result.amount),
        paymentMethod: Number(result.paymentMethod), paymentDate: result.paymentDate,
        transactionId: result.transactionId, isAnonymous: !!result.isAnonymous,
        whatsAppNumber: result.whatsAppNumber || null
      }).subscribe(() => {
        this.toast.success('Contribution recorded.');
        this.paid.set(this.paid() + Number(result.amount));
        this.changed = true;
        this.loadHistory();
      });
    });
  }

  editContribution(contribution: FestivalContributionDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '460px',
      data: {
        title: 'Edit Contribution',
        submitLabel: 'Save',
        fields: [
          { key: 'memberName', label: 'Donor Name', type: 'text', defaultValue: contribution.memberName },
          { key: 'amount', label: 'Amount', type: 'number', defaultValue: contribution.amount },
          {
            key: 'paymentMethod', label: 'Payment Method', type: 'select', defaultValue: contribution.paymentMethod,
            options: [{ value: 1, label: 'Cash' }, { value: 2, label: 'UPI' }, { value: 3, label: 'Bank Transfer' }]
          },
          { key: 'paymentDate', label: 'Payment Date', type: 'date', defaultValue: contribution.paymentDate.substring(0, 10) },
          { key: 'transactionId', label: 'Transaction ID', type: 'text', required: false, defaultValue: contribution.transactionId ?? '' },
          { key: 'isAnonymous', label: 'Keep donor anonymous on public displays', type: 'checkbox', defaultValue: contribution.isAnonymous }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      const amountDelta = Number(result.amount) - contribution.amount;
      this.festivalService.updateContribution(contribution.id, {
        memberName: result.memberName, amount: Number(result.amount), paymentMethod: Number(result.paymentMethod),
        paymentDate: result.paymentDate, transactionId: result.transactionId || null, isAnonymous: !!result.isAnonymous
      }).subscribe(() => {
        this.toast.success('Contribution updated.');
        this.paid.set(this.paid() + amountDelta);
        this.changed = true;
        this.loadHistory();
      });
    });
  }

  deleteContribution(contribution: FestivalContributionDto): void {
    this.confirmDialog.confirm({
      title: 'Remove Contribution', destructive: true,
      message: `Remove this ₹${contribution.amount} contribution? The flat's paid amount and status will update immediately.`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.festivalService.deleteContribution(contribution.id).subscribe(() => {
        this.toast.success('Contribution removed.');
        this.paid.set(this.paid() - contribution.amount);
        this.changed = true;
        this.loadHistory();
      });
    });
  }

  setDeclineReason(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: {
        title: 'Mark as Declined to Pay',
        submitLabel: 'Save',
        fields: [{ key: 'reason', label: 'Reason', type: 'textarea', defaultValue: this.declineReason() ?? '' }]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.setFlatDeclineReason(this.data.festivalId, this.data.flat.flatId, result.reason).subscribe(() => {
        this.toast.success('Decline reason saved.');
        this.declineReason.set(result.reason);
        this.changed = true;
      });
    });
  }

  clearDeclineReason(): void {
    this.festivalService.setFlatDeclineReason(this.data.festivalId, this.data.flat.flatId, null).subscribe(() => {
      this.toast.success('Decline reason cleared.');
      this.declineReason.set(null);
      this.changed = true;
    });
  }

  downloadReceipt(contribution: FestivalContributionDto): void {
    this.festivalService.downloadReceipt(contribution.id).subscribe((blob) => {
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `receipt-${contribution.receiptNumber}.pdf`;
      link.click();
      window.URL.revokeObjectURL(url);
    });
  }

  close(): void {
    this.dialogRef.close(this.changed);
  }
}
