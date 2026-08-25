import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { SelectionModel } from '@angular/cdk/collections';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { ToastService } from '../../../core/services/toast.service';
import { RoleService } from '../../roles/role.service';
import { SocietyService } from '../../society-setup/services/society.service';
import { BulkLoginResultDto, FlatOwnershipGridDto, ResidentsOverviewSummaryDto } from '../models/occupancy.model';
import { OccupancyService } from '../services/occupancy.service';

/** Residents → Owner tab: compact stats strip + the society-wide Owner grid —
 * click a row for that flat's full Owner detail
 * (owner-flat-detail.component.ts). Deliberately not the full
 * ResidentsOverviewComponent here (settings button + recent-changes feed) —
 * that would just duplicate content already on the Overview tab. Also hosts
 * bulk "Create Logins" — select several flats and create a login for each
 * one's current Primary Owner in a single action. */
@Component({
  selector: 'app-owners-tab',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatCheckboxModule, MatChipsModule, MatIconModule, MatSortModule,
    MatTableModule, DataTableComponent, StatCardComponent
  ],
  template: `
    <div class="tab-content">
      @if (summary(); as s) {
        <div class="stats-grid">
          <app-stat-card label="Total Flats" [value]="s.totalFlats" icon="apartment" />
          <app-stat-card label="Owner Occupied" [value]="s.ownerOccupiedFlats" icon="home" iconColor="#2563eb" iconBg="#eff6ff" />
          <app-stat-card label="Vacant" [value]="s.vacantFlats" icon="meeting_room" iconColor="#64748b" iconBg="#f1f5f9" />
          <app-stat-card label="Total Owners" [value]="s.totalOwners" icon="badge" iconColor="#16a34a" iconBg="#ecfdf5" />
        </div>
      }

      @if (selection.selected.length > 0) {
        <div class="bulk-toolbar">
          <span>{{ selection.selected.length }} flat(s) selected</span>
          <button mat-flat-button color="primary" (click)="bulkCreateLogins()"><mat-icon>vpn_key</mat-icon> Create Logins</button>
        </div>
      }

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search flat number or owner name..." emptyIcon="home" emptyTitle="No flats found"
        emptyMessage="Flats will appear here once added under Society Setup."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="rows()" matSort (matSortChange)="onSort($event)" table>
          <ng-container matColumnDef="select">
            <th mat-header-cell *matHeaderCellDef>
              <mat-checkbox (change)="$event ? toggleAll() : null" [checked]="allSelected()" [indeterminate]="someSelected()" />
            </th>
            <td mat-cell *matCellDef="let r">
              <mat-checkbox (click)="$event.stopPropagation()" (change)="selection.toggle(r.flatId)" [checked]="selection.isSelected(r.flatId)" />
            </td>
          </ng-container>
          <ng-container matColumnDef="flat">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="flatnumber">Flat</th>
            <td mat-cell *matCellDef="let r"><strong>{{ r.flatNumber }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="location">
            <th mat-header-cell *matHeaderCellDef>Building / Wing</th>
            <td mat-cell *matCellDef="let r">{{ r.buildingName }}@if (r.wingName) { / {{ r.wingName }} }</td>
          </ng-container>
          <ng-container matColumnDef="owner">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="ownername">Primary Owner</th>
            <td mat-cell *matCellDef="let r">{{ r.primaryOwnerName ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="phone">
            <th mat-header-cell *matHeaderCellDef>Mobile</th>
            <td mat-cell *matCellDef="let r">{{ r.primaryOwnerPhone ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="members">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="membercount">Members</th>
            <td mat-cell *matCellDef="let r">{{ r.memberCount }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let r">
              <mat-chip-set><mat-chip [class.owned]="r.hasOwner" [class.vacant]="!r.hasOwner">{{ r.hasOwner ? 'Owned' : 'Vacant' }}</mat-chip></mat-chip-set>
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
    .bulk-toolbar { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; padding: 10px 14px; background: var(--app-primary-light); border-radius: 8px; }
    .bulk-toolbar span { font-size: 13px; font-weight: 600; color: var(--app-primary); }
    table { width: 100%; }
    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background: var(--app-surface-alt); }
    .owned { background: #dcfce7 !important; color: #15803d !important; }
    .vacant { background: #f1f5f9 !important; color: #64748b !important; }
  `]
})
export class OwnersTabComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly societyService = inject(SocietyService);
  private readonly occupancyService = inject(OccupancyService);
  private readonly roleService = inject(RoleService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly rows = signal<FlatOwnershipGridDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  readonly displayedColumns = ['select', 'flat', 'location', 'owner', 'phone', 'members', 'status'];
  readonly summary = signal<ResidentsOverviewSummaryDto | null>(null);
  readonly selection = new SelectionModel<number>(true, []);

  private societyId = 0;
  private roleOptions: { value: number; label: string }[] = [];

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.occupancyService.getResidentsOverviewSummary(this.societyId).subscribe((s) => this.summary.set(s));
      this.load();
    });
    this.roleService.getRoles().subscribe((roles) => {
      this.roleOptions = roles.map((r) => ({ value: r.id, label: r.name }));
    });
  }

  load(): void {
    this.loading.set(true);
    const sort = this.sortState();
    this.occupancyService.getOwnersGrid({
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

  openFlat(row: FlatOwnershipGridDto): void {
    this.router.navigate(['/residents/owners', row.flatId]);
  }

  allSelected(): boolean {
    return this.rows().length > 0 && this.rows().every((r) => this.selection.isSelected(r.flatId));
  }

  someSelected(): boolean {
    return this.rows().some((r) => this.selection.isSelected(r.flatId)) && !this.allSelected();
  }

  toggleAll(): void {
    if (this.allSelected()) {
      this.rows().forEach((r) => this.selection.deselect(r.flatId));
    } else {
      this.rows().forEach((r) => this.selection.select(r.flatId));
    }
  }

  bulkCreateLogins(): void {
    const flatIds = [...this.selection.selected];
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: `Create Logins for ${flatIds.length} Flat(s)`, submitLabel: 'Create',
        fields: [
          { key: 'roleId', label: 'Role', type: 'select' as const, options: this.roleOptions },
          { key: 'password', label: 'Password (default: Test@12345)', type: 'password' as const, required: false, defaultValue: '' }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.occupancyService.bulkCreateOwnerLogins(flatIds, Number(result.roleId), result.password || undefined)
        .subscribe((results) => {
          this.reportBulkResults(results);
          this.selection.clear();
          this.load();
        });
    });
  }

  private reportBulkResults(results: BulkLoginResultDto[]): void {
    const created = results.filter((r) => r.created);
    const skipped = results.filter((r) => !r.created);
    if (created.length > 0) {
      this.toast.success(`Created ${created.length} login(s): ${created.map((r) => r.flatNumber).join(', ')}.`);
    }
    if (skipped.length > 0) {
      this.toast.error(`Skipped ${skipped.length}: ${skipped.map((r) => `${r.flatNumber} (${r.skipReason})`).join('; ')}`);
    }
  }
}
