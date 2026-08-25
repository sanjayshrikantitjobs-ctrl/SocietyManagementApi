import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ToastService } from '../../../core/services/toast.service';
import { BudgetCardComponent } from '../../../shared/components/budget-card/budget-card.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { BUDGET_CATEGORY_LABELS, FestivalBudgetCategoryDto } from '../models/festival.model';
import { FestivalService } from '../services/festival.service';
import { BudgetRevisionsDialogComponent } from './budget-revisions-dialog.component';

@Component({
  selector: 'app-festival-budget-tab',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatIconModule, BudgetCardComponent, EmptyStateComponent, SkeletonLoaderComponent
  ],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <div class="totals">
          <span>Total Estimated: <strong>₹{{ totalEstimated() | number }}</strong></span>
          <span>Total Approved: <strong>₹{{ totalApproved() | number }}</strong></span>
          <span>Total Actual: <strong>₹{{ totalActual() | number }}</strong></span>
        </div>
        @if (canManage()) {
          <button mat-flat-button color="primary" (click)="addCategory()"><mat-icon>add</mat-icon> Add Category</button>
        }
      </div>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="110" />
      } @else if (categories().length === 0) {
        <app-empty-state icon="pie_chart" title="No budget categories yet"
          message="Add categories like Decoration, Sound, Food to start tracking the budget."
          [actionLabel]="canManage() ? 'Add Category' : null" (action)="addCategory()" />
      } @else {
        <div class="grid">
          @for (category of categories(); track category.id) {
            <app-budget-card [category]="category" [canManage]="canManage()"
              (edit)="editCategory(category)" (remove)="removeCategory(category)" (viewHistory)="viewHistory(category)" />
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; flex-wrap: wrap; gap: 12px; }
    .totals { display: flex; gap: 20px; font-size: 13px; color: var(--app-text-muted); }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 16px; }
  `]
})
export class FestivalBudgetTabComponent implements OnInit {
  festivalId = input.required<number>();
  canManage = input(false);

  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly categories = signal<FestivalBudgetCategoryDto[]>([]);

  private readonly categoryOptions = Object.entries(BUDGET_CATEGORY_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  totalEstimated(): number { return this.categories().reduce((sum, c) => sum + c.estimatedAmount, 0); }
  totalApproved(): number { return this.categories().reduce((sum, c) => sum + c.approvedAmount, 0); }
  totalActual(): number { return this.categories().reduce((sum, c) => sum + c.actualAmount, 0); }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.festivalService.getBudgetCategories(this.festivalId()).subscribe((data) => {
      this.categories.set(data);
      this.loading.set(false);
    });
  }

  addCategory(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: {
        title: 'Add Budget Category',
        fields: [
          { key: 'category', label: 'Category', type: 'select', options: this.categoryOptions },
          { key: 'estimatedAmount', label: 'Estimated Amount', type: 'number' },
          { key: 'approvedAmount', label: 'Approved Amount', type: 'number' },
          { key: 'notes', label: 'Notes', type: 'textarea', required: false }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.createBudgetCategory({
        festivalId: this.festivalId(), category: Number(result.category),
        estimatedAmount: Number(result.estimatedAmount), approvedAmount: Number(result.approvedAmount), notes: result.notes
      }).subscribe(() => {
        this.toast.success('Budget category added.');
        this.load();
      });
    });
  }

  editCategory(category: FestivalBudgetCategoryDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: {
        title: `Edit ${BUDGET_CATEGORY_LABELS[category.category]} Budget`,
        submitLabel: 'Save',
        fields: [
          { key: 'estimatedAmount', label: 'Estimated Amount', type: 'number', defaultValue: category.estimatedAmount },
          { key: 'approvedAmount', label: 'Approved Amount', type: 'number', defaultValue: category.approvedAmount },
          { key: 'notes', label: 'Notes', type: 'textarea', required: false, defaultValue: category.notes ?? '' },
          { key: 'reason', label: 'Reason for change (if any)', type: 'text', required: false }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.updateBudgetCategory(category.id, {
        estimatedAmount: Number(result.estimatedAmount), approvedAmount: Number(result.approvedAmount),
        notes: result.notes, reason: result.reason
      }).subscribe(() => {
        this.toast.success('Budget category updated.');
        this.load();
      });
    });
  }

  removeCategory(category: FestivalBudgetCategoryDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Budget Category', destructive: true,
      message: `Delete the "${BUDGET_CATEGORY_LABELS[category.category]}" budget category?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.festivalService.deleteBudgetCategory(category.id).subscribe(() => {
        this.toast.success('Budget category deleted.');
        this.load();
      });
    });
  }

  viewHistory(category: FestivalBudgetCategoryDto): void {
    this.dialog.open(BudgetRevisionsDialogComponent, { width: '640px', data: category.id });
  }
}
