import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../../core/services/auth.service';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { SocietyService } from '../../society-setup/services/society.service';
import { OccupancySettingsComponent } from '../occupancy-settings/occupancy-settings.component';
import { OCCUPANCY_TYPE_LABELS, RecentOccupancyChangeDto, ResidentsOverviewSummaryDto } from '../models/occupancy.model';
import { OccupancyService } from '../services/occupancy.service';

/** The Residents "Overview" tab — aggregate stat cards + a recent-changes
 * feed, computed live from FlatOccupancy/OccupancyMember (not the older,
 * unreliable Flat.Status field). Also hosts the Occupancy Settings entry
 * point (AllowMultiplePrimaryOwners). */
@Component({
  selector: 'app-residents-overview',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, SkeletonLoaderComponent, StatCardComponent],
  template: `
    <div class="tab-content">
      @if (auth.hasPermission('occupancy.manage_settings')) {
        <div class="settings-row">
          <button mat-stroked-button (click)="openSettings()"><mat-icon>settings</mat-icon> Occupancy Settings</button>
        </div>
      }
      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="90" />
      } @else {
        <div class="stats-grid">
          <app-stat-card label="Total Flats" [value]="summary()?.totalFlats ?? 0" icon="apartment" />
          <app-stat-card label="Owner Occupied" [value]="summary()?.ownerOccupiedFlats ?? 0" icon="home" iconColor="#2563eb" iconBg="#eff6ff" />
          <app-stat-card label="Tenant Occupied" [value]="summary()?.tenantOccupiedFlats ?? 0" icon="key" iconColor="#b45309" iconBg="#fffbeb" />
          <app-stat-card label="Vacant" [value]="summary()?.vacantFlats ?? 0" icon="meeting_room" iconColor="#64748b" iconBg="#f1f5f9" />
          <app-stat-card label="Total Members" [value]="summary()?.totalMembers ?? 0" icon="groups" />
          <app-stat-card label="Total Owners" [value]="summary()?.totalOwners ?? 0" icon="badge" iconColor="#16a34a" iconBg="#ecfdf5" />
          <app-stat-card label="Total Tenants" [value]="summary()?.totalTenants ?? 0" icon="assignment_ind" iconColor="#b45309" iconBg="#fffbeb" />
        </div>

        <div class="app-card recent-card">
          <h3>Recent Occupancy Changes</h3>
          @if (recentChanges().length === 0) {
            <p class="empty">No recent activity.</p>
          } @else {
            <ul class="recent-list">
              @for (c of recentChanges(); track c.flatId + c.personName + c.changeDate) {
                <li>
                  <mat-icon [class.in]="c.movedIn" [class.out]="!c.movedIn">{{ c.movedIn ? 'login' : 'logout' }}</mat-icon>
                  <span class="text">
                    <strong>{{ c.personName }}</strong> {{ c.movedIn ? 'moved into' : 'moved out of' }}
                    <strong>{{ c.flatNumber }}</strong> as {{ typeLabels[c.type] }}
                  </span>
                  <span class="date">{{ c.changeDate | date: 'mediumDate' }}</span>
                </li>
              }
            </ul>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .settings-row { display: flex; justify-content: flex-end; margin-bottom: 16px; }
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .recent-card { padding: 20px; }
    .recent-card h3 { margin: 0 0 12px; font-size: 15px; }
    .empty { color: var(--app-text-muted); font-size: 13px; margin: 0; }
    .recent-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 10px; }
    .recent-list li { display: flex; align-items: center; gap: 10px; font-size: 13px; }
    .recent-list mat-icon.in { color: #16a34a; }
    .recent-list mat-icon.out { color: #dc2626; }
    .recent-list .text { flex: 1; }
    .recent-list .date { color: var(--app-text-muted); font-size: 12px; }
  `]
})
export class ResidentsOverviewComponent implements OnInit {
  private readonly societyService = inject(SocietyService);
  private readonly occupancyService = inject(OccupancyService);
  private readonly dialog = inject(MatDialog);
  readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly summary = signal<ResidentsOverviewSummaryDto | null>(null);
  readonly recentChanges = signal<RecentOccupancyChangeDto[]>([]);
  readonly typeLabels: Record<number, string> = OCCUPANCY_TYPE_LABELS;
  private societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.occupancyService.getResidentsOverviewSummary(this.societyId).subscribe((summary) => {
        this.summary.set(summary);
        this.loading.set(false);
      });
      this.occupancyService.getRecentOccupancyChanges(this.societyId).subscribe((changes) => this.recentChanges.set(changes));
    });
  }

  openSettings(): void {
    this.dialog.open(OccupancySettingsComponent, { data: { societyId: this.societyId } });
  }
}
