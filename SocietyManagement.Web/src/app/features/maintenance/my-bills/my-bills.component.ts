import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { Society } from '../../../core/models/society.model';
import { SocietyService } from '../../society-setup/services/society.service';
import { BILL_STATUS_LABELS, MaintenanceBillDto } from '../models/maintenance.model';
import { MaintenanceService } from '../services/maintenance.service';
import { MyBillDetailDialogComponent } from './my-bill-detail-dialog.component';

const MY_FLAT_STORAGE_KEY = 'societyManagement.myFlatId';

/** Member-facing read-only bill view. The flat picker only ever lists
 * flats the current user actually resides at (via GetMyFlatsQuery,
 * resolved server-side from their Member/FlatResidency rows) — a person
 * can legitimately own more than one flat, so it's still a picker, just
 * never the whole society's flat list. Auto-selects when there's only one. */
@Component({
  selector: 'app-my-bills',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatSelectModule, MatTableModule, MatTooltipModule,
    EmptyStateComponent, PageHeaderComponent, SkeletonLoaderComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="My Bills" subtitle="View your maintenance bills, download invoices, and check payment history."
        [breadcrumbs]="[{ label: 'My Bills' }]">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="flat-picker">
          <mat-label>My Flat</mat-label>
          <mat-select [value]="selectedFlatId()" (selectionChange)="onFlatChange($event.value)">
            @for (opt of flatOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
          </mat-select>
        </mat-form-field>
      </app-page-header>

      @if (!selectedFlatId()) {
        <app-empty-state icon="apartment" title="Select your flat" message="Choose your flat above to see your maintenance bills." />
      } @else if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="60" />
      } @else if (bills().length === 0) {
        <app-empty-state icon="receipt_long" title="No bills yet" message="Bills for your flat will appear here once generated." />
      } @else {
        <table mat-table [dataSource]="bills()" class="app-card">
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
              <button mat-icon-button (click)="viewDetail(b)" matTooltip="View details"><mat-icon>visibility</mat-icon></button>
              <button mat-icon-button (click)="downloadPdf(b)" matTooltip="Download PDF"><mat-icon>download</mat-icon></button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      }
    </div>
  `,
  styles: [`
    .flat-picker { width: 200px; }
    table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .status-1 { background: #e2e8f0; color: #475569; }
    .status-2 { background: #fef3c7; color: #b45309; }
    .status-3 { background: #dcfce7; color: #15803d; }
    .status-4 { background: #fee2e2; color: #dc2626; }
  `]
})
export class MyBillsComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);

  readonly loading = signal(true);
  readonly bills = signal<MaintenanceBillDto[]>([]);
  readonly selectedFlatId = signal<number | null>(null);
  readonly displayedColumns = ['invoice', 'total', 'balance', 'dueDate', 'status', 'actions'];
  readonly statusLabels: Record<number, string> = BILL_STATUS_LABELS;

  flatOptions: { value: number; label: string }[] = [];
  private societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies: Society[]) => {
      this.societyId = societies[0]?.id ?? 0;

      this.societyService.getMyFlats().subscribe((flats) => {
        this.flatOptions = flats.map((f) => ({ value: f.id, label: f.flatNumber }));

        const stored = Number(localStorage.getItem(MY_FLAT_STORAGE_KEY));
        if (stored && this.flatOptions.some((o) => o.value === stored)) {
          this.selectedFlatId.set(stored);
          this.load();
        } else if (this.flatOptions.length === 1) {
          this.onFlatChange(this.flatOptions[0].value);
        } else {
          this.loading.set(false);
        }
      });
    });
  }

  onFlatChange(flatId: number): void {
    this.selectedFlatId.set(flatId);
    localStorage.setItem(MY_FLAT_STORAGE_KEY, String(flatId));
    this.load();
  }

  load(): void {
    const flatId = this.selectedFlatId();
    if (!flatId) return;

    this.loading.set(true);
    this.maintenanceService.getBills({ societyId: this.societyId, flatId, pageSize: 100 }).subscribe((result) => {
      this.bills.set(result.items);
      this.loading.set(false);
    });
  }

  viewDetail(bill: MaintenanceBillDto): void {
    this.dialog.open(MyBillDetailDialogComponent, { width: '560px', data: bill.id });
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
}
