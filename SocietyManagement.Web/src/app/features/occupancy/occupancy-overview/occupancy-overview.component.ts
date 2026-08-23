import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { AuthService } from '../../../core/services/auth.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { SocietyService } from '../../society-setup/services/society.service';
import { OccupancyHistoryComponent } from '../occupancy-history/occupancy-history.component';
import { OccupancySettingsComponent } from '../occupancy-settings/occupancy-settings.component';
import { OwnerOccupancyPanelComponent } from '../owner-occupancy-panel/owner-occupancy-panel.component';
import { TenantOccupancyPanelComponent } from '../tenant-occupancy-panel/tenant-occupancy-panel.component';
import { FlatOccupancyOverviewDto } from '../models/occupancy.model';
import { OccupancyService } from '../services/occupancy.service';

/** The flat detail screen for the Owner/Tenant Occupancy module — Flat
 * Selector, current Owner panel, current Tenant panel (rental agreement +
 * family), and an Occupancy History section. Deliberately a new route
 * (residents/occupancy/:flatId), separate from the older
 * residents/flat/:flatId (FlatOccupancyComponent), which owns the
 * unrelated Member/FlatResidency model. */
@Component({
  selector: 'app-occupancy-overview',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatExpansionModule, MatFormFieldModule, MatIconModule, MatSelectModule,
    PageHeaderComponent, SkeletonLoaderComponent, OwnerOccupancyPanelComponent, TenantOccupancyPanelComponent,
    OccupancyHistoryComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Owner/Tenant Occupancy" [subtitle]="flatNumber()"
        [breadcrumbs]="[{ label: 'Residents', link: '/residents' }, { label: 'Occupancy' }]">
        @if (auth.hasPermission('occupancy.manage_settings')) {
          <button mat-stroked-button (click)="openSettings()"><mat-icon>settings</mat-icon> Settings</button>
        }
      </app-page-header>

      <mat-form-field appearance="outline" class="flat-selector">
        <mat-label>Flat</mat-label>
        <mat-select [value]="flatId()" (selectionChange)="onFlatChange($event.value)">
          @for (f of flatOptions(); track f.value) {
            <mat-option [value]="f.value">{{ f.label }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="120" />
      } @else if (overview(); as o) {
        <app-owner-occupancy-panel [flatId]="flatId()" [societyId]="societyId()" [occupancy]="o.currentOwnerOccupancy ?? null" (changed)="load()" />
        <app-tenant-occupancy-panel [flatId]="flatId()" [societyId]="societyId()" [occupancy]="o.currentTenantOccupancy ?? null" (changed)="load()" />

        @if (auth.hasPermission('occupancy.view_history')) {
          <mat-expansion-panel class="history-panel">
            <mat-expansion-panel-header><mat-panel-title>Occupancy History</mat-panel-title></mat-expansion-panel-header>
            <app-occupancy-history [flatId]="flatId()" />
          </mat-expansion-panel>
        }
      }
    </div>
  `,
  styles: [`
    .flat-selector { width: 280px; margin-bottom: 16px; }
    .history-panel { margin-top: 16px; }
  `]
})
export class OccupancyOverviewComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly societyService = inject(SocietyService);
  private readonly occupancyService = inject(OccupancyService);
  private readonly dialog = inject(MatDialog);
  readonly auth = inject(AuthService);

  readonly flatId = signal(0);
  readonly societyId = signal(0);
  readonly flatNumber = signal('');
  readonly loading = signal(true);
  readonly overview = signal<FlatOccupancyOverviewDto | null>(null);
  readonly flatOptions = signal<{ value: number; label: string }[]>([]);

  ngOnInit(): void {
    this.flatId.set(Number(this.route.snapshot.paramMap.get('flatId')));

    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) return;
      this.societyId.set(societies[0].id);
      this.societyService.getFlats({ pageSize: 500 }).subscribe((result) => {
        this.flatOptions.set(result.items.map((f) => ({ value: f.id, label: f.flatNumber })));
        this.flatNumber.set(result.items.find((f) => f.id === this.flatId())?.flatNumber ?? '');
      });
    });

    this.load();
  }

  onFlatChange(flatId: number): void {
    this.router.navigate(['/residents/occupancy', flatId]);
    this.flatId.set(flatId);
    this.flatNumber.set(this.flatOptions().find((f) => f.value === flatId)?.label ?? '');
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.occupancyService.getOverview(this.flatId()).subscribe((overview) => {
      this.overview.set(overview);
      this.loading.set(false);
    });
  }

  openSettings(): void {
    this.dialog.open(OccupancySettingsComponent, { data: { societyId: this.societyId() } });
  }
}
