import { CommonModule } from '@angular/common';
import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { ToastService } from '../../core/services/toast.service';
import { SignalrService } from '../../core/services/signalr.service';
import { SUPPORT_TICKET_STATUS_LABELS, SupportTicketDto } from './models/support.model';
import { SupportService } from './services/support.service';

/** Admin/Member self-service: raise a bug/support ticket against the
 * software itself and see the status of every ticket you've raised.
 * Resolution happens on the Super Admin side (see support-tickets-admin);
 * this page just reflects it once SupportTicketResolved arrives. */
@Component({
  selector: 'app-my-tickets',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatChipsModule, MatIconModule, PageHeaderComponent, EmptyStateComponent, SkeletonLoaderComponent],
  template: `
    <div class="app-page">
      <app-page-header title="Help & Support" subtitle="Report a bug or issue with the app — Super Admin will follow up here."
        [breadcrumbs]="[{ label: 'Help & Support' }]">
        <button mat-flat-button color="primary" (click)="createTicket()"><mat-icon>add</mat-icon> Raise a Ticket</button>
      </app-page-header>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="90" />
      } @else if (tickets().length === 0) {
        <app-empty-state icon="support_agent" title="No tickets yet" message="Raise a ticket if you run into a bug or need help." actionLabel="Raise a Ticket" (action)="createTicket()" />
      } @else {
        <div class="ticket-list">
          @for (t of tickets(); track t.id) {
            <div class="app-card ticket-card">
              <div class="ticket-header">
                <strong>{{ t.subject }}</strong>
                <mat-chip-set><mat-chip [class]="'status-' + t.status">{{ statusLabels[t.status] }}</mat-chip></mat-chip-set>
              </div>
              <p class="description">{{ t.description }}</p>
              <div class="meta">
                <span>Raised {{ t.createdAt | date: 'mediumDate' }}</span>
                @if (t.resolvedAt) {
                  <span>· Resolved {{ t.resolvedAt | date: 'mediumDate' }} by {{ t.resolvedByName }}</span>
                }
              </div>
              @if (t.resolutionNotes) {
                <div class="resolution"><strong>Resolution:</strong> {{ t.resolutionNotes }}</div>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .ticket-list { display: flex; flex-direction: column; gap: 12px; }
    .ticket-card { padding: 16px 20px; }
    .ticket-header { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 6px; }
    .description { margin: 0 0 8px; font-size: 13px; color: var(--app-text-muted); white-space: pre-wrap; }
    .meta { font-size: 12px; color: var(--app-text-muted); }
    .resolution { margin-top: 8px; padding: 10px 12px; background: var(--app-surface-alt); border-radius: 8px; font-size: 13px; }
    .status-1 { background: #fef3c7 !important; color: #b45309 !important; }
    .status-2 { background: #dbeafe !important; color: #1d4ed8 !important; }
    .status-3 { background: #dcfce7 !important; color: #15803d !important; }
  `]
})
export class MyTicketsComponent implements OnInit {
  private readonly supportService = inject(SupportService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly signalr = inject(SignalrService);

  readonly loading = signal(true);
  readonly tickets = signal<SupportTicketDto[]>([]);
  readonly statusLabels: Record<number, string> = SUPPORT_TICKET_STATUS_LABELS;

  constructor() {
    // Live-refresh when Super Admin resolves one of this user's tickets.
    effect(() => {
      const latest = this.signalr.notifications()[0];
      if (latest?.eventName === 'SupportTicketResolved') this.load();
    });
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.supportService.getMyTickets().subscribe((tickets) => {
      this.tickets.set(tickets);
      this.loading.set(false);
    });
  }

  createTicket(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px',
      data: {
        title: 'Raise a Ticket',
        submitLabel: 'Submit',
        fields: [
          { key: 'subject', label: 'Subject', type: 'text' },
          { key: 'description', label: 'Describe the issue', type: 'textarea' }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.supportService.createTicket(result.subject, result.description).subscribe(() => {
        this.toast.success('Ticket submitted.');
        this.load();
      });
    });
  }
}
