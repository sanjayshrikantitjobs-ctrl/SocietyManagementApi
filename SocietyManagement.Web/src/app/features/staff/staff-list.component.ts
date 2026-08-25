import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { ToastService } from '../../core/services/toast.service';
import { SocietyService } from '../society-setup/services/society.service';
import { StaffFormDialogComponent } from './staff-form-dialog.component';
import { STAFF_CATEGORY_LABELS, StaffDto } from './models/staff.model';
import { StaffService } from './services/staff.service';

@Component({
  selector: 'app-staff-list',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatSortModule, MatTableModule,
    DataTableComponent, PageHeaderComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Staff" subtitle="Society employees — watchmen, sweepers, gardeners, and more."
        [breadcrumbs]="[{ label: 'Staff' }]">
        <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> Add Staff</button>
      </app-page-header>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search name or phone..." emptyIcon="badge" emptyTitle="No staff yet"
        emptyMessage="Add a watchman, sweeper, or other staff member to get started."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="rows()" matSort (matSortChange)="onSort($event)" table>
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Name</th>
            <td mat-cell *matCellDef="let s"><strong>{{ s.firstName }} {{ s.lastName }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="category">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Category</th>
            <td mat-cell *matCellDef="let s">{{ categoryLabels[s.category] }}</td>
          </ng-container>
          <ng-container matColumnDef="phone">
            <th mat-header-cell *matHeaderCellDef>Phone</th>
            <td mat-cell *matCellDef="let s">{{ s.phone }}</td>
          </ng-container>
          <ng-container matColumnDef="joiningDate">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Joining Date</th>
            <td mat-cell *matCellDef="let s">{{ s.joiningDate | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let s">
              <mat-chip-set><mat-chip [class.active]="s.isActive" [class.inactive]="!s.isActive">{{ s.isActive ? 'Active' : 'Inactive' }}</mat-chip></mat-chip-set>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;" (click)="openStaff(row)" class="clickable-row"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    table { width: 100%; }
    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background: var(--app-surface-alt); }
    .active { background: #dcfce7 !important; color: #15803d !important; }
    .inactive { background: #f1f5f9 !important; color: #64748b !important; }
  `]
})
export class StaffListComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly societyService = inject(SocietyService);
  private readonly staffService = inject(StaffService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly rows = signal<StaffDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  readonly displayedColumns = ['name', 'category', 'phone', 'joiningDate', 'status'];
  readonly categoryLabels: Record<number, string> = STAFF_CATEGORY_LABELS;

  private societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    const sort = this.sortState();
    this.staffService.getStaff({
      societyId: this.societyId, search: this.searchTerm() || undefined,
      sortBy: sort?.direction ? sort.active : undefined, sortDescending: sort?.direction === 'desc',
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.rows.set(result.items);
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

  openStaff(row: StaffDto): void {
    this.router.navigate(['/staff', row.id]);
  }

  add(): void {
    const ref = this.dialog.open(StaffFormDialogComponent, { data: { staff: null, societyId: this.societyId } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.staffService.createStaff({ ...result, societyId: this.societyId }).subscribe(() => {
        this.toast.success('Staff member added.');
        this.load();
      });
    });
  }
}
