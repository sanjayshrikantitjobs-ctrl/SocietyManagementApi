import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AssetUrlPipe } from '../../../shared/pipes/asset-url.pipe';
import { FileUploadService } from '../../../core/services/file-upload.service';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { RESIDENT_DOCUMENT_TYPE_LABELS, ResidentDocumentDto } from '../models/occupancy.model';
import { OccupancyService } from '../services/occupancy.service';

/** Unlimited documents per occupancy (Possession Letter, Parking Allotment
 * Letter, Tenant Police NOC, Rental Agreement, Other) — generalizes
 * RentalAgreementCard's one-off upload into a per-occupancy list. Used on
 * both Owner and Tenant panels. */
@Component({
  selector: 'app-resident-documents-card',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatProgressSpinnerModule, MatSelectModule, MatTooltipModule, AssetUrlPipe
  ],
  template: `
    <div class="card">
      <div class="card-header">
        <h4>Documents</h4>
        @if (!readOnly && !adding()) {
          <button mat-button (click)="startAdd()"><mat-icon>upload_file</mat-icon> Upload Document</button>
        }
      </div>

      @if (!adding()) {
        @if (documents().length > 0) {
          <div class="doc-list">
            @for (d of documents(); track d.id) {
              <div class="doc-row">
                <div>
                  <strong>{{ typeLabels[d.documentType] }}</strong>
                  <span class="muted">{{ d.uploadedAt | date: 'mediumDate' }} · {{ d.uploadedByName }}</span>
                  @if (d.notes) { <span class="muted notes">{{ d.notes }}</span> }
                </div>
                <div class="doc-actions">
                  <a mat-icon-button [href]="d.documentUrl | assetUrl" target="_blank" matTooltip="View / Download"><mat-icon>visibility</mat-icon></a>
                  @if (!readOnly) {
                    <button mat-icon-button (click)="remove(d)"><mat-icon>delete</mat-icon></button>
                  }
                </div>
              </div>
            }
          </div>
        } @else {
          <p class="empty">No documents uploaded yet.</p>
        }
      } @else {
        <form [formGroup]="form" (ngSubmit)="save()" class="edit-grid">
          <mat-form-field appearance="outline">
            <mat-label>Document Type</mat-label>
            <mat-select formControlName="documentType">
              @for (t of typeKeys; track t) {
                <mat-option [value]="t">{{ typeLabels[t] }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Notes (optional)</mat-label><input matInput formControlName="notes" /></mat-form-field>

          <div class="upload-field span-2">
            <label>File</label>
            <div class="upload-row">
              @if (form.value.documentUrl) { <a [href]="form.value.documentUrl! | assetUrl" target="_blank">Uploaded file</a> }
              <button mat-stroked-button type="button" (click)="fileInput.click()" [disabled]="uploading()">
                @if (uploading()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
                Upload
              </button>
              <input #fileInput type="file" hidden (change)="onFileSelected($event)" />
            </div>
          </div>

          <div class="span-2 actions">
            <button mat-button type="button" (click)="adding.set(false)">Cancel</button>
            <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Save</button>
          </div>
        </form>
      }
    </div>
  `,
  styles: [`
    .card { border: 1px solid var(--app-border); border-radius: 10px; padding: 16px; margin-bottom: 16px; }
    .card-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; }
    .card-header h4 { margin: 0; font-size: 14px; }
    .empty { color: var(--app-text-muted); font-size: 13px; margin: 0; }
    .doc-list { display: flex; flex-direction: column; gap: 8px; }
    .doc-row { display: flex; align-items: center; justify-content: space-between; padding: 8px 0;
      border-bottom: 1px solid var(--app-border); }
    .doc-row:last-child { border-bottom: none; }
    .doc-row .muted { display: block; font-size: 11px; color: var(--app-text-muted); }
    .doc-row .notes { font-style: italic; }
    .doc-actions { display: flex; }
    .edit-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0 16px; }
    .span-2 { grid-column: span 2; }
    .upload-field { margin-bottom: 12px; }
    .upload-field label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .upload-row { display: flex; gap: 12px; align-items: center; }
    .actions { display: flex; justify-content: flex-end; gap: 8px; }
  `]
})
export class ResidentDocumentsCardComponent implements OnChanges {
  @Input() flatOccupancyId!: number;
  @Input() readOnly = false;

  private readonly fb = inject(FormBuilder);
  private readonly occupancyService = inject(OccupancyService);
  private readonly fileUploadService = inject(FileUploadService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly documents = signal<ResidentDocumentDto[]>([]);
  readonly adding = signal(false);
  readonly uploading = signal(false);
  readonly typeLabels = RESIDENT_DOCUMENT_TYPE_LABELS;
  readonly typeKeys = [1, 2, 3, 4, 5] as const;

  form = this.fb.nonNullable.group({
    documentType: [1, Validators.required],
    documentUrl: ['', Validators.required],
    notes: ['']
  });

  ngOnChanges(): void {
    this.adding.set(false);
    if (this.flatOccupancyId) this.load();
  }

  load(): void {
    this.occupancyService.getResidentDocuments(this.flatOccupancyId).subscribe((docs) => this.documents.set(docs));
  }

  startAdd(): void {
    this.form.reset({ documentType: 1, documentUrl: '', notes: '' });
    this.adding.set(true);
  }

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.fileUploadService.upload(file, 'resident-documents').subscribe({
      next: (url) => {
        this.form.get('documentUrl')?.setValue(url);
        this.uploading.set(false);
      },
      error: () => this.uploading.set(false)
    });
  }

  save(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    this.occupancyService.uploadResidentDocument({
      flatOccupancyId: this.flatOccupancyId, documentType: value.documentType as 1 | 2 | 3 | 4 | 5,
      documentUrl: value.documentUrl, notes: value.notes || null
    }).subscribe(() => {
      this.toast.success('Document uploaded.');
      this.adding.set(false);
      this.load();
    });
  }

  remove(doc: ResidentDocumentDto): void {
    this.confirmDialog.confirm({
      title: 'Remove Document', destructive: true,
      message: `Remove "${this.typeLabels[doc.documentType]}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.occupancyService.deleteResidentDocument(doc.id).subscribe(() => {
        this.toast.success('Document removed.');
        this.load();
      });
    });
  }
}
