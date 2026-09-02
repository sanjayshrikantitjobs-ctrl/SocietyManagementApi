import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** The building block of every dashboard "cards" row per spec (Total Members,
 * Occupied Flats, Collected Maintenance, ...). */
@Component({
  selector: 'app-stat-card',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  template: `
    <div class="stat-card app-card app-fade-in" [class.stat-card--accent]="accent()">
      <div class="stat-icon" [style.background]="iconBg()" [style.color]="iconColor()">
        <mat-icon>{{ icon() }}</mat-icon>
      </div>
      <div class="stat-body">
        <span class="stat-label">{{ label() }}</span>
        <span class="stat-value">{{ value() }}</span>
        @if (subtext()) { <span class="stat-subtext">{{ subtext() }}</span> }
      </div>
    </div>
  `,
  styles: [`
    .stat-card { display:flex; align-items:center; gap:16px; padding:20px; overflow: hidden; }
    .stat-icon { width:48px; height:48px; border-radius:12px; display:flex; align-items:center; justify-content:center; flex-shrink:0; }
    .stat-body { display:flex; flex-direction:column; min-width:0; flex:1; }
    .stat-label { font-size:13px; color: var(--app-text-muted); }
    .stat-value { font-size:24px; font-weight:700; line-height:1.3; overflow-wrap: anywhere; }
    .stat-subtext { font-size:12px; color: var(--app-text-muted); overflow-wrap: anywhere; }
  `]
})
export class StatCardComponent {
  label = input.required<string>();
  value = input.required<string | number>();
  icon = input('insights');
  subtext = input<string | null>(null);
  iconColor = input('var(--app-primary)');
  iconBg = input('var(--app-primary-light)');
  accent = input(false);
}
