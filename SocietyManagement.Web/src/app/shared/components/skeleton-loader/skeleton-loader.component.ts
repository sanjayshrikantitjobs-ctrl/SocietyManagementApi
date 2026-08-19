import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';

@Component({
  selector: 'app-skeleton-loader',
  standalone: true,
  imports: [CommonModule],
  template: `
    @for (row of rowsArray(); track row) {
      <div class="skeleton-row" [style.height.px]="height()"></div>
    }
  `,
  styles: [`
    .skeleton-row {
      margin-bottom: 10px;
      border-radius: 6px;
      background: linear-gradient(90deg, var(--app-border) 25%, var(--app-surface-alt) 37%, var(--app-border) 63%);
      background-size: 400% 100%;
      animation: shimmer 1.4s ease infinite;
    }
    @keyframes shimmer {
      0% { background-position: 100% 50%; }
      100% { background-position: 0 50%; }
    }
  `]
})
export class SkeletonLoaderComponent {
  rows = input(5);
  height = input(40);

  rowsArray(): number[] {
    return Array.from({ length: this.rows() }, (_, i) => i);
  }
}
