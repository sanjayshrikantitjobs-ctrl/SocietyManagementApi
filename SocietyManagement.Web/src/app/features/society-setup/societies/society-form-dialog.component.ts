import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Society } from '../../../core/models/society.model';

@Component({
  selector: 'app-society-form-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Edit Society' : 'Add Society' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="grid">
        <mat-form-field appearance="outline"><mat-label>Name</mat-label><input matInput formControlName="name" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Registration Number</mat-label><input matInput formControlName="registrationNumber" /></mat-form-field>
        <mat-form-field appearance="outline" class="span-2"><mat-label>Address</mat-label><input matInput formControlName="address" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>City</mat-label><input matInput formControlName="city" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>State</mat-label><input matInput formControlName="state" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Pincode</mat-label><input matInput formControlName="pincode" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Contact Email</mat-label><input matInput formControlName="contactEmail" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Contact Phone</mat-label><input matInput formControlName="contactPhone" /></mat-form-field>
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
  `]
})
export class SocietyFormDialogComponent {
  dialogRef = inject(MatDialogRef<SocietyFormDialogComponent>);
  data = inject<Society | null>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);

  form = this.fb.nonNullable.group({
    name: [this.data?.name ?? '', Validators.required],
    registrationNumber: [this.data?.registrationNumber ?? ''],
    address: [this.data?.address ?? '', Validators.required],
    city: [this.data?.city ?? '', Validators.required],
    state: [this.data?.state ?? '', Validators.required],
    pincode: [this.data?.pincode ?? '', [Validators.required, Validators.pattern(/^\d{6}$/)]],
    contactEmail: [this.data?.contactEmail ?? ''],
    contactPhone: [this.data?.contactPhone ?? '']
  });

  submit(): void {
    if (this.form.invalid) return;
    this.dialogRef.close(this.form.getRawValue());
  }
}
