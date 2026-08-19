import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-forbidden',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule],
  template: `
    <div class="wrap">
      <mat-icon>block</mat-icon>
      <h1>403 — Access denied</h1>
      <p>You don't have permission to view this page.</p>
      <a mat-flat-button color="primary" routerLink="/dashboard">Back to dashboard</a>
    </div>
  `,
  styles: [`
    .wrap { display:flex; flex-direction:column; align-items:center; justify-content:center;
      height:100vh; gap:12px; text-align:center; }
    mat-icon { font-size:64px; width:64px; height:64px; color: var(--app-danger); }
  `]
})
export class ForbiddenComponent {}
