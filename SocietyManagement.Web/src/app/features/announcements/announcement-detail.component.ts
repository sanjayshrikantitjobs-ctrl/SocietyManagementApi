import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { AssetUrlPipe } from '../../shared/pipes/asset-url.pipe';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { AnnouncementFormDialogComponent } from './announcement-form-dialog.component';
import { ANNOUNCEMENT_PRIORITY_LABELS, ANNOUNCEMENT_STATUS_LABELS, ANNOUNCEMENT_TYPE_LABELS, AnnouncementDto } from './models/announcement.model';
import { AnnouncementService } from './services/announcement.service';

@Component({
  selector: 'app-announcement-detail',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatMenuModule, PageHeaderComponent, AssetUrlPipe],
  template: `
    <div class="app-page">
    @if (announcement(); as a) {
      <app-page-header [title]="a.title" [subtitle]="typeLabel(a) + ' · ' + priorityLabel(a)">
        <button mat-button (click)="router.navigate(['/announcements'])">
          <mat-icon>arrow_back</mat-icon> Back
        </button>
        @if (canManage()) {
          <button mat-icon-button [matMenuTriggerFor]="menu"><mat-icon>more_vert</mat-icon></button>
          <mat-menu #menu="matMenu">
            <button mat-menu-item (click)="edit(a)">Edit</button>
            @if (a.status === 1 || a.status === 2) {
              <button mat-menu-item (click)="publish(a)">Publish Now</button>
            }
            <button mat-menu-item class="danger" (click)="remove(a)">Delete</button>
          </mat-menu>
        }
      </app-page-header>

      <div class="content">
        <div class="meta-row">
          <span class="badge status-{{ a.status }}">{{ statusLabel(a) }}</span>
          <span class="badge priority-{{ a.priority }}">{{ priorityLabel(a) }}</span>
          <span class="date">{{ a.publishAt ?? a.createdAt | date: 'medium' }}</span>
          @if (a.expiryAt) { <span class="date">Expires {{ a.expiryAt | date: 'medium' }}</span> }
        </div>

        @if (a.attachmentUrl) {
          <img [src]="a.attachmentUrl | assetUrl" class="attachment" alt="Attachment" />
        }

        <p class="description">{{ a.description }}</p>
      </div>
    } @else if (loading()) {
      <p>Loading&hellip;</p>
    }
    </div>
  `,
  styles: [`
    .content { max-width: 720px; }
    .meta-row { display: flex; align-items: center; gap: 10px; margin-bottom: 16px; flex-wrap: wrap; }
    .badge { font-size: 12px; padding: 3px 10px; border-radius: 12px; background: #eee; color: #555; }
    .badge.priority-3 { background: #fff3e0; color: #b25f00; }
    .badge.priority-4 { background: #fdecea; color: #c0392b; }
    .badge.status-1 { background: #f0f0f0; color: #888; }
    .badge.status-2 { background: #fff3e0; color: #b26a00; }
    .badge.status-3 { background: #e8f5e9; color: #2e7d32; }
    .badge.status-4 { background: #f0f0f0; color: #999; }
    .date { font-size: 12px; color: var(--app-text-muted); }
    .attachment { max-width: 100%; border-radius: 10px; margin-bottom: 16px; display: block; }
    .description { white-space: pre-wrap; line-height: 1.6; font-size: 14px; }
    .danger { color: #c0392b; }
  `]
})
export class AnnouncementDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  readonly router = inject(Router);
  private readonly announcementService = inject(AnnouncementService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly announcement = signal<AnnouncementDto | null>(null);

  canManage(): boolean {
    return this.auth.hasPermission('notices.manage');
  }

  typeLabel(a: AnnouncementDto): string { return ANNOUNCEMENT_TYPE_LABELS[a.type]; }
  priorityLabel(a: AnnouncementDto): string { return ANNOUNCEMENT_PRIORITY_LABELS[a.priority]; }
  statusLabel(a: AnnouncementDto): string { return ANNOUNCEMENT_STATUS_LABELS[a.status]; }

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.announcementService.getById(id).subscribe((a) => {
      this.announcement.set(a);
      this.loading.set(false);

      if (!this.canManage() && !a.isRead) {
        this.announcementService.markRead(id).subscribe(() => {
          this.announcement.set({ ...a, isRead: true });
        });
      }
    });
  }

  edit(a: AnnouncementDto): void {
    const ref = this.dialog.open(AnnouncementFormDialogComponent, {
      width: '640px', data: { societyId: a.societyId, announcement: a }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.announcementService.update(a.id, result).subscribe(() => {
        this.toast.success('Announcement updated.');
        this.announcementService.getById(a.id).subscribe((updated) => this.announcement.set(updated));
      });
    });
  }

  publish(a: AnnouncementDto): void {
    this.announcementService.publish(a.id).subscribe(() => {
      this.toast.success('Announcement published.');
      this.announcementService.getById(a.id).subscribe((updated) => this.announcement.set(updated));
    });
  }

  remove(a: AnnouncementDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Announcement', destructive: true,
      message: `Delete "${a.title}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.announcementService.delete(a.id).subscribe(() => {
        this.toast.success('Announcement deleted.');
        this.router.navigate(['/announcements']);
      });
    });
  }
}
