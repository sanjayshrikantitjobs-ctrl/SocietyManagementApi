import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { AssetUrlPipe } from '../../shared/pipes/asset-url.pipe';
import { StaffDto } from '../staff/models/staff.model';
import { StaffService } from '../staff/services/staff.service';
import {
  COMPLAINT_CATEGORY_LABELS, COMPLAINT_PRIORITY_LABELS, COMPLAINT_STATUS_LABELS, ComplaintDto
} from './models/complaint.model';
import { ComplaintService } from './services/complaint.service';

export interface ComplaintDetailDialogData {
  complaintId: number;
  societyId: number;
}

/** Shared by both the admin Kanban board and the resident's My Complaints
 * page — action buttons are shown/hidden based on the caller's role and
 * whether they raised this specific complaint. */
@Component({
  selector: 'app-complaint-detail-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatChipsModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule, AssetUrlPipe
  ],
  template: `
    <h2 mat-dialog-title>Complaint Details</h2>
    <mat-dialog-content class="content">
      @if (loading()) {
        <div class="loading-row"><mat-spinner diameter="32" /></div>
      } @else if (complaint(); as c) {
        <div class="header-row">
          <mat-chip-set>
            <mat-chip [class]="'status-' + c.status">{{ statusLabels[c.status] }}</mat-chip>
            <mat-chip [class]="'priority-' + c.priority">{{ priorityLabels[c.priority] }} Priority</mat-chip>
          </mat-chip-set>
        </div>

        <h3>{{ c.title }}</h3>
        <p class="description">{{ c.description }}</p>

        @if (c.photoUrl) {
          <img [src]="c.photoUrl | assetUrl" alt="Complaint photo" class="photo" />
        }

        <div class="info-grid">
          <div><span class="label">Flat</span><span>{{ c.flatNumber }}</span></div>
          <div><span class="label">Category</span><span>{{ categoryLabels[c.category] }}</span></div>
          <div><span class="label">Raised By</span><span>{{ c.raisedByName }}</span></div>
          <div><span class="label">Raised On</span><span>{{ c.createdAt | date: 'medium' }}</span></div>
          @if (c.assignedStaffName) { <div><span class="label">Assigned To</span><span>{{ c.assignedStaffName }}</span></div> }
          @if (c.assignedAt) { <div><span class="label">Assigned On</span><span>{{ c.assignedAt | date: 'medium' }}</span></div> }
          @if (c.resolvedAt) { <div><span class="label">Resolved On</span><span>{{ c.resolvedAt | date: 'medium' }}</span></div> }
          @if (c.closedAt) { <div><span class="label">Closed On</span><span>{{ c.closedAt | date: 'medium' }}</span></div> }
        </div>

        @if (c.resolutionNotes) {
          <div class="notes-block"><span class="label">Resolution Notes</span><p>{{ c.resolutionNotes }}</p></div>
        }
        @if (c.reopenReason) {
          <div class="notes-block"><span class="label">Reopen Reason</span><p>{{ c.reopenReason }}</p></div>
        }

        <!-- Admin actions -->
        @if (isAdmin() && c.status === 1) {
          <div class="action-block">
            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full-width">
              <mat-label>Assign To Staff</mat-label>
              <mat-select [(ngModel)]="selectedStaffId">
                @for (s of staffOptions(); track s.id) { <mat-option [value]="s.id">{{ s.firstName }} {{ s.lastName }}</mat-option> }
              </mat-select>
            </mat-form-field>
            <button mat-flat-button color="primary" [disabled]="!selectedStaffId" (click)="assign(c.id)">Assign</button>
          </div>
        }
        @if (isAdmin() && c.status === 2) {
          <div class="action-block">
            <button mat-flat-button color="primary" (click)="start(c.id)">Start Progress</button>
          </div>
        }
        @if (isAdmin() && c.status === 3) {
          <div class="action-block">
            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full-width">
              <mat-label>Resolution Notes</mat-label>
              <textarea matInput rows="2" [(ngModel)]="resolutionNotes"></textarea>
            </mat-form-field>
            <button mat-flat-button color="primary" [disabled]="!resolutionNotes" (click)="resolve(c.id)">Resolve</button>
          </div>
        }

        <!-- Raiser (or admin) actions -->
        @if ((isAdmin() || isRaiser(c)) && c.status === 4) {
          <div class="action-block">
            <button mat-flat-button color="primary" (click)="close(c.id)">Close (Confirm Fixed)</button>
          </div>
          <div class="action-block">
            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full-width">
              <mat-label>Reopen Reason</mat-label>
              <textarea matInput rows="2" [(ngModel)]="reopenReason"></textarea>
            </mat-form-field>
            <button mat-stroked-button color="warn" [disabled]="!reopenReason" (click)="reopen(c.id)">Reopen</button>
          </div>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close()">Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .content { width: 480px; max-width: 100%; }
    .loading-row { display: flex; justify-content: center; padding: 32px; }
    .header-row { margin-bottom: 8px; }
    h3 { margin: 8px 0 4px; }
    .description { color: var(--app-text-muted); margin: 0 0 12px; white-space: pre-wrap; }
    .photo { max-width: 100%; border-radius: 8px; margin-bottom: 12px; }
    .info-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-bottom: 12px; }
    .info-grid > div { display: flex; flex-direction: column; }
    .label { font-size: 11px; color: var(--app-text-muted); text-transform: uppercase; }
    .notes-block { margin-bottom: 12px; }
    .notes-block p { margin: 2px 0 0; }
    .action-block { display: flex; flex-direction: column; gap: 8px; padding: 12px; border-top: 1px solid var(--app-border); }
    .full-width { width: 100%; }
    .status-1 { background: #e2e8f0 !important; color: #475569 !important; }
    .status-2 { background: #dbeafe !important; color: #1d4ed8 !important; }
    .status-3 { background: #fef3c7 !important; color: #b45309 !important; }
    .status-4 { background: #dcfce7 !important; color: #15803d !important; }
    .status-5 { background: #f1f5f9 !important; color: #64748b !important; }
    .priority-1 { background: #f1f5f9 !important; color: #64748b !important; }
    .priority-2 { background: #fef3c7 !important; color: #b45309 !important; }
    .priority-3 { background: #fee2e2 !important; color: #dc2626 !important; }
  `]
})
export class ComplaintDetailDialogComponent implements OnInit {
  dialogRef = inject(MatDialogRef<ComplaintDetailDialogComponent>);
  data = inject<ComplaintDetailDialogData>(MAT_DIALOG_DATA);
  private readonly complaintService = inject(ComplaintService);
  private readonly staffService = inject(StaffService);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly complaint = signal<ComplaintDto | null>(null);
  readonly staffOptions = signal<StaffDto[]>([]);
  readonly isAdmin = this.auth.isAdmin;

