import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { Society } from '../../core/models/society.model';
import { SocietyService } from '../society-setup/services/society.service';
import { FACILITY_BOOKING_STATUS_LABELS, FacilityBookingDto, FacilityBookingStatus } from './models/facility.model';
import { FacilityService } from './services/facility.service';

@Component({
  selector: 'app-facility-bookings-admin',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatFormFieldModule, MatMenuModule, MatSelectModule, EmptyStateComponent, PageHeaderComponent, SkeletonLoaderComponent],
  template: `
    <div class="app-page">
    <app-page-header title="Facility Bookings" subtitle="Every booking request across your society's facilities.">
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
    </app-page-header>

    @if (loading()) {
      <app-skeleton-loader [rows]="4" />
    } @else if (bookings().length === 0) {
      <app-empty-state icon="event_available" title="No bookings yet" message="Facility booking requests will show up here." />
    } @else {
      <div class="list">
        @for (b of bookings(); track b.id) {
          <div class="row">
            <div class="main">
              <div class="title">{{ b.facilityName }} &middot; Flat {{ b.flatNumber }}</div>
              <div class="meta">{{ b.bookingDate | date: 'mediumDate' }}, {{ b.startTime.substring(0,5) }}-{{ b.endTime.substring(0,5) }} &middot; {{ b.bookedByName }}</div>
              @if (b.purpose) { <div class="meta">{{ b.purpose }}</div> }
              <div class="amount">{{ b.totalAmount | currency: 'INR' }}</div>
            </div>
            <div class="status status-{{ b.status }}">{{ statusLabel(b.status) }}</div>
            <button mat-button [matMenuTriggerFor]="menu">Actions</button>
            <mat-menu #menu="matMenu">
              @if (b.status === 1) {
                <button mat-menu-item (click)="setStatus(b, 2)">Approve</button>
                <button mat-menu-item (click)="setStatus(b, 3)">Reject</button>
              }
              @if (b.status === 2) {
                <button mat-menu-item (click)="setStatus(b, 5)">Mark Completed</button>
                <button mat-menu-item (click)="setStatus(b, 4)">Cancel</button>
              }
              <button mat-menu-item (click)="markPaymentCompleted(b)">Mark Payment Completed</button>
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
    .amount { font-size: 13px; font-weight: 600; margin-top: 4px; }
    .status { font-size: 12px; font-weight: 500; padding: 3px 10px; border-radius: 10px; background: #f0f0f0; }
    .status-1 { background: #fff3e0; color: #b26a00; }
    .status-2 { background: #e8f5e9; color: #2e7d32; }
    .status-3, .status-4 { background: #fdecea; color: #c0392b; }
    .status-5 { background: #e3f2fd; color: #1565c0; }
  `]
})
export class FacilityBookingsAdminComponent implements OnInit {
  private readonly facilityService = inject(FacilityService);
  private readonly societyService = inject(SocietyService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly societies = signal<Society[]>([]);
  readonly societyId = signal(0);
  readonly bookings = signal<FacilityBookingDto[]>([]);
  readonly statusFilter = signal<FacilityBookingStatus | null>(null);

  readonly statusOptions = Object.entries(FACILITY_BOOKING_STATUS_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  statusLabel(status: FacilityBookingStatus): string { return FACILITY_BOOKING_STATUS_LABELS[status]; }

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
  onStatusChange(status: FacilityBookingStatus | null): void { this.statusFilter.set(status); this.load(); }

  load(): void {
    this.loading.set(true);
    this.facilityService.getBookings({ societyId: this.societyId(), status: this.statusFilter() ?? undefined, pageSize: 100 })
      .subscribe((result) => {
        this.bookings.set(result.items);
        this.loading.set(false);
      });
  }

  setStatus(b: FacilityBookingDto, status: FacilityBookingStatus): void {
    this.facilityService.updateBookingStatus(b.id, status).subscribe(() => {
      this.toast.success('Booking updated.');
      this.load();
    });
  }

  markPaymentCompleted(b: FacilityBookingDto): void {
    this.facilityService.recordPayment(b.id, 2).subscribe(() => {
      this.toast.success('Payment marked completed.');
      this.load();
    });
  }
}
