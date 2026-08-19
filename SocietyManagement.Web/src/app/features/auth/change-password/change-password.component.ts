import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

/** Self-service "Profile Management > Password Change" — also where a user
 * lands after login if MustChangePassword is true. */
@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatCardModule, MatFormFieldModule,
    MatInputModule, MatIconModule, PageHeaderComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Change Password" [breadcrumbs]="[{ label: 'Profile' }]" />

      @if (auth.currentUser()?.mustChangePassword) {
        <div class="notice">Your account requires a password change before continuing.</div>
      }

      <div class="app-card form-card">
        <form [formGroup]="form" (ngSubmit)="submit()">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Current Password</mat-label>
            <input matInput type="password" formControlName="currentPassword" />
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>New Password</mat-label>
            <input matInput type="password" formControlName="newPassword" />
            <mat-hint>Min 8 characters, include upper/lower/digit/special character.</mat-hint>
            @if (form.controls.newPassword.touched && form.controls.newPassword.invalid) {
              <mat-error>
                @if (form.controls.newPassword.hasError('required')) {
                  New password is required.
                } @else {
                  Must be at least 8 characters and include an uppercase letter, a lowercase letter, a digit and a special character.
                }
              </mat-error>
            }
          </mat-form-field>

          <button mat-flat-button color="primary" type="submit" [disabled]="submitting()">
            Update Password
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .form-card { max-width: 480px; padding: 24px; }
    .full-width { width: 100%; }
    .notice { background: #fffbeb; border: 1px solid #fde68a; color: #92400e;
      padding: 10px 14px; border-radius: 8px; margin-bottom: 16px; font-size: 13px; }
  `]
})
export class ChangePasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly submitting = signal(false);

  // Mirrors ChangePasswordCommandValidator's server-side rule exactly (min 8
  // chars, upper/lower/digit/special) so an invalid password is caught here
  // instead of round-tripping to the API to find out.
  private static readonly PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$/;

  form = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.pattern(ChangePasswordComponent.PASSWORD_PATTERN)]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const { currentPassword, newPassword } = this.form.getRawValue();

    this.auth.changePassword(currentPassword, newPassword)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe(() => {
        this.toast.success('Password updated successfully.');
        this.form.reset();
      });
  }
}
