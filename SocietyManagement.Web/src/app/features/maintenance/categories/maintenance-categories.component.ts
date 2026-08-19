import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { Society } from '../../../core/models/society.model';
import { SocietyService } from '../../society-setup/services/society.service';
import { CHARGE_TYPE_LABELS, MaintenanceCategoryDto } from '../models/maintenance.model';
import { MaintenanceService } from '../services/maintenance.service';

@Component({
  selector: 'app-maintenance-categories',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatTableModule, EmptyStateComponent, SkeletonLoaderComponent],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Charge Categories</h3>
        <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> Add Category</button>
      </div>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="60" />
      } @else if (categories().length === 0) {
        <app-empty-state icon="receipt_long" title="No charge categories yet"
          message="Add categories like Maintenance, Water Tanker, Lift, Security to start billing." actionLabel="Add Category" (action)="add()" />
      } @else {
        <table mat-table [dataSource]="categories()" class="app-card">
          <ng-container matColumnDef="chargeName">
            <th mat-header-cell *matHeaderCellDef>Charge Name</th>
            <td mat-cell *matCellDef="let c">{{ c.chargeName }}</td>
          </ng-container>
          <ng-container matColumnDef="chargeType">
            <th mat-header-cell *matHeaderCellDef>Type</th>
            <td mat-cell *matCellDef="let c"><mat-chip-set><mat-chip>{{ chargeTypeLabels[c.chargeType] }}</mat-chip></mat-chip-set></td>
          </ng-container>
          <ng-container matColumnDef="monthlyAmount">
            <th mat-header-cell *matHeaderCellDef>Amount</th>
            <td mat-cell *matCellDef="let c">₹{{ c.monthlyAmount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="effectiveFrom">
            <th mat-header-cell *matHeaderCellDef>Effective From</th>
            <td mat-cell *matCellDef="let c">{{ c.effectiveFrom | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let c">
              <span class="badge" [class.badge-success]="c.isActive" [class.badge-muted]="!c.isActive">
                {{ c.isActive ? 'Active' : 'Inactive' }}
              </span>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let c">
              <button mat-icon-button (click)="edit(c)"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button (click)="remove(c)"><mat-icon>delete_outline</mat-icon></button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
    .toolbar h3 { margin: 0; font-size: 15px; }
    table { width: 100%; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .badge-success { background: #dcfce7; color: #15803d; }
    .badge-muted { background: #e2e8f0; color: #475569; }
  `]
})
export class MaintenanceCategoriesComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly categories = signal<MaintenanceCategoryDto[]>([]);
  readonly displayedColumns = ['chargeName', 'chargeType', 'monthlyAmount', 'effectiveFrom', 'status', 'actions'];
  // Widened to a numeric index signature — the mat-table row variable (`let c`)
  // doesn't narrow to the ChargeType literal union, so indexing the stricter
  // Record<ChargeType, string> mapped type fails to type-check in the template.
  readonly chargeTypeLabels: Record<number, string> = CHARGE_TYPE_LABELS;

  private societyId = 0;
  private readonly typeOptions = Object.entries(CHARGE_TYPE_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies: Society[]) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.maintenanceService.getCategories(this.societyId).subscribe((data) => {
      this.categories.set(data);
      this.loading.set(false);
    });
  }

  private fields(category?: MaintenanceCategoryDto) {
    return [
      { key: 'chargeName', label: 'Charge Name', type: 'text' as const, defaultValue: category?.chargeName ?? '' },
      { key: 'chargeType', label: 'Charge Type', type: 'select' as const, options: this.typeOptions, defaultValue: category?.chargeType ?? 1 },
      { key: 'monthlyAmount', label: 'Monthly Amount', type: 'number' as const, defaultValue: category?.monthlyAmount ?? 0 },
      { key: 'effectiveFrom', label: 'Effective From', type: 'date' as const, defaultValue: category?.effectiveFrom?.substring(0, 10) ?? new Date().toISOString().substring(0, 10) },
      { key: 'displayOrder', label: 'Display Order', type: 'number' as const, defaultValue: category?.displayOrder ?? 0 },
      { key: 'isActive', label: 'Active', type: 'checkbox' as const, defaultValue: category?.isActive ?? true }
    ];
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '420px', data: { title: 'Add Charge Category', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.maintenanceService.createCategory({
        societyId: this.societyId, ...result, chargeType: Number(result.chargeType),
        monthlyAmount: Number(result.monthlyAmount), displayOrder: Number(result.displayOrder)
      }).subscribe(() => {
        this.toast.success('Category added.');
        this.load();
      });
    });
  }

  edit(category: MaintenanceCategoryDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px', data: { title: 'Edit Charge Category', submitLabel: 'Save', fields: this.fields(category) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.maintenanceService.updateCategory(category.id, {
        ...result, chargeType: Number(result.chargeType), monthlyAmount: Number(result.monthlyAmount), displayOrder: Number(result.displayOrder)
      }).subscribe(() => {
        this.toast.success('Category updated.');
        this.load();
      });
    });
  }

  remove(category: MaintenanceCategoryDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Category', destructive: true, message: `Delete "${category.chargeName}"? Existing bills keep their history.`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.maintenanceService.deleteCategory(category.id).subscribe(() => {
        this.toast.success('Category deleted.');
        this.load();
      });
    });
  }
}
