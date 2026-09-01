import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { UserListItem } from '../../core/models/user.model';

/** Admin-typed counterpart to the existing "Reset Password" (which emails a
 * random temp password) — for a target whose email isn't reliably checked
 * (e.g. a Watchman). Still forces a change on next login, same as reset. */
@Component({
  selector: 'app-set-user-password-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>Set Password</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        <p class="hint">Set a new password for <strong>{{ data.firstName }} {{ data.lastName }}</strong>. They'll be asked to change it on next login.</p>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>New Password</mat-label>
          <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
          @if (form.controls.newPassword.hasError('required') && form.controls.newPassword.touched) {
            <mat-error>Password is required.</mat-error>
          } @else if (form.controls.newPassword.hasError('weak')) {
            <mat-error>Must be 8+ chars with an uppercase letter, a lowercase letter, a digit and a special character.</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Confirm Password</mat-label>
          <input matInput type="password" formControlName="confirmPassword" autocomplete="new-password" />
          @if (form.hasError('mismatch') && form.controls.confirmPassword.touched) {
            <mat-error>Passwords don't match.</mat-error>
          }
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Set Password</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .full-width { width: 100%; }
    .hint { color: var(--app-text-muted); font-size: 13px; margin: 0 0 16px; }
  `]
})
export class SetUserPasswordDialogComponent {
  dialogRef = inject(MatDialogRef<SetUserPasswordDialogComponent>);
  data = inject<UserListItem>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);

  private static passwordStrength(control: AbstractControl): ValidationErrors | null {
    const value = control.value as string;
    if (!value) return null;
    const strong = /^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$/.test(value);
    return strong ? null : { weak: true };
  }

  private static passwordsMatch(group: AbstractControl): ValidationErrors | null {
    const newPassword = group.get('newPassword')?.value;
    const confirmPassword = group.get('confirmPassword')?.value;
    return newPassword && confirmPassword && newPassword !== confirmPassword ? { mismatch: true } : null;
  }

  form = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, SetUserPasswordDialogComponent.passwordStrength]],
    confirmPassword: ['', Validators.required]
  }, { validators: SetUserPasswordDialogComponent.passwordsMatch });

  submit(): void {
    if (this.form.invalid) return;
    this.dialogRef.close(this.form.getRawValue().newPassword);
  }
}
