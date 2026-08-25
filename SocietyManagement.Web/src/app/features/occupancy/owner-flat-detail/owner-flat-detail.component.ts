import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SocietyService } from '../../society-setup/services/society.service';
import { FlatEmergencyContactsCardComponent } from '../flat-emergency-contacts-card/flat-emergency-contacts-card.component';
import { FlatLoginCardComponent } from '../flat-login-card/flat-login-card.component';
import { FlatVehiclesCardComponent } from '../flat-vehicles-card/flat-vehicles-card.component';
import { FlatOccupancyOverviewDto } from '../models/occupancy.model';
import { OccupancyHistoryComponent } from '../occupancy-history/occupancy-history.component';
import { OwnerOccupancyPanelComponent } from '../owner-occupancy-panel/owner-occupancy-panel.component';
import { OccupancyService } from '../services/occupancy.service';

/** Full Owner detail for one flat, reached by clicking a row in the
 * Owners grid — owner panel + emergency contacts + login accounts +
 * occupancy history + vehicles, all scoped to this flat. */
@Component({
  selector: 'app-owner-flat-detail',
  standalone: true,
  imports: [
    CommonModule, PageHeaderComponent, OwnerOccupancyPanelComponent, FlatEmergencyContactsCardComponent,
    FlatLoginCardComponent, OccupancyHistoryComponent, FlatVehiclesCardComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header [title]="flatNumber() ? ('Flat ' + flatNumber() + ' — Owner') : 'Owner Detail'"
        subtitle="Owner members, emergency contacts, login accounts, occupancy history, and vehicles for this flat."
        [breadcrumbs]="[{ label: 'Residents', link: '/residents' }, { label: 'Owners', link: '/residents/owners' }, { label: flatNumber() || '...' }]" />

      @if (societyId(); as sid) {
        <app-owner-occupancy-panel [flatId]="flatId" [societyId]="sid" [occupancy]="overview()?.currentOwnerOccupancy ?? null" (changed)="load()" />
        <app-flat-login-card [occupancy]="overview()?.currentOwnerOccupancy ?? null" [flatId]="flatId" />
        <app-flat-emergency-contacts-card [flatId]="flatId" />
        <app-flat-vehicles-card [flatId]="flatId" [societyId]="sid" />

        <div class="panel">
          <h3>Occupancy History</h3>
          <app-occupancy-history [flatId]="flatId" />
        </div>
      }
    </div>
  `,
  styles: [`
    .panel { border: 1px solid var(--app-border); border-radius: 10px; padding: 16px; margin-bottom: 16px; }
    .panel h3 { margin: 0 0 12px; font-size: 15px; }
  `]
})
export class OwnerFlatDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly societyService = inject(SocietyService);
  private readonly occupancyService = inject(OccupancyService);

  readonly flatNumber = signal<string | null>(null);
  readonly overview = signal<FlatOccupancyOverviewDto | null>(null);

  readonly societyId = signal(0);
  flatId = 0;

  ngOnInit(): void {
    this.flatId = Number(this.route.snapshot.paramMap.get('flatId'));
    this.societyService.getFlat(this.flatId).subscribe((flat) => this.flatNumber.set(flat.flatNumber));
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) return;
      this.societyId.set(societies[0].id);
      this.load();
    });
  }

  load(): void {
    this.occupancyService.getOverview(this.flatId).subscribe((overview) => this.overview.set(overview));
  }
}
