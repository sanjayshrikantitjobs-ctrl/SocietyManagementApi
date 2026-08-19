import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule],
  template: `
    <div class="wrap">
      <mat-icon>search_off</mat-icon>
      <h1>404 — Page not found</h1>
      <p>The page you're looking for doesn't exist.</p>
      <a mat-flat-button color="primary" routerLink="/dashboard">Back to dashboard</a>
    </div>
  `,
  styles: [`
    .wrap { display:flex; flex-direction:column; align-items:center; justify-content:center;
      height:100vh; gap:12px; text-align:center; }
    mat-icon { font-size:64px; width:64px; height:64px; color: var(--app-text-muted); }
  `]
})
export class NotFoundComponent {}
