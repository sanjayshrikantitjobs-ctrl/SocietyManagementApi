import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Flat } from '../../core/models/society.model';
import { SocietyService } from '../society-setup/services/society.service';
import { FacilityDto } from './models/facility.model';

export interface FacilityBookingFormDialogData {
  facility: FacilityDto;
  date: Date;
}

/** Purely a request form — the facility's rate card is shown for reference,
 * but the actual charge is always computed and validated server-side (see
 * FacilityBookingFeature.CreateFacilityBookingCommandHandler), never here. */
@Component({
  selector: 'app-facility-booking-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatDatepickerModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>Book {{ data.facility.name }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="grid">
        @if (flats().length > 1) {
          <mat-form-field appearance="outline" class="span-2">
            <mat-label>Flat</mat-label>
            <mat-select formControlName="flatId">
              @for (f of flats(); track f.id) { <mat-option [value]="f.id">{{ f.flatNumber }}</mat-option> }
            </mat-select>
          </mat-form-field>
        }

        <mat-form-field appearance="outline">
          <mat-label>Date</mat-label>
          <input matInput [matDatepicker]="datePicker" formControlName="bookingDate" />
          <mat-datepicker-toggle matSuffix [for]="datePicker"></mat-datepicker-toggle>
          <mat-datepicker #datePicker></mat-datepicker>
        </mat-form-field>
        <div></div>

        <mat-form-field appearance="outline">
          <mat-label>Start Time</mat-label>
          <input matInput type="time" formControlName="startTime" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>End Time</mat-label>
          <input matInput type="time" formControlName="endTime" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Purpose (optional)</mat-label>
          <input matInput formControlName="purpose" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Guest Count</mat-label>
          <input matInput type="number" formControlName="guestCount" />
        </mat-form-field>
        <div></div>
        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Notes (optional)</mat-label>
          <textarea matInput rows="2" formControlName="notes"></textarea>
        </mat-form-field>

        <div class="rate-card span-2">
          <div>Rate: {{ data.facility.pricePerUnit | currency: 'INR' }}</div>
          @if (data.facility.securityDeposit > 0) { <div>Security Deposit: {{ data.facility.securityDeposit | currency: 'INR' }}</div> }
          @if (data.facility.cleaningCharge > 0) { <div>Cleaning Charge: {{ data.facility.cleaningCharge | currency: 'INR' }}</div> }
          @if (data.facility.additionalCharge > 0) { <div>Additional Charge: {{ data.facility.additionalCharge | currency: 'INR' }}</div> }
          <div class="hint">Final total is calculated and confirmed after you submit.</div>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Book</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .grid { display:grid; grid-template-columns: 1fr 1fr; gap: 0 16px; width: 480px; max-width: 100%; }
    .span-2 { grid-column: span 2; }
    .rate-card { background: var(--app-surface-hover, #f6f8ff); border-radius: 8px; padding: 12px 16px; font-size: 13px; margin-bottom: 12px; }
    .rate-card .hint { margin-top: 6px; color: var(--app-text-muted); font-size: 12px; }
  `]
})
export class FacilityBookingFormDialogComponent {
  dialogRef = inject(MatDialogRef<FacilityBookingFormDialogComponent>);
  data = inject<FacilityBookingFormDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly societyService = inject(SocietyService);

  readonly flats = signal<Flat[]>([]);

  form = this.fb.nonNullable.group({
    flatId: [0, Validators.required],
    bookingDate: [this.data.date, Validators.required],
    startTime: ['10:00', Validators.required],
    endTime: ['13:00', Validators.required],
    purpose: [''],
    guestCount: [0, Validators.min(0)],
    notes: ['']
  });

  constructor() {
    this.societyService.getMyFlats().subscribe((flats) => {
      this.flats.set(flats);
      if (flats.length > 0) this.form.get('flatId')?.setValue(flats[0].id);
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    const d = value.bookingDate;
    const bookingDate = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    this.dialogRef.close({
      facilityId: this.data.facility.id, flatId: value.flatId, bookingDate,
      startTime: value.startTime + ':00', endTime: value.endTime + ':00',
      purpose: value.purpose || null, guestCount: value.guestCount, notes: value.notes || null
    });
  }
}
