import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { ToastService } from '../../core/services/toast.service';
import { SocietyService } from '../society-setup/services/society.service';
import { ComplaintDetailDialogComponent } from './complaint-detail-dialog.component';
import { ComplaintFormDialogComponent } from './complaint-form-dialog.component';
import { ComplaintsListViewComponent } from './complaints-list-view.component';
import { COMPLAINT_CATEGORY_LABELS, COMPLAINT_PRIORITY_LABELS, ComplaintDto, ComplaintStatus } from './models/complaint.model';
import { ComplaintService } from './services/complaint.service';

interface BoardColumn {
  status: ComplaintStatus;
  label: string;
}

const COLUMNS: BoardColumn[] = [
  { status: 1, label: 'Open' },
  { status: 2, label: 'Assigned' },
  { status: 3, label: 'In Progress' },
  { status: 4, label: 'Resolved' },
  { status: 5, label: 'Closed' }
];

/** Static Kanban board — 5 columns bucketed client-side from one
 * unpaginated complaint list, no drag-and-drop. Clicking a card opens the
 * detail dialog, where status transitions actually happen via buttons. */
@Component({
  selector: 'app-complaints-board',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatButtonToggleModule, MatChipsModule, MatFormFieldModule, MatIconModule,
    MatInputModule, MatSelectModule, PageHeaderComponent, SkeletonLoaderComponent, ComplaintsListViewComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Complaints" subtitle="Track resident complaints from open through resolved."
        [breadcrumbs]="[{ label: 'Complaints' }]">
        <mat-button-toggle-group [(ngModel)]="viewMode" class="view-toggle">
          <mat-button-toggle value="board"><mat-icon>view_kanban</mat-icon> Board</mat-button-toggle>
          <mat-button-toggle value="list"><mat-icon>view_list</mat-icon> List</mat-button-toggle>
        </mat-button-toggle-group>
        <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> Add Complaint</button>
      </app-page-header>

      @if (viewMode === 'list') {
        <app-complaints-list-view [societyId]="societyId" />
      } @else {
        <div class="filters">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Category</mat-label>
            <mat-select [(ngModel)]="categoryFilter" (selectionChange)="load()">
              <mat-option [value]="null">All</mat-option>
              @for (opt of categoryOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Priority</mat-label>
            <mat-select [(ngModel)]="priorityFilter" (selectionChange)="load()">
              <mat-option [value]="null">All</mat-option>
              @for (opt of priorityOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Search</mat-label>
            <input matInput [(ngModel)]="searchTerm" (ngModelChange)="load()" placeholder="Title or flat..." />
          </mat-form-field>
        </div>

        @if (loading()) {
          <app-skeleton-loader [rows]="5" [height]="120" />
        } @else {
          <div class="board">
            @for (col of columns; track col.status) {
              <div class="column">
                <div class="column-header">
                  <span>{{ col.label }}</span>
                  <span class="count">{{ grouped()[col.status].length }}</span>
                </div>
                <div class="column-body">
                  @for (c of grouped()[col.status]; track c.id) {
                    <div class="card" (click)="openDetail(c)">
                      <div class="card-top">
                        <strong>Flat {{ c.flatNumber }}</strong>
                        <span class="priority-dot" [class]="'priority-' + c.priority"></span>
                      </div>
                      <div class="card-title">{{ c.title }}</div>
                      <div class="card-meta">{{ categoryLabels[c.category] }}</div>
                      @if (c.assignedStaffName) { <div class="card-assignee"><mat-icon>engineering</mat-icon> {{ c.assignedStaffName }}</div> }
                    </div>
                  } @empty {
                    <div class="empty-column">No complaints</div>
                  }
                </div>
              </div>
            }
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .view-toggle { margin-right: 8px; height: 40px; }
    .filters { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; }
    .filters mat-form-field { width: 200px; }
    .board { display: grid; grid-template-columns: repeat(5, minmax(220px, 1fr)); gap: 12px; overflow-x: auto; padding-bottom: 8px; }
    .column { background: var(--app-surface-alt); border-radius: 10px; padding: 10px; min-width: 220px; }
    .column-header { display: flex; justify-content: space-between; align-items: center; font-weight: 600; padding: 4px 6px 10px; }
    .count { background: var(--app-surface); border-radius: 10px; padding: 1px 8px; font-size: 12px; }
    .column-body { display: flex; flex-direction: column; gap: 8px; min-height: 40px; }
    .card { background: var(--app-surface); border-radius: 8px; padding: 10px; cursor: pointer; box-shadow: 0 1px 2px rgba(0,0,0,0.06); }
    .card:hover { box-shadow: 0 2px 6px rgba(0,0,0,0.12); }
    .card-top { display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px; }
    .priority-dot { width: 10px; height: 10px; border-radius: 50%; }
    .priority-1 { background: #94a3b8; }
    .priority-2 { background: #f59e0b; }
    .priority-3 { background: #dc2626; }
    .card-title { font-size: 13px; margin-bottom: 4px; }
    .card-meta { font-size: 12px; color: var(--app-text-muted); }
    .card-assignee { display: flex; align-items: center; gap: 4px; font-size: 12px; color: var(--app-text-muted); margin-top: 6px; }
    .card-assignee mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .empty-column { font-size: 12px; color: var(--app-text-muted); text-align: center; padding: 12px 0; }
  `]
})
export class ComplaintsBoardComponent implements OnInit {
  private readonly complaintService = inject(ComplaintService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly complaints = signal<ComplaintDto[]>([]);
  readonly grouped = computed<Record<ComplaintStatus, ComplaintDto[]>>(() => {
    const byStatus = { 1: [], 2: [], 3: [], 4: [], 5: [] } as Record<ComplaintStatus, ComplaintDto[]>;
    for (const c of this.complaints()) byStatus[c.status].push(c);
    return byStatus;
  });
  readonly columns = COLUMNS;
  readonly categoryLabels: Record<number, string> = COMPLAINT_CATEGORY_LABELS;
  readonly categoryOptions = Object.entries(COMPLAINT_CATEGORY_LABELS).map(([value, label]) => ({ value: Number(value), label }));
  readonly priorityOptions = Object.entries(COMPLAINT_PRIORITY_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  categoryFilter: number | null = null;
  priorityFilter: number | null = null;
  searchTerm = '';
  viewMode: 'board' | 'list' = 'board';

  societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.complaintService.getComplaints({
      societyId: this.societyId, category: this.categoryFilter ?? undefined, priority: this.priorityFilter ?? undefined,
      search: this.searchTerm || undefined
    }).subscribe((result) => {
      this.complaints.set(result);
      this.loading.set(false);
    });
  }

  add(): void {
    const ref = this.dialog.open(ComplaintFormDialogComponent, { data: { flatId: null, flatNumber: null, lockFlat: false } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.complaintService.create(result).subscribe(() => {
        this.toast.success('Complaint raised.');
        this.load();
      });
    });
  }

  openDetail(complaint: ComplaintDto): void {
    const ref = this.dialog.open(ComplaintDetailDialogComponent, {
      data: { complaintId: complaint.id, societyId: this.societyId }
    });
    ref.afterClosed().subscribe(() => this.load());
  }
}
