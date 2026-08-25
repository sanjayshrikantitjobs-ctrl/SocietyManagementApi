import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, OnInit, ViewChild, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { SocietyService } from '../../society-setup/services/society.service';
import { ComplaintDetailDialogComponent } from '../complaint-detail-dialog.component';
import { ComplaintFormDialogComponent } from '../complaint-form-dialog.component';
import { COMPLAINT_CATEGORY_LABELS, COMPLAINT_STATUS_LABELS, ComplaintDto } from '../models/complaint.model';
import { ComplaintService } from '../services/complaint.service';

/** Resident-facing complaint list — mirrors my-bills.component.ts's
 * structure (list + status badge + a dialog for raising/viewing). Unlike
 * My Bills, there's no per-flat picker here: a complaint belongs to
 * whichever flat it was raised for, and residents with multiple flats
 * just see all their own complaints across all of them in one list; the
 * flat is picked at raise-time instead (in the raise dialog). Sorting/
 * pagination are client-side (MatTableDataSource) rather than a server
 * round-trip — one resident's own complaint count is always small. */
@Component({
  selector: 'app-my-complaints',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatPaginatorModule, MatSortModule, MatTableModule,
    EmptyStateComponent, PageHeaderComponent, SkeletonLoaderComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="My Complaints" subtitle="Raise a complaint for your flat and track its progress."
        [breadcrumbs]="[{ label: 'My Complaints' }]">
        <button mat-flat-button color="primary" (click)="add()" [disabled]="flatOptions.length === 0">
          <mat-icon>add</mat-icon> Raise Complaint
        </button>
      </app-page-header>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="60" />
      } @else if (dataSource.data.length === 0) {
        <app-empty-state icon="report_problem" title="No complaints yet" message="Raise a complaint if something in your flat or the common area needs attention." />
      } @else {
        <div class="app-card">
          <table mat-table [dataSource]="dataSource" matSort>
            <ng-container matColumnDef="flat">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Flat</th>
              <td mat-cell *matCellDef="let c">{{ c.flatNumber }}</td>
            </ng-container>
            <ng-container matColumnDef="title">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Complaint</th>
              <td mat-cell *matCellDef="let c">{{ c.title }}<br /><span class="muted">{{ categoryLabels[c.category] }}</span></td>
            </ng-container>
            <ng-container matColumnDef="raised">
              <th mat-header-cell *matHeaderCellDef mat-sort-header="createdAt">Raised On</th>
              <td mat-cell *matCellDef="let c">{{ c.createdAt | date: 'mediumDate' }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
              <td mat-cell *matCellDef="let c"><span class="badge" [class]="'status-' + c.status">{{ statusLabels[c.status] }}</span></td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let c">
                <button mat-icon-button (click)="viewDetail(c)"><mat-icon>visibility</mat-icon></button>
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;" (click)="viewDetail(row)" class="clickable-row"></tr>
          </table>
          <mat-paginator [pageSizeOptions]="[10, 25, 50]" [pageSize]="10" showFirstLastButtons />
        </div>
      }
    </div>
  `,
  styles: [`
    table { width: 100%; }
    .clickable-row { cursor: pointer; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .status-1 { background: #e2e8f0; color: #475569; }
    .status-2 { background: #dbeafe; color: #1d4ed8; }
    .status-3 { background: #fef3c7; color: #b45309; }
    .status-4 { background: #dcfce7; color: #15803d; }
    .status-5 { background: #f1f5f9; color: #64748b; }
  `]
})
export class MyComplaintsComponent implements OnInit, AfterViewInit {
  private readonly complaintService = inject(ComplaintService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);

  @ViewChild(MatSort) sort!: MatSort;
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  readonly loading = signal(true);
  readonly dataSource = new MatTableDataSource<ComplaintDto>([]);
  readonly displayedColumns = ['flat', 'title', 'raised', 'status', 'actions'];
  readonly categoryLabels: Record<number, string> = COMPLAINT_CATEGORY_LABELS;
  readonly statusLabels: Record<number, string> = COMPLAINT_STATUS_LABELS;

  flatOptions: { value: number; label: string }[] = [];
  private societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      this.societyId = societies[0]?.id ?? 0;
    });
    this.societyService.getMyFlats().subscribe((flats) => {
      this.flatOptions = flats.map((f) => ({ value: f.id, label: f.flatNumber }));
    });
    this.load();
  }

  ngAfterViewInit(): void {
    this.dataSource.sort = this.sort;
    this.dataSource.paginator = this.paginator;
  }

  load(): void {
    this.loading.set(true);
    this.complaintService.getMine().subscribe((result) => {
      this.dataSource.data = result;
      this.loading.set(false);
    });
  }

  add(): void {
    const flat = this.flatOptions.length === 1 ? this.flatOptions[0] : null;
    const ref = this.dialog.open(ComplaintFormDialogComponent, {
      data: { flatId: flat?.value ?? null, flatNumber: flat?.label ?? null, lockFlat: !!flat, flatOptions: this.flatOptions }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.complaintService.create(result).subscribe(() => this.load());
    });
  }

  viewDetail(complaint: ComplaintDto): void {
    const ref = this.dialog.open(ComplaintDetailDialogComponent, {
      data: { complaintId: complaint.id, societyId: this.societyId }
    });
    ref.afterClosed().subscribe(() => this.load());
  }
}
