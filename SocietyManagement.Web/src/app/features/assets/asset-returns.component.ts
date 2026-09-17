import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { Society } from '../../core/models/society.model';
import { SocietyService } from '../society-setup/services/society.service';
import { AssetBookingDto, AssetBookingItemDto } from './models/asset.model';
import { AssetService } from './services/asset.service';

@Component({
  selector: 'app-asset-returns',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatFormFieldModule, MatSelectModule, EmptyStateComponent, PageHeaderComponent, SkeletonLoaderComponent],
  template: `
    <div class="app-page">
    <app-page-header title="Asset Returns" subtitle="Track issued/returned/damaged/lost quantities and deposit refunds.">
      @if (societies().length > 1) {
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="picker">
          <mat-select [value]="societyId()" (selectionChange)="onSocietyChange($event.value)">
            @for (s of societies(); track s.id) { <mat-option [value]="s.id">{{ s.name }}</mat-option> }
          </mat-select>
        </mat-form-field>
      }
    </app-page-header>

    @if (loading()) {
      <app-skeleton-loader [rows]="4" />
    } @else if (bookings().length === 0) {
      <app-empty-state icon="assignment_return" title="Nothing to track" message="Approved rentals will show up here for issue/return tracking." />
    } @else {
      <div class="list">
        @for (b of bookings(); track b.id) {
          <div class="booking-card">
            <div class="header">
              <div>
                <strong>Flat {{ b.flatNumber }}</strong> &middot; {{ b.startDate | date: 'mediumDate' }} - {{ b.endDate | date: 'mediumDate' }}
              </div>
              <div class="deposit">
                <span>Deposit {{ b.securityDepositAmount | currency: 'INR' }}</span>
                <input type="number" [value]="b.depositRefundAmount" #refundInput (change)="setRefund(b, refundInput.value)" placeholder="Refund amount" />
              </div>
            </div>
            @for (item of b.items; track item.id) {
              <div class="item-row">
                <span class="name">{{ item.quantity }}&times; {{ item.assetName }}</span>
                <label>Issued <input type="number" min="0" [max]="item.quantity" [value]="item.quantityIssued" #issuedInput (change)="setIssued(item, issuedInput.value)" /></label>
                <label>Returned <input type="number" min="0" [value]="item.quantityReturned" #returnedInput (change)="setReturn(item, returnedInput.value, damagedInput.value, lostInput.value)" /></label>
                <label>Damaged <input type="number" min="0" [value]="item.quantityDamaged" #damagedInput (change)="setReturn(item, returnedInput.value, damagedInput.value, lostInput.value)" /></label>
                <label>Lost <input type="number" min="0" [value]="item.quantityLost" #lostInput (change)="setReturn(item, returnedInput.value, damagedInput.value, lostInput.value)" /></label>
              </div>
            }
          </div>
        }
      </div>
    }
    </div>
  `,
  styles: [`
    .picker { width: 200px; margin-right: 8px; }
    .list { display: flex; flex-direction: column; gap: 14px; }
    .booking-card { border: 1px solid var(--app-border); border-radius: 8px; padding: 14px; background: var(--app-surface); }
    .header { display: flex; align-items: center; justify-content: space-between; font-size: 13px; margin-bottom: 10px; }
    .deposit { display: flex; align-items: center; gap: 8px; font-size: 12px; color: var(--app-text-muted); }
    .deposit input { width: 110px; padding: 4px 6px; border: 1px solid var(--app-border); border-radius: 4px; }
    .item-row { display: flex; align-items: center; gap: 14px; padding: 8px 0; border-top: 1px solid var(--app-border); font-size: 12px; flex-wrap: wrap; }
    .item-row .name { flex: 1; min-width: 160px; font-weight: 600; font-size: 13px; }
    .item-row label { display: flex; flex-direction: column; gap: 2px; color: var(--app-text-muted); }
    .item-row input { width: 56px; padding: 4px 6px; border: 1px solid var(--app-border); border-radius: 4px; }
  `]
})
export class AssetReturnsComponent implements OnInit {
  private readonly assetService = inject(AssetService);
  private readonly societyService = inject(SocietyService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly societies = signal<Society[]>([]);
  readonly societyId = signal(0);
  readonly bookings = signal<AssetBookingDto[]>([]);

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

  load(): void {
    this.loading.set(true);
    // Approved (2) and Completed (5) bookings are the ones with something to issue/return.
    this.assetService.getBookings({ societyId: this.societyId(), pageSize: 100 }).subscribe((result) => {
      this.bookings.set(result.items.filter((b) => b.status === 2 || b.status === 5));
      this.loading.set(false);
    });
  }

  setIssued(item: AssetBookingItemDto, value: string): void {
    this.assetService.recordIssue(item.id, Number(value) || 0).subscribe(() => {
      this.toast.success('Issued quantity recorded.');
      this.load();
    });
  }

  setReturn(item: AssetBookingItemDto, returned: string, damaged: string, lost: string): void {
    this.assetService.recordReturn(item.id, Number(returned) || 0, Number(damaged) || 0, Number(lost) || 0).subscribe(() => {
      this.toast.success('Return recorded.');
      this.load();
    });
  }

  setRefund(b: AssetBookingDto, value: string): void {
    this.assetService.recordDepositRefund(b.id, Number(value) || 0).subscribe(() => {
      this.toast.success('Deposit refund recorded.');
      this.load();
    });
  }
}
