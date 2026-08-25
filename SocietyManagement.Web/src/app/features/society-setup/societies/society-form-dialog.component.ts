import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AssetUrlPipe } from '../../../shared/pipes/asset-url.pipe';
import { FileUploadService } from '../../../core/services/file-upload.service';
import { Society } from '../../../core/models/society.model';

@Component({
  selector: 'app-society-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatProgressSpinnerModule, AssetUrlPipe
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
        <mat-form-field appearance="outline"><mat-label>Contact Phone</mat-label><input matInput formControlName="contactPhone" /></mat-form-field>
        @if (data) {
          <mat-form-field appearance="outline" class="span-2">
            <mat-label>Society Code</mat-label>
            <input matInput formControlName="code" />
            <mat-hint>Residents enter this at login. Share it with them.</mat-hint>
          </mat-form-field>
        }
      </mat-dialog-content>
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
  `]
})
export class SocietyFormDialogComponent {
  dialogRef = inject(MatDialogRef<SocietyFormDialogComponent>);
  data = inject<Society | null>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly fileUploadService = inject(FileUploadService);

  readonly uploadingLogo = signal(false);

  form = this.fb.nonNullable.group({
    name: [this.data?.name ?? '', Validators.required],
    code: [this.data?.code ?? ''],
    registrationNumber: [this.data?.registrationNumber ?? ''],
    address: [this.data?.address ?? '', Validators.required],
    city: [this.data?.city ?? '', Validators.required],
    state: [this.data?.state ?? '', Validators.required],
    pincode: [this.data?.pincode ?? '', [Validators.required, Validators.pattern(/^\d{6}$/)]],
    contactEmail: [this.data?.contactEmail ?? ''],
    contactPhone: [this.data?.contactPhone ?? ''],
    logoUrl: [this.data?.logoUrl ?? '']
  });

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
    this.dialogRef.close(this.form.getRawValue());
  }
}
