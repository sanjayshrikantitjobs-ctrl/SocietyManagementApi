import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule],
  template: `
    <div class="empty-state app-fade-in">
      <mat-icon>{{ icon() }}</mat-icon>
      <h3>{{ title() }}</h3>
      <p>{{ message() }}</p>
      @if (actionLabel()) {
        <button mat-flat-button color="primary" (click)="action.emit()">{{ actionLabel() }}</button>
      }
    </div>
  `,
  styles: [`
    .empty-state { display:flex; flex-direction:column; align-items:center; justify-content:center;
      padding: 48px 24px; text-align:center; color: var(--app-text-muted); }
    mat-icon { font-size:48px; width:48px; height:48px; margin-bottom:12px; opacity:.6; }
    h3 { margin:0 0 4px; color: var(--app-text); }
    p { margin: 0 0 16px; }
  `]
})
export class EmptyStateComponent {
  icon = input('inbox');
  title = input('Nothing here yet');
  message = input('');
  actionLabel = input<string | null>(null);
  action = output<void>();
}
