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
import { FileUploadService } from '../../../core/services/file-upload.service';
import { StaffDto } from '../../staff/models/staff.model';
import { StaffService } from '../../staff/services/staff.service';
import { EXPENSE_CATEGORY_LABELS, ExpenseDto, PAYMENT_METHOD_LABELS } from '../models/finance.model';

export interface ExpenseFormDialogData {
  expense: ExpenseDto | null;
  societyId: number;
}

/** Dedicated dialog (not app-prompt-dialog) since Expense needs an
 * optional bill-image upload — mirrors staff-form-dialog.component.ts's
 * upload-field pattern. Staff select only appears for Category =
 * StaffSalary (2). */
@Component({
  selector: 'app-expense-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.expense ? 'Edit Expense' : 'Add Expense' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="content">
        <div class="row">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Category</mat-label>
            <mat-select formControlName="category">
              @for (opt of categoryOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Payment Method</mat-label>
            <mat-select formControlName="paymentMethod">
              @for (opt of paymentMethodOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" placeholder="e.g. August Electricity Bill" />
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Amount (₹)</mat-label>
            <input matInput type="number" formControlName="amount" />
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Expense Date</mat-label>
            <input matInput type="date" formControlName="expenseDate" />
          </mat-form-field>
        </div>

        @if (form.value.category === 2) {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Staff Member (optional)</mat-label>
            <mat-select formControlName="staffId">
              <mat-option [value]="null">None</mat-option>
              @for (s of staffOptions(); track s.id) { <mat-option [value]="s.id">{{ s.firstName }} {{ s.lastName }}</mat-option> }
            </mat-select>
          </mat-form-field>
        }

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Paid To (optional)</mat-label>
          <input matInput formControlName="paidTo" placeholder="Vendor, staff, or utility name" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Notes (optional)</mat-label>
          <textarea matInput rows="2" formControlName="notes"></textarea>
        </mat-form-field>

        <div class="upload-field">
          <label>Bill / Receipt Image (optional)</label>
          <div class="upload-row">
            @if (form.value.billImageUrl) { <mat-icon class="ok">check_circle</mat-icon> <span class="uploaded">Uploaded</span> }
            <button mat-stroked-button type="button" (click)="billInput.click()" [disabled]="uploading()">
              @if (uploading()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload
            </button>
            <input #billInput type="file" hidden (change)="onFileSelected($event)" />
          </div>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">
          {{ data.expense ? 'Save' : 'Add' }}
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
export class ExpenseFormDialogComponent implements OnInit {
  dialogRef = inject(MatDialogRef<ExpenseFormDialogComponent>);
  data = inject<ExpenseFormDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly fileUploadService = inject(FileUploadService);
  private readonly staffService = inject(StaffService);

  readonly uploading = signal(false);
  readonly staffOptions = signal<StaffDto[]>([]);
  readonly categoryOptions = Object.entries(EXPENSE_CATEGORY_LABELS).map(([value, label]) => ({ value: Number(value), label }));
  readonly paymentMethodOptions = Object.entries(PAYMENT_METHOD_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  form = this.fb.nonNullable.group({
    category: [this.data.expense?.category ?? 5, Validators.required],
    title: [this.data.expense?.title ?? '', Validators.required],
    amount: [this.data.expense?.amount ?? 0, [Validators.required, Validators.min(0.01)]],
    expenseDate: [this.data.expense?.expenseDate?.substring(0, 10) ?? new Date().toISOString().substring(0, 10), Validators.required],
    paymentMethod: [this.data.expense?.paymentMethod ?? 1, Validators.required],
    paidTo: [this.data.expense?.paidTo ?? ''],
    staffId: [this.data.expense?.staffId ?? null],
    notes: [this.data.expense?.notes ?? ''],
    billImageUrl: [this.data.expense?.billImageUrl ?? '']
  });

  ngOnInit(): void {
    this.staffService.getStaff({ societyId: this.data.societyId, isActive: true, pageSize: 200 })
      .subscribe((result) => this.staffOptions.set(result.items));
  }

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.fileUploadService.upload(file, 'expenses').subscribe({
      next: (url) => {
        this.form.get('billImageUrl')?.setValue(url);
        this.uploading.set(false);
      },
      error: () => this.uploading.set(false)
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const raw = this.form.getRawValue();
    this.dialogRef.close({
      ...raw,
      category: Number(raw.category),
      amount: Number(raw.amount),
      paymentMethod: Number(raw.paymentMethod),
      staffId: raw.staffId ? Number(raw.staffId) : null,
      paidTo: raw.paidTo || null,
      notes: raw.notes || null,
      billImageUrl: raw.billImageUrl || null
    });
  }
}
