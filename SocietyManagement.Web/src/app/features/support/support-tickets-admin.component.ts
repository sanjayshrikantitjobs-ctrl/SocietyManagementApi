import { CommonModule } from '@angular/common';
import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { ToastService } from '../../core/services/toast.service';
import { SignalrService } from '../../core/services/signalr.service';
import { SUPPORT_TICKET_STATUS_LABELS, SupportTicketDto, SupportTicketStatus } from './models/support.model';
import { SupportService } from './services/support.service';

/** Super Admin-only — every support ticket across every society (Super
 * Admin has no SocietyId to scope by, so this is deliberately unfiltered
 * by tenant; see GetAllTicketsQuery). Resolving one notifies whoever
 * raised it. */
@Component({
  selector: 'app-support-tickets-admin',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatChipsModule, MatFormFieldModule, MatIconModule,
    MatSelectModule, MatTableModule, DataTableComponent, PageHeaderComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Support Tickets" subtitle="Bug reports and support requests raised by every society's Admin/Member."
        [breadcrumbs]="[{ label: 'Support Tickets' }]" />

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        [showSearch]="false" emptyTitle="No tickets" emptyMessage="No support tickets have been raised yet."
        (page)="onPage($event)">
        <div toolbar>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="status-filter">
            <mat-select [value]="statusFilter()" (selectionChange)="onStatusFilterChange($event.value)" placeholder="All statuses">
              <mat-option [value]="null">All statuses</mat-option>
              @for (opt of statusOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
        </div>
        <table mat-table [dataSource]="tickets()" table>
          <ng-container matColumnDef="subject">
            <th mat-header-cell *matHeaderCellDef>Subject</th>
            <td mat-cell *matCellDef="let t">
              <strong>{{ t.subject }}</strong><br /><span class="muted">{{ t.description }}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="society">
            <th mat-header-cell *matHeaderCellDef>Society</th>
            <td mat-cell *matCellDef="let t">{{ t.societyName }}<br /><span class="muted">{{ t.createdByName }}</span></td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let t"><mat-chip-set><mat-chip [class]="'status-' + t.status">{{ statusLabels[t.status] }}</mat-chip></mat-chip-set></td>
          </ng-container>
          <ng-container matColumnDef="createdAt">
            <th mat-header-cell *matHeaderCellDef>Raised</th>
            <td mat-cell *matCellDef="let t">{{ t.createdAt | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let t">
              @if (t.status !== 3) {
                <button mat-stroked-button (click)="updateStatus(t)">Update</button>
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
    div[toolbar] { display: flex; align-items: center; gap: 12px; }
    .status-filter { width: 180px; }
    table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .status-1 { background: #fef3c7 !important; color: #b45309 !important; }
    .status-2 { background: #dbeafe !important; color: #1d4ed8 !important; }
    .status-3 { background: #dcfce7 !important; color: #15803d !important; }
  `]
})
export class SupportTicketsAdminComponent implements OnInit {
  private readonly supportService = inject(SupportService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly signalr = inject(SignalrService);

  readonly loading = signal(true);
  readonly tickets = signal<SupportTicketDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly statusFilter = signal<SupportTicketStatus | null>(null);
  readonly displayedColumns = ['subject', 'society', 'status', 'createdAt', 'actions'];
  readonly statusLabels: Record<number, string> = SUPPORT_TICKET_STATUS_LABELS;
  readonly statusOptions = Object.entries(SUPPORT_TICKET_STATUS_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  constructor() {
    effect(() => {
      const latest = this.signalr.notifications()[0];
      if (latest?.eventName === 'SupportTicketCreated') this.load();
    });
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.supportService.getAllTickets({
      status: this.statusFilter() ?? undefined, pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.tickets.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  onStatusFilterChange(status: SupportTicketStatus | null): void {
    this.statusFilter.set(status);
    this.pageIndex.set(0);
    this.load();
  }

  updateStatus(ticket: SupportTicketDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '460px',
      data: {
        title: `Update — ${ticket.subject}`,
        submitLabel: 'Save',
        fields: [
          {
            key: 'status', label: 'Status', type: 'select', defaultValue: ticket.status,
            options: [{ value: 2, label: 'In Progress' }, { value: 3, label: 'Resolved' }]
          },
          { key: 'resolutionNotes', label: 'Resolution Notes (shown to the reporter once resolved)', type: 'textarea', required: false, defaultValue: ticket.resolutionNotes ?? '' }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.supportService.updateTicketStatus(ticket.id, Number(result.status), result.resolutionNotes || null).subscribe(() => {
        this.toast.success('Ticket updated.');
        this.load();
      });
    });
  }
}
