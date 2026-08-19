import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { FLAT_STATUS_LABELS, FLAT_TYPE_LABELS, Flat } from '../../../core/models/society.model';

export interface FlatFormDialogData {
  flat: Flat | null;
  floorId: number;
}

@Component({
  selector: 'app-flat-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.flat ? 'Edit Flat' : 'Add Flat' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="grid">
        <mat-form-field appearance="outline"><mat-label>Flat Number</mat-label><input matInput formControlName="flatNumber" /></mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Flat Type</mat-label>
          <mat-select formControlName="flatType">
            @for (opt of typeOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Area (sq.ft)</mat-label><input matInput type="number" formControlName="areaSqFt" /></mat-form-field>
        @if (data.flat) {
          <mat-form-field appearance="outline">
            <mat-label>Status</mat-label>
            <mat-select formControlName="status">
              @for (opt of statusOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
        }

        <h4 class="section-heading">Owner Contact <span class="muted">(used for maintenance bills &amp; WhatsApp delivery)</span></h4>
        <mat-form-field appearance="outline"><mat-label>Owner Name</mat-label><input matInput formControlName="ownerName" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Owner Phone</mat-label><input matInput formControlName="ownerPhone" /></mat-form-field>
        <mat-form-field appearance="outline" class="span-2"><mat-label>Owner Email</mat-label><input matInput formControlName="ownerEmail" /></mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .grid { display:grid; grid-template-columns:1fr 1fr; gap:0 16px; }
    .span-2 { grid-column: span 2; }
    .section-heading { grid-column: span 2; margin: 4px 0 0; font-size: 13px; font-weight: 600; }
    .section-heading .muted { font-weight: 400; color: var(--app-text-muted); font-size: 12px; }
  `]
})
export class FlatFormDialogComponent {
  dialogRef = inject(MatDialogRef<FlatFormDialogComponent>);
  data = inject<FlatFormDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);

  typeOptions = Object.entries(FLAT_TYPE_LABELS).map(([value, label]) => ({ value: Number(value), label }));
  statusOptions = Object.entries(FLAT_STATUS_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  form = this.fb.nonNullable.group({
    flatNumber: [this.data.flat?.flatNumber ?? '', Validators.required],
    flatType: [this.data.flat?.flatType ?? 3, Validators.required],
    areaSqFt: [this.data.flat?.areaSqFt ?? null],
    status: [this.data.flat?.status ?? 1],
    ownerName: [this.data.flat?.ownerName ?? ''],
    ownerPhone: [this.data.flat?.ownerPhone ?? ''],
    ownerEmail: [this.data.flat?.ownerEmail ?? '', Validators.email]
  });

  submit(): void {
    if (this.form.invalid) return;
    this.dialogRef.close(this.form.getRawValue());
  }
}
