import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { Society } from '../../../core/models/society.model';
import { SocietyService } from '../../society-setup/services/society.service';
import { CHARGE_FREQUENCY_LABELS, SpecialChargeDto } from '../models/maintenance.model';
import { MaintenanceService } from '../services/maintenance.service';

@Component({
  selector: 'app-special-charges',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatSortModule, MatTableModule, DataTableComponent],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Special Charges</h3>
        <button mat-flat-button color="primary" (click)="add()" [disabled]="flatOptions.length === 0"><mat-icon>add</mat-icon> Add Special Charge</button>
      </div>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search flat or charge name..." emptyIcon="request_quote" emptyTitle="No special charges yet"
        emptyMessage="Assign Parking, Club House, Generator or custom charges to specific flats."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="charges()" matSort (matSortChange)="onSort($event)" table>
          <ng-container matColumnDef="flatNumber">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="flatnumber">Flat</th>
            <td mat-cell *matCellDef="let c">{{ c.flatNumber }}</td>
          </ng-container>
          <ng-container matColumnDef="chargeName">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="chargename">Charge</th>
            <td mat-cell *matCellDef="let c">{{ c.chargeName }}</td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Amount</th>
            <td mat-cell *matCellDef="let c">₹{{ c.amount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="frequency">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Frequency</th>
            <td mat-cell *matCellDef="let c"><mat-chip-set><mat-chip>{{ frequencyLabels[c.frequency] }}</mat-chip></mat-chip-set></td>
          </ng-container>
          <ng-container matColumnDef="period">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="startdate">Period</th>
            <td mat-cell *matCellDef="let c">{{ c.startDate | date: 'mediumDate' }} @if (c.endDate) { – {{ c.endDate | date: 'mediumDate' }} }</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let c">
              <span class="badge" [class.badge-success]="c.isActive" [class.badge-muted]="!c.isActive">{{ c.isActive ? 'Active' : 'Inactive' }}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let c">
              <button mat-icon-button (click)="edit(c)"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button (click)="remove(c)"><mat-icon>delete_outline</mat-icon></button>
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
    table { width: 100%; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .badge-success { background: #dcfce7; color: #15803d; }
    .badge-muted { background: #e2e8f0; color: #475569; }
  `]
})
export class SpecialChargesComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly charges = signal<SpecialChargeDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  readonly displayedColumns = ['flatNumber', 'chargeName', 'amount', 'frequency', 'period', 'status', 'actions'];
  readonly frequencyLabels: Record<number, string> = CHARGE_FREQUENCY_LABELS;

  private societyId = 0;
  flatOptions: { value: number; label: string }[] = [];
  private readonly frequencyOptions = Object.entries(CHARGE_FREQUENCY_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies: Society[]) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.societyService.getFlats({ societyId: this.societyId, pageSize: 500 }).subscribe((result) => {
        this.flatOptions = result.items.map((f) => ({ value: f.id, label: f.flatNumber }));
      });
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    const sort = this.sortState();
    this.maintenanceService.getSpecialCharges({
      societyId: this.societyId, search: this.searchTerm() || undefined,
      sortBy: sort?.direction ? sort.active : undefined, sortDescending: sort?.direction === 'desc',
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.charges.set(result.items);
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

  onSort(sort: Sort): void {
    this.sortState.set(sort);
    this.pageIndex.set(0);
    this.load();
  }

  private fields(charge?: SpecialChargeDto) {
    return [
      { key: 'flatId', label: 'Flat', type: 'select' as const, options: this.flatOptions, defaultValue: charge?.flatId },
      { key: 'chargeName', label: 'Charge Name', type: 'text' as const, defaultValue: charge?.chargeName ?? '' },
      { key: 'amount', label: 'Amount', type: 'number' as const, defaultValue: charge?.amount ?? 0 },
      { key: 'frequency', label: 'Frequency', type: 'select' as const, options: this.frequencyOptions, defaultValue: charge?.frequency ?? 1 },
      { key: 'startDate', label: 'Start Date', type: 'date' as const, defaultValue: charge?.startDate?.substring(0, 10) ?? new Date().toISOString().substring(0, 10) },
      { key: 'endDate', label: 'End Date (optional)', type: 'date' as const, required: false, defaultValue: charge?.endDate?.substring(0, 10) ?? '' },
      { key: 'notes', label: 'Notes', type: 'textarea' as const, required: false, defaultValue: charge?.notes ?? '' }
    ];
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '460px', data: { title: 'Add Special Charge', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.maintenanceService.createSpecialCharge({
        ...result, flatId: Number(result.flatId), amount: Number(result.amount), frequency: Number(result.frequency),
        endDate: result.endDate || null
      }).subscribe(() => {
        this.toast.success('Special charge added.');
        this.load();
      });
    });
  }

  edit(charge: SpecialChargeDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '460px', data: { title: 'Edit Special Charge', submitLabel: 'Save', fields: this.fields(charge) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.maintenanceService.updateSpecialCharge(charge.id, {
        ...result, amount: Number(result.amount), frequency: Number(result.frequency), endDate: result.endDate || null,
        isActive: charge.isActive
      }).subscribe(() => {
        this.toast.success('Special charge updated.');
        this.load();
      });
    });
  }

  remove(charge: SpecialChargeDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Special Charge', destructive: true, message: `Delete "${charge.chargeName}" for flat ${charge.flatNumber}?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.maintenanceService.deleteSpecialCharge(charge.id).subscribe(() => {
        this.toast.success('Special charge deleted.');
        this.load();
      });
    });
  }
}
