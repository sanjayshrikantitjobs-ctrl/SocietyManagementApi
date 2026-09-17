import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { FACILITY_BOOKING_STATUS_LABELS, FacilityBookingDto } from './models/facility.model';
import { FacilityService } from './services/facility.service';

@Component({
  selector: 'app-my-facility-bookings',
  standalone: true,
  imports: [CommonModule, MatButtonModule, EmptyStateComponent, PageHeaderComponent, SkeletonLoaderComponent],
  template: `
    <div class="app-page">
    <app-page-header title="My Bookings" subtitle="Facilities you've booked for your flat." />

    @if (loading()) {
      <app-skeleton-loader [rows]="4" />
    } @else if (bookings().length === 0) {
      <app-empty-state icon="event_available" title="No bookings yet" message="Book a facility from the Facilities page to see it here." />
    } @else {
      <div class="list">
        @for (b of bookings(); track b.id) {
          <div class="row">
            <div class="main">
              <div class="title">{{ b.facilityName }}</div>
              <div class="meta">{{ b.bookingDate | date: 'mediumDate' }}, {{ b.startTime.substring(0,5) }}-{{ b.endTime.substring(0,5) }}</div>
              @if (b.purpose) { <div class="meta">{{ b.purpose }}</div> }
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
    .meta { font-size: 12px; color: var(--app-text-muted); }
    .amount { font-size: 13px; font-weight: 600; margin-top: 4px; }
    .status { font-size: 12px; font-weight: 500; padding: 3px 10px; border-radius: 10px; background: #f0f0f0; }
    .status-1 { background: #fff3e0; color: #b26a00; }
    .status-2 { background: #e8f5e9; color: #2e7d32; }
    .status-3, .status-4 { background: #fdecea; color: #c0392b; }
    .status-5 { background: #e3f2fd; color: #1565c0; }
  `]
})
export class MyFacilityBookingsComponent implements OnInit {
  private readonly facilityService = inject(FacilityService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly bookings = signal<FacilityBookingDto[]>([]);

  statusLabel(b: FacilityBookingDto): string { return FACILITY_BOOKING_STATUS_LABELS[b.status]; }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.facilityService.getMyBookings({ pageSize: 100 }).subscribe((result) => {
      this.bookings.set(result.items);
      this.loading.set(false);
    });
  }

  cancel(b: FacilityBookingDto): void {
    this.confirmDialog.confirm({ title: 'Cancel Booking', destructive: true, message: `Cancel your booking for "${b.facilityName}"?` })
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.facilityService.cancelBooking(b.id).subscribe(() => {
          this.toast.success('Booking cancelled.');
          this.load();
        });
      });
  }
}
