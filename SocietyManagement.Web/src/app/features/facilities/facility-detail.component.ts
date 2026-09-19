import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { AssetUrlPipe } from '../../shared/pipes/asset-url.pipe';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { FacilityBookingFormDialogComponent } from './facility-booking-form-dialog.component';
import { FacilityFormDialogComponent } from './facility-form-dialog.component';
import { FACILITY_BOOKING_STATUS_LABELS, FACILITY_PRICING_TYPE_LABELS, FACILITY_TYPE_LABELS, FacilityBlackoutDateDto, FacilityDto, FacilitySlotDto } from './models/facility.model';
import { FacilityService } from './services/facility.service';

@Component({
  selector: 'app-facility-detail',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatDatepickerModule, MatFormFieldModule, MatIconModule, MatInputModule,
    AssetUrlPipe, PageHeaderComponent, StatusBadgeComponent
  ],
  template: `
    @if (facility(); as f) {
    <div class="app-page">
      <app-page-header [title]="f.name" [subtitle]="typeLabel(f) + ' · Capacity ' + f.capacity">
        <button mat-button (click)="router.navigate(['/facilities'])"><mat-icon>arrow_back</mat-icon> Back</button>
        @if (canManage()) {
          <button mat-button (click)="editFacility(f)"><mat-icon>edit</mat-icon> Edit</button>
        }
      </app-page-header>

      <div class="content">
        @if (f.imageUrl) { <img [src]="f.imageUrl | assetUrl" class="hero" alt="" /> }

        <div class="rate-card">
          <span class="rate-card-label">Rate &amp; charges</span>
          <div class="rate-row">
            <div><strong>{{ f.pricePerUnit | currency: 'INR' }}</strong> / {{ pricingLabel(f) }}</div>
            @if (f.securityDeposit > 0) { <div>Deposit {{ f.securityDeposit | currency: 'INR' }}</div> }
            @if (f.cleaningCharge > 0) { <div>Cleaning {{ f.cleaningCharge | currency: 'INR' }}</div> }
            @if (f.requiresApproval) { <app-status-badge variant="warning" label="Requires Approval" /> }
          </div>
        </div>
        @if (f.description) { <p class="description">{{ f.description }}</p> }

        <h3>Availability</h3>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="date-field">
          <mat-label>Check availability for</mat-label>
          <input matInput [matDatepicker]="picker" [value]="selectedDate()" (dateChange)="onDateChange($event.value)" />
          <mat-datepicker-toggle matSuffix [for]="picker"></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
        </mat-form-field>

        @if (slots().length === 0) {
          <p class="empty">No bookings yet on this date — fully available.</p>
        } @else {
          <div class="slots">
            @for (s of slots(); track s.startTime) {
              <div class="slot slot-{{ s.status }}">{{ s.startTime.substring(0,5) }} - {{ s.endTime.substring(0,5) }} &middot; {{ statusLabel(s.status) }}</div>
            }
          </div>
        }

        @if (canBook()) {
          <button mat-flat-button color="primary" class="book-btn" (click)="bookFacility(f)">Book This Facility</button>
        }

        @if (canManage()) {
          <h3>Blackout Dates</h3>
          <div class="blackout-form">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Add blackout date</mat-label>
              <input matInput [matDatepicker]="blackoutPicker" (dateChange)="pendingBlackoutDate.set($event.value)" />
              <mat-datepicker-toggle matSuffix [for]="blackoutPicker"></mat-datepicker-toggle>
              <mat-datepicker #blackoutPicker></mat-datepicker>
            </mat-form-field>
            <button mat-stroked-button [disabled]="!pendingBlackoutDate()" (click)="addBlackoutDate(pendingBlackoutDate())">Add</button>
          </div>
          @if (blackoutDates().length > 0) {
            <div class="blackout-list">
              @for (b of blackoutDates(); track b.id) {
                <div class="blackout-row">
                  <span>{{ b.blackoutDate | date: 'mediumDate' }}</span>
                  @if (b.reason) { <span class="muted">{{ b.reason }}</span> }
                  <button mat-icon-button (click)="removeBlackoutDate(b)"><mat-icon>close</mat-icon></button>
                </div>
              }
            </div>
          }
        }
      </div>
    </div>
    }
  `,
  styles: [`
    .content { max-width: 720px; }
    .hero { width: 100%; max-height: 260px; object-fit: cover; border-radius: 10px; margin-bottom: 16px; }
    .rate-card { background: var(--app-surface-alt); border-radius: 10px; padding: 12px 16px; margin-bottom: 8px; }
    .rate-card-label { display: block; font-size: 11px; text-transform: uppercase; letter-spacing: .03em; color: var(--app-text-muted); margin-bottom: 6px; }
    .rate-row { display: flex; gap: 16px; align-items: center; font-size: 13px; flex-wrap: wrap; }
    .description { color: var(--app-text-muted); font-size: 14px; }
    h3 { margin: 20px 0 10px; font-size: 15px; }
    .date-field { width: 240px; }
    .slots { display: flex; flex-direction: column; gap: 6px; margin-top: 10px; }
    .slot { padding: 8px 12px; border-radius: 6px; font-size: 13px; background: #f0f0f0; }
    .slot-1 { background: #fff3e0; color: #b26a00; }
    .slot-2, .slot-6, .slot-7 { background: #fdecea; color: #c0392b; }
    .slot-5 { background: #e8f5e9; color: #2e7d32; }
    .empty { color: var(--app-text-muted); font-size: 13px; }
    .book-btn { margin-top: 16px; }
    .blackout-form { display: flex; align-items: center; gap: 10px; }
    .blackout-list { margin-top: 10px; display: flex; flex-direction: column; gap: 4px; }
    .blackout-row { display: flex; align-items: center; gap: 10px; font-size: 13px; }
    .muted { color: var(--app-text-muted); }
  `]
})
export class FacilityDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  readonly router = inject(Router);
  private readonly facilityService = inject(FacilityService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  readonly facility = signal<FacilityDto | null>(null);
  readonly selectedDate = signal(new Date());
  readonly slots = signal<FacilitySlotDto[]>([]);
  readonly blackoutDates = signal<FacilityBlackoutDateDto[]>([]);
  readonly pendingBlackoutDate = signal<Date | null>(null);

  private facilityId = 0;

  canManage(): boolean { return this.auth.hasPermission('facilities.manage'); }
  canBook(): boolean { return this.auth.hasPermission('facilities.book'); }

  typeLabel(f: FacilityDto): string { return FACILITY_TYPE_LABELS[f.type]; }
  pricingLabel(f: FacilityDto): string { return FACILITY_PRICING_TYPE_LABELS[f.pricingType]; }
  statusLabel(status: number): string { return FACILITY_BOOKING_STATUS_LABELS[status as 1]; }

  ngOnInit(): void {
    this.facilityId = Number(this.route.snapshot.paramMap.get('id'));
    this.facilityService.getFacility(this.facilityId).subscribe((f) => this.facility.set(f));
    this.loadAvailability();
    if (this.canManage()) {
      this.facilityService.getBlackoutDates(this.facilityId).subscribe((rows) => this.blackoutDates.set(rows));
    }
  }

  private toDateOnly(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  onDateChange(date: Date | null): void {
    if (!date) return;
    this.selectedDate.set(date);
    this.loadAvailability();
  }

  private loadAvailability(): void {
    this.facilityService.getAvailability(this.facilityId, this.toDateOnly(this.selectedDate())).subscribe((slots) => this.slots.set(slots));
  }

  editFacility(f: FacilityDto): void {
    const ref = this.dialog.open(FacilityFormDialogComponent, { width: '660px', data: { societyId: f.societyId, facility: f } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.facilityService.updateFacility(f.id, result).subscribe((updated) => {
        this.toast.success('Facility updated.');
        this.facilityService.getFacility(f.id).subscribe((fresh) => this.facility.set(fresh));
      });
    });
  }

  bookFacility(f: FacilityDto): void {
    const ref = this.dialog.open(FacilityBookingFormDialogComponent, { width: '520px', data: { facility: f, date: this.selectedDate() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.facilityService.createBooking(result).subscribe({
        next: () => {
          this.toast.success('Facility booked.');
          this.loadAvailability();
        },
        error: (err) => this.toast.error(err?.error?.message || 'Could not complete the booking.')
      });
    });
  }

  addBlackoutDate(date: Date | null): void {
    if (!date) return;
    this.facilityService.addBlackoutDate(this.facilityId, this.toDateOnly(date)).subscribe(() => {
      this.toast.success('Blackout date added.');
      this.pendingBlackoutDate.set(null);
      this.facilityService.getBlackoutDates(this.facilityId).subscribe((rows) => this.blackoutDates.set(rows));
    });
  }

  removeBlackoutDate(b: FacilityBlackoutDateDto): void {
    this.confirmDialog.confirm({ title: 'Remove Blackout Date', message: 'Remove this blackout date?' }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.facilityService.removeBlackoutDate(b.id).subscribe(() => {
        this.blackoutDates.set(this.blackoutDates().filter((x) => x.id !== b.id));
      });
    });
  }
}
