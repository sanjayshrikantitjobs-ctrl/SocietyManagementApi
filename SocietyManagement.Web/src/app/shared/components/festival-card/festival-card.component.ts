import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { Festival, FESTIVAL_STATUS_LABELS } from '../../../features/festivals/models/festival.model';

/** Reusable festival summary card for the festivals grid — spec's "Festival
 * Card" reusable component. */
@Component({
  selector: 'app-festival-card',
  standalone: true,
  imports: [CommonModule, MatChipsModule, MatIconModule],
  template: `
    <div class="festival-card app-card app-fade-in" (click)="open.emit()">
      <div class="banner" [style.backgroundImage]="festival().bannerImageUrl ? 'url(' + festival().bannerImageUrl + ')' : null">
        @if (!festival().bannerImageUrl) { <mat-icon>celebration</mat-icon> }
        <span class="status-chip" [class]="'status-' + festival().status">{{ statusLabel() }}</span>
      </div>
      <div class="body">
        <h3>{{ festival().name }}</h3>
        <p class="dates">
          <mat-icon>event</mat-icon>
          {{ festival().startDate | date: 'MMM d' }} – {{ festival().endDate | date: 'MMM d, y' }}
        </p>
        <div class="stats">
          <div><span class="label">Budget</span><span class="value">₹{{ festival().totalBudget | number }}</span></div>
          <div><span class="label">Collected</span><span class="value collected">₹{{ festival().collected | number }}</span></div>
          <div><span class="label">Spent</span><span class="value spent">₹{{ festival().spent | number }}</span></div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .festival-card { padding: 0; overflow: hidden; cursor: pointer; transition: transform .15s ease, box-shadow .15s ease; }
    .festival-card:hover { transform: translateY(-2px); box-shadow: 0 8px 20px rgba(0,0,0,.1); }
    .banner { height: 120px; background: var(--app-primary-light); background-size: cover; background-position: center;
      display: flex; align-items: center; justify-content: center; position: relative; }
    .banner mat-icon { font-size: 40px; width: 40px; height: 40px; color: var(--app-primary); opacity: .6; }
    .status-chip { position: absolute; top: 10px; right: 10px; padding: 3px 10px; border-radius: 12px;
      font-size: 11px; font-weight: 600; background: rgba(255,255,255,.9); }
    .status-chip.status-1 { color: #b45309; }
    .status-chip.status-2 { color: #15803d; }
    .status-chip.status-3 { color: #475569; }
    .body { padding: 14px 16px 16px; }
    h3 { margin: 0 0 6px; font-size: 16px; font-weight: 700; }
    .dates { display: flex; align-items: center; gap: 4px; margin: 0 0 12px; font-size: 12px; color: var(--app-text-muted); }
    .dates mat-icon { font-size: 15px; width: 15px; height: 15px; }
    .stats { display: flex; justify-content: space-between; gap: 8px; padding-top: 10px; border-top: 1px solid var(--app-border); }
    .stats > div { display: flex; flex-direction: column; }
    .label { font-size: 10px; color: var(--app-text-muted); text-transform: uppercase; letter-spacing: .03em; }
    .value { font-size: 13px; font-weight: 700; }
    .value.collected { color: #15803d; }
    .value.spent { color: #b45309; }
  `]
})
export class FestivalCardComponent {
  festival = input.required<Festival>();
  open = output<void>();

  statusLabel(): string {
    return FESTIVAL_STATUS_LABELS[this.festival().status];
  }
}
