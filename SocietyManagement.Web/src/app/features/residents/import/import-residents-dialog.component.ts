import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { ToastService } from '../../../core/services/toast.service';
import { ResidentImportResultDto } from '../models/resident.model';
import { ResidentService } from '../services/resident.service';

export interface ImportResidentsDialogData {
  societyId: number;
}

/** Upload a spreadsheet of Building/Wing/Floor/Flat + owner rows and create
 * whatever's missing in one shot — the bulk alternative to repeating
 * QuickAddFlatDialogComponent one flat at a time. Shows a per-row result so
 * a partially-bad file (a typo'd phone, a duplicate flat number) is visible
 * without losing everything else that imported fine. */
@Component({
  selector: 'app-import-residents-dialog',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatChipsModule, MatDialogModule, MatIconModule,
    MatProgressSpinnerModule, MatTableModule
  ],
  template: `
    <h2 mat-dialog-title>Import Residents from Excel</h2>
    <mat-dialog-content>
      @if (!result()) {
        <p class="intro">
          Upload a spreadsheet of flats and owners to add many at once. Buildings, wings and floors
          named in the sheet are created automatically if they don't already exist.
        </p>
        <button mat-stroked-button (click)="downloadTemplate()" [disabled]="downloadingTemplate()">
          @if (downloadingTemplate()) { <mat-spinner diameter="18" /> } @else { <mat-icon>download</mat-icon> }
          Download Template
        </button>

        <div class="file-row">
          <input #fileInput type="file" accept=".xlsx" (change)="onFileSelected($event)" hidden />
          <button mat-stroked-button (click)="fileInput.click()" [disabled]="importing()">
            <mat-icon>attach_file</mat-icon> {{ selectedFile()?.name ?? 'Choose File' }}
          </button>
          @if (selectedFile()) {
            <button mat-flat-button color="primary" (click)="upload()" [disabled]="importing()">
              @if (importing()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload & Import
            </button>
          }
        </div>
      } @else {
        <div class="summary">
          <div class="stat"><span class="value">{{ result()!.totalRows }}</span><span class="label">Rows</span></div>
          <div class="stat success"><span class="value">{{ result()!.flatsCreated }}</span><span class="label">Flats Created</span></div>
          <div class="stat success"><span class="value">{{ result()!.membersCreated }}</span><span class="label">Owners Created</span></div>
          <div class="stat success"><span class="value">{{ result()!.residenciesCreated }}</span><span class="label">Residencies Linked</span></div>
          @if (result()!.rowsFailed > 0) {
            <div class="stat danger"><span class="value">{{ result()!.rowsFailed }}</span><span class="label">Rows Failed</span></div>
          }
        </div>

        <table mat-table [dataSource]="result()!.rowResults" class="results-table">
          <ng-container matColumnDef="row">
            <th mat-header-cell *matHeaderCellDef>Row</th>
            <td mat-cell *matCellDef="let r">{{ r.rowNumber }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let r">
              <mat-chip-set><mat-chip [class]="'status-' + r.status.toLowerCase()">{{ r.status }}</mat-chip></mat-chip-set>
            </td>
          </ng-container>
          <ng-container matColumnDef="message">
            <th mat-header-cell *matHeaderCellDef>Details</th>
            <td mat-cell *matCellDef="let r">{{ r.message }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="resultColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: resultColumns;"></tr>
        </table>

        <button mat-stroked-button (click)="reset()"><mat-icon>replay</mat-icon> Import Another File</button>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="close()">{{ result() ? 'Done' : 'Cancel' }}</button>
    </mat-dialog-actions>
  `,
  styles: [`
    :host { display: block; width: min(640px, 92vw); max-width: 100%; }
    mat-dialog-content { max-height: 70vh; }
    .intro { color: var(--app-text-muted); font-size: 13px; margin: 0 0 16px; }
    .file-row { display: flex; align-items: center; gap: 12px; margin-top: 16px; }
    .summary { display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 16px; }
    .stat { display: flex; flex-direction: column; align-items: center; padding: 10px 16px; border-radius: 8px; background: var(--app-primary-light); min-width: 90px; }
    .stat .value { font-size: 20px; font-weight: 700; }
    .stat .label { font-size: 11px; color: var(--app-text-muted); }
    .stat.success { background: #ecfdf5; }
    .stat.success .value { color: #15803d; }
    .stat.danger { background: #fef2f2; }
    .stat.danger .value { color: #b91c1c; }
    .results-table { width: 100%; }
    .status-created { background: #ecfdf5 !important; color: #15803d !important; }
    .status-updated, .status-skipped { background: #f1f5f9 !important; color: #475569 !important; }
    .status-error { background: #fef2f2 !important; color: #b91c1c !important; }
  `]
})
export class ImportResidentsDialogComponent {
  dialogRef = inject(MatDialogRef<ImportResidentsDialogComponent>);
  data = inject<ImportResidentsDialogData>(MAT_DIALOG_DATA);
  private readonly residentService = inject(ResidentService);
  private readonly toast = inject(ToastService);

  readonly selectedFile = signal<File | null>(null);
  readonly importing = signal(false);
  readonly downloadingTemplate = signal(false);
  readonly result = signal<ResidentImportResultDto | null>(null);
  readonly resultColumns = ['row', 'status', 'message'];
  private didImport = false;

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  downloadTemplate(): void {
    this.downloadingTemplate.set(true);
    this.residentService.downloadImportTemplate().subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'resident-import-template.xlsx';
        link.click();
        window.URL.revokeObjectURL(url);
        this.downloadingTemplate.set(false);
      },
      error: () => this.downloadingTemplate.set(false)
    });
  }

  upload(): void {
    const file = this.selectedFile();
    if (!file) return;
    this.importing.set(true);
    this.residentService.importResidents(this.data.societyId, file).subscribe({
      next: (result) => {
        this.result.set(result);
        this.importing.set(false);
        this.didImport = true;
        this.toast.success(`${result.flatsCreated} flat(s), ${result.membersCreated} owner(s) added.`);
      },
      error: () => this.importing.set(false)
    });
  }

  reset(): void {
    this.result.set(null);
    this.selectedFile.set(null);
  }

  close(): void {
    this.dialogRef.close(this.didImport);
  }
}
