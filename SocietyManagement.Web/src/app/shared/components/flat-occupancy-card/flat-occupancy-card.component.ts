import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import {
  FlatOccupancySummaryDto, MEMBER_TYPE_LABELS
} from '../../../features/residents/models/resident.model';

/** Reusable "is this flat rented, by whom, how many people live here" card
 * — used both standalone (per-flat) and embedded in other resident views. */
@Component({
  selector: 'app-flat-occupancy-card',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  template: `
    <div class="app-card occupancy-card">
      <div class="header">
        <span class="badge" [class.rented]="summary().isRented" [class.owned]="!summary().isRented">
          <mat-icon>{{ summary().isRented ? 'key' : 'home' }}</mat-icon>
          {{ summary().isRented ? 'Rented' : 'Owner-Occupied' }}
        </span>
        <span class="occupant-count"><mat-icon>groups</mat-icon> {{ summary().occupantCount }} occupant(s)</span>
      </div>

      @if (summary().primaryContact; as pc) {
        <div class="primary-contact">
          <span class="label">Primary Contact</span>
          <span class="value">{{ pc.memberName }} ({{ memberTypeLabels[pc.memberType] }})</span>
          <span class="phone">{{ pc.memberPhone }}</span>
        </div>
      }

      @if (summary().currentResidents.length > 0) {
        <div class="residents-list">
          <span class="label">Current Residents</span>
          @for (r of summary().currentResidents; track r.id) {
            <div class="resident-row">
              <span>{{ r.memberName }}</span>
              <span class="type">{{ memberTypeLabels[r.memberType] }}</span>
            </div>
          }
        </div>
      } @else {
        <p class="muted">No current residents on file.</p>
      }
    </div>
  `,
  styles: [`
    .occupancy-card { padding: 16px; }
    .header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
    .badge { display: flex; align-items: center; gap: 4px; padding: 4px 10px; border-radius: 12px; font-size: 12px; font-weight: 700; }
    .badge mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .badge.rented { background: #fef3c7; color: #b45309; }
    .badge.owned { background: #dcfce7; color: #15803d; }
    .occupant-count { display: flex; align-items: center; gap: 4px; font-size: 12px; color: var(--app-text-muted); }
    .occupant-count mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .primary-contact { padding: 10px; background: var(--app-surface-alt); border-radius: 8px; margin-bottom: 10px;
      display: flex; flex-direction: column; gap: 2px; }
    .label { font-size: 11px; color: var(--app-text-muted); text-transform: uppercase; }
    .value { font-size: 13px; font-weight: 700; }
    .phone { font-size: 12px; color: var(--app-text-muted); }
    .residents-list { display: flex; flex-direction: column; gap: 6px; }
    .resident-row { display: flex; justify-content: space-between; font-size: 13px; padding: 4px 0; border-bottom: 1px solid var(--app-border); }
    .resident-row .type { color: var(--app-text-muted); font-size: 12px; }
    .muted { color: var(--app-text-muted); font-size: 13px; }
  `]
})
export class FlatOccupancyCardComponent {
  summary = input.required<FlatOccupancySummaryDto>();
  memberTypeLabels = MEMBER_TYPE_LABELS;
}
