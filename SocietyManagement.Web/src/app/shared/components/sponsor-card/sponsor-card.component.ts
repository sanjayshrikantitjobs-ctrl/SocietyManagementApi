import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { FestivalSponsorDto } from '../../../features/festivals/models/festival.model';
import { AssetUrlPipe } from '../../pipes/asset-url.pipe';

/** Reusable sponsor card for the sponsors tab grid — spec's "Sponsor Card"
 * reusable component. */
@Component({
  selector: 'app-sponsor-card',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, AssetUrlPipe],
  template: `
    <div class="sponsor-card app-card">
      <div class="logo">
        @if (sponsor().logoUrl) {
          <img [src]="sponsor().logoUrl | assetUrl" [alt]="sponsor().companyName" />
        } @else {
          <mat-icon>storefront</mat-icon>
        }
      </div>
      <div class="body">
        <div class="title-row">
          <h4>{{ sponsor().companyName }}</h4>
        </div>
        @if (sponsor().contactPerson) { <p class="contact">{{ sponsor().contactPerson }}</p> }
        <div class="amounts">
          <div><span class="label">Promised</span><span class="value">₹{{ sponsor().promisedAmount | number }}</span></div>
          <div><span class="label">Received</span><span class="value received">₹{{ sponsor().receivedAmount | number }}</span></div>
          <div><span class="label">Pending</span><span class="value pending">₹{{ sponsor().pendingAmount | number }}</span></div>
        </div>
      </div>
      <div class="actions">
        <button mat-icon-button (click)="edit.emit()"><mat-icon>edit</mat-icon></button>
        <button mat-icon-button (click)="remove.emit()"><mat-icon>delete_outline</mat-icon></button>
      </div>
    </div>
  `,
  styles: [`
    .sponsor-card { display: flex; align-items: flex-start; gap: 12px; padding: 16px; position: relative; }
    .logo { width: 48px; height: 48px; border-radius: 10px; background: var(--app-primary-light);
      display: flex; align-items: center; justify-content: center; flex-shrink: 0; overflow: hidden; }
    .logo img { width: 100%; height: 100%; object-fit: cover; }
    .logo mat-icon { color: var(--app-primary); }
    .body { flex: 1; min-width: 0; }
    .title-row { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    h4 { margin: 0; font-size: 14px; font-weight: 700; }
    .contact { margin: 2px 0 8px; font-size: 12px; color: var(--app-text-muted); }
    .amounts { display: flex; gap: 16px; margin-top: 8px; }
    .label { display: block; font-size: 10px; color: var(--app-text-muted); text-transform: uppercase; }
    .value { display: block; font-size: 13px; font-weight: 700; }
    .value.received { color: #15803d; }
    .value.pending { color: #b45309; }
    .actions { display: flex; flex-direction: column; }
  `]
})
export class SponsorCardComponent {
  sponsor = input.required<FestivalSponsorDto>();
  edit = output<void>();
  remove = output<void>();
}
