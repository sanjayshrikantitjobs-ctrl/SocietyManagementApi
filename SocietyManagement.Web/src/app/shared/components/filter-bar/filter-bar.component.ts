import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';

export interface FilterBarOption {
  value: string;
  label: string;
  /** Optional trailing count pill (e.g. an unread count). */
  count?: number;
}

/** Single-select chip row — the pattern Announcements' Unread/Saved filter
 * and (on mobile) its FilterChipRow counterpart both need, extracted here
 * instead of hand-rolled per screen. Designed to drop into DataTable's
 * existing `[toolbar]` projection slot without any change to DataTable
 * itself. */
@Component({
  selector: 'app-filter-bar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="filter-bar" role="tablist">
      @for (opt of options(); track opt.value) {
        <button type="button" class="filter-chip" [class.active]="opt.value === selected()"
                role="tab" [attr.aria-selected]="opt.value === selected()"
                (click)="selectedChange.emit(opt.value)">
          {{ opt.label }}
          @if (opt.count) { <span class="count">{{ opt.count }}</span> }
        </button>
      }
    </div>
  `,
  styles: [`
    .filter-bar { display: flex; flex-wrap: wrap; gap: 8px; }
    .filter-chip {
      display: inline-flex; align-items: center; gap: 6px;
      font: inherit; font-size: 13px; font-weight: 500;
      padding: 6px 14px; border-radius: 20px;
      border: 1px solid var(--app-border);
      background: var(--app-surface);
      color: var(--app-text);
      cursor: pointer;
      transition: background .12s, border-color .12s, color .12s;
    }
    .filter-chip:hover { background: var(--app-surface-alt); }
    .filter-chip.active {
      background: var(--app-primary-light);
      border-color: var(--app-primary);
      color: var(--app-primary);
    }
    .count {
      display: inline-flex; align-items: center; justify-content: center;
      min-width: 16px; height: 16px; padding: 0 4px;
      border-radius: 8px; background: var(--app-primary); color: #fff;
      font-size: 10.5px; font-weight: 700; line-height: 16px;
    }
  `]
})
export class FilterBarComponent {
  options = input.required<FilterBarOption[]>();
  selected = input<string>('');
  selectedChange = output<string>();
}
