import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatChipsModule } from '@angular/material/chips';
import { PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { SocietyService } from '../../society-setup/services/society.service';
import { FlatTenancyGridDto, ResidentsOverviewSummaryDto } from '../models/occupancy.model';
import { OccupancyService } from '../services/occupancy.service';

/** Residents → Tenant tab: compact stats strip + the society-wide Tenant
 * grid — click a row for that flat's full Tenant detail
 * (tenant-flat-detail.component.ts). See owners-tab.component.ts for why
 * this isn't the full ResidentsOverviewComponent. */
@Component({
  selector: 'app-tenants-tab',
  standalone: true,
  imports: [CommonModule, MatChipsModule, MatSortModule, MatTableModule, DataTableComponent, StatCardComponent],
  template: `
    <div class="tab-content">
      @if (summary(); as s) {
        <div class="stats-grid">
          <app-stat-card label="Total Flats" [value]="s.totalFlats" icon="apartment" />
          <app-stat-card label="Tenant Occupied" [value]="s.tenantOccupiedFlats" icon="key" iconColor="#b45309" iconBg="#fffbeb" />
          <app-stat-card label="Vacant" [value]="s.vacantFlats" icon="meeting_room" iconColor="#64748b" iconBg="#f1f5f9" />
          <app-stat-card label="Total Tenants" [value]="s.totalTenants" icon="assignment_ind" iconColor="#b45309" iconBg="#fffbeb" />
        </div>
      }

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search flat number or tenant name..." emptyIcon="key" emptyTitle="No flats found"
        emptyMessage="Flats will appear here once added under Society Setup."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="rows()" matSort (matSortChange)="onSort($event)" table>
          <ng-container matColumnDef="flat">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="flatnumber">Flat</th>
            <td mat-cell *matCellDef="let r"><strong>{{ r.flatNumber }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="location">
            <th mat-header-cell *matHeaderCellDef>Building / Wing</th>
            <td mat-cell *matCellDef="let r">{{ r.buildingName }}@if (r.wingName) { / {{ r.wingName }} }</td>
          </ng-container>
          <ng-container matColumnDef="tenant">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="tenantname">Primary Tenant</th>
            <td mat-cell *matCellDef="let r">{{ r.primaryTenantName ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="phone">
            <th mat-header-cell *matHeaderCellDef>Mobile</th>
            <td mat-cell *matCellDef="let r">{{ r.primaryTenantPhone ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="members">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="membercount">Members</th>
            <td mat-cell *matCellDef="let r">{{ r.memberCount }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let r">
              <mat-chip-set><mat-chip [class.rented]="r.hasTenant" [class.vacant]="!r.hasTenant">{{ r.hasTenant ? 'Rented' : 'Vacant' }}</mat-chip></mat-chip-set>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;" (click)="openFlat(row)" class="clickable-row"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; margin-bottom: 20px; }
    table { width: 100%; }
    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background: var(--app-surface-alt); }
    .rented { background: #fef3c7 !important; color: #b45309 !important; }
    .vacant { background: #f1f5f9 !important; color: #64748b !important; }
  `]
})
export class TenantsTabComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly societyService = inject(SocietyService);
  private readonly occupancyService = inject(OccupancyService);

  readonly loading = signal(true);
  readonly rows = signal<FlatTenancyGridDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  readonly displayedColumns = ['flat', 'location', 'tenant', 'phone', 'members', 'status'];
  readonly summary = signal<ResidentsOverviewSummaryDto | null>(null);

  private societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.occupancyService.getResidentsOverviewSummary(this.societyId).subscribe((s) => this.summary.set(s));
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    const sort = this.sortState();
    this.occupancyService.getTenantsGrid({
      societyId: this.societyId, search: this.searchTerm() || undefined,
      sortBy: sort?.direction ? sort.active : undefined, sortDescending: sort?.direction === 'desc',
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.rows.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.pageIndex.set(0);
    this.load();
  }

  onSort(sort: Sort): void {
    this.sortState.set(sort);
    this.pageIndex.set(0);
    this.load();
  }

  openFlat(row: FlatTenancyGridDto): void {
    this.router.navigate(['/residents/tenants', row.flatId]);
  }
}
