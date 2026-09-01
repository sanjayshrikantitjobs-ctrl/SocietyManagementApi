import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';

/** Login by email OR mobile number, per spec. A single "identifier" field is
 * validated loosely client-side; the backend does the authoritative check. */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule,
    MatInputModule, MatIconModule, MatCheckboxModule, MatProgressSpinnerModule
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly hidePassword = signal(true);

  form = this.fb.nonNullable.group({
    identifier: ['', [Validators.required]],
    password: ['', [Validators.required]],
    societyCode: ['']
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.submitting.set(true);
    const { identifier, password, societyCode } = this.form.getRawValue();

    this.auth.login(identifier, password, societyCode || undefined)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (res) => {
          if (res.user.mustChangePassword) {
            this.router.navigate(['/profile']);
            return;
          }
          // Watchman skips the generic dashboard entirely — /visitors is
          // their actual job (gate/approval duty), not a Member-shaped
          // maintenance/bills view. See dashboard.routes.ts for the
          // matching bounce if they land on /dashboard some other way.
          this.router.navigate([this.auth.isWatchman() ? '/visitors' : '/dashboard']);
        },
        error: (err) => {
          this.errorMessage.set(err?.error?.message ?? 'Invalid credentials. Please try again.');
        }
      });
  }
}
