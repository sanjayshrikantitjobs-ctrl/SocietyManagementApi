import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { FileUploadService } from '../../core/services/file-upload.service';
import { parseDateOnly, toDateOnlyString } from '../../shared/utils/date.util';
import { STAFF_CATEGORY_LABELS, StaffDto } from './models/staff.model';

export interface StaffFormDialogData {
  staff: StaffDto | null;
  societyId: number;
}

/** Dedicated dialog (not the generic app-prompt-dialog) since Staff needs
 * two file uploads (joining document + photo) — mirrors
 * issue-noc-dialog.component.ts's upload-field pattern. */
@Component({
  selector: 'app-staff-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatDatepickerModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.staff ? 'Edit Staff Member' : 'Add Staff Member' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="content">
        <div class="row">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>First Name</mat-label>
            <input matInput formControlName="firstName" />
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Last Name</mat-label>
            <input matInput formControlName="lastName" />
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Category</mat-label>
          <mat-select formControlName="category">
            @for (opt of categoryOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
          </mat-select>
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Phone</mat-label>
            <input matInput formControlName="phone" />
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Email (optional)</mat-label>
            <input matInput formControlName="email" />
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Address (optional)</mat-label>
          <textarea matInput rows="2" formControlName="address"></textarea>
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Joining Date</mat-label>
            <input matInput [matDatepicker]="joiningPicker" formControlName="joiningDate" />
            <mat-datepicker-toggle matSuffix [for]="joiningPicker"></mat-datepicker-toggle>
            <mat-datepicker #joiningPicker></mat-datepicker>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Salary (₹)</mat-label>
            <input matInput type="number" formControlName="salary" />
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Salary Pay Day (1-31)</mat-label>
            <input matInput type="number" min="1" max="31" formControlName="salaryPayDay" />
          </mat-form-field>
        </div>

        <div class="upload-field">
          <label>Joining Document (optional)</label>
          <div class="upload-row">
            @if (form.value.joiningDocumentUrl) { <mat-icon class="ok">check_circle</mat-icon> <span class="uploaded">Uploaded</span> }
            <button mat-stroked-button type="button" (click)="docInput.click()" [disabled]="uploadingDoc()">
              @if (uploadingDoc()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload
            </button>
            <input #docInput type="file" hidden (change)="onFileSelected($event, 'joiningDocumentUrl')" />
          </div>
        </div>

        <div class="upload-field">
          <label>Photo (optional)</label>
          <div class="upload-row">
            @if (form.value.photoUrl) { <mat-icon class="ok">check_circle</mat-icon> <span class="uploaded">Uploaded</span> }
            <button mat-stroked-button type="button" (click)="photoInput.click()" [disabled]="uploadingPhoto()">
              @if (uploadingPhoto()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload
            </button>
            <input #photoInput type="file" hidden accept="image/*" (change)="onFileSelected($event, 'photoUrl')" />
          </div>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">
          {{ data.staff ? 'Save' : 'Add' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .content { width: 520px; max-width: 100%; }
    .row { display: flex; gap: 12px; }
    .row .full-width { flex: 1; min-width: 0; }
    .full-width { width: 100%; }
    .upload-field { margin-bottom: 16px; }
    .upload-field label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .upload-row { display: flex; gap: 8px; align-items: center; }
    .ok { color: #16a34a; }
    .uploaded { font-size: 13px; color: var(--app-text-muted); }
  `]
})
export class StaffFormDialogComponent {
  dialogRef = inject(MatDialogRef<StaffFormDialogComponent>);
  data = inject<StaffFormDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly fileUploadService = inject(FileUploadService);

  readonly uploadingDoc = signal(false);
  readonly uploadingPhoto = signal(false);
  readonly categoryOptions = Object.entries(STAFF_CATEGORY_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  form = this.fb.nonNullable.group({
    firstName: [this.data.staff?.firstName ?? '', Validators.required],
    lastName: [this.data.staff?.lastName ?? '', Validators.required],
    category: [this.data.staff?.category ?? 1, Validators.required],
    phone: [this.data.staff?.phone ?? '', Validators.required],
    email: [this.data.staff?.email ?? ''],
    address: [this.data.staff?.address ?? ''],
    joiningDate: [parseDateOnly(this.data.staff?.joiningDate) ?? new Date(), Validators.required],
    salary: [this.data.staff?.salary ?? 0, [Validators.required, Validators.min(0)]],
    salaryPayDay: [this.data.staff?.salaryPayDay ?? 1, [Validators.required, Validators.min(1), Validators.max(31)]],
    joiningDocumentUrl: [this.data.staff?.joiningDocumentUrl ?? ''],
    photoUrl: [this.data.staff?.photoUrl ?? '']
  });

  onFileSelected(event: Event, field: 'joiningDocumentUrl' | 'photoUrl'): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    const uploading = field === 'joiningDocumentUrl' ? this.uploadingDoc : this.uploadingPhoto;
    uploading.set(true);
    this.fileUploadService.upload(file, 'staff').subscribe({
      next: (url) => {
        this.form.get(field)?.setValue(url);
        uploading.set(false);
      },
      error: () => uploading.set(false)
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const raw = this.form.getRawValue();
    this.dialogRef.close({
      ...raw,
      joiningDate: toDateOnlyString(raw.joiningDate),
      category: Number(raw.category),
      salary: Number(raw.salary),
      salaryPayDay: Number(raw.salaryPayDay),
      email: raw.email || null,
      address: raw.address || null,
      joiningDocumentUrl: raw.joiningDocumentUrl || null,
      photoUrl: raw.photoUrl || null
    });
  }
}
