import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { FestivalsEventsTabsComponent } from '../../shared/components/festivals-events-tabs/festivals-events-tabs.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { FestivalService } from '../festivals/services/festival.service';
import { SocietyService } from '../society-setup/services/society.service';
import { EVENT_STATUS_LABELS, EventDto } from './models/event.model';
import { EventService } from './services/event.service';

@Component({
  selector: 'app-events-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink, MatButtonModule, MatChipsModule, MatFormFieldModule,
    MatIconModule, MatSelectModule, MatTableModule, MatTooltipModule, DataTableComponent,
    FestivalsEventsTabsComponent, PageHeaderComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Events" subtitle="Dinners, gatherings, and other capacity-limited society events."
        [breadcrumbs]="[{ label: 'Events' }]">
        @if (auth.isAdmin()) {
          <button mat-flat-button color="primary" (click)="add()" [disabled]="societyId === 0">
            <mat-icon>add</mat-icon> Add Event
          </button>
        }
      </app-page-header>

      <app-festivals-events-tabs />

      <div class="toolbar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="status-filter">
          <mat-label>Status</mat-label>
          <mat-select [(ngModel)]="statusFilter" (selectionChange)="onFilterChange()">
            <mat-option [value]="null">All</mat-option>
            @for (opt of statusOptions; track opt.value) {
              <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>

      <app-data-table
        [loading]="loading()"
        [totalCount]="totalCount()"
        [pageSize]="pageSize()"
        [pageIndex]="pageIndex()"
        [showSearch]="false"
        emptyIcon="event"
        emptyTitle="No events yet"
        emptyMessage="Create an event to start collecting RSVPs."
        (page)="onPage($event)">
        <table mat-table [dataSource]="events()" table>
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Event</th>
            <td mat-cell *matCellDef="let e">
              <strong>{{ e.name }}</strong>
              @if (e.festivalName) { <div class="muted">{{ e.festivalName }}</div> }
            </td>
          </ng-container>
          <ng-container matColumnDef="when">
            <th mat-header-cell *matHeaderCellDef>When</th>
            <td mat-cell *matCellDef="let e">{{ e.eventDateTime | date: 'medium' }}</td>
          </ng-container>
          <ng-container matColumnDef="venue">
            <th mat-header-cell *matHeaderCellDef>Venue</th>
            <td mat-cell *matCellDef="let e">{{ e.venue || '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="capacity">
            <th mat-header-cell *matHeaderCellDef>Capacity</th>
            <td mat-cell *matCellDef="let e">{{ e.capacityLimit ?? 'Unlimited' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let e">
              <mat-chip-set><mat-chip [class]="'status-' + e.status">{{ statusLabels[e.status] }}</mat-chip></mat-chip-set>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let e">
              @if (e.status === 2) {
                <button mat-stroked-button color="primary" [routerLink]="['/events', e.id, 'rsvp']">RSVP</button>
              }
              @if (auth.isAdmin()) {
                @if (e.status === 1) {
                  <button mat-icon-button matTooltip="Open for RSVPs" (click)="open(e)"><mat-icon>lock_open</mat-icon></button>
                }
                @if (e.status === 2) {
                  <button mat-icon-button matTooltip="Close to new RSVPs" (click)="close(e)"><mat-icon>lock</mat-icon></button>
                }
                @if (e.status === 2 || e.status === 3) {
                  <button mat-icon-button matTooltip="Check In" [routerLink]="['/events', e.id, 'check-in']"><mat-icon>how_to_reg</mat-icon></button>
                  <button mat-icon-button matTooltip="Mark Completed" (click)="complete(e)"><mat-icon>check_circle</mat-icon></button>
                }
                @if (e.status !== 4 && e.status !== 5) {
                  <button mat-icon-button matTooltip="Cancel Event" (click)="cancel(e)"><mat-icon>cancel</mat-icon></button>
                }
                @if (e.status === 1) {
                  <button mat-icon-button matTooltip="Edit" (click)="edit(e)"><mat-icon>edit</mat-icon></button>
                  <button mat-icon-button matTooltip="Delete" (click)="remove(e)"><mat-icon>delete_outline</mat-icon></button>
                }
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    .toolbar { display: flex; align-items: center; margin-bottom: 16px; }
    .status-filter { width: 200px; }
    table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
  `]
})
export class EventsListComponent implements OnInit {
  private readonly eventService = inject(EventService);
  private readonly societyService = inject(SocietyService);
  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly events = signal<EventDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly displayedColumns = ['name', 'when', 'venue', 'capacity', 'status', 'actions'];
  readonly statusLabels: Record<number, string> = EVENT_STATUS_LABELS;
  readonly statusOptions = Object.entries(EVENT_STATUS_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  societyId = 0;
  statusFilter: number | null = null;
  private festivalOptions: { value: number; label: string }[] = [];

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.festivalService.getFestivals({ societyId: this.societyId, pageSize: 500 }).subscribe((result) => {
        this.festivalOptions = result.items.map((f) => ({ value: f.id, label: f.name }));
      });
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.eventService.getEvents({
      societyId: this.societyId, status: this.statusFilter ?? undefined,
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.events.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  onFilterChange(): void {
    this.pageIndex.set(0);
    this.load();
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  private fields(existing?: EventDto) {
    return [
      {
        key: 'festivalId', label: 'Linked Festival (optional)', type: 'select' as const, required: false,
        options: [{ value: 0, label: 'None — standalone event' }, ...this.festivalOptions],
        defaultValue: existing?.festivalId ?? 0
      },
      { key: 'name', label: 'Event Name', type: 'text' as const, defaultValue: existing?.name ?? '' },
      { key: 'description', label: 'Description', type: 'textarea' as const, required: false, defaultValue: existing?.description ?? '' },
      { key: 'eventDateTime', label: 'Date & Time', type: 'datetime-local' as const, defaultValue: existing?.eventDateTime?.substring(0, 16) ?? '' },
      { key: 'venue', label: 'Venue', type: 'text' as const, required: false, defaultValue: existing?.venue ?? '' },
      { key: 'capacityLimit', label: 'Capacity (optional, blank = unlimited)', type: 'number' as const, required: false, defaultValue: existing?.capacityLimit ?? '' },
      { key: 'rsvpDeadline', label: 'RSVP Deadline (optional)', type: 'datetime-local' as const, required: false, defaultValue: existing?.rsvpDeadline?.substring(0, 16) ?? '' }
    ];
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '480px', data: { title: 'Add Event', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.eventService.createEvent({
        ...result, societyId: this.societyId, festivalId: Number(result.festivalId) || null,
        description: result.description || null, venue: result.venue || null,
        capacityLimit: result.capacityLimit ? Number(result.capacityLimit) : null,
        rsvpDeadline: result.rsvpDeadline || null
      }).subscribe(() => {
        this.toast.success('Event created.');
        this.load();
      });
    });
  }

  edit(event: EventDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px', data: { title: 'Edit Event', submitLabel: 'Save', fields: this.fields(event) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.eventService.updateEvent(event.id, {
        name: result.name, description: result.description || null, eventDateTime: result.eventDateTime,
        venue: result.venue || null, capacityLimit: result.capacityLimit ? Number(result.capacityLimit) : null,
        rsvpDeadline: result.rsvpDeadline || null
      }).subscribe(() => {
        this.toast.success('Event updated.');
        this.load();
      });
    });
  }

  open(event: EventDto): void {
    this.eventService.openEvent(event.id).subscribe(() => {
      this.toast.success('Event opened for RSVPs.');
      this.load();
    });
  }

  close(event: EventDto): void {
    this.eventService.closeEvent(event.id).subscribe(() => {
      this.toast.success('Event closed to new RSVPs.');
      this.load();
    });
  }

  complete(event: EventDto): void {
    this.confirmDialog.confirm({
      title: 'Mark Completed', message: `Mark "${event.name}" as completed?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.eventService.completeEvent(event.id).subscribe(() => {
        this.toast.success('Event marked completed.');
        this.load();
      });
    });
  }

  cancel(event: EventDto): void {
    this.confirmDialog.confirm({
      title: 'Cancel Event', destructive: true, message: `Cancel "${event.name}"? This cannot be undone.`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.eventService.cancelEvent(event.id).subscribe(() => {
        this.toast.success('Event cancelled.');
        this.load();
      });
    });
  }

  remove(event: EventDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Event', destructive: true, message: `Delete "${event.name}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.eventService.deleteEvent(event.id).subscribe(() => {
        this.toast.success('Event deleted.');
        this.load();
      });
    });
  }
}
