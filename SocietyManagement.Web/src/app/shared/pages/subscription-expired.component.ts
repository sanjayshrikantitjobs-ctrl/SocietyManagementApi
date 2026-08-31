import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';

/** Landed on when the backend rejects a request with 402 (SubscriptionExpiredException)
 * — see error.interceptor.ts. Unlike ForbiddenComponent, there's no "back to dashboard"
 * link: every request from this society will keep 402'ing until the Super Admin extends
 * the subscription, so the only useful action is signing out. */
@Component({
  selector: 'app-subscription-expired',
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="wrap">
      <mat-icon>event_busy</mat-icon>
      <h1>Subscription expired</h1>
      <p>Your society's subscription has ended. Please contact your platform administrator to renew access.</p>
      <button mat-flat-button color="primary" (click)="logout()">Sign out</button>
    </div>
  `,
  styles: [`
    .wrap { display:flex; flex-direction:column; align-items:center; justify-content:center;
      height:100vh; gap:12px; text-align:center; padding: 24px; }
    p { max-width: 360px; color: var(--app-text-muted); }
    mat-icon { font-size:64px; width:64px; height:64px; color: var(--app-danger); }
  `]
})
export class SubscriptionExpiredComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/auth/login']);
  }
}
