import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { FileUploadService } from '../../core/services/file-upload.service';
import { FACILITY_PRICING_TYPE_LABELS, FACILITY_TYPE_LABELS, FacilityDto } from './models/facility.model';

export interface FacilityFormDialogData {
  societyId: number;
  facility: FacilityDto | null;
}

@Component({
  selector: 'app-facility-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.facility ? 'Edit Facility' : 'New Facility' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="grid">
        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" maxlength="200" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Type</mat-label>
          <mat-select formControlName="type">
            @for (t of typeOptions; track t.value) { <mat-option [value]="t.value">{{ t.label }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Capacity</mat-label>
          <input matInput type="number" formControlName="capacity" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Location (optional)</mat-label>
          <input matInput formControlName="location" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Description (optional)</mat-label>
          <textarea matInput rows="3" formControlName="description"></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Pricing Type</mat-label>
          <mat-select formControlName="pricingType">
            @for (p of pricingTypeOptions; track p.value) { <mat-option [value]="p.value">{{ p.label }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Price</mat-label>
          <input matInput type="number" formControlName="pricePerUnit" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Security Deposit</mat-label>
          <input matInput type="number" formControlName="securityDeposit" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Cleaning Charge</mat-label>
          <input matInput type="number" formControlName="cleaningCharge" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Additional Charge</mat-label>
          <input matInput type="number" formControlName="additionalCharge" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Advance Booking Limit (days, 0 = no limit)</mat-label>
          <input matInput type="number" formControlName="advanceBookingDaysLimit" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Cancellation Cutoff (hours before)</mat-label>
          <input matInput type="number" formControlName="cancellationHoursBeforeStart" />
        </mat-form-field>

        <div class="upload-field">
          <label>Image (optional)</label>
          <div class="upload-row">
            <input matInput formControlName="imageUrl" placeholder="Image URL" class="url-input" />
            <button mat-stroked-button type="button" (click)="fileInput.click()" [disabled]="uploading()">
              @if (uploading()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload
            </button>
            <input #fileInput type="file" accept="image/*" hidden (change)="onFileSelected($event)" />
          </div>
        </div>

        <mat-checkbox formControlName="requiresApproval" class="span-2">Requires admin approval before confirming a booking</mat-checkbox>
        @if (data.facility) {
          <mat-checkbox formControlName="isActive" class="span-2">Active (visible for booking)</mat-checkbox>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .grid { display:grid; grid-template-columns: 1fr 1fr; gap: 0 16px; width: 600px; max-width: 100%; }
    .span-2 { grid-column: span 2; }
    .upload-field { grid-column: span 2; margin-bottom: 16px; }
    .upload-field label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .upload-row { display: flex; gap: 8px; align-items: center; }
    .url-input { flex: 1; padding: 8px; border: 1px solid var(--app-border); border-radius: 4px; }
    .upload-row button { flex-shrink: 0; }
  `]
})
export class FacilityFormDialogComponent {
  dialogRef = inject(MatDialogRef<FacilityFormDialogComponent>);
  data = inject<FacilityFormDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly fileUploadService = inject(FileUploadService);

  readonly uploading = signal(false);

  readonly typeOptions = Object.entries(FACILITY_TYPE_LABELS).map(([value, label]) => ({ value: Number(value), label }));
  readonly pricingTypeOptions = Object.entries(FACILITY_PRICING_TYPE_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  form = this.fb.nonNullable.group({
    name: [this.data.facility?.name ?? '', Validators.required],
    type: [this.data.facility?.type ?? 1, Validators.required],
    capacity: [this.data.facility?.capacity ?? 0, [Validators.required, Validators.min(0)]],
    location: [this.data.facility?.location ?? ''],
    description: [this.data.facility?.description ?? ''],
    pricingType: [this.data.facility?.pricingType ?? 1, Validators.required],
    pricePerUnit: [this.data.facility?.pricePerUnit ?? 0, [Validators.required, Validators.min(0)]],
    securityDeposit: [this.data.facility?.securityDeposit ?? 0, Validators.min(0)],
    cleaningCharge: [this.data.facility?.cleaningCharge ?? 0, Validators.min(0)],
    additionalCharge: [this.data.facility?.additionalCharge ?? 0, Validators.min(0)],
    advanceBookingDaysLimit: [this.data.facility?.advanceBookingDaysLimit ?? 0, Validators.min(0)],
    cancellationHoursBeforeStart: [this.data.facility?.cancellationHoursBeforeStart ?? 0, Validators.min(0)],
    imageUrl: [this.data.facility?.imageUrl ?? ''],
    requiresApproval: [this.data.facility?.requiresApproval ?? false],
    isActive: [this.data.facility?.isActive ?? true]
  });

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.fileUploadService.upload(file, 'facilities').subscribe({
      next: (url) => {
        this.form.get('imageUrl')?.setValue(url);
        this.uploading.set(false);
      },
      error: () => this.uploading.set(false)
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    this.dialogRef.close({
      societyId: this.data.societyId,
      name: value.name, type: value.type, capacity: value.capacity, location: value.location || null,
      description: value.description || null, pricingType: value.pricingType, pricePerUnit: value.pricePerUnit,
      securityDeposit: value.securityDeposit, cleaningCharge: value.cleaningCharge, additionalCharge: value.additionalCharge,
      advanceBookingDaysLimit: value.advanceBookingDaysLimit, cancellationHoursBeforeStart: value.cancellationHoursBeforeStart,
      imageUrl: value.imageUrl || null, requiresApproval: value.requiresApproval, isActive: value.isActive
    });
  }
}
