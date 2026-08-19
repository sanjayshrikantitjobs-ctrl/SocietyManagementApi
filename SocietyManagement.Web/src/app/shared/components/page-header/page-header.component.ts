import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

export interface Breadcrumb {
  label: string;
  link?: string;
}

/** Every page in the app uses this: breadcrumb + title + a slot for action
 * buttons (spec: "Every page must include Breadcrumb, Page Title, Action Buttons"). */
@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule],
  template: `
    <div class="page-header">
      @if (breadcrumbs().length) {
        <nav class="breadcrumb">
          @for (crumb of breadcrumbs(); track crumb.label; let last = $last) {
            @if (crumb.link && !last) {
              <a [routerLink]="crumb.link">{{ crumb.label }}</a>
              <mat-icon>chevron_right</mat-icon>
            } @else {
              <span class="current">{{ crumb.label }}</span>
            }
          }
        </nav>
      }
      <div class="title-row">
        <h1>{{ title() }}</h1>
        <div class="actions"><ng-content /></div>
      </div>
      @if (subtitle()) { <p class="subtitle">{{ subtitle() }}</p> }
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 20px; }
    .breadcrumb { display:flex; align-items:center; gap:2px; font-size:13px; color: var(--app-text-muted); margin-bottom:8px; }
    .breadcrumb a { color: var(--app-text-muted); text-decoration:none; }
    .breadcrumb a:hover { color: var(--app-primary); }
    .breadcrumb mat-icon { font-size:16px; width:16px; height:16px; }
    .breadcrumb .current { color: var(--app-text); font-weight: 500; }
    .title-row { display:flex; align-items:center; justify-content:space-between; gap:16px; flex-wrap:wrap; }
    h1 { margin:0; font-size:22px; font-weight:700; }
    .actions { display:flex; gap:8px; flex-wrap:wrap; }
    .subtitle { margin:4px 0 0; color: var(--app-text-muted); font-size:14px; }
  `]
})
export class PageHeaderComponent {
  title = input.required<string>();
  subtitle = input<string | null>(null);
  breadcrumbs = input<Breadcrumb[]>([]);
}
