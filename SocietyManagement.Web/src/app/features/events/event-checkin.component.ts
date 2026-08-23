import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ToastService } from '../../core/services/toast.service';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatCardComponent } from '../../shared/components/stat-card/stat-card.component';
import { EVENT_RSVP_STATUS_LABELS, EventCapacitySummaryDto, EventDto, EventRsvpDto } from './models/event.model';
import { EventService } from './services/event.service';

/** Admin fallback for check-in when scanning isn't convenient (working the
 * door from a laptop, or a QR won't scan) — the primary flow is a phone
 * camera app opening /events/check-in/:qrToken directly. */
@Component({
  selector: 'app-event-checkin',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatIconModule, MatSortModule, MatTableModule,
    PageHeaderComponent, SkeletonLoaderComponent, StatCardComponent, DataTableComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Check-in" [subtitle]="event()?.name ?? ''"
        [breadcrumbs]="[{ label: 'Events', link: '/events' }, { label: 'Check-in' }]" />

      @if (pageLoading()) {
        <app-skeleton-loader [rows]="4" [height]="70" />
      } @else {
        @if (capacity(); as c) {
          <div class="stats-grid">
            <app-stat-card label="Capacity" [value]="c.capacityLimit ?? 'Unlimited'" icon="event_seat" />
            <app-stat-card label="Registered" [value]="c.totalRegistered" icon="how_to_reg" />
            <app-stat-card label="Checked In" [value]="c.totalCheckedIn" icon="fact_check" iconColor="#16a34a" iconBg="#ecfdf5" />
            @if (c.remainingSeats !== null && c.remainingSeats !== undefined) {
              <app-stat-card label="Seats Left" [value]="c.remainingSeats" icon="airline_seat_recline_normal"
                [iconColor]="c.remainingSeats <= 0 ? '#dc2626' : 'var(--app-primary)'"
                [iconBg]="c.remainingSeats <= 0 ? '#fef2f2' : 'var(--app-primary-light)'" />
            }
          </div>
        }

        <app-data-table
          [loading]="rsvpsLoading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
          searchPlaceholder="Search flat or member..." emptyIcon="how_to_reg" emptyTitle="No RSVPs yet"
          emptyMessage="Nobody has registered for this event yet."
          (page)="onPage($event)" (search)="onSearch($event)">
          <table mat-table [dataSource]="rsvps()" matSort (matSortChange)="onSort($event)" table>
            <ng-container matColumnDef="flat">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Flat</th>
              <td mat-cell *matCellDef="let r">{{ r.flatNumber }}</td>
            </ng-container>
            <ng-container matColumnDef="member">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Registered By</th>
              <td mat-cell *matCellDef="let r">{{ r.memberName }} — {{ r.memberPhone }}</td>
            </ng-container>
            <ng-container matColumnDef="headCount">
              <th mat-header-cell *matHeaderCellDef mat-sort-header="headcount">Registered</th>
              <td mat-cell *matCellDef="let r">{{ r.headCount }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
              <td mat-cell *matCellDef="let r">
                <span class="badge" [class.badge-success]="r.status === 2" [class.badge-muted]="r.status === 3" [class.badge-info]="r.status === 1">
                  {{ statusLabels[r.status] }}{{ r.status === 2 ? ' (' + r.checkedInCount + ')' : '' }}
                </span>
              </td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let r">
                @if (r.status === 1) {
                  <button mat-stroked-button color="primary" (click)="checkIn(r)">Check In</button>
                }
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
          </table>
        </app-data-table>
      }
    </div>
  `,
  styles: [`
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 16px; margin-bottom: 20px; }
    table { width: 100%; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .badge-success { background: #dcfce7; color: #15803d; }
    .badge-info { background: #dbeafe; color: #1d4ed8; }
    .badge-muted { background: #e2e8f0; color: #475569; }
  `]
})
export class EventCheckinComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly eventService = inject(EventService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  readonly pageLoading = signal(true);
  readonly rsvpsLoading = signal(true);
  readonly event = signal<EventDto | null>(null);
  readonly capacity = signal<EventCapacitySummaryDto | null>(null);
  readonly rsvps = signal<EventRsvpDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  readonly statusLabels: Record<number, string> = EVENT_RSVP_STATUS_LABELS;
  readonly displayedColumns = ['flat', 'member', 'headCount', 'status', 'actions'];

  private eventId = 0;

  ngOnInit(): void {
    this.eventId = Number(this.route.snapshot.paramMap.get('id'));
    this.eventService.getEventById(this.eventId).subscribe((e) => this.event.set(e));
    this.refresh();
    this.pageLoading.set(false);
  }

  private refresh(): void {
    this.eventService.getCapacitySummary(this.eventId).subscribe((c) => this.capacity.set(c));
    this.loadRsvps();
  }

  private loadRsvps(): void {
    this.rsvpsLoading.set(true);
    const sort = this.sortState();
    this.eventService.getRsvpsForEvent({
      eventId: this.eventId, search: this.searchTerm() || undefined,
      sortBy: sort?.direction ? sort.active : undefined, sortDescending: sort?.direction === 'desc',
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.rsvps.set(result.items);
      this.totalCount.set(result.totalCount);
      this.rsvpsLoading.set(false);
    });
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.loadRsvps();
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.pageIndex.set(0);
    this.loadRsvps();
  }

  onSort(sort: Sort): void {
    this.sortState.set(sort);
    this.pageIndex.set(0);
    this.loadRsvps();
  }

  checkIn(rsvp: EventRsvpDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '360px',
      data: {
        title: `Check In — Flat ${rsvp.flatNumber}`,
        submitLabel: 'Confirm',
        fields: [{ key: 'actualHeadCount', label: 'Actual Headcount', type: 'number' as const, defaultValue: rsvp.headCount }]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.eventService.checkIn(rsvp.qrToken, Number(result.actualHeadCount)).subscribe(() => {
        this.toast.success(`Flat ${rsvp.flatNumber} checked in.`);
        this.refresh();
      });
    });
  }
}
