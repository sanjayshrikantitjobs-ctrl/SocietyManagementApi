import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { AssetUrlPipe } from '../../shared/pipes/asset-url.pipe';
import { VISIT_STATUS_LABELS, VisitorVisitDto } from './models/visitor.model';

/** Full-detail view of one visitor visit, opened from a row click on the
 * Recent Visitors / Currently Inside tables — shows the captured photo at
 * a readable size instead of the 36px table thumbnail. Takes the row's
 * already-loaded VisitorVisitDto directly; no extra API round-trip since
 * the list query already returns every field this needs. */
@Component({
  selector: 'app-visitor-visit-detail-dialog',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatDialogModule, MatIconModule, AssetUrlPipe],
  template: `
    <h2 mat-dialog-title>Visitor Details</h2>
    <mat-dialog-content class="content">
      <div class="photo-wrap">
        @if (visit.visitorPhotoUrl && !photoError()) {
          <img [src]="visit.visitorPhotoUrl | assetUrl" alt="" class="photo" (error)="photoError.set(true)" />
        } @else {
          <div class="photo photo-placeholder"><mat-icon>person</mat-icon></div>
        }
      </div>

      <div class="fields">
        <div class="field"><span class="label">Name</span><span class="value">{{ visit.visitorName }}</span></div>
        <div class="field"><span class="label">Mobile</span><span class="value">{{ visit.visitorMobile }}</span></div>
        <div class="field"><span class="label">Flat</span><span class="value">{{ visit.flatNumber }}</span></div>
        <div class="field"><span class="label">Purpose</span><span class="value">{{ visit.purposeName }}</span></div>
        <div class="field"><span class="label">Gate</span><span class="value">{{ visit.gateName }}</span></div>
        <div class="field"><span class="label">Number of Visitors</span><span class="value">{{ visit.numberOfVisitors }}</span></div>
        @if (visit.visitorVehicleNumber) {
          <div class="field"><span class="label">Vehicle No.</span><span class="value">{{ visit.visitorVehicleNumber }}</span></div>
        }
        <div class="field"><span class="label">Status</span><span class="value">{{ statusLabel }}</span></div>
        <div class="field"><span class="label">Requested</span><span class="value">{{ visit.requestedAt | date: 'medium' }}</span></div>
        @if (visit.approvedAt) {
          <div class="field"><span class="label">Approved</span><span class="value">{{ visit.approvedAt | date: 'medium' }}</span></div>
        }
        @if (visit.rejectedAt) {
          <div class="field"><span class="label">Rejected</span><span class="value">{{ visit.rejectedAt | date: 'medium' }}</span></div>
        }
        @if (visit.rejectionReason) {
          <div class="field"><span class="label">Reason</span><span class="value">{{ visit.rejectionReason }}</span></div>
        }
        @if (visit.checkInTime) {
          <div class="field"><span class="label">Checked In</span><span class="value">{{ visit.checkInTime | date: 'medium' }}</span></div>
        }
        @if (visit.checkOutTime) {
          <div class="field"><span class="label">Checked Out</span><span class="value">{{ visit.checkOutTime | date: 'medium' }}</span></div>
        }
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close()">Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    :host { display: block; width: min(420px, 90vw); }
    .content { display: flex; flex-direction: column; align-items: center; gap: 16px; padding-top: 8px; }
    .photo-wrap { display: flex; justify-content: center; }
    .photo { width: 160px; height: 160px; border-radius: 12px; object-fit: cover; }
    .photo-placeholder { display: flex; align-items: center; justify-content: center; background: var(--app-primary-light); color: var(--app-primary); }
    .photo-placeholder mat-icon { font-size: 64px; width: 64px; height: 64px; }
    .fields { width: 100%; display: flex; flex-direction: column; gap: 10px; }
    .field { display: flex; justify-content: space-between; gap: 12px; font-size: 14px; border-bottom: 1px solid var(--app-border); padding-bottom: 8px; }
    .label { color: var(--app-text-muted); }
    .value { font-weight: 600; text-align: right; }
  `]
})
export class VisitorVisitDetailDialogComponent {
  dialogRef = inject(MatDialogRef<VisitorVisitDetailDialogComponent>);
  visit = inject<VisitorVisitDto>(MAT_DIALOG_DATA);
  readonly photoError = signal(false);

  get statusLabel(): string {
    return VISIT_STATUS_LABELS[this.visit.status];
  }
}
