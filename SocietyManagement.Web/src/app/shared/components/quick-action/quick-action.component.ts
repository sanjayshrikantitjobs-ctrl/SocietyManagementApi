import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

/** Icon-tile + label shortcut — the building block for the Dashboard's
 * quick-action row (§6/§12 of the strategy doc) and any other "jump
 * straight to X" affordance (e.g. an SOS entry point). Same contract as
 * Mobile's QuickAction control: icon, label, optional badge count, and
 * either a routerLink or a click handler, never both at once. */
@Component({
  selector: 'app-quick-action',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule],
  template: `
    @if (routerLink()) {
      <a class="quick-action" [routerLink]="routerLink()">
        <ng-container *ngTemplateOutlet="body" />
      </a>
    } @else {
      <button type="button" class="quick-action" (click)="activated.emit()">
        <ng-container *ngTemplateOutlet="body" />
      </button>
    }
    <ng-template #body>
      <span class="icon-tile">
        <mat-icon>{{ icon() }}</mat-icon>
        @if (badge()) { <span class="badge">{{ badge() }}</span> }
      </span>
      <span class="label">{{ label() }}</span>
    </ng-template>
  `,
  styles: [`
    .quick-action {
      display: flex; flex-direction: column; align-items: center; gap: 6px;
      width: 76px; padding: 4px 0;
      border: none; background: none; cursor: pointer;
      text-decoration: none; color: inherit; font: inherit;
    }
    .icon-tile {
      position: relative;
      display: flex; align-items: center; justify-content: center;
      width: 48px; height: 48px; border-radius: 14px;
      background: var(--app-primary-light); color: var(--app-primary);
    }
    .badge {
      position: absolute; top: -4px; right: -4px;
      min-width: 16px; height: 16px; padding: 0 4px;
      display: flex; align-items: center; justify-content: center;
      border-radius: 8px; background: var(--app-danger); color: #fff;
      font-size: 10px; font-weight: 700;
    }
    .label { font-size: 12px; text-align: center; color: var(--app-text); line-height: 1.25; }
  `]
})
export class QuickActionComponent {
  icon = input.required<string>();
  label = input.required<string>();
  badge = input<number | null>(null);
  routerLink = input<string | null>(null);
  activated = output<void>();
}
