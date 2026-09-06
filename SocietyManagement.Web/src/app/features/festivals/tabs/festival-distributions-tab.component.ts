import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ToastService } from '../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { DISTRIBUTION_STATUS_LABELS, FestivalDistributionDto } from '../models/festival.model';
import { FestivalService } from '../services/festival.service';
import { FestivalDistributionDetailDialogComponent } from './festival-distribution-detail-dialog.component';

/** Generic festival giveaway tab — Kurta, T-shirt, gift, prasad, coupon,
 * anything a festival hands out to eligible flats. Mirrors the Sponsors
 * tab's card-grid + dialog-drill-down shape: this tab lists distributions,
 * the dialog (FestivalDistributionDetailDialogComponent) holds the
 * eligibility/variant/claim machinery for one of them. */
@Component({
  selector: 'app-festival-distributions-tab',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatChipsModule, MatIconModule, EmptyStateComponent, SkeletonLoaderComponent],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Distributions ({{ distributions().length }})</h3>
        @if (canManage()) {
          <button mat-flat-button color="primary" (click)="addDistribution()"><mat-icon>add</mat-icon> Add Distribution</button>
        }
      </div>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="110" />
      } @else if (distributions().length === 0) {
        <app-empty-state icon="redeem" title="No distributions yet"
          message="Set up a giveaway — Kurta, T-shirt, gift, prasad, coupon — for flats that qualify."
          [actionLabel]="canManage() ? 'Add Distribution' : null" (action)="addDistribution()" />
      } @else {
        <div class="grid">
          @for (d of distributions(); track d.id) {
            <div class="app-card distribution-card" (click)="open(d)">
              <div class="card-header">
                <h4>🎁 {{ d.itemName }}</h4>
                <mat-chip-set><mat-chip [class]="'status-' + d.status">{{ statusLabel(d.status) }}</mat-chip></mat-chip-set>
              </div>
              @if (d.description) { <p class="description">{{ d.description }}</p> }
              <div class="stats">
                <div><span class="value">{{ d.eligibleFlatsCount }}</span><span class="label">Eligible Flats</span></div>
                <div><span class="value">{{ d.totalQuantity }}</span><span class="label">Total Qty</span></div>
                <div><span class="value pending">{{ d.pendingCount }}</span><span class="label">Pending</span></div>
                <div><span class="value distributed">{{ d.distributedCount }}</span><span class="label">Distributed</span></div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
    .toolbar h3 { margin: 0; font-size: 15px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; }
    .distribution-card { cursor: pointer; padding: 18px; transition: box-shadow 0.15s; }
    .distribution-card:hover { box-shadow: 0 2px 10px rgba(0,0,0,0.08); }
    .card-header { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-bottom: 6px; }
    .card-header h4 { margin: 0; font-size: 15px; }
    .description { margin: 0 0 12px; font-size: 12px; color: var(--app-text-muted); }
    .stats { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; text-align: center; }
    .stats div { display: flex; flex-direction: column; }
    .stats .value { font-size: 16px; font-weight: 700; }
    .stats .value.pending { color: #b45309; }
    .stats .value.distributed { color: #15803d; }
    .stats .label { font-size: 10px; color: var(--app-text-muted); text-transform: uppercase; }
    .status-1 { background: #e2e8f0; color: #475569; }
    .status-2 { background: #dcfce7; color: #15803d; }
    .status-3 { background: #fee2e2; color: #991b1b; }
  `]
})
export class FestivalDistributionsTabComponent implements OnInit {
  festivalId = input.required<number>();
  societyId = input.required<number>();
  canManage = input(false);
  canSelect = input(false);

  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly distributions = signal<FestivalDistributionDto[]>([]);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.festivalService.getDistributions(this.festivalId()).subscribe((distributions) => {
      this.distributions.set(distributions);
      this.loading.set(false);
    });
  }

  statusLabel(status: number): string {
    return DISTRIBUTION_STATUS_LABELS[status as 1 | 2 | 3];
  }

  addDistribution(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px',
      data: {
        title: 'Add Distribution', submitLabel: 'Create',
        fields: [
          { key: 'itemName', label: 'Item Name (e.g. Kurta, T-shirt, Prasad)', type: 'text' },
          { key: 'description', label: 'Description (optional)', type: 'textarea', required: false },
          { key: 'quantityPerFlat', label: 'Quantity per Eligible Flat', type: 'number', defaultValue: 1 },
          {
            key: 'eligibilityType', label: 'Eligibility', type: 'select', defaultValue: 1,
            options: [
              { value: 1, label: 'Every Flat' },
              { value: 2, label: 'Partially or Fully Paid Contribution' },
              { value: 3, label: 'Minimum Contribution Amount' }
            ]
          },
          {
            key: 'eligibilityMinContribution', label: 'Minimum Contribution Amount (₹)', type: 'number', required: false,
            hint: 'Only used when Eligibility is "Minimum Contribution Amount" above.'
          },
          {
            key: 'variantLabels', label: 'Size/Variant Options (optional)', type: 'text', required: false,
            hint: 'Comma-separated, e.g. L, XL, XXL, XXXL, 4XL. Leave blank if this item has no size/variant choice.'
          }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      const variantLabels = String(result.variantLabels || '').split(',').map((s: string) => s.trim()).filter(Boolean);
      this.festivalService.createDistribution({
        festivalId: this.festivalId(), itemName: result.itemName, description: result.description || null,
        eligibilityType: Number(result.eligibilityType),
        eligibilityMinContribution: result.eligibilityMinContribution ? Number(result.eligibilityMinContribution) : null,
        quantityPerFlat: Number(result.quantityPerFlat), variantLabels
      }).subscribe(() => {
        this.toast.success('Distribution created.');
        this.load();
      });
    });
  }

  open(distribution: FestivalDistributionDto): void {
    const ref = this.dialog.open(FestivalDistributionDetailDialogComponent, {
      width: '900px', maxWidth: '95vw', maxHeight: '90vh',
      data: { distributionId: distribution.id, societyId: this.societyId(), canManage: this.canManage(), canSelect: this.canSelect() }
    });
    ref.afterClosed().subscribe((changed) => {
      if (changed) this.load();
    });
  }
}
