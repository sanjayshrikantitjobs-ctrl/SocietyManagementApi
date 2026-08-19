import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MEMBER_TYPE_LABELS, MemberDetailDto } from '../models/resident.model';

/** Read-only view of a member's residency and vehicle history, opened from
 * the Members tab's "View" row action. */
@Component({
  selector: 'app-member-detail-dialog',
  standalone: true,
  imports: [CommonModule, RouterLink, MatDialogModule, MatIconModule],
  template: `
    <h2 mat-dialog-title>{{ member.firstName }} {{ member.lastName }}</h2>
    <mat-dialog-content>
      <p class="contact"><mat-icon>call</mat-icon> {{ member.phone }} @if (member.email) { <span>· {{ member.email }}</span> }</p>

      <h4>Residencies</h4>
      @if (member.residencies.length === 0) {
        <p class="muted">No residencies on file.</p>
      } @else {
        <div class="rows">
          @for (r of member.residencies; track r.id) {
            <div class="row">
              <a [routerLink]="['/residents/flat', r.flatId]" (click)="dialogRef.close()">Flat {{ r.flatNumber }}</a>
              <span>{{ memberTypeLabels[r.memberType] }}</span>
              <span class="muted">{{ r.moveInDate | date: 'mediumDate' }} @if (r.moveOutDate) { – {{ r.moveOutDate | date: 'mediumDate' }} } @else { (current) }</span>
            </div>
          }
        </div>
      }

      <h4>Vehicles</h4>
      @if (member.vehicles.length === 0) {
        <p class="muted">No vehicles on file.</p>
      } @else {
        <div class="rows">
          @for (v of member.vehicles; track v.id) {
            <div class="row">
              <span>{{ v.registrationNumber }}</span>
              <span class="muted">{{ v.make }} {{ v.model }}</span>
            </div>
          }
        </div>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close()">Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content { width: 420px; max-width: 100%; }
    .contact { display: flex; align-items: center; gap: 6px; color: var(--app-text-muted); font-size: 13px; }
    .contact mat-icon { font-size: 16px; width: 16px; height: 16px; }
    h4 { margin: 16px 0 8px; font-size: 13px; text-transform: uppercase; color: var(--app-text-muted); }
    .rows { display: flex; flex-direction: column; gap: 6px; }
    .row { display: flex; justify-content: space-between; gap: 8px; font-size: 13px; padding: 6px 0; border-bottom: 1px solid var(--app-border); }
    .muted { color: var(--app-text-muted); }
  `]
})
export class MemberDetailDialogComponent {
  dialogRef = inject(MatDialogRef<MemberDetailDialogComponent>);
  member = inject<MemberDetailDto>(MAT_DIALOG_DATA);
  memberTypeLabels = MEMBER_TYPE_LABELS;
}
