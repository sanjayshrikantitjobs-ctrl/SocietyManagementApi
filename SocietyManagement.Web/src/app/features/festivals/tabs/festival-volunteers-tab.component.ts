import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { FestivalVolunteerDto } from '../models/festival.model';
import { FestivalService } from '../services/festival.service';

@Component({
  selector: 'app-festival-volunteers-tab',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTableModule, EmptyStateComponent, SkeletonLoaderComponent],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Volunteers ({{ volunteers().length }})</h3>
        @if (canManage()) {
          <button mat-flat-button color="primary" (click)="addVolunteer()"><mat-icon>add</mat-icon> Add Volunteer</button>
        }
      </div>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="60" />
      } @else if (volunteers().length === 0) {
        <app-empty-state icon="groups" title="No volunteers yet" message="Add the people helping run this festival."
          [actionLabel]="canManage() ? 'Add Volunteer' : null" (action)="addVolunteer()" />
      } @else {
        <table mat-table [dataSource]="volunteers()" class="app-card">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let v"><strong>{{ v.name }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="contact">
            <th mat-header-cell *matHeaderCellDef>Contact</th>
            <td mat-cell *matCellDef="let v">{{ v.phone ?? '—' }}<br /><span class="muted">{{ v.email ?? '' }}</span></td>
          </ng-container>
          <ng-container matColumnDef="notes">
            <th mat-header-cell *matHeaderCellDef>Notes</th>
            <td mat-cell *matCellDef="let v">{{ v.notes ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let v">
              @if (canManage()) {
                <button mat-icon-button (click)="editVolunteer(v)"><mat-icon>edit</mat-icon></button>
                <button mat-icon-button (click)="removeVolunteer(v)"><mat-icon>delete_outline</mat-icon></button>
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
  `]
})
export class FestivalVolunteersTabComponent implements OnInit {
  festivalId = input.required<number>();
  canManage = input(false);

  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly volunteers = signal<FestivalVolunteerDto[]>([]);
  readonly displayedColumns = ['name', 'contact', 'notes', 'actions'];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.festivalService.getVolunteers(this.festivalId()).subscribe((data) => {
      this.volunteers.set(data);
      this.loading.set(false);
    });
  }

  private fields(volunteer?: FestivalVolunteerDto) {
    return [
      { key: 'name', label: 'Name', type: 'text' as const, defaultValue: volunteer?.name ?? '' },
      { key: 'phone', label: 'Phone', type: 'text' as const, required: false, defaultValue: volunteer?.phone ?? '' },
      { key: 'email', label: 'Email', type: 'text' as const, required: false, defaultValue: volunteer?.email ?? '' },
      { key: 'notes', label: 'Notes', type: 'textarea' as const, required: false, defaultValue: volunteer?.notes ?? '' }
    ];
  }

  addVolunteer(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '480px', data: { title: 'Add Volunteer', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.createVolunteer({ festivalId: this.festivalId(), ...result }).subscribe(() => {
        this.toast.success('Volunteer added.');
        this.load();
      });
    });
  }

  editVolunteer(volunteer: FestivalVolunteerDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px', data: { title: 'Edit Volunteer', submitLabel: 'Save', fields: this.fields(volunteer) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.updateVolunteer(volunteer.id, result).subscribe(() => {
        this.toast.success('Volunteer updated.');
        this.load();
      });
    });
  }

  removeVolunteer(volunteer: FestivalVolunteerDto): void {
    this.confirmDialog.confirm({
      title: 'Remove Volunteer', destructive: true, message: `Remove "${volunteer.name}" as a volunteer?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.festivalService.deleteVolunteer(volunteer.id).subscribe(() => {
        this.toast.success('Volunteer removed.');
        this.load();
      });
    });
  }
}
