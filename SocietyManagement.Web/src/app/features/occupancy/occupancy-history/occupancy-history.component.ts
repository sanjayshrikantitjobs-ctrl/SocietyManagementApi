import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { AssetUrlPipe } from '../../../shared/pipes/asset-url.pipe';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { FlatOccupancyDto, OCCUPANCY_TYPE_LABELS, PERSON_RELATIONSHIP_LABELS } from '../models/occupancy.model';
import { OccupancyService } from '../services/occupancy.service';

/** Admin-only, read-only timeline of every past+current episode for a flat —
 * there is deliberately no delete action anywhere here, since the backend
 * exposes no delete endpoint for occupancy history at all. */
@Component({
  selector: 'app-occupancy-history',
  standalone: true,
  imports: [CommonModule, MatChipsModule, MatExpansionModule, MatIconModule, AssetUrlPipe, SkeletonLoaderComponent],
  template: `
    @if (loading()) {
      <app-skeleton-loader [rows]="3" [height]="60" />
    } @else if (episodes().length === 0) {
      <p class="empty">No occupancy history yet for this flat.</p>
    } @else {
      <mat-accordion>
        @for (episode of episodes(); track episode.id) {
          <mat-expansion-panel>
            <mat-expansion-panel-header>
              <mat-panel-title>
                <mat-chip-set>
                  <mat-chip [class.type-owner]="episode.type === 1" [class.type-tenant]="episode.type === 2">{{ typeLabels[episode.type] }}</mat-chip>
                </mat-chip-set>
                &nbsp;{{ episode.startDate | date: 'mediumDate' }} – {{ episode.endDate ? (episode.endDate | date: 'mediumDate') : 'Current' }}
              </mat-panel-title>
            </mat-expansion-panel-header>
            <table class="members-table">
              <tr>
                <th></th><th>Name</th><th>Relationship</th><th>Joined</th><th>Left</th>
              </tr>
              @for (m of episode.members; track m.id) {
                <tr>
                  <td>@if (m.photoUrl) { <img [src]="m.photoUrl | assetUrl" class="avatar" alt="" /> } @else { <mat-icon class="avatar-placeholder">account_circle</mat-icon> }</td>
                  <td>{{ m.personName }} @if (m.isPrimary) { <span class="badge">Primary</span> }</td>
                  <td>{{ relationshipLabels[m.relationship] }}</td>
                  <td>{{ m.joinedDate | date: 'mediumDate' }}</td>
                  <td>{{ m.leftDate ? (m.leftDate | date: 'mediumDate') : '—' }}</td>
                </tr>
              }
            </table>
          </mat-expansion-panel>
        }
      </mat-accordion>
    }
  `,
  styles: [`
    .empty { color: var(--app-text-muted); font-size: 13px; }
    .type-owner { background: #dbeafe; color: #1d4ed8; }
    .type-tenant { background: #fef3c7; color: #b45309; }
    .members-table { width: 100%; border-collapse: collapse; margin-top: 8px; }
    .members-table th { text-align: left; font-size: 11px; color: var(--app-text-muted); padding: 6px 8px; }
    .members-table td { padding: 6px 8px; font-size: 13px; border-top: 1px solid var(--app-border); }
    .avatar { width: 24px; height: 24px; border-radius: 50%; object-fit: cover; }
    .avatar-placeholder { color: var(--app-text-muted); font-size: 20px; }
    .badge { margin-left: 6px; padding: 1px 6px; border-radius: 8px; font-size: 10px; font-weight: 600; background: var(--app-primary-light); color: var(--app-primary); }
  `]
})
export class OccupancyHistoryComponent implements OnChanges {
  @Input() flatId!: number;

  private readonly occupancyService = inject(OccupancyService);

  readonly loading = signal(true);
  readonly episodes = signal<FlatOccupancyDto[]>([]);
  readonly typeLabels: Record<number, string> = OCCUPANCY_TYPE_LABELS;
  readonly relationshipLabels: Record<number, string> = PERSON_RELATIONSHIP_LABELS;

  ngOnChanges(): void {
    if (!this.flatId) return;
    this.loading.set(true);
    this.occupancyService.getHistory(this.flatId).subscribe((episodes) => {
      this.episodes.set(episodes);
      this.loading.set(false);
    });
  }
}
