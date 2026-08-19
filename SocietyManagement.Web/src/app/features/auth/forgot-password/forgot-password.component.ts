import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatStepperModule } from '@angular/material/stepper';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

/** Two-step flow: request an OTP, then submit OTP + new password together
 * (spec: "Forgot Password ... OTP Support ... Password Change"). */
@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule,
    MatInputModule, MatIconModule, MatStepperModule, MatProgressSpinnerModule
  ],
  templateUrl: './forgot-password.component.html',
  styleUrl: '../login/login.component.scss'
})
export class ForgotPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly requesting = signal(false);
  readonly resetting = signal(false);
  readonly otpSent = signal(false);

  requestForm = this.fb.nonNullable.group({
    identifier: ['', [Validators.required]]
  });

  resetForm = this.fb.nonNullable.group({
    otpCode: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
    newPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

  requestOtp(): void {
    if (this.requestForm.invalid) {
      this.requestForm.markAllAsTouched();
      return;
    }

    this.requesting.set(true);
    this.auth.forgotPassword(this.requestForm.getRawValue().identifier)
      .pipe(finalize(() => this.requesting.set(false)))
      .subscribe(() => {
        this.otpSent.set(true);
        this.toast.success('If an account exists, an OTP has been sent.');
      });
  }

  resetPassword(): void {
    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      return;
    }

    this.resetting.set(true);
    const { otpCode, newPassword } = this.resetForm.getRawValue();
    const { identifier } = this.requestForm.getRawValue();

    this.auth.resetPassword(identifier, otpCode, newPassword)
      .pipe(finalize(() => this.resetting.set(false)))
      .subscribe(() => {
        this.toast.success('Password reset successfully. Please log in.');
        this.router.navigate(['/auth/login']);
      });
  }
}
