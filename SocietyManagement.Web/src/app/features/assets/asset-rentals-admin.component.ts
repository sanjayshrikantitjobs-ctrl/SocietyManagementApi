import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { Society } from '../../core/models/society.model';
import { SocietyService } from '../society-setup/services/society.service';
import { ASSET_BOOKING_STATUS_LABELS, AssetBookingDto, AssetBookingStatus } from './models/asset.model';
import { AssetService } from './services/asset.service';

@Component({
  selector: 'app-asset-rentals-admin',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatFormFieldModule, MatMenuModule, MatSelectModule, EmptyStateComponent, PageHeaderComponent, SkeletonLoaderComponent],
  template: `
    <div class="app-page">
    <app-page-header title="Asset Rentals" subtitle="Rental requests across your society's equipment.">
      @if (societies().length > 1) {
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="picker">
          <mat-select [value]="societyId()" (selectionChange)="onSocietyChange($event.value)">
            @for (s of societies(); track s.id) { <mat-option [value]="s.id">{{ s.name }}</mat-option> }
          </mat-select>
        </mat-form-field>
      }
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="picker">
        <mat-label>Status</mat-label>
        <mat-select [value]="statusFilter()" (selectionChange)="onStatusChange($event.value)">
          <mat-option [value]="null">All</mat-option>
          @for (s of statusOptions; track s.value) { <mat-option [value]="s.value">{{ s.label }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <button mat-button routerLink="/asset-returns" (click)="$event.stopPropagation()">Issue / Return Tracking</button>
    </app-page-header>

    @if (loading()) {
      <app-skeleton-loader [rows]="4" />
    } @else if (bookings().length === 0) {
      <app-empty-state icon="inventory_2" title="No rental requests yet" message="Asset rental requests will show up here." />
    } @else {
      <div class="list">
        @for (b of bookings(); track b.id) {
          <div class="row">
            <div class="main">
              <div class="title">Flat {{ b.flatNumber }} &middot; {{ b.requestedByName }}</div>
              <div class="meta">{{ b.startDate | date: 'mediumDate' }} - {{ b.endDate | date: 'mediumDate' }}</div>
              <div class="items">
                @for (i of b.items; track i.id) { <span class="item-chip">{{ i.quantity }}&times; {{ i.assetName }}</span> }
              </div>
              <div class="amount">{{ b.totalAmount | currency: 'INR' }}</div>
            </div>
            <div class="status status-{{ b.status }}">{{ statusLabel(b) }}</div>
            <button mat-button [matMenuTriggerFor]="menu">Actions</button>
            <mat-menu #menu="matMenu">
              @if (b.status === 1) {
                <button mat-menu-item (click)="setStatus(b, 2)">Approve</button>
                <button mat-menu-item (click)="setStatus(b, 3)">Reject</button>
              }
              @if (b.status === 2) {
                <button mat-menu-item (click)="setStatus(b, 4)">Cancel</button>
              }
            </mat-menu>
          </div>
        }
      </div>
    }
    </div>
  `,
  styles: [`
    .picker { width: 200px; margin-right: 8px; }
    .list { display: flex; flex-direction: column; gap: 10px; }
    .row { display: flex; align-items: center; gap: 12px; padding: 14px; border-radius: 8px; border: 1px solid var(--app-border); background: var(--app-surface); }
    .main { flex: 1; }
    .title { font-weight: 600; font-size: 14px; }
    .meta { font-size: 12px; color: var(--app-text-muted); }
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
export class AssetRentalsAdminComponent implements OnInit {
  private readonly assetService = inject(AssetService);
  private readonly societyService = inject(SocietyService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly societies = signal<Society[]>([]);
  readonly societyId = signal(0);
  readonly bookings = signal<AssetBookingDto[]>([]);
  readonly statusFilter = signal<AssetBookingStatus | null>(null);

  readonly statusOptions = Object.entries(ASSET_BOOKING_STATUS_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  statusLabel(b: AssetBookingDto): string { return ASSET_BOOKING_STATUS_LABELS[b.status]; }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      this.societies.set(societies);
      if (societies.length > 0) {
        this.societyId.set(societies[0].id);
        this.load();
      } else {
        this.loading.set(false);
      }
    });
  }

  onSocietyChange(id: number): void { this.societyId.set(id); this.load(); }
  onStatusChange(status: AssetBookingStatus | null): void { this.statusFilter.set(status); this.load(); }

  load(): void {
    this.loading.set(true);
    this.assetService.getBookings({ societyId: this.societyId(), status: this.statusFilter() ?? undefined, pageSize: 100 })
      .subscribe((result) => {
        this.bookings.set(result.items);
        this.loading.set(false);
      });
  }

  setStatus(b: AssetBookingDto, status: AssetBookingStatus): void {
    this.assetService.updateBookingStatus(b.id, status).subscribe(() => {
      this.toast.success('Rental status updated.');
      this.load();
    });
  }
}
