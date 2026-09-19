import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { PURCHASE_PRIORITY_LABELS, PurchaseItemInput, PurchaseRequestDto } from './purchases.service';

export interface PurchaseFormData {
  request: PurchaseRequestDto | null;
  vendors: { id: number; name: string }[];
  inventoryItems: { id: number; name: string; unit: string }[];
}

export interface PurchaseFormResult {
  title: string;
  description: string | null;
  priority: number;
  dueDate: string | null;
  vendorId: number | null;
  items: PurchaseItemInput[];
  submit: boolean;
}

/** Create/edit form for a purchase request — the one place a variable-length
 * list of line items is needed, which the generic PromptDialog can't express. */
@Component({
  selector: 'app-purchase-form-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>{{ data.request ? 'Edit Purchase Request' : 'New Purchase Request' }}</h2>
    <form [formGroup]="form">
      <mat-dialog-content class="content">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" maxlength="200" />
        </mat-form-field>
        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Priority</mat-label>
            <mat-select formControlName="priority">
              @for (p of priorities; track p.value) { <mat-option [value]="p.value">{{ p.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Needed by</mat-label>
            <input matInput type="date" formControlName="dueDate" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Preferred vendor</mat-label>
            <mat-select formControlName="vendorId">
              <mat-option [value]="null">— None yet —</mat-option>
              @for (v of data.vendors; track v.id) { <mat-option [value]="v.id">{{ v.name }}</mat-option> }
            </mat-select>
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Description</mat-label>
          <textarea matInput rows="2" formControlName="description"></textarea>
        </mat-form-field>

        <h3>Items</h3>
        <div formArrayName="items">
          @for (line of items.controls; track line; let i = $index) {
            <div class="line" [formGroupName]="i">
              <mat-form-field appearance="outline" class="grow">
                <mat-label>Item</mat-label>
                <input matInput formControlName="itemName" />
              </mat-form-field>
              <mat-form-field appearance="outline" class="qty">
                <mat-label>Qty</mat-label>
                <input matInput type="number" min="0" formControlName="quantity" />
              </mat-form-field>
              <mat-form-field appearance="outline" class="unit">
                <mat-label>Unit</mat-label>
                <input matInput formControlName="unit" />
              </mat-form-field>
              <mat-form-field appearance="outline" class="qty">
                <mat-label>Est. price</mat-label>
                <input matInput type="number" min="0" formControlName="estimatedUnitPrice" />
              </mat-form-field>
              <mat-form-field appearance="outline" class="stock">
                <mat-label>Add to stock of</mat-label>
                <mat-select formControlName="inventoryItemId">
                  <mat-option [value]="null">— Not tracked —</mat-option>
                  @for (inv of data.inventoryItems; track inv.id) { <mat-option [value]="inv.id">{{ inv.name }}</mat-option> }
                </mat-select>
              </mat-form-field>
              <button mat-icon-button type="button" (click)="removeLine(i)" [disabled]="items.length === 1" aria-label="Remove line"><mat-icon>close</mat-icon></button>
            </div>
          }
        </div>
        <button mat-stroked-button type="button" (click)="addLine()"><mat-icon>add</mat-icon> Add item</button>
        <p class="total">Estimated total: {{ total() | currency: 'INR' : 'symbol' : '1.0-0' }}</p>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="ref.close()">Cancel</button>
        <button mat-stroked-button type="button" [disabled]="form.invalid" (click)="save(false)">Save as draft</button>
        <button mat-flat-button color="primary" type="button" [disabled]="form.invalid" (click)="save(true)">Submit for approval</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .content { min-width: 760px; max-width: 860px; }
    .full { width: 100%; }
    .row { display: flex; gap: 12px; }
    .row mat-form-field { flex: 1; }
    .line { display: flex; gap: 8px; align-items: flex-start; }
    .grow { flex: 2; }
    .qty { width: 90px; }
    .unit { width: 90px; }
    .stock { flex: 1.4; }
    h3 { margin: 4px 0 8px; }
    .total { font-weight: 600; margin: 12px 0 0; }
    @media (max-width: 900px) { .content { min-width: 0; } .row, .line { flex-wrap: wrap; } }
  `]
})
export class PurchaseFormDialogComponent {
  readonly ref = inject(MatDialogRef<PurchaseFormDialogComponent, PurchaseFormResult>);
  readonly data = inject<PurchaseFormData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);

  readonly priorities = Object.entries(PURCHASE_PRIORITY_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  readonly form = this.fb.group({
    title: [this.data.request?.title ?? '', [Validators.required, Validators.maxLength(200)]],
    description: [this.data.request?.description ?? ''],
    priority: [this.data.request?.priority ?? 2],
    dueDate: [this.data.request?.dueDate?.substring(0, 10) ?? ''],
    vendorId: [this.data.request?.vendorId ?? null as number | null],
    items: this.fb.array([] as ReturnType<PurchaseFormDialogComponent['newLine']>[])
  });

  get items(): FormArray { return this.form.get('items') as FormArray; }

  constructor() {
    const existing = this.data.request?.items ?? [];
    if (existing.length === 0) this.addLine();
    existing.forEach((i) => this.items.push(this.newLine(i.itemName, i.quantity, i.unit, i.estimatedUnitPrice, i.inventoryItemId ?? null)));
  }

  private newLine(name = '', qty = 1, unit = 'Units', price = 0, inventoryItemId: number | null = null) {
    return this.fb.group({
      itemName: [name, [Validators.required, Validators.maxLength(200)]],
      quantity: [qty, [Validators.required, Validators.min(0.01)]],
      unit: [unit, [Validators.required, Validators.maxLength(30)]],
      estimatedUnitPrice: [price, [Validators.required, Validators.min(0)]],
      inventoryItemId: [inventoryItemId as number | null]
    });
  }

  addLine(): void { this.items.push(this.newLine()); }
  removeLine(i: number): void { if (this.items.length > 1) this.items.removeAt(i); }

  total(): number {
    return this.items.controls.reduce((sum, c) => sum + Number(c.value.quantity || 0) * Number(c.value.estimatedUnitPrice || 0), 0);
  }

  save(submit: boolean): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.ref.close({
      title: v.title!, description: v.description || null, priority: Number(v.priority), dueDate: v.dueDate || null,
      vendorId: v.vendorId ?? null,
      items: (v.items as PurchaseItemInput[]).map((i) => ({
        itemName: i.itemName, quantity: Number(i.quantity), unit: i.unit, estimatedUnitPrice: Number(i.estimatedUnitPrice),
        inventoryItemId: i.inventoryItemId ?? null
      })),
      submit
    });
  }
}
