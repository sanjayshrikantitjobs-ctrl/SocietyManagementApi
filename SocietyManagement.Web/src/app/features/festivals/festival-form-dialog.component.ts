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
import { Festival } from './models/festival.model';

export interface FestivalFormDialogData {
  societyId: number;
  festival: Festival | null;
}

/** Dedicated create/edit dialog for Festival — richer than the generic
 * app-prompt-dialog (banner/cover upload, description textarea, date range). */
@Component({
  selector: 'app-festival-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.festival ? 'Edit Festival' : 'New Festival' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="grid">
        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Festival Name</mat-label>
          <input matInput formControlName="name" placeholder="e.g. Ganesh Chaturthi" />
        </mat-form-field>

        <mat-form-field appearance="outline"><mat-label>Year</mat-label><input matInput type="number" formControlName="year" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Theme</mat-label><input matInput formControlName="theme" /></mat-form-field>

        <mat-form-field appearance="outline"><mat-label>Start Date</mat-label><input matInput type="date" formControlName="startDate" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>End Date</mat-label><input matInput type="date" formControlName="endDate" /></mat-form-field>

        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Description</mat-label>
          <textarea matInput rows="3" formControlName="description"></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Visibility</mat-label>
          <mat-select formControlName="visibility">
            <mat-option [value]="1">Public</mat-option>
            <mat-option [value]="2">Members Only</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-checkbox formControlName="isRecurring" class="recurring">Recurs every year</mat-checkbox>

        <div class="upload-field">
          <label>Banner Image</label>
          <div class="upload-row">
            <input matInput formControlName="bannerImageUrl" placeholder="Image URL" class="url-input" />
            <button mat-stroked-button type="button" (click)="bannerInput.click()" [disabled]="uploadingBanner()">
              @if (uploadingBanner()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload
            </button>
            <input #bannerInput type="file" accept="image/*" hidden (change)="onFileSelected($event, 'bannerImageUrl', 'uploadingBanner')" />
          </div>
        </div>
        <div class="upload-field">
          <label>Cover Photo</label>
          <div class="upload-row">
            <input matInput formControlName="coverPhotoUrl" placeholder="Image URL" class="url-input" />
            <button mat-stroked-button type="button" (click)="coverInput.click()" [disabled]="uploadingCover()">
              @if (uploadingCover()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload
            </button>
            <input #coverInput type="file" accept="image/*" hidden (change)="onFileSelected($event, 'coverPhotoUrl', 'uploadingCover')" />
          </div>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .grid { display:grid; grid-template-columns: 1fr 1fr; gap: 0 16px; width: 560px; max-width: 100%; }
    .span-2 { grid-column: span 2; }
    .recurring { grid-column: span 1; margin: 8px 0; }
    .upload-field { grid-column: span 2; margin-bottom: 16px; }
    .upload-field label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .upload-row { display: flex; gap: 8px; align-items: center; }
    .url-input { flex: 1; padding: 8px; border: 1px solid var(--app-border); border-radius: 4px; }
    .upload-row button { flex-shrink: 0; }
  `]
})
export class FestivalFormDialogComponent {
  dialogRef = inject(MatDialogRef<FestivalFormDialogComponent>);
  data = inject<FestivalFormDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly fileUploadService = inject(FileUploadService);

  readonly uploadingBanner = signal(false);
  readonly uploadingCover = signal(false);

  form = this.fb.nonNullable.group({
    name: [this.data.festival?.name ?? '', Validators.required],
    year: [this.data.festival?.year ?? new Date().getFullYear(), Validators.required],
    theme: [this.data.festival?.theme ?? ''],
    startDate: [this.data.festival?.startDate?.substring(0, 10) ?? '', Validators.required],
    endDate: [this.data.festival?.endDate?.substring(0, 10) ?? '', Validators.required],
    description: [this.data.festival?.description ?? ''],
    visibility: [this.data.festival?.visibility ?? 1, Validators.required],
    isRecurring: [this.data.festival?.isRecurring ?? false],
    bannerImageUrl: [this.data.festival?.bannerImageUrl ?? ''],
    coverPhotoUrl: [this.data.festival?.coverPhotoUrl ?? '']
  });

  onFileSelected(event: Event, controlKey: 'bannerImageUrl' | 'coverPhotoUrl', flagKey: 'uploadingBanner' | 'uploadingCover'): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this[flagKey].set(true);
    this.fileUploadService.upload(file, 'festivals').subscribe({
      next: (url) => {
        this.form.get(controlKey)?.setValue(url);
        this[flagKey].set(false);
      },
      error: () => this[flagKey].set(false)
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    this.dialogRef.close({
      ...value,
      parentFestivalId: this.data.festival?.parentFestivalId ?? null,
      societyId: this.data.societyId
    });
  }
}
