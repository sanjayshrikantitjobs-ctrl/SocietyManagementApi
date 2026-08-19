import { CommonModule } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BUDGET_CATEGORY_LABELS, FestivalBudgetCategoryDto } from '../../../features/festivals/models/festival.model';

/** Reusable per-category budget card (Estimated/Approved/Actual/Remaining +
 * spend progress bar) — spec's "Budget Card" reusable component. */
@Component({
  selector: 'app-budget-card',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule],
  template: `
    <div class="budget-card app-card">
      <div class="header">
        <h4>{{ categoryLabel() }}</h4>
        <div class="actions">
          <button mat-icon-button (click)="viewHistory.emit()" matTooltip="Revision history">
            <mat-icon>history</mat-icon>
          </button>
          <button mat-icon-button (click)="edit.emit()">
            <mat-icon>edit</mat-icon>
          </button>
          <button mat-icon-button (click)="remove.emit()">
            <mat-icon>delete_outline</mat-icon>
          </button>
        </div>
      </div>
      <mat-progress-bar mode="determinate" [value]="spendPercent()" [color]="spendPercent() > 100 ? 'warn' : 'primary'" />
      <div class="figures">
        <div><span class="label">Estimated</span><span class="value">₹{{ category().estimatedAmount | number }}</span></div>
        <div><span class="label">Approved</span><span class="value">₹{{ category().approvedAmount | number }}</span></div>
        <div><span class="label">Actual</span><span class="value">₹{{ category().actualAmount | number }}</span></div>
        <div><span class="label">Remaining</span>
          <span class="value" [class.negative]="category().remaining < 0">₹{{ category().remaining | number }}</span>
        </div>
      </div>
      @if (category().notes) { <p class="notes">{{ category().notes }}</p> }
    </div>
  `,
  styles: [`
    .budget-card { padding: 16px; }
    .header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; }
    h4 { margin: 0; font-size: 14px; font-weight: 700; }
    .actions { display: flex; }
    .actions button { width: 32px; height: 32px; line-height: 32px; }
    .actions mat-icon { font-size: 18px; width: 18px; height: 18px; }
    mat-progress-bar { border-radius: 4px; margin-bottom: 12px; }
    .figures { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; }
    .label { display: block; font-size: 10px; color: var(--app-text-muted); text-transform: uppercase; }
    .value { display: block; font-size: 13px; font-weight: 700; }
    .value.negative { color: var(--app-danger); }
    .notes { margin: 10px 0 0; font-size: 12px; color: var(--app-text-muted); }
  `]
})
export class BudgetCardComponent {
  category = input.required<FestivalBudgetCategoryDto>();
  edit = output<void>();
  remove = output<void>();
  viewHistory = output<void>();

  categoryLabel = computed(() => BUDGET_CATEGORY_LABELS[this.category().category]);
  spendPercent = computed(() => {
    const c = this.category();
    return c.approvedAmount > 0 ? Math.round((c.actualAmount / c.approvedAmount) * 100) : 0;
  });
}
