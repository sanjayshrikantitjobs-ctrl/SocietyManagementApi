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
import { ASSET_CATEGORY_LABELS, ASSET_PRICING_TYPE_LABELS, AssetDto } from './models/asset.model';

export interface AssetFormDialogData {
  societyId: number;
  asset: AssetDto | null;
}

@Component({
  selector: 'app-asset-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.asset ? 'Edit Asset' : 'New Asset' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="grid">
        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" maxlength="200" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Category</mat-label>
          <mat-select formControlName="category">
            @for (c of categoryOptions; track c.value) { <mat-option [value]="c.value">{{ c.label }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Total Quantity</mat-label>
          <input matInput type="number" formControlName="totalQuantity" />
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
          <mat-label>Rental Price</mat-label>
          <input matInput type="number" formControlName="rentalPrice" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Security Deposit (per unit)</mat-label>
          <input matInput type="number" formControlName="securityDeposit" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Damage Charge (per unit)</mat-label>
          <input matInput type="number" formControlName="damageCharge" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Late Return Charge (per unit/day)</mat-label>
          <input matInput type="number" formControlName="lateReturnCharge" />
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

        @if (data.asset) {
          <mat-checkbox formControlName="isActive" class="span-2">Active (visible for rent)</mat-checkbox>
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
export class AssetFormDialogComponent {
  dialogRef = inject(MatDialogRef<AssetFormDialogComponent>);
  data = inject<AssetFormDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly fileUploadService = inject(FileUploadService);

  readonly uploading = signal(false);

  readonly categoryOptions = Object.entries(ASSET_CATEGORY_LABELS).map(([value, label]) => ({ value: Number(value), label }));
  readonly pricingTypeOptions = Object.entries(ASSET_PRICING_TYPE_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  form = this.fb.nonNullable.group({
    name: [this.data.asset?.name ?? '', Validators.required],
    category: [this.data.asset?.category ?? 1, Validators.required],
    totalQuantity: [this.data.asset?.totalQuantity ?? 1, [Validators.required, Validators.min(1)]],
    description: [this.data.asset?.description ?? ''],
    pricingType: [this.data.asset?.pricingType ?? 1, Validators.required],
    rentalPrice: [this.data.asset?.rentalPrice ?? 0, [Validators.required, Validators.min(0)]],
    securityDeposit: [this.data.asset?.securityDeposit ?? 0, Validators.min(0)],
    damageCharge: [this.data.asset?.damageCharge ?? 0, Validators.min(0)],
    lateReturnCharge: [this.data.asset?.lateReturnCharge ?? 0, Validators.min(0)],
    imageUrl: [this.data.asset?.imageUrl ?? ''],
    isActive: [this.data.asset?.isActive ?? true]
  });

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.fileUploadService.upload(file, 'assets').subscribe({
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
      name: value.name, category: value.category, totalQuantity: value.totalQuantity, description: value.description || null,
      pricingType: value.pricingType, rentalPrice: value.rentalPrice, securityDeposit: value.securityDeposit,
      damageCharge: value.damageCharge, lateReturnCharge: value.lateReturnCharge, imageUrl: value.imageUrl || null,
      isActive: value.isActive
    });
  }
}