  selectedStaffId: number | null = null;
  resolutionNotes = '';
  reopenReason = '';

  readonly categoryLabels: Record<number, string> = COMPLAINT_CATEGORY_LABELS;
  readonly priorityLabels: Record<number, string> = COMPLAINT_PRIORITY_LABELS;
  readonly statusLabels: Record<number, string> = COMPLAINT_STATUS_LABELS;

  ngOnInit(): void {
    this.load();
    this.staffService.getStaff({ societyId: this.data.societyId, isActive: true, pageSize: 200 })
      .subscribe((result) => this.staffOptions.set(result.items));
  }

  load(): void {
    this.loading.set(true);
    this.complaintService.getById(this.data.complaintId).subscribe((c) => {
      this.complaint.set(c);
      this.loading.set(false);
    });
  }

  isRaiser(c: ComplaintDto): boolean {
    return c.raisedByUserId === this.auth.currentUser()?.id;
  }

  assign(id: number): void {
    if (!this.selectedStaffId) return;
    this.complaintService.assign(id, this.selectedStaffId).subscribe(() => {
      this.toast.success('Complaint assigned.');
      this.load();
    });
  }

  start(id: number): void {
    this.complaintService.start(id).subscribe(() => {
      this.toast.success('Complaint moved to in progress.');
      this.load();
    });
  }

  resolve(id: number): void {
    this.complaintService.resolve(id, this.resolutionNotes).subscribe(() => {
      this.toast.success('Complaint resolved.');
      this.load();
    });
  }

  close(id: number): void {
    this.complaintService.close(id).subscribe(() => {
      this.toast.success('Complaint closed.');
      this.load();
    });
  }

  reopen(id: number): void {
    this.complaintService.reopen(id, this.reopenReason).subscribe(() => {
      this.toast.success('Complaint reopened.');
      this.load();
    });
  }
}
