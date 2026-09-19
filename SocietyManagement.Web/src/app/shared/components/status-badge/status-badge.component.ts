import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';

export type StatusBadgeVariant = 'success' | 'warning' | 'danger' | 'neutral' | 'info';

/** The one status pill every screen should use instead of hand-rolling its
 * own `.badge`/`.status-N` CSS — no new color tokens, just consistent use
 * of the --app-success/warning/danger tokens already in styles.scss
 * (info/neutral fall back to the existing primary/muted tokens, since
 * there's no dedicated --app-info token yet). */
@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="status-badge status-badge--{{ variant() }}">{{ label() }}</span>`,
  styles: [`
    .status-badge {
      display: inline-block;
      font-size: 11.5px;
      font-weight: 600;
      line-height: 1.6;
      padding: 2px 10px;
      border-radius: 10px;
      white-space: nowrap;
    }
    .status-badge--success { background: color-mix(in srgb, var(--app-success) 14%, transparent); color: var(--app-success); }
    .status-badge--warning { background: color-mix(in srgb, var(--app-warning) 16%, transparent); color: var(--app-warning); }
    .status-badge--danger  { background: color-mix(in srgb, var(--app-danger) 12%, transparent); color: var(--app-danger); }
    .status-badge--info    { background: var(--app-primary-light); color: var(--app-primary); }
    .status-badge--neutral { background: var(--app-surface-alt); color: var(--app-text-muted); }
  `]
})
export class StatusBadgeComponent {
  variant = input<StatusBadgeVariant>('neutral');
  label = input.required<string>();
}
