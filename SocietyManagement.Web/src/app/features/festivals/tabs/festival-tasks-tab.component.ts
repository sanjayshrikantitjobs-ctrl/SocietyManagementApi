import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { FESTIVAL_TASK_STATUS_LABELS, FestivalTaskDto, FestivalVolunteerDto } from '../models/festival.model';
import { FestivalService } from '../services/festival.service';

@Component({
  selector: 'app-festival-tasks-tab',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatTableModule, EmptyStateComponent, SkeletonLoaderComponent],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Tasks ({{ tasks().length }})</h3>
        @if (canManage()) {
          <button mat-flat-button color="primary" (click)="addTask()"><mat-icon>add</mat-icon> Add Task</button>
        }
      </div>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="60" />
      } @else if (tasks().length === 0) {
        <app-empty-state icon="checklist" title="No tasks yet" message="Add coordination tasks like arranging the Pandit or booking decorations."
          [actionLabel]="canManage() ? 'Add Task' : null" (action)="addTask()" />
      } @else {
        <table mat-table [dataSource]="tasks()" class="app-card">
          <ng-container matColumnDef="title">
            <th mat-header-cell *matHeaderCellDef>Task</th>
            <td mat-cell *matCellDef="let t"><strong>{{ t.title }}</strong><br /><span class="muted">{{ t.description }}</span></td>
          </ng-container>
          <ng-container matColumnDef="assignedTo">
            <th mat-header-cell *matHeaderCellDef>Assigned To</th>
            <td mat-cell *matCellDef="let t">{{ t.assignedVolunteerName ?? 'Unassigned' }}</td>
          </ng-container>
          <ng-container matColumnDef="dueDate">
            <th mat-header-cell *matHeaderCellDef>Due Date</th>
            <td mat-cell *matCellDef="let t">{{ t.dueDate ? (t.dueDate | date: 'mediumDate') : '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let t"><mat-chip-set><mat-chip [class]="'status-' + t.status">{{ statusLabels[t.status] }}</mat-chip></mat-chip-set></td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let t">
              @if (canManage()) {
                <button mat-icon-button (click)="editTask(t)"><mat-icon>edit</mat-icon></button>
                <button mat-icon-button (click)="removeTask(t)"><mat-icon>delete_outline</mat-icon></button>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
    .toolbar h3 { margin: 0; font-size: 15px; }
    table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .status-1 { background: #fef3c7; color: #b45309; }
    .status-2 { background: #dbeafe; color: #1d4ed8; }
    .status-3 { background: #dcfce7; color: #15803d; }
  `]
})
export class FestivalTasksTabComponent implements OnInit {
  festivalId = input.required<number>();
  canManage = input(false);

  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly tasks = signal<FestivalTaskDto[]>([]);
  readonly displayedColumns = ['title', 'assignedTo', 'dueDate', 'status', 'actions'];
  readonly statusLabels: Record<number, string> = FESTIVAL_TASK_STATUS_LABELS;

  private volunteers: FestivalVolunteerDto[] = [];

  ngOnInit(): void {
    this.load();
    this.festivalService.getVolunteers(this.festivalId()).subscribe((data) => (this.volunteers = data));
  }

  load(): void {
    this.loading.set(true);
    this.festivalService.getTasks(this.festivalId()).subscribe((data) => {
      this.tasks.set(data);
      this.loading.set(false);
    });
  }

  private fields(task?: FestivalTaskDto) {
    const volunteerOptions = [
      { value: 0, label: 'Unassigned' },
      ...this.volunteers.map((v) => ({ value: v.id, label: v.name }))
    ];
    const statusOptions = Object.entries(this.statusLabels).map(([value, label]) => ({ value: Number(value), label }));
    return [
      { key: 'title', label: 'Task', type: 'text' as const, defaultValue: task?.title ?? '' },
      { key: 'description', label: 'Description', type: 'textarea' as const, required: false, defaultValue: task?.description ?? '' },
      { key: 'assignedVolunteerId', label: 'Assigned To', type: 'select' as const, options: volunteerOptions, defaultValue: task?.assignedVolunteerId ?? 0 },
      { key: 'dueDate', label: 'Due Date', type: 'date' as const, required: false, defaultValue: task?.dueDate?.substring(0, 10) ?? '' },
      ...(task
        ? [{ key: 'status', label: 'Status', type: 'select' as const, options: statusOptions, defaultValue: task.status }]
        : [])
    ];
  }

  private toPayload(result: Record<string, unknown>) {
    return {
      ...result,
      assignedVolunteerId: Number(result['assignedVolunteerId']) || null,
      dueDate: result['dueDate'] || null
    };
  }

  addTask(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '480px', data: { title: 'Add Task', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.createTask({ festivalId: this.festivalId(), ...this.toPayload(result) }).subscribe(() => {
        this.toast.success('Task added.');
        this.load();
      });
    });
  }

  editTask(task: FestivalTaskDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px', data: { title: 'Edit Task', submitLabel: 'Save', fields: this.fields(task) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.updateTask(task.id, this.toPayload(result)).subscribe(() => {
        this.toast.success('Task updated.');
        this.load();
      });
    });
  }

  removeTask(task: FestivalTaskDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Task', destructive: true, message: `Delete "${task.title}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.festivalService.deleteTask(task.id).subscribe(() => {
        this.toast.success('Task deleted.');
        this.load();
      });
    });
  }
}
