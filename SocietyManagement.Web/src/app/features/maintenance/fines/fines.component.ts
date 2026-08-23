import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { Society } from '../../../core/models/society.model';
import { SocietyService } from '../../society-setup/services/society.service';
import { FineRecordDto, FineStatus } from '../models/maintenance.model';
import { MaintenanceService } from '../services/maintenance.service';

@Component({
  selector: 'app-fines',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatButtonToggleModule, MatIconModule, MatSortModule, MatTableModule,
    MatTooltipModule, DataTableComponent
  ],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Fines</h3>
        <button mat-flat-button color="primary" (click)="add()" [disabled]="flatOptions.length === 0"><mat-icon>add</mat-icon> Record Fine</button>
      </div>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search flat or reason..." emptyIcon="gavel" emptyTitle="No fines recorded"
        emptyMessage="Record fines for late payment or rule violations here."
        (page)="onPage($event)" (search)="onSearch($event)">
        <div toolbar>
          <mat-button-toggle-group [value]="statusFilter()" (change)="onStatusFilterChange($event.value)" class="status-filter">
            <mat-button-toggle [value]="null">All</mat-button-toggle>
            <mat-button-toggle [value]="1">Pending</mat-button-toggle>
            <mat-button-toggle [value]="2">Billed</mat-button-toggle>
            <mat-button-toggle [value]="3">Waived</mat-button-toggle>
          </mat-button-toggle-group>
        </div>
        <table mat-table [dataSource]="fines()" matSort (matSortChange)="onSort($event)" table>
          <ng-container matColumnDef="flatNumber">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="flatnumber">Flat</th>
            <td mat-cell *matCellDef="let f">{{ f.flatNumber }}</td>
          </ng-container>
          <ng-container matColumnDef="reason">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Reason</th>
            <td mat-cell *matCellDef="let f">{{ f.reason }}</td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Amount</th>
            <td mat-cell *matCellDef="let f">₹{{ f.amount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="fineDate">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="finedate">Date</th>
            <td mat-cell *matCellDef="let f">{{ f.fineDate | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
            <td mat-cell *matCellDef="let f">
              <span class="badge" [class]="'status-' + f.status">{{ statusLabel(f.status) }}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let f">
              @if (f.status === 1) {
                <button mat-icon-button (click)="waive(f)" matTooltip="Waive"><mat-icon>block</mat-icon></button>
                <button mat-icon-button (click)="remove(f)"><mat-icon>delete_outline</mat-icon></button>
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
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
    .toolbar h3 { margin: 0; font-size: 15px; }
    table { width: 100%; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .status-1 { background: #fef3c7; color: #b45309; }
    .status-2 { background: #dbeafe; color: #1d4ed8; }
    .status-3 { background: #e2e8f0; color: #475569; }
  `]
})
export class FinesComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly fines = signal<FineRecordDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  readonly statusFilter = signal<FineStatus | null>(null);
  readonly displayedColumns = ['flatNumber', 'reason', 'amount', 'fineDate', 'status', 'actions'];

  private societyId = 0;
  flatOptions: { value: number; label: string }[] = [];

  statusLabel(status: FineStatus): string {
    return { 1: 'Pending', 2: 'Billed', 3: 'Waived' }[status];
  }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies: Society[]) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.societyService.getFlats({ pageSize: 500 }).subscribe((result) => {
        this.flatOptions = result.items.map((f) => ({ value: f.id, label: f.flatNumber }));
      });
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    const sort = this.sortState();
    this.maintenanceService.getFines({
      societyId: this.societyId, status: this.statusFilter() ?? undefined, search: this.searchTerm() || undefined,
      sortBy: sort?.direction ? sort.active : undefined, sortDescending: sort?.direction === 'desc',
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.fines.set(result.items);
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

  onStatusFilterChange(status: FineStatus | null): void {
    this.statusFilter.set(status);
    this.pageIndex.set(0);
    this.load();
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: {
        title: 'Record Fine',
        fields: [
          { key: 'flatId', label: 'Flat', type: 'select', options: this.flatOptions },
          { key: 'reason', label: 'Reason', type: 'text' },
          { key: 'amount', label: 'Amount', type: 'number' },
          { key: 'fineDate', label: 'Date', type: 'date', defaultValue: new Date().toISOString().substring(0, 10) }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.maintenanceService.createFine({ ...result, flatId: Number(result.flatId), amount: Number(result.amount) }).subscribe(() => {
        this.toast.success('Fine recorded.');
        this.load();
      });
    });
  }

  waive(fine: FineRecordDto): void {
    this.confirmDialog.confirm({
      title: 'Waive Fine', message: `Waive the "${fine.reason}" fine of ₹${fine.amount} for flat ${fine.flatNumber}?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.maintenanceService.waiveFine(fine.id).subscribe(() => {
        this.toast.success('Fine waived.');
        this.load();
      });
    });
  }

  remove(fine: FineRecordDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Fine', destructive: true, message: `Delete the "${fine.reason}" fine for flat ${fine.flatNumber}?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.maintenanceService.deleteFine(fine.id).subscribe(() => {
        this.toast.success('Fine deleted.');
        this.load();
      });
    });
  }
}
