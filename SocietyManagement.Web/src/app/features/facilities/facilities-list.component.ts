import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { AssetUrlPipe } from '../../shared/pipes/asset-url.pipe';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { Society } from '../../core/models/society.model';
import { SocietyService } from '../society-setup/services/society.service';
import { FacilityBookingFormDialogComponent } from './facility-booking-form-dialog.component';
import { FacilityFormDialogComponent } from './facility-form-dialog.component';
import { FACILITY_PRICING_TYPE_LABELS, FACILITY_TYPE_LABELS, FacilityDto } from './models/facility.model';
import { FacilityService } from './services/facility.service';

@Component({
  selector: 'app-facilities-list',
  standalone: true,
  imports: [
    CommonModule, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatMenuModule, MatSelectModule,
    AssetUrlPipe, EmptyStateComponent, PageHeaderComponent, SkeletonLoaderComponent, StatusBadgeComponent
  ],
  template: `
    <div class="app-page">
    <app-page-header title="Facilities" subtitle="Club house, party hall and other bookable spaces in your society.">
      @if (societies().length > 1) {
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="society-picker">
          <mat-select [value]="societyId()" (selectionChange)="onSocietyChange($event.value)">
            @for (s of societies(); track s.id) { <mat-option [value]="s.id">{{ s.name }}</mat-option> }
          </mat-select>
        </mat-form-field>
      }
      <button mat-stroked-button routerLink="/my-facility-bookings"><mat-icon>event_available</mat-icon> My Bookings</button>
      @if (canManage()) {
        <button mat-flat-button color="primary" (click)="createFacility()">
          <mat-icon>add</mat-icon> New Facility
        </button>
      }
    </app-page-header>

    @if (loading()) {
      <app-skeleton-loader [rows]="4" />
    } @else if (facilities().length === 0) {
      <app-empty-state icon="villa" title="No facilities yet"
        message="Add a facility to let residents start booking it."
        [actionLabel]="canManage() ? 'New Facility' : null" (action)="createFacility()" />
    } @else {
      <div class="grid">
        @for (f of facilities(); track f.id) {
          <div class="card" [class.inactive]="!f.isActive" (click)="openFacility(f)">
            <div class="thumb" [style.backgroundImage]="'url(' + (f.imageUrl | assetUrl) + ')'"></div>
            <div class="body">
              <div class="title-row">
                <span class="title">{{ f.name }}</span>
                @if (canManage()) {
                  <button mat-icon-button (click)="$event.stopPropagation()" [matMenuTriggerFor]="menu"><mat-icon>more_vert</mat-icon></button>
                  <mat-menu #menu="matMenu">
                    <button mat-menu-item (click)="editFacility(f)">Edit</button>
                    <button mat-menu-item class="danger" (click)="deleteFacility(f)">Delete</button>
                  </mat-menu>
                }
              </div>
              <div class="meta">{{ typeLabel(f) }} &middot; Capacity {{ f.capacity }}</div>
              <div class="price">{{ f.pricePerUnit | currency: 'INR' }} / {{ pricingLabel(f) }}</div>
              @if (!f.isActive) { <app-status-badge variant="neutral" label="Inactive" /> }
              @if (f.isActive) {
                <button mat-stroked-button color="primary" class="book-btn" (click)="$event.stopPropagation(); bookFacility(f)">Book</button>
              }
            </div>
          </div>
        }
      </div>
    }
    </div>
  `,
  styles: [`
    .society-picker { width: 200px; margin-right: 8px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 16px; }
    .card { border-radius: 10px; overflow: hidden; border: 1px solid var(--app-border); background: var(--app-surface); cursor: pointer; }
    .card.inactive { opacity: .6; }
    .card:hover { box-shadow: 0 2px 8px rgba(0,0,0,.08); }
    .thumb { height: 120px; background-size: cover; background-position: center; background-color: var(--app-primary-light); }
    .body { padding: 14px; }
    .title-row { display: flex; align-items: center; justify-content: space-between; }
    .title { font-weight: 600; font-size: 15px; }
    .meta { font-size: 12px; color: var(--app-text-muted); margin-top: 4px; }
    .price { font-size: 13px; font-weight: 600; margin-top: 6px; }
    .book-btn { display: block; width: 100%; margin-top: 10px; }
    .danger { color: #c0392b; }
  `]
})
export class FacilitiesListComponent implements OnInit {
  private readonly facilityService = inject(FacilityService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly societies = signal<Society[]>([]);
  readonly societyId = signal(0);
  readonly facilities = signal<FacilityDto[]>([]);

  canManage(): boolean {
    return this.auth.hasPermission('facilities.manage');
  }

  typeLabel(f: FacilityDto): string { return FACILITY_TYPE_LABELS[f.type]; }
  pricingLabel(f: FacilityDto): string { return FACILITY_PRICING_TYPE_LABELS[f.pricingType]; }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      this.societies.set(societies);
      if (societies.length > 0) {
        this.societyId.set(societies[0].id);
        this.load();
      } else {
        this.loading.set(false);
      }
    });
  }

  onSocietyChange(societyId: number): void {
    this.societyId.set(societyId);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.facilityService.getFacilities(this.societyId(), !this.canManage()).subscribe((facilities) => {
      this.facilities.set(facilities);
      this.loading.set(false);
    });
  }

  openFacility(f: FacilityDto): void {
    this.router.navigate(['/facilities', f.id]);
  }

  /** Same dialog + create flow as the detail page's "Book This Facility"
   * button — lets a resident book straight from the list without an extra
   * navigation hop, defaulting to today the way the detail page defaults
   * to whatever date is selected there. */
  bookFacility(f: FacilityDto): void {
    const ref = this.dialog.open(FacilityBookingFormDialogComponent, { width: '520px', data: { facility: f, date: new Date() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.facilityService.createBooking(result).subscribe({
        next: () => this.toast.success('Facility booked.'),
        error: (err) => this.toast.error(err?.error?.message || 'Could not complete the booking.')
      });
    });
  }

  createFacility(): void {
    const ref = this.dialog.open(FacilityFormDialogComponent, { width: '660px', data: { societyId: this.societyId(), facility: null } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.facilityService.createFacility(result).subscribe(() => {
        this.toast.success('Facility created.');
        this.load();
      });
    });
  }

  editFacility(f: FacilityDto): void {
    const ref = this.dialog.open(FacilityFormDialogComponent, { width: '660px', data: { societyId: this.societyId(), facility: f } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.facilityService.updateFacility(f.id, result).subscribe(() => {
        this.toast.success('Facility updated.');
        this.load();
      });
    });
  }

  deleteFacility(f: FacilityDto): void {
    this.confirmDialog.confirm({ title: 'Delete Facility', destructive: true, message: `Delete "${f.name}"?` })
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.facilityService.deleteFacility(f.id).subscribe(() => {
          this.toast.success('Facility deleted.');
          this.load();
        });
      });
  }
}
