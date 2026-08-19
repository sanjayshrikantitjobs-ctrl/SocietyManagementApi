import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ToastService } from '../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { SponsorCardComponent } from '../../../shared/components/sponsor-card/sponsor-card.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { FestivalSponsorDto, SPONSORSHIP_TYPE_LABELS } from '../models/festival.model';
import { FestivalService } from '../services/festival.service';

@Component({
  selector: 'app-festival-sponsors-tab',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, EmptyStateComponent, SkeletonLoaderComponent, SponsorCardComponent],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Sponsors ({{ sponsors().length }})</h3>
        @if (canManage()) {
          <button mat-flat-button color="primary" (click)="addSponsor()"><mat-icon>add</mat-icon> Add Sponsor</button>
        }
      </div>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="90" />
      } @else if (sponsors().length === 0) {
        <app-empty-state icon="handshake" title="No sponsors yet" message="Add companies or individuals sponsoring this festival."
          [actionLabel]="canManage() ? 'Add Sponsor' : null" (action)="addSponsor()" />
      } @else {
        <div class="grid">
          @for (sponsor of sponsors(); track sponsor.id) {
            <app-sponsor-card [sponsor]="sponsor" (edit)="editSponsor(sponsor)" (remove)="removeSponsor(sponsor)" />
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
    .toolbar h3 { margin: 0; font-size: 15px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 16px; }
  `]
})
export class FestivalSponsorsTabComponent implements OnInit {
  festivalId = input.required<number>();
  canManage = input(false);

  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly sponsors = signal<FestivalSponsorDto[]>([]);

  private readonly typeOptions = Object.entries(SPONSORSHIP_TYPE_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.festivalService.getSponsors(this.festivalId()).subscribe((data) => {
      this.sponsors.set(data);
      this.loading.set(false);
    });
  }

  private fields(sponsor?: FestivalSponsorDto) {
    return [
      { key: 'companyName', label: 'Company / Sponsor Name', type: 'text' as const, defaultValue: sponsor?.companyName ?? '' },
      { key: 'contactPerson', label: 'Contact Person', type: 'text' as const, required: false, defaultValue: sponsor?.contactPerson ?? '' },
      { key: 'phone', label: 'Phone', type: 'text' as const, required: false, defaultValue: sponsor?.phone ?? '' },
      { key: 'email', label: 'Email', type: 'text' as const, required: false, defaultValue: sponsor?.email ?? '' },
      { key: 'sponsorshipType', label: 'Sponsorship Type', type: 'select' as const, options: this.typeOptions, defaultValue: sponsor?.sponsorshipType ?? 1 },
      { key: 'promisedAmount', label: 'Promised Amount', type: 'number' as const, defaultValue: sponsor?.promisedAmount ?? 0 },
      { key: 'receivedAmount', label: 'Received Amount', type: 'number' as const, defaultValue: sponsor?.receivedAmount ?? 0 },
      { key: 'logoUrl', label: 'Logo URL', type: 'text' as const, required: false, defaultValue: sponsor?.logoUrl ?? '' },
      { key: 'bannerUrl', label: 'Banner URL', type: 'text' as const, required: false, defaultValue: sponsor?.bannerUrl ?? '' }
    ];
  }

  addSponsor(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px', data: { title: 'Add Sponsor', fields: this.fields() }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.createSponsor({
        festivalId: this.festivalId(), ...result,
        sponsorshipType: Number(result.sponsorshipType), promisedAmount: Number(result.promisedAmount), receivedAmount: Number(result.receivedAmount)
      }).subscribe(() => {
        this.toast.success('Sponsor added.');
        this.load();
      });
    });
  }

  editSponsor(sponsor: FestivalSponsorDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px', data: { title: 'Edit Sponsor', submitLabel: 'Save', fields: this.fields(sponsor) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.updateSponsor(sponsor.id, {
        ...result,
        sponsorshipType: Number(result.sponsorshipType), promisedAmount: Number(result.promisedAmount), receivedAmount: Number(result.receivedAmount)
      }).subscribe(() => {
        this.toast.success('Sponsor updated.');
        this.load();
      });
    });
  }

  removeSponsor(sponsor: FestivalSponsorDto): void {
    this.confirmDialog.confirm({
      title: 'Remove Sponsor', destructive: true, message: `Remove "${sponsor.companyName}" as a sponsor?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.festivalService.deleteSponsor(sponsor.id).subscribe(() => {
        this.toast.success('Sponsor removed.');
        this.load();
      });
    });
  }
}
