import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { VisitorVisitDto } from './models/visitor.model';

/** Reused both embedded on the Member Dashboard and on the resident's own
 * /visitors landing page — approval must take seconds, so this is
 * deliberately just a photo + a few facts + two big buttons, no menus. */
@Component({
  selector: 'app-visitor-approval-card',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule],
  template: `
    <div class="app-card approval-card">
      <div class="header">
        @if (visit().visitorPhotoUrl) {
          <img [src]="visit().visitorPhotoUrl" alt="" class="photo" />
        } @else {
          <div class="photo photo-placeholder"><mat-icon>person</mat-icon></div>
        }
        <div class="details">
          <strong>{{ visit().visitorName }}</strong>
          <span class="muted">{{ visit().visitorMobile }}</span>
        </div>
      </div>
      <div class="meta">
        <span><mat-icon>info</mat-icon> {{ visit().purposeName }}</span>
        <span><mat-icon>door_front</mat-icon> {{ visit().gateName }}</span>
        <span><mat-icon>schedule</mat-icon> {{ visit().requestedAt | date: 'shortTime' }}</span>
        @if (visit().numberOfVisitors > 1) {
          <span><mat-icon>groups</mat-icon> {{ visit().numberOfVisitors }} people</span>
        }
      </div>
      <div class="actions">
        <button mat-flat-button color="primary" (click)="approve.emit(visit().id)">
          <mat-icon>check</mat-icon> Allow
        </button>
        <button mat-stroked-button color="warn" (click)="reject.emit(visit().id)">
          <mat-icon>close</mat-icon> Reject
        </button>
      </div>
    </div>
  `,
  styles: [`
    .approval-card { padding: 16px; }
    .header { display: flex; align-items: center; gap: 12px; margin-bottom: 10px; }
    .photo { width: 56px; height: 56px; border-radius: 50%; object-fit: cover; flex-shrink: 0; }
    .photo-placeholder { display: flex; align-items: center; justify-content: center; background: var(--app-surface-alt); color: var(--app-text-muted); }
    .details { display: flex; flex-direction: column; }
    .muted { font-size: 12px; color: var(--app-text-muted); }
    .meta { display: flex; flex-wrap: wrap; gap: 12px; font-size: 12px; color: var(--app-text-muted); margin-bottom: 12px; }
    .meta span { display: flex; align-items: center; gap: 4px; }
    .meta mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .actions { display: flex; gap: 8px; }
    .actions button { flex: 1; }
  `]
})
export class VisitorApprovalCardComponent {
  visit = input.required<VisitorVisitDto>();
  approve = output<number>();
  reject = output<number>();
}
