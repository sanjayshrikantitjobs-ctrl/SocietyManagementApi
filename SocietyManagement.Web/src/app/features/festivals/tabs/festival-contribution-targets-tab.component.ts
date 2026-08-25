import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import {
  FLAT_CONTRIBUTION_STATUS_LABELS, FlatContributionDto, FlatContributionKpisDto, FlatContributionStatus
} from '../models/festival.model';
import { FestivalService } from '../services/festival.service';

/** "Per-Flat Contribution Targets" — the Flat/Target/Paid/Outstanding/
 * Status table, split into its own tab from the old combined Contributions
 * tab (which crowded this together with the ledger). */
@Component({
  selector: 'app-festival-contribution-targets-tab',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatChipsModule, MatFormFieldModule, MatIconModule, MatSelectModule,
    MatSortModule, MatTableModule, DataTableComponent, StatCardComponent
  ],
  template: `
    <div class="tab-content">
      <div class="stats-grid">
        <app-stat-card label="Target (All Flats)" [value]="'₹' + (kpis()?.totalTargetAmount ?? 0 | number)" icon="flag" />
        <app-stat-card label="Collected Toward Target" [value]="'₹' + (kpis()?.totalPaidAmount ?? 0 | number)" icon="paid" iconColor="#16a34a" iconBg="#ecfdf5" />
        <app-stat-card label="Outstanding" [value]="'₹' + (kpis()?.totalOutstandingAmount ?? 0 | number)" icon="hourglass_empty" iconColor="#dc2626" iconBg="#fef2f2" />
        <app-stat-card label="Flats Fully Paid" [value]="(kpis()?.flatsPaidCount ?? 0) + ' / ' + (kpis()?.totalFlats ?? 0)" icon="task_alt" iconColor="#2563eb" iconBg="#eff6ff" />
      </div>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search flat number..." emptyTitle="No flats found"
        emptyMessage="Set a contribution target to start tracking who's paid."
        (page)="onPage($event)" (search)="onSearch($event)">
        <div toolbar>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="status-filter">
            <mat-select [(ngModel)]="statusFilter" (ngModelChange)="onStatusFilterChange()" placeholder="All statuses">
              <mat-option [value]="null">All statuses</mat-option>
              @for (opt of statusOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <button mat-stroked-button (click)="setTargetsForAllFlats()"><mat-icon>flag</mat-icon> Set Target for All Flats</button>
        </div>
        <table mat-table [dataSource]="flatContributions()" matSort (matSortChange)="onSort($event)" table>
          <ng-container matColumnDef="flat">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Flat</th>
            <td mat-cell *matCellDef="let f"><strong>{{ f.flatNumber }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="target">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Target</th>
            <td mat-cell *matCellDef="let f">₹{{ f.targetAmount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="paid">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Paid</th>
            <td mat-cell *matCellDef="let f">₹{{ f.paidAmount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="outstanding">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Outstanding</th>
            <td mat-cell *matCellDef="let f">₹{{ f.outstandingAmount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
            <td mat-cell *matCellDef="let f">
              <mat-chip-set><mat-chip [class]="'status-' + f.status">{{ statusLabel(f.status) }}</mat-chip></mat-chip-set>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let f">
              <button mat-icon-button (click)="editFlatTarget(f)" matTooltip="Edit target"><mat-icon>edit</mat-icon></button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="flatColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: flatColumns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .stats-grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(220px,1fr)); gap:16px; margin-bottom:24px; }
    .status-filter { width: 180px; }
    div[toolbar] { display: flex; align-items: center; gap: 12px; }
    .status-0 { background: #f1f5f9 !important; }
    .status-1 { background: #fef2f2 !important; color: #b91c1c !important; }
    .status-2 { background: #fffbeb !important; color: #b45309 !important; }
    .status-3 { background: #ecfdf5 !important; color: #15803d !important; }
  `]
})
export class FestivalContributionTargetsTabComponent implements OnInit {
  festivalId = input.required<number>();

  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly flatContributions = signal<FlatContributionDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  readonly kpis = signal<FlatContributionKpisDto | null>(null);
  readonly flatColumns = ['flat', 'target', 'paid', 'outstanding', 'status', 'actions'];
  statusFilter: FlatContributionStatus | null = null;
  readonly statusOptions = [
    { value: 1 as FlatContributionStatus, label: 'Pending' },
    { value: 2 as FlatContributionStatus, label: 'Partially Paid' },
    { value: 3 as FlatContributionStatus, label: 'Paid' },
    { value: 0 as FlatContributionStatus, label: 'No Target' }
  ];

  statusLabel(status: number): string {
    return FLAT_CONTRIBUTION_STATUS_LABELS[status as FlatContributionStatus];
  }

  ngOnInit(): void {
    this.load();
    this.loadKpis();
  }

  load(): void {
    this.loading.set(true);
    const sort = this.sortState();
    this.festivalService.getFlatContributions({
      festivalId: this.festivalId(), search: this.searchTerm() || undefined, status: this.statusFilter ?? undefined,
      sortBy: sort?.direction ? sort.active : undefined, sortDescending: sort?.direction === 'desc',
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.flatContributions.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  loadKpis(): void {
    this.festivalService.getFlatContributionKpis(this.festivalId()).subscribe((data) => this.kpis.set(data));
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

  onStatusFilterChange(): void {
    this.pageIndex.set(0);
    this.load();
  }

  setTargetsForAllFlats(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: 'Set Target for All Flats',
        submitLabel: 'Apply',
        fields: [{ key: 'targetAmount', label: 'Annual Target per Flat (₹)', type: 'number', defaultValue: 0 }]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.setContributionTargets(this.festivalId(), Number(result.targetAmount)).subscribe((count) => {
        this.toast.success(`Target set for ${count} flat(s).`);
        this.load();
        this.loadKpis();
      });
    });
  }

  editFlatTarget(flat: FlatContributionDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: `Edit Target — ${flat.flatNumber}`,
        submitLabel: 'Save',
        fields: [{ key: 'targetAmount', label: 'Target Amount (₹)', type: 'number', defaultValue: flat.targetAmount }]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.updateFlatContributionTarget(this.festivalId(), flat.flatId, Number(result.targetAmount)).subscribe(() => {
        this.toast.success('Target updated.');
        this.load();
        this.loadKpis();
      });
    });
  }
}
