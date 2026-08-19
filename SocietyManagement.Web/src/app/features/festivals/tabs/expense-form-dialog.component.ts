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
import { BUDGET_CATEGORY_LABELS, FestivalBudgetCategoryDto, FestivalExpenseDto, FestivalVendorDto } from '../models/festival.model';
import { FestivalService } from '../services/festival.service';

export interface ExpenseFormDialogData {
  festivalId: number;
  societyId: number;
  expense: FestivalExpenseDto | null;
}

@Component({
  selector: 'app-expense-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.expense ? 'Edit Expense' : 'Record Expense' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="grid">
        <mat-form-field appearance="outline">
          <mat-label>Budget Category</mat-label>
          <mat-select formControlName="festivalBudgetCategoryId">
            @for (c of categories(); track c.id) { <mat-option [value]="c.id">{{ categoryLabel(c) }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Vendor (optional)</mat-label>
          <mat-select formControlName="vendorId">
            <mat-option [value]="null">— None —</mat-option>
            @for (v of vendors(); track v.id) { <mat-option [value]="v.id">{{ v.name }}</mat-option> }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline"><mat-label>Amount</mat-label><input matInput type="number" formControlName="amount" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Expense Date</mat-label><input matInput type="date" formControlName="expenseDate" /></mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Payment Method</mat-label>
          <mat-select formControlName="paymentMethod">
            <mat-option [value]="1">Cash</mat-option>
            <mat-option [value]="2">UPI</mat-option>
            <mat-option [value]="3">Bank Transfer</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Invoice Number</mat-label><input matInput formControlName="invoiceNumber" /></mat-form-field>

        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Description</mat-label>
          <textarea matInput rows="2" formControlName="description"></textarea>
        </mat-form-field>

        <div class="upload-field">
          <label>Bill / Invoice Image</label>
          <div class="upload-row">
            <input matInput formControlName="billImageUrl" placeholder="Image URL" class="url-input" />
            <button mat-stroked-button type="button" (click)="billInput.click()" [disabled]="uploading()">
              @if (uploading()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload
            </button>
            <input #billInput type="file" accept="image/*,.pdf" hidden (change)="onFileSelected($event)" />
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
    .upload-field { grid-column: span 2; margin-bottom: 16px; }
    .upload-field label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .upload-row { display: flex; gap: 8px; align-items: center; }
    .url-input { flex: 1; padding: 8px; border: 1px solid var(--app-border); border-radius: 4px; }
  `]
})
export class ExpenseFormDialogComponent implements OnInit {
  dialogRef = inject(MatDialogRef<ExpenseFormDialogComponent>);
  data = inject<ExpenseFormDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly festivalService = inject(FestivalService);
  private readonly fileUploadService = inject(FileUploadService);

  readonly uploading = signal(false);
  readonly categories = signal<FestivalBudgetCategoryDto[]>([]);
  readonly vendors = signal<FestivalVendorDto[]>([]);

  form = this.fb.nonNullable.group({
    festivalBudgetCategoryId: [this.data.expense?.festivalBudgetCategoryId ?? null, Validators.required],
    vendorId: [this.data.expense?.vendorId ?? null],
    amount: [this.data.expense?.amount ?? 0, [Validators.required, Validators.min(0.01)]],
    expenseDate: [this.data.expense?.expenseDate?.substring(0, 10) ?? '', Validators.required],
    paymentMethod: [this.data.expense?.paymentMethod ?? 1, Validators.required],
    invoiceNumber: [this.data.expense?.invoiceNumber ?? ''],
    description: [this.data.expense?.description ?? ''],
    billImageUrl: [this.data.expense?.billImageUrl ?? '']
  });

  categoryLabel(c: FestivalBudgetCategoryDto): string {
    return BUDGET_CATEGORY_LABELS[c.category];
  }

  ngOnInit(): void {
    this.festivalService.getBudgetCategories(this.data.festivalId).subscribe((data) => this.categories.set(data));
    this.festivalService.getVendors({ societyId: this.data.societyId, pageSize: 100 }).subscribe((result) => this.vendors.set(result.items));
  }

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.fileUploadService.upload(file, 'festivals/bills').subscribe({
      next: (url) => {
        this.form.get('billImageUrl')?.setValue(url);
        this.uploading.set(false);
      },
      error: () => this.uploading.set(false)
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.dialogRef.close({ ...this.form.getRawValue(), festivalId: this.data.festivalId });
  }
}
