import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, output, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { EmptyStateComponent } from '../empty-state/empty-state.component';
import { SkeletonLoaderComponent } from '../skeleton-loader/skeleton-loader.component';
import { NotificationDto, resolveNotificationLink } from '../../models/notification.model';
import { NotificationService } from '../../services/notification.service';

/** The dropdown body of the new, separate notification bell (main-layout's
 * existing "expiring services" bell is untouched — this is an additional
 * affordance, not a repurposing of it). Deliberately its own component
 * rather than inline in main-layout's already-large template. */
@Component({
  selector: 'app-notification-panel',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, EmptyStateComponent, SkeletonLoaderComponent],
  template: `
    <div class="panel">
      <div class="panel-header">
        <span class="panel-title">Notifications</span>
        @if (notifications().length > 0) {
          <button mat-button (click)="markAllRead()">Mark all read</button>
        }
      </div>

      @if (loading()) {
        <app-skeleton-loader [rows]="4" [height]="52" />
      } @else if (notifications().length === 0) {
        <app-empty-state icon="notifications_none" title="You're all caught up" message="Nothing new right now." />
      } @else {
        <div class="list">
          @for (n of notifications(); track n.id) {
            <button type="button" class="row" [class.unread]="!n.isRead" (click)="open(n)">
              @if (!n.isRead) { <span class="dot"></span> }
              <span class="body">
                <span class="row-title">{{ n.title }}</span>
                <span class="row-message">{{ n.message }}</span>
                <span class="row-time">{{ n.createdAt | date: 'short' }}</span>
              </span>
            </button>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .panel { width: 360px; max-width: 90vw; }
    .panel-header { display: flex; align-items: center; justify-content: space-between; padding: 12px 16px 4px; }
    .panel-title { font-weight: 700; font-size: 15px; }
    .list { max-height: 420px; overflow-y: auto; display: flex; flex-direction: column; }
    .row {
      display: flex; align-items: flex-start; gap: 8px; padding: 10px 16px;
      border: none; background: none; text-align: left; cursor: pointer; width: 100%;
      border-bottom: 1px solid var(--app-border);
    }
    .row:hover { background: var(--app-surface-alt); }
    .row.unread { background: var(--app-surface-hover, #f6f8ff); }
    .dot { width: 8px; height: 8px; border-radius: 50%; background: var(--app-primary, #4f6ef7); margin-top: 6px; flex-shrink: 0; }
    .body { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .row-title { font-weight: 600; font-size: 13px; }
    .row-message { font-size: 12.5px; color: var(--app-text-muted); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .row-time { font-size: 11px; color: var(--app-text-muted); }
  `]
})
export class NotificationPanelComponent implements OnInit {
  private readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);

  /** Emitted whenever the unread count might have changed (loaded, marked
   * read, marked-all-read) so main-layout's badge count stays in sync
   * without this panel needing to know how the badge itself is rendered. */
  unreadCountChanged = output<number>();
  closed = output<void>();

  readonly loading = signal(true);
  readonly notifications = signal<NotificationDto[]>([]);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.notificationService.getNotifications({ pageSize: 20 }).subscribe((result) => {
      this.notifications.set(result.items);
      this.loading.set(false);
      this.unreadCountChanged.emit(result.items.filter((n) => !n.isRead).length);
    });
  }

  open(n: NotificationDto): void {
    if (!n.isRead) {
      this.notificationService.markRead(n.id).subscribe(() => {
        this.notifications.update((list) => list.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
        this.unreadCountChanged.emit(this.notifications().filter((x) => !x.isRead).length);
      });
    }

    const link = resolveNotificationLink(n);
    this.closed.emit();
    if (link) this.router.navigate(link);
  }

  markAllRead(): void {
    this.notificationService.markAllRead().subscribe(() => {
      this.notifications.update((list) => list.map((x) => ({ ...x, isRead: true })));
      this.unreadCountChanged.emit(0);
    });
  }
}
