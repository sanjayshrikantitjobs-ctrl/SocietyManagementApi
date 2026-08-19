import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FileUploadService } from '../../../core/services/file-upload.service';

/** Small dedicated dialog for issuing a resale listing's society NOC — needs
 * a file upload, which the generic app-prompt-dialog doesn't support. */
@Component({
  selector: 'app-issue-noc-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule
  ],
  template: `
    <h2 mat-dialog-title>Issue NOC</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="content">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>NOC Issued Date</mat-label>
          <input matInput type="date" formControlName="nocIssuedDate" />
        </mat-form-field>

        <div class="upload-field">
          <label>NOC Document</label>
          <div class="upload-row">
            <input matInput formControlName="nocDocumentUrl" placeholder="Document URL" class="url-input" />
            <button mat-stroked-button type="button" (click)="fileInput.click()" [disabled]="uploading()">
              @if (uploading()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload
            </button>
            <input #fileInput type="file" hidden (change)="onFileSelected($event)" />
          </div>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Issue</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .content { width: 420px; max-width: 100%; }
    .full-width { width: 100%; }
    .upload-field { margin-bottom: 16px; }
    .upload-field label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .upload-row { display: flex; gap: 8px; align-items: center; }
    .url-input { flex: 1; padding: 8px; border: 1px solid var(--app-border); border-radius: 4px; }
    .upload-row button { flex-shrink: 0; }
  `]
})
export class IssueNocDialogComponent {
  dialogRef = inject(MatDialogRef<IssueNocDialogComponent>);
  data = inject(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly fileUploadService = inject(FileUploadService);

  readonly uploading = signal(false);

  form = this.fb.nonNullable.group({
    nocIssuedDate: [new Date().toISOString().substring(0, 10), Validators.required],
    nocDocumentUrl: ['', Validators.required]
  });

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.fileUploadService.upload(file, 'resale-listings').subscribe({
      next: (url) => {
        this.form.get('nocDocumentUrl')?.setValue(url);
        this.uploading.set(false);
      },
      error: () => this.uploading.set(false)
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.dialogRef.close(this.form.getRawValue());
  }
}
