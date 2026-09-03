import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { SocietyService } from '../../society-setup/services/society.service';
import { MONTH_YEAR_FORMATS } from '../../../shared/utils/month-picker-format';
import { WaterTankerLogDto, WaterTankerLogMonthSummaryDto } from '../models/maintenance.model';
import { MaintenanceService } from '../services/maintenance.service';

/** Operational log of tanker deliveries (provider, vehicle, count, cost) —
 * not billed to flats. Replaces the old per-flat water tanker collection
 * feature going forward (that data/Finance history is left untouched, just
 * no longer reachable from this screen — see [[water-tanker-billing-removed]]).
 * Add/Edit/Delete are Admin/Super Admin-only (maintenance.manage), matching
 * the API's own [HasPermission(Permissions.Maintenance.Manage)] gate on
 * those endpoints — everyone else with maintenance.view sees a read-only log. */
@Component({
  selector: 'app-water-tanker',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatDatepickerModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatTableModule, MatTooltipModule, DataTableComponent, StatCardComponent
  ],
  providers: [{ provide: MAT_DATE_FORMATS, useValue: MONTH_YEAR_FORMATS }],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Water Tanker Log</h3>
        <div class="month-controls">
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="month-field">
            <mat-label>Month</mat-label>
            <input matInput [matDatepicker]="picker" [value]="selectedMonthDate" readonly (click)="picker.open()" />
            <mat-datepicker-toggle matIconSuffix [for]="picker" />
            <mat-datepicker #picker startView="year" (monthSelected)="onMonthSelected($event, picker)" />
          </mat-form-field>
          @if (canManage()) {
            <button mat-flat-button color="primary" (click)="addEntry()" [disabled]="societyId === 0">
              <mat-icon>add</mat-icon> Log Tanker Entry
            </button>
          }
        </div>
      </div>

      <div class="stats-grid">
        <app-stat-card label="Deliveries This Month" [value]="summary()?.totalDeliveries ?? 0" icon="local_shipping" />
        <app-stat-card label="Tankers Used" [value]="summary()?.totalTankers ?? 0" icon="water_drop" iconColor="#2563eb" iconBg="#eff6ff" />
        <app-stat-card label="Amount Paid" [value]="'₹' + (summary()?.totalAmount ?? 0 | number)" icon="paid" iconColor="#16a34a" iconBg="#ecfdf5" />
      </div>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search provider or vehicle number..." emptyTitle="No tanker entries this month"
        emptyMessage="Click 'Log Tanker Entry' to record a water tanker delivery."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="logs()" table>
          <ng-container matColumnDef="date">
            <th mat-header-cell *matHeaderCellDef>Date</th>
            <td mat-cell *matCellDef="let l">{{ l.date | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="provider">
            <th mat-header-cell *matHeaderCellDef>Provider</th>
            <td mat-cell *matCellDef="let l">{{ l.providerName }}</td>
          </ng-container>
          <ng-container matColumnDef="vehicle">
            <th mat-header-cell *matHeaderCellDef>Vehicle No.</th>
            <td mat-cell *matCellDef="let l">{{ l.vehicleNumber }}</td>
          </ng-container>
          <ng-container matColumnDef="tankers">
            <th mat-header-cell *matHeaderCellDef>Tankers</th>
            <td mat-cell *matCellDef="let l">{{ l.numberOfTankers }}</td>
          </ng-container>
          <ng-container matColumnDef="pricePerTanker">
            <th mat-header-cell *matHeaderCellDef>Price / Tanker</th>
            <td mat-cell *matCellDef="let l">₹{{ l.pricePerTanker | number }}</td>
          </ng-container>
          <ng-container matColumnDef="totalAmount">
            <th mat-header-cell *matHeaderCellDef>Total</th>
            <td mat-cell *matCellDef="let l">₹{{ l.totalAmount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="notes">
            <th mat-header-cell *matHeaderCellDef>Notes</th>
            <td mat-cell *matCellDef="let l">{{ l.notes || '—' }}</td>
          </ng-container>
          @if (canManage()) {
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let l">
                <button mat-icon-button matTooltip="Edit" (click)="editEntry(l)"><mat-icon>edit</mat-icon></button>
                <button mat-icon-button matTooltip="Delete" (click)="deleteEntry(l)"><mat-icon>delete_outline</mat-icon></button>
              </td>
            </ng-container>
          }

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
    .month-field { width: 200px; }
    .stats-grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(200px,1fr)); gap:16px; margin-bottom:24px; }
  `]
})
export class WaterTankerComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly logs = signal<WaterTankerLogDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly summary = signal<WaterTankerLogMonthSummaryDto | null>(null);
  private readonly auth = inject(AuthService);

  get displayedColumns(): string[] {
    const base = ['date', 'provider', 'vehicle', 'tankers', 'pricePerTanker', 'totalAmount', 'notes'];
    return this.canManage() ? [...base, 'actions'] : base;
  }

  canManage(): boolean {
    return this.auth.hasPermission('maintenance.manage');
  }

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
    this.maintenanceService.getWaterTankerLogs({
      societyId: this.societyId, month: this.monthAsDate(), search: this.searchTerm() || undefined,
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.logs.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  loadSummary(): void {
    this.maintenanceService.getWaterTankerLogSummary(this.societyId, this.monthAsDate()).subscribe((data) => this.summary.set(data));
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

  private fields(log?: WaterTankerLogDto) {
    return [
      { key: 'date', label: 'Date', type: 'date' as const, defaultValue: log?.date?.substring(0, 10) ?? new Date().toISOString().substring(0, 10) },
      { key: 'providerName', label: 'Tanker Provider Name', type: 'text' as const, defaultValue: log?.providerName ?? '' },
      { key: 'vehicleNumber', label: 'Vehicle Number', type: 'text' as const, defaultValue: log?.vehicleNumber ?? '' },
      { key: 'numberOfTankers', label: 'Number of Tankers', type: 'number' as const, defaultValue: log?.numberOfTankers ?? 1 },
      { key: 'pricePerTanker', label: 'Price per Tanker (₹)', type: 'number' as const, defaultValue: log?.pricePerTanker ?? 0 },
      { key: 'notes', label: 'Notes (optional)', type: 'text' as const, required: false, defaultValue: log?.notes ?? '' }
    ];
  }

  addEntry(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px', data: { title: 'Log Tanker Entry', submitLabel: 'Save', fields: this.fields() }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.maintenanceService.createWaterTankerLog(this.societyId, {
        date: result.date, providerName: result.providerName, vehicleNumber: result.vehicleNumber,
        numberOfTankers: Number(result.numberOfTankers), pricePerTanker: Number(result.pricePerTanker),
        notes: result.notes || null
      }).subscribe(() => {
        this.toast.success('Tanker entry logged.');
        this.load();
        this.loadSummary();
      });
    });
  }

  editEntry(log: WaterTankerLogDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px', data: { title: 'Edit Tanker Entry', submitLabel: 'Save', fields: this.fields(log) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.maintenanceService.updateWaterTankerLog(log.id, {
        date: result.date, providerName: result.providerName, vehicleNumber: result.vehicleNumber,
        numberOfTankers: Number(result.numberOfTankers), pricePerTanker: Number(result.pricePerTanker),
        notes: result.notes || null
      }).subscribe(() => {
        this.toast.success('Entry updated.');
        this.load();
        this.loadSummary();
      });
    });
  }

  deleteEntry(log: WaterTankerLogDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Tanker Entry', destructive: true,
      message: `Delete the ${log.providerName} entry from ${log.date}?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.maintenanceService.deleteWaterTankerLog(log.id).subscribe(() => {
        this.toast.success('Entry deleted.');
        this.load();
        this.loadSummary();
      });
    });
  }
}
