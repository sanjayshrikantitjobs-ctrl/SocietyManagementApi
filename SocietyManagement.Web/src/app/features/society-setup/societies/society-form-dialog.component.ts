import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AssetUrlPipe } from '../../../shared/pipes/asset-url.pipe';
import { parseDateOnly, toDateOnlyString } from '../../../shared/utils/date.util';
import { AuthService } from '../../../core/services/auth.service';
import { FileUploadService } from '../../../core/services/file-upload.service';
import { ToastService } from '../../../core/services/toast.service';
import { Society } from '../../../core/models/society.model';
import { optionalMobileValidator } from '../../../shared/validators/mobile.validator';
import { SocietyService } from '../services/society.service';

@Component({
  selector: 'app-society-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatDatepickerModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, AssetUrlPipe
  ],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Edit Society' : 'Add Society' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="grid">
        <div class="upload-field span-2">
          <label>Logo</label>
          <div class="upload-row">
            @if (form.value.logoUrl) {
              <img [src]="form.value.logoUrl | assetUrl" class="logo-preview" alt="" />
            }
            <button mat-stroked-button type="button" (click)="logoInput.click()" [disabled]="uploadingLogo()">
              @if (uploadingLogo()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload Logo
            </button>
            <input #logoInput type="file" accept="image/*" hidden (change)="onLogoSelected($event)" />
          </div>
        </div>
        <mat-form-field appearance="outline"><mat-label>Name</mat-label><input matInput formControlName="name" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Registration Number</mat-label><input matInput formControlName="registrationNumber" /></mat-form-field>
        <mat-form-field appearance="outline" class="span-2"><mat-label>Address</mat-label><input matInput formControlName="address" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>City</mat-label><input matInput formControlName="city" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>State</mat-label><input matInput formControlName="state" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Pincode</mat-label><input matInput formControlName="pincode" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Contact Email</mat-label><input matInput formControlName="contactEmail" /></mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Contact Phone</mat-label>
          <input matInput formControlName="contactPhone" maxlength="10" />
          @if (form.get('contactPhone')?.hasError('pattern')) { <mat-error>Enter a valid 10-digit mobile number.</mat-error> }
        </mat-form-field>
        @if (data) {
          <mat-form-field appearance="outline" class="span-2">
            <mat-label>Society Code</mat-label>
            <input matInput formControlName="code" />
            <mat-hint>Residents enter this at login. Share it with them.</mat-hint>
          </mat-form-field>
        }

        @if (!data) {
          <!-- New society: Society.Create is Super Admin-only, so every date
               required here goes straight into CreateSocietyCommand. -->
          <mat-form-field appearance="outline">
            <mat-label>Subscription Start</mat-label>
            <input matInput [matDatepicker]="startPicker" [formControl]="subscriptionStart" />
            <mat-datepicker-toggle matSuffix [for]="startPicker"></mat-datepicker-toggle>
            <mat-datepicker #startPicker></mat-datepicker>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Subscription End</mat-label>
            <input matInput [matDatepicker]="endPicker" [formControl]="subscriptionEnd" />
            <mat-datepicker-toggle matSuffix [for]="endPicker"></mat-datepicker-toggle>
            <mat-datepicker #endPicker></mat-datepicker>
          </mat-form-field>
          <div class="span-2 preset-row">
            <button mat-stroked-button type="button" (click)="applyPreset(30)">+30 days</button>
            <button mat-stroked-button type="button" (click)="applyPreset(60)">+60 days</button>
            <button mat-stroked-button type="button" (click)="applyPreset(365)">+1 year</button>
          </div>
        }
      </mat-dialog-content>

      @if (data && auth.isSuperAdmin()) {
        <!-- Editing an existing society, as Super Admin: subscription dates
             are deliberately NOT part of the form above / UpdateSocietyCommand
             (see SetSocietySubscriptionCommand doc comment) — extending here
             calls a separate, Super Admin-only endpoint immediately. -->
        <div class="subscription-panel">
          <div class="subscription-info">
            <mat-icon [class.expired]="isExpired()">event_available</mat-icon>
            <span>Subscription {{ isExpired() ? 'expired' : 'active until' }}
              <strong>{{ currentEnd() | date: 'mediumDate' }}</strong></span>
          </div>
          <div class="preset-row">
            <button mat-stroked-button type="button" [disabled]="extending()" (click)="extendSubscription(30)">+30 days</button>
            <button mat-stroked-button type="button" [disabled]="extending()" (click)="extendSubscription(60)">+60 days</button>
            <button mat-stroked-button type="button" [disabled]="extending()" (click)="extendSubscription(365)">+1 year</button>
          </div>
          <mat-checkbox [checked]="isSuspended()" [disabled]="suspending()" (change)="toggleSuspension($event.checked)">
            Restrict this society (blocks access immediately, regardless of dates)
          </mat-checkbox>
        </div>
      }

      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .grid { display:grid; grid-template-columns: 1fr 1fr; gap: 0 16px; }
    .span-2 { grid-column: span 2; }
    .upload-field { margin-bottom: 16px; }
    .upload-field label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .upload-row { display: flex; gap: 12px; align-items: center; }
    .logo-preview { width: 48px; height: 48px; border-radius: 6px; object-fit: cover; }
    .preset-row { display: flex; gap: 8px; margin-bottom: 16px; }
    .subscription-panel { padding: 12px 24px; background: var(--app-surface-alt); }
    .subscription-info { display: flex; align-items: center; gap: 8px; margin-bottom: 10px; font-size: 13px; }
    .subscription-info mat-icon { color: var(--app-primary); }
    .subscription-info mat-icon.expired { color: var(--app-danger); }
    mat-checkbox { font-size: 13px; }
  `]
})
export class SocietyFormDialogComponent {
  dialogRef = inject(MatDialogRef<SocietyFormDialogComponent>);
  data = inject<Society | null>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly fileUploadService = inject(FileUploadService);
  private readonly societyService = inject(SocietyService);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly uploadingLogo = signal(false);
  readonly extending = signal(false);
  readonly suspending = signal(false);
  readonly isSuspended = signal(this.data?.isSubscriptionSuspended ?? false);
  readonly currentEnd = signal<Date | null>(parseDateOnly(this.data?.subscriptionEndDate));
  readonly isExpired = () => {
    const end = this.currentEnd();
    return end !== null && end.getTime() < Date.now();
  };

  readonly subscriptionStart = new FormControl<Date>(new Date(), { nonNullable: true });
  readonly subscriptionEnd = new FormControl<Date>(this.addDays(new Date(), 30), { nonNullable: true });

  form = this.fb.nonNullable.group({
    name: [this.data?.name ?? '', Validators.required],
    code: [this.data?.code ?? ''],
    registrationNumber: [this.data?.registrationNumber ?? ''],
    address: [this.data?.address ?? '', Validators.required],
    city: [this.data?.city ?? '', Validators.required],
    state: [this.data?.state ?? '', Validators.required],
    pincode: [this.data?.pincode ?? '', [Validators.required, Validators.pattern(/^\d{6}$/)]],
    contactEmail: [this.data?.contactEmail ?? ''],
    contactPhone: [this.data?.contactPhone ?? '', optionalMobileValidator()],
    logoUrl: [this.data?.logoUrl ?? '']
  });

  private addDays(date: Date, days: number): Date {
    const result = new Date(date);
    result.setDate(result.getDate() + days);
    return result;
  }

  applyPreset(days: number): void {
    this.subscriptionEnd.setValue(this.addDays(this.subscriptionStart.value, days));
  }

  extendSubscription(days: number): void {
    if (!this.data) return;
    const base = this.isExpired() ? new Date() : (this.currentEnd() ?? new Date());
    const newEnd = this.addDays(base, days);

    this.extending.set(true);
    this.societyService.setSubscription(this.data.id, this.data.subscriptionStartDate, toDateOnlyString(newEnd)!)
      .subscribe({
        next: () => {
          this.currentEnd.set(newEnd);
          this.extending.set(false);
          this.toast.success('Subscription extended.');
        },
        error: () => this.extending.set(false)
      });
  }

  toggleSuspension(checked: boolean): void {
    if (!this.data) return;

    this.suspending.set(true);
    this.societyService.setSuspension(this.data.id, checked).subscribe({
      next: () => {
        this.isSuspended.set(checked);
        this.suspending.set(false);
        this.toast.success(checked ? 'Society restricted.' : 'Society reinstated.');
      },
      error: () => this.suspending.set(false)
    });
  }

  onLogoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploadingLogo.set(true);
    this.fileUploadService.upload(file, 'societies').subscribe({
      next: (url) => {
        this.form.get('logoUrl')?.setValue(url);
        this.uploadingLogo.set(false);
      },
      error: () => this.uploadingLogo.set(false)
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const payload: Record<string, unknown> = { ...this.form.getRawValue() };
    if (!this.data) {
      payload['subscriptionStartDate'] = toDateOnlyString(this.subscriptionStart.value);
      payload['subscriptionEndDate'] = toDateOnlyString(this.subscriptionEnd.value);
    }
    this.dialogRef.close(payload);
  }
}
