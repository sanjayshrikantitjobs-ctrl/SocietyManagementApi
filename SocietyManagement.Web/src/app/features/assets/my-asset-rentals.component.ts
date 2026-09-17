import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { ASSET_BOOKING_STATUS_LABELS, AssetBookingDto } from './models/asset.model';
import { AssetService } from './services/asset.service';

@Component({
  selector: 'app-my-asset-rentals',
  standalone: true,
  imports: [CommonModule, MatButtonModule, EmptyStateComponent, PageHeaderComponent, SkeletonLoaderComponent],
  template: `
    <div class="app-page">
    <app-page-header title="My Rentals" subtitle="Society equipment you've requested to rent." />

    @if (loading()) {
      <app-skeleton-loader [rows]="4" />
    } @else if (bookings().length === 0) {
      <app-empty-state icon="inventory_2" title="No rentals yet" message="Rent chairs, tables and more from the Assets page." />
    } @else {
      <div class="list">
        @for (b of bookings(); track b.id) {
          <div class="row">
            <div class="main">
              <div class="title">{{ b.startDate | date: 'mediumDate' }} - {{ b.endDate | date: 'mediumDate' }}</div>
              <div class="items">
                @for (i of b.items; track i.id) { <span class="item-chip">{{ i.quantity }}&times; {{ i.assetName }}</span> }
              </div>
              <div class="amount">{{ b.totalAmount | currency: 'INR' }}</div>
            </div>
            <div class="status status-{{ b.status }}">{{ statusLabel(b) }}</div>
            @if (b.status === 1 || b.status === 2) {
              <button mat-button (click)="cancel(b)">Cancel</button>
            }
          </div>
        }
      </div>
    }
    </div>
  `,
  styles: [`
    .list { display: flex; flex-direction: column; gap: 10px; }
    .row { display: flex; align-items: center; gap: 12px; padding: 14px; border-radius: 8px; border: 1px solid var(--app-border); background: var(--app-surface); }
    .main { flex: 1; }
    .title { font-weight: 600; font-size: 14px; }
    .items { display: flex; gap: 6px; flex-wrap: wrap; margin-top: 4px; }
    .item-chip { font-size: 11px; background: #f0f0f0; padding: 2px 8px; border-radius: 10px; }
    .amount { font-size: 13px; font-weight: 600; margin-top: 4px; }
    .status { font-size: 12px; font-weight: 500; padding: 3px 10px; border-radius: 10px; background: #f0f0f0; }
    .status-1 { background: #fff3e0; color: #b26a00; }
    .status-2 { background: #e8f5e9; color: #2e7d32; }
    .status-3, .status-4 { background: #fdecea; color: #c0392b; }
    .status-5 { background: #e3f2fd; color: #1565c0; }
  `]
})
export class MyAssetRentalsComponent implements OnInit {
  private readonly assetService = inject(AssetService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly bookings = signal<AssetBookingDto[]>([]);

  statusLabel(b: AssetBookingDto): string { return ASSET_BOOKING_STATUS_LABELS[b.status]; }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.assetService.getMyBookings({ pageSize: 100 }).subscribe((result) => {
      this.bookings.set(result.items);
      this.loading.set(false);
    });
  }

  cancel(b: AssetBookingDto): void {
    this.confirmDialog.confirm({ title: 'Cancel Rental Request', destructive: true, message: 'Cancel this rental request?' })
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.assetService.cancelBooking(b.id).subscribe(() => {
          this.toast.success('Rental request cancelled.');
          this.load();
        });
      });
  }
}
