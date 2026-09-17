import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { FileUploadService } from '../../core/services/file-upload.service';
import { ANNOUNCEMENT_PRIORITY_LABELS, ANNOUNCEMENT_TYPE_LABELS, AnnouncementDto } from './models/announcement.model';

export interface AnnouncementFormDialogData {
  societyId: number;
  announcement: AnnouncementDto | null;
}

/** Dedicated create/edit dialog (mirrors FestivalFormDialogComponent's shape)
 * rather than the generic app-prompt-dialog, since the attachment upload row
 * needs the same upload-button + URL-field pattern Festival's banner/cover
 * fields use — PromptDialogComponent has no file-upload field type. */
@Component({
  selector: 'app-announcement-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatDatepickerModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.announcement ? 'Edit Announcement' : 'New Announcement' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="grid">
        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" maxlength="200" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Description</mat-label>
          <textarea matInput rows="4" formControlName="description" maxlength="4000"></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Type</mat-label>
          <mat-select formControlName="type">
            @for (t of typeOptions; track t.value) { <mat-option [value]="t.value">{{ t.label }}</mat-option> }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Priority</mat-label>
          <mat-select formControlName="priority">
            @for (p of priorityOptions; track p.value) { <mat-option [value]="p.value">{{ p.label }}</mat-option> }
          </mat-select>
        </mat-form-field>

        <div class="datetime-group">
          <label class="datetime-group-label">Publish At (optional — leave blank to save as Draft)</label>
          <div class="datetime-row">
            <mat-form-field appearance="outline" class="date-part">
              <mat-label>Date</mat-label>
              <input matInput [matDatepicker]="publishDatePicker" formControlName="publishAtDate" />
              <mat-datepicker-toggle matSuffix [for]="publishDatePicker"></mat-datepicker-toggle>
              <mat-datepicker #publishDatePicker></mat-datepicker>
            </mat-form-field>
            <mat-form-field appearance="outline" class="time-part">
              <mat-label>Time</mat-label>
              <input matInput type="time" formControlName="publishAtTime" />
              <mat-icon matSuffix>schedule</mat-icon>
            </mat-form-field>
          </div>
        </div>

        <div class="datetime-group">
          <label class="datetime-group-label">Expiry At (optional)</label>
          <div class="datetime-row">
            <mat-form-field appearance="outline" class="date-part">
              <mat-label>Date</mat-label>
              <input matInput [matDatepicker]="expiryDatePicker" formControlName="expiryAtDate" />
              <mat-datepicker-toggle matSuffix [for]="expiryDatePicker"></mat-datepicker-toggle>
              <mat-datepicker #expiryDatePicker></mat-datepicker>
            </mat-form-field>
            <mat-form-field appearance="outline" class="time-part">
              <mat-label>Time</mat-label>
              <input matInput type="time" formControlName="expiryAtTime" />
              <mat-icon matSuffix>schedule</mat-icon>
            </mat-form-field>
          </div>
        </div>

        <div class="upload-field">
          <label>Image / Attachment (optional)</label>
          <div class="upload-row">
            <input matInput formControlName="attachmentUrl" placeholder="Attachment URL" class="url-input" />
            <button mat-stroked-button type="button" (click)="fileInput.click()" [disabled]="uploading()">
              @if (uploading()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload
            </button>
            <input #fileInput type="file" hidden (change)="onFileSelected($event)" />
          </div>
        </div>

        @if (!data.announcement) {
          <mat-checkbox formControlName="publishNow" class="span-2">Publish immediately (notify residents now)</mat-checkbox>
        }
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
    .datetime-group-label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .datetime-row { display: flex; gap: 8px; }
    .datetime-row .date-part { flex: 1.4; }
    .datetime-row .time-part { flex: 1; }
    .upload-field { grid-column: span 2; margin-bottom: 16px; }
    .upload-field label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .upload-row { display: flex; gap: 8px; align-items: center; }
    .url-input { flex: 1; padding: 8px; border: 1px solid var(--app-border); border-radius: 4px; }
    .upload-row button { flex-shrink: 0; }
  `]
})
export class AnnouncementFormDialogComponent {
  dialogRef = inject(MatDialogRef<AnnouncementFormDialogComponent>);
  data = inject<AnnouncementFormDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly fileUploadService = inject(FileUploadService);

  readonly uploading = signal(false);

  readonly typeOptions = Object.entries(ANNOUNCEMENT_TYPE_LABELS).map(([value, label]) => ({ value: Number(value), label }));
  readonly priorityOptions = Object.entries(ANNOUNCEMENT_PRIORITY_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  private readonly publishAtParts = splitDateTime(this.data.announcement?.publishAt);
  private readonly expiryAtParts = splitDateTime(this.data.announcement?.expiryAt);

  form = this.fb.nonNullable.group({
    title: [this.data.announcement?.title ?? '', Validators.required],
    description: [this.data.announcement?.description ?? '', Validators.required],
    type: [this.data.announcement?.type ?? 1, Validators.required],
    priority: [this.data.announcement?.priority ?? 2, Validators.required],
    publishAtDate: [this.publishAtParts.date as Date | null],
    publishAtTime: [this.publishAtParts.time],
    expiryAtDate: [this.expiryAtParts.date as Date | null],
    expiryAtTime: [this.expiryAtParts.time],
    attachmentUrl: [this.data.announcement?.attachmentUrl ?? ''],
    publishNow: [false]
  });

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.fileUploadService.upload(file, 'announcements').subscribe({
      next: (url) => {
        this.form.get('attachmentUrl')?.setValue(url);
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
      title: value.title,
      description: value.description,
      type: value.type,
      priority: value.priority,
      attachmentUrl: value.attachmentUrl || null,
      publishAt: combineDateTime(value.publishAtDate, value.publishAtTime),
      expiryAt: combineDateTime(value.expiryAtDate, value.expiryAtTime),
      publishNow: value.publishNow
    });
  }
}

/** Splits an ISO datetime string from the API into a Date (for mat-datepicker)
 * and an "HH:mm" string (for the plain time input) — the reverse of
 * combineDateTime below. */
function splitDateTime(iso: string | null | undefined): { date: Date | null; time: string } {
  if (!iso) return { date: null, time: '' };
  const parsed = new Date(iso);
  if (isNaN(parsed.getTime())) return { date: null, time: '' };
  const hh = String(parsed.getHours()).padStart(2, '0');
  const mm = String(parsed.getMinutes()).padStart(2, '0');
  return { date: parsed, time: `${hh}:${mm}` };
}

/** Combines a mat-datepicker Date with an "HH:mm" time string into the same
 * "yyyy-MM-ddTHH:mm" local shape a native <input type="datetime-local">
 * produces, so the API keeps binding it exactly as before (see Events'
 * eventDateTime field, which uses the same unspecified-kind local string). */
function combineDateTime(date: Date | null, time: string): string | null {
  if (!date) return null;
  const [hours, minutes] = (time || '00:00').split(':').map(Number);
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  const hh = String(hours || 0).padStart(2, '0');
  const mm = String(minutes || 0).padStart(2, '0');
  return `${year}-${month}-${day}T${hh}:${mm}`;
}
