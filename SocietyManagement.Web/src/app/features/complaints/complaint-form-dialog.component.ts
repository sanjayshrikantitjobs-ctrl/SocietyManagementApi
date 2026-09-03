import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { CurrentSocietyService } from '../../core/services/current-society.service';
import { FileUploadService } from '../../core/services/file-upload.service';
import { SocietyService } from '../society-setup/services/society.service';
import { COMPLAINT_CATEGORY_LABELS } from './models/complaint.model';

export interface ComplaintFormDialogData {
  /** Preset when raised from "My Complaints" (locked, no picker shown) or
   * from the admin board's "Add" action for a specific flat; null means
   * the admin must pick one. */
  flatId: number | null;
  flatNumber: string | null;
  lockFlat: boolean;
  /** When set (My Complaints, resident with multiple flats), the picker is
   * limited to exactly these flats instead of fetching the whole society's
   * flat list — a resident must never be shown flats they don't live at. */
  flatOptions?: { value: number; label: string }[];
}

/** Dedicated dialog (not app-prompt-dialog) for the optional photo upload —
 * mirrors staff-form-dialog.component.ts's upload-field pattern. */
@Component({
  selector: 'app-complaint-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>Raise Complaint</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="content">
        @if (data.lockFlat) {
          <div class="locked-flat"><mat-icon>apartment</mat-icon> Flat {{ data.flatNumber }}</div>
        } @else {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Flat</mat-label>
            <mat-select formControlName="flatId">
              @for (opt of flatOptions(); track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
        }

        <div class="row">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Category</mat-label>
            <mat-select formControlName="category">
              @for (opt of categoryOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Priority</mat-label>
            <mat-select formControlName="priority">
              <mat-option [value]="1">Low</mat-option>
              <mat-option [value]="2">Medium</mat-option>
              <mat-option [value]="3">High</mat-option>
            </mat-select>
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" placeholder="e.g. Water leakage" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Description</mat-label>
          <textarea matInput rows="3" formControlName="description"></textarea>
        </mat-form-field>

        <div class="upload-field">
          <label>Photo (optional)</label>
          <div class="upload-row">
            @if (form.value.photoUrl) { <mat-icon class="ok">check_circle</mat-icon> <span class="uploaded">Uploaded</span> }
            <button mat-stroked-button type="button" (click)="photoInput.click()" [disabled]="uploading()">
              @if (uploading()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload
            </button>
            <input #photoInput type="file" hidden accept="image/*" (change)="onFileSelected($event)" />
          </div>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Raise Complaint</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .content { width: 480px; max-width: 100%; }
    .row { display: flex; gap: 12px; }
    .row .full-width { flex: 1; min-width: 0; }
    .full-width { width: 100%; }
    .locked-flat { display: flex; align-items: center; gap: 8px; margin-bottom: 16px; font-weight: 600; color: var(--app-text); }
    .upload-field { margin-bottom: 8px; }
    .upload-field label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .upload-row { display: flex; gap: 8px; align-items: center; }
    .ok { color: #16a34a; }
    .uploaded { font-size: 13px; color: var(--app-text-muted); }
  `]
})
export class ComplaintFormDialogComponent implements OnInit {
  dialogRef = inject(MatDialogRef<ComplaintFormDialogComponent>);
  data = inject<ComplaintFormDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly fileUploadService = inject(FileUploadService);
  private readonly societyService = inject(SocietyService);
  private readonly currentSociety = inject(CurrentSocietyService);

  readonly uploading = signal(false);
  readonly flatOptions = signal<{ value: number; label: string }[]>([]);
  readonly categoryOptions = Object.entries(COMPLAINT_CATEGORY_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  form = this.fb.nonNullable.group({
    flatId: [this.data.flatId ?? null, Validators.required],
    category: [1, Validators.required],
    priority: [2, Validators.required],
    title: ['', Validators.required],
    description: ['', Validators.required],
    photoUrl: ['']
  });

  ngOnInit(): void {
    if (this.data.lockFlat) return;

    if (this.data.flatOptions) {
      this.flatOptions.set(this.data.flatOptions);
    } else {
      // No caller-supplied list means this is the admin board's "Add"
      // action — admins may raise a complaint for any flat in THEIR OWN
      // society only. Without societyId this endpoint returns every flat
      // platform-wide (it doesn't auto-scope from the caller's JWT), which
      // let an Admin pick another society's flat entirely.
      this.societyService.getFlats({ pageSize: 500, societyId: this.currentSociety.society()?.id }).subscribe((result) => {
        this.flatOptions.set(result.items.map((f) => ({ value: f.id, label: f.flatNumber })));
      });
    }
  }

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.fileUploadService.upload(file, 'complaints').subscribe({
      next: (url) => {
        this.form.get('photoUrl')?.setValue(url);
        this.uploading.set(false);
      },
      error: () => this.uploading.set(false)
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const raw = this.form.getRawValue();
    this.dialogRef.close({
      flatId: Number(raw.flatId), category: Number(raw.category), priority: Number(raw.priority),
      title: raw.title, description: raw.description, photoUrl: raw.photoUrl || null
    });
  }
}
