import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

export interface PromptField {
  key: string;
  label: string;
  type: 'text' | 'number' | 'select' | 'date' | 'datetime-local' | 'textarea' | 'checkbox';
  required?: boolean;
  options?: { value: number | string; label: string }[];
  defaultValue?: string | number | boolean;
}

export interface PromptDialogData {
  title: string;
  fields: PromptField[];
  submitLabel?: string;
}

/** Small generic dialog for quick creates/edits (Building name, Wing name,
 * Floor number, Parking slot number+type, Festival budget categories,
 * sponsors, vendors, contributions, ...) — avoids a bespoke form component
 * for every "add/edit a record" action across modules. */
@Component({
  selector: 'app-prompt-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        @for (field of data.fields; track field.key) {
          @if (field.type === 'checkbox') {
            <mat-checkbox [formControlName]="field.key" class="checkbox-field">{{ field.label }}</mat-checkbox>
          } @else {
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>{{ field.label }}</mat-label>
              @if (field.type === 'select') {
                <mat-select [formControlName]="field.key">
                  @for (opt of field.options; track opt.value) {
                    <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
                  }
                </mat-select>
              } @else if (field.type === 'textarea') {
                <textarea matInput rows="3" [formControlName]="field.key"></textarea>
              } @else {
                <input matInput [type]="field.type" [formControlName]="field.key" />
              }
            </mat-form-field>
          }
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">
          {{ data.submitLabel ?? 'Save' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`.full-width { width: 100%; } .checkbox-field { display: block; margin: 8px 0 16px; }`]
})
export class PromptDialogComponent {
  dialogRef = inject(MatDialogRef<PromptDialogComponent>);
  data = inject<PromptDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);

  form = this.fb.group(
    Object.fromEntries(
      this.data.fields.map((f) => [
        f.key,
        [f.defaultValue ?? (f.type === 'checkbox' ? false : ''), f.required !== false && f.type !== 'checkbox' ? [Validators.required] : []]
      ])
    )
  );

  submit(): void {
    if (this.form.invalid) return;
    this.dialogRef.close(this.form.value);
  }
}
