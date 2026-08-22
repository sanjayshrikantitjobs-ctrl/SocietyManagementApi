import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { SocietyService } from '../../society-setup/services/society.service';
import { WaterTankerCollectionDto, WaterTankerMonthSummaryDto } from '../models/maintenance.model';
import { MaintenanceService } from '../services/maintenance.service';

/** Tracks the per-flat monthly water tanker contribution (a flat rate every
 * flat owes when the borewell/municipal supply runs short) — admin
 * generates one charge row per flat for a month, then marks each paid as
 * cash/UPI comes in, viewable month by month. */
@Component({
  selector: 'app-water-tanker',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatChipsModule, MatDatepickerModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatTableModule, MatTooltipModule, DataTableComponent, StatCardComponent
  ],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Water Tanker Collection</h3>
        <div class="month-controls">
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="month-field">
            <mat-label>Month</mat-label>
            <input matInput [matDatepicker]="picker" [value]="selectedMonthDate" readonly (click)="picker.open()" />
            <mat-datepicker-toggle matIconSuffix [for]="picker" />
            <mat-datepicker #picker startView="year" (monthSelected)="onMonthSelected($event, picker)" />
          </mat-form-field>
          <button mat-stroked-button (click)="generateCharges()" [disabled]="societyId === 0">
            <mat-icon>add_circle_outline</mat-icon> Generate Charges for This Month
          </button>
        </div>
      </div>

      <div class="stats-grid">
        <app-stat-card label="Flats Charged" [value]="summary()?.totalFlats ?? 0" icon="apartment" />
        <app-stat-card label="Collected" [value]="'₹' + (summary()?.totalCollected ?? 0 | number)" icon="paid" iconColor="#16a34a" iconBg="#ecfdf5" />
        <app-stat-card label="Pending" [value]="'₹' + (summary()?.totalPending ?? 0 | number)" icon="hourglass_empty" iconColor="#dc2626" iconBg="#fef2f2" />
        <app-stat-card label="Flats Paid" [value]="(summary()?.flatsPaidCount ?? 0) + ' / ' + (summary()?.totalFlats ?? 0)" icon="task_alt" iconColor="#2563eb" iconBg="#eff6ff" />
      </div>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search flat number..." emptyTitle="No charges generated yet"
        emptyMessage="Click 'Generate Charges for This Month' to create a ₹1000 charge for every flat."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="collections()" table>
          <ng-container matColumnDef="flat">
            <th mat-header-cell *matHeaderCellDef>Flat</th>
            <td mat-cell *matCellDef="let c"><strong>{{ c.flatNumber }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef>Amount</th>
            <td mat-cell *matCellDef="let c">₹{{ c.amount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let c">
              <mat-chip-set><mat-chip [class]="c.isPaid ? 'status-paid' : 'status-pending'">{{ c.isPaid ? 'Paid' : 'Pending' }}</mat-chip></mat-chip-set>
            </td>
          </ng-container>
          <ng-container matColumnDef="paymentDate">
            <th mat-header-cell *matHeaderCellDef>Payment Date</th>
            <td mat-cell *matCellDef="let c">{{ c.paymentDate ? (c.paymentDate | date: 'mediumDate') : '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="notes">
            <th mat-header-cell *matHeaderCellDef>Notes</th>
            <td mat-cell *matCellDef="let c">{{ c.notes || '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let c">
              @if (!c.isPaid) {
                <button mat-icon-button matTooltip="Mark Paid" (click)="markPaid(c)"><mat-icon>check_circle</mat-icon></button>
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
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; flex-wrap: wrap; gap: 12px; }
    .toolbar h3 { margin: 0; font-size: 15px; }
    .month-controls { display: flex; align-items: center; gap: 12px; }
    .month-field { width: 160px; }
    .stats-grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(200px,1fr)); gap:16px; margin-bottom:24px; }
    .status-paid { background: #ecfdf5 !important; color: #15803d !important; }
    .status-pending { background: #fef2f2 !important; color: #b91c1c !important; }
  `]
})
export class WaterTankerComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly collections = signal<WaterTankerCollectionDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly summary = signal<WaterTankerMonthSummaryDto | null>(null);
  readonly displayedColumns = ['flat', 'amount', 'status', 'paymentDate', 'notes', 'actions'];

  selectedMonthDate = new Date();
  societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
      this.loadSummary();
    });
  }

  private monthAsDate(): string {
    const y = this.selectedMonthDate.getFullYear();
    const m = String(this.selectedMonthDate.getMonth() + 1).padStart(2, '0');
    return `${y}-${m}-01`;
  }

  onMonthSelected(date: Date, picker: { close: () => void }): void {
    this.selectedMonthDate = date;
    picker.close();
    this.onMonthChange();
  }

  load(): void {
    this.loading.set(true);
    this.maintenanceService.getWaterTankerCollections({
      societyId: this.societyId, month: this.monthAsDate(), search: this.searchTerm() || undefined,
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.collections.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  loadSummary(): void {
    this.maintenanceService.getWaterTankerSummary(this.societyId, this.monthAsDate()).subscribe((data) => this.summary.set(data));
  }

  onMonthChange(): void {
    this.pageIndex.set(0);
    this.load();
    this.loadSummary();
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

  generateCharges(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: 'Generate Water Tanker Charges', submitLabel: 'Generate',
        fields: [{ key: 'amount', label: 'Amount per Flat (₹)', type: 'number', defaultValue: 1000 }]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.maintenanceService.generateWaterTankerCharges(this.societyId, this.monthAsDate(), Number(result.amount)).subscribe((count) => {
        this.toast.success(`Charge generated for ${count} flat(s).`);
        this.load();
        this.loadSummary();
      });
    });
  }

  markPaid(collection: WaterTankerCollectionDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: `Mark Paid — Flat ${collection.flatNumber}`, submitLabel: 'Confirm',
        fields: [
          { key: 'paymentDate', label: 'Payment Date', type: 'date', defaultValue: new Date().toISOString().substring(0, 10) },
          { key: 'notes', label: 'Notes (optional)', type: 'text', required: false, defaultValue: '' }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.maintenanceService.recordWaterTankerPayment(collection.id, result.paymentDate, result.notes || undefined).subscribe(() => {
        this.toast.success('Payment recorded.');
        this.load();
        this.loadSummary();
      });
    });
  }
}
