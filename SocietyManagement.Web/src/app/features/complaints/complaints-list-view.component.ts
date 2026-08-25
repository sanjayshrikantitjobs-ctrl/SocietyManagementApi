import { CommonModule } from '@angular/common';
import { Component, OnChanges, inject, input, signal } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { ComplaintDetailDialogComponent } from './complaint-detail-dialog.component';
import { COMPLAINT_CATEGORY_LABELS, COMPLAINT_STATUS_LABELS, ComplaintDto } from './models/complaint.model';
import { ComplaintService } from './services/complaint.service';

/** Sortable/paginated table alternative to the Kanban board — same shared
 * detail dialog on row click, server-paginated via GetComplaintsPagedQuery
 * (added alongside the board's own unpaginated GetComplaintsQuery, which
 * still needs the full list to bucket into status columns). */
@Component({
  selector: 'app-complaints-list-view',
  standalone: true,
  imports: [CommonModule, MatChipsModule, MatPaginatorModule, MatSortModule, MatTableModule, DataTableComponent],
  template: `
    <app-data-table
      [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
      searchPlaceholder="Search title or flat..." emptyTitle="No complaints found" emptyMessage="No complaints match the current filters."
      (page)="onPage($event)" (search)="onSearch($event)">
      <table mat-table [dataSource]="complaints()" matSort (matSortChange)="onSort($event)" table>
        <ng-container matColumnDef="flat">
          <th mat-header-cell *matHeaderCellDef>Flat</th>
          <td mat-cell *matCellDef="let c">{{ c.flatNumber }}</td>
        </ng-container>
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Complaint</th>
          <td mat-cell *matCellDef="let c">{{ c.title }}<br /><span class="muted">{{ categoryLabels[c.category] }}</span></td>
        </ng-container>
        <ng-container matColumnDef="priority">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Priority</th>
          <td mat-cell *matCellDef="let c"><span class="priority-dot" [class]="'priority-' + c.priority"></span></td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
          <td mat-cell *matCellDef="let c"><mat-chip-set><mat-chip>{{ statusLabels[c.status] }}</mat-chip></mat-chip-set></td>
        </ng-container>
        <ng-container matColumnDef="createdAt">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="createdAt">Raised On</th>
          <td mat-cell *matCellDef="let c">{{ c.createdAt | date: 'mediumDate' }}</td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
        <tr mat-row *matRowDef="let row; columns: displayedColumns;" (click)="openDetail(row)" class="clickable-row"></tr>
      </table>
    </app-data-table>
  `,
  styles: [`
    table { width: 100%; }
    .clickable-row { cursor: pointer; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .priority-dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; }
    .priority-1 { background: #94a3b8; }
    .priority-2 { background: #f59e0b; }
    .priority-3 { background: #dc2626; }
  `]
})
export class ComplaintsListViewComponent implements OnChanges {
  societyId = input.required<number>();

  private readonly complaintService = inject(ComplaintService);
  private readonly dialog = inject(MatDialog);

  readonly loading = signal(true);
  readonly complaints = signal<ComplaintDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  readonly displayedColumns = ['flat', 'title', 'priority', 'status', 'createdAt'];
  readonly categoryLabels: Record<number, string> = COMPLAINT_CATEGORY_LABELS;
  readonly statusLabels: Record<number, string> = COMPLAINT_STATUS_LABELS;

  ngOnChanges(): void {
    if (this.societyId()) this.load();
  }

  load(): void {
    this.loading.set(true);
    const sort = this.sortState();
    this.complaintService.getComplaintsPaged({
      societyId: this.societyId(),
      search: this.searchTerm() || undefined,
      sortBy: sort?.direction ? sort.active : undefined,
      sortDescending: sort?.direction === 'desc',
      pageNumber: this.pageIndex() + 1,
      pageSize: this.pageSize()
    }).subscribe((result) => {
      this.complaints.set(result.items);
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

  openDetail(complaint: ComplaintDto): void {
    const ref = this.dialog.open(ComplaintDetailDialogComponent, {
      data: { complaintId: complaint.id, societyId: this.societyId() }
    });
    ref.afterClosed().subscribe(() => this.load());
  }
}
