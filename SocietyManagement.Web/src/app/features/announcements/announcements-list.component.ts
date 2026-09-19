import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { FilterBarComponent, FilterBarOption } from '../../shared/components/filter-bar/filter-bar.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { Society } from '../../core/models/society.model';
import { SocietyService } from '../society-setup/services/society.service';
import { AnnouncementFormDialogComponent } from './announcement-form-dialog.component';
import {
  ANNOUNCEMENT_PRIORITY_LABELS, ANNOUNCEMENT_STATUS_LABELS, ANNOUNCEMENT_TYPE_LABELS,
  AnnouncementDto, AnnouncementStatus, AnnouncementType
} from './models/announcement.model';
import { AnnouncementService } from './services/announcement.service';

@Component({
  selector: 'app-announcements-list',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatMenuModule, MatSelectModule,
    EmptyStateComponent, FilterBarComponent, PageHeaderComponent, SkeletonLoaderComponent, StatusBadgeComponent
  ],
  template: `
    <div class="app-page">
    <app-page-header title="Announcements" subtitle="Society-wide notices, festival reminders and important updates.">
      @if (societies().length > 1) {
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="society-picker">
          <mat-select [value]="societyId()" (selectionChange)="onSocietyChange($event.value)">
            @for (s of societies(); track s.id) { <mat-option [value]="s.id">{{ s.name }}</mat-option> }
          </mat-select>
        </mat-form-field>
      }
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="type-filter">
        <mat-label>Type</mat-label>
        <mat-select [value]="typeFilter()" (selectionChange)="onTypeFilterChange($event.value)">
          <mat-option [value]="null">All Types</mat-option>
          @for (t of typeOptions; track t.value) { <mat-option [value]="t.value">{{ t.label }}</mat-option> }
        </mat-select>
      </mat-form-field>
      @if (canManage()) {
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="status-filter">
          <mat-label>Status</mat-label>
          <mat-select [value]="statusFilter()" (selectionChange)="onStatusFilterChange($event.value)">
            <mat-option [value]="null">All</mat-option>
            @for (s of statusOptions; track s.value) { <mat-option [value]="s.value">{{ s.label }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <button mat-flat-button color="primary" (click)="createAnnouncement()">
          <mat-icon>add</mat-icon> New Announcement
        </button>
      }
    </app-page-header>

    @if (!canManage() && !loading() && announcements().length > 0) {
      <app-filter-bar class="read-filters" [options]="readFilterOptions()" [selected]="readFilter()"
        (selectedChange)="onReadFilterChange($event)" />
    }

    @if (loading()) {
      <app-skeleton-loader [rows]="4" />
    } @else if (announcements().length === 0) {
      <app-empty-state icon="campaign" title="No announcements yet"
        message="Society notices, festival reminders and important updates will show up here."
        [actionLabel]="canManage() ? 'New Announcement' : null" (action)="createAnnouncement()" />
    } @else if (visibleAnnouncements().length === 0) {
      <app-empty-state icon="campaign" title="Nothing here"
        [message]="readFilter() === 'unread' ? 'You\\'re all caught up.' : 'You haven\\'t saved any announcements yet.'" />
    } @else {
      <div class="list">
        @for (a of visibleAnnouncements(); track a.id) {
          <div class="card" [class.unread]="!canManage() && !a.isRead" (click)="openAnnouncement(a)">
            <div class="card-main">
              <div class="card-title-row">
                @if (!canManage() && !a.isRead) { <span class="unread-dot"></span> }
                <span class="title">{{ a.title }}</span>
                <app-status-badge [variant]="priorityVariant(a)" [label]="priorityLabel(a)" />
              </div>
              <div class="meta">
                <span class="type">{{ typeLabel(a) }}</span>
                <span class="dot">&middot;</span>
                <span>{{ a.publishAt ?? a.createdAt | date: 'mediumDate' }}</span>
                @if (canManage()) {
                  <span class="dot">&middot;</span>
                  <app-status-badge [variant]="statusVariant(a)" [label]="statusLabel(a)" />
                }
              </div>
              <p class="excerpt">{{ a.description }}</p>
            </div>
            @if (!canManage()) {
              <button mat-icon-button class="save-btn" [class.saved]="a.isSaved"
                [attr.aria-label]="a.isSaved ? 'Remove from saved' : 'Save for later'"
                (click)="$event.stopPropagation(); toggleSaved(a)">
                <mat-icon>{{ a.isSaved ? 'bookmark' : 'bookmark_border' }}</mat-icon>
              </button>
            }
            @if (canManage()) {
              <button mat-icon-button [matMenuTriggerFor]="menu" (click)="$event.stopPropagation()">
                <mat-icon>more_vert</mat-icon>
              </button>
              <mat-menu #menu="matMenu">
                <button mat-menu-item (click)="editAnnouncement(a)">Edit</button>
                @if (a.status === 1 || a.status === 2) {
                  <button mat-menu-item (click)="publishAnnouncement(a)">Publish Now</button>
                }
                <button mat-menu-item class="danger" (click)="deleteAnnouncement(a)">Delete</button>
              </mat-menu>
            }
          </div>
        }
      </div>
    }
    </div>
  `,
  styles: [`
    .society-picker { width: 200px; margin-right: 8px; }
    .status-filter { width: 160px; margin-right: 8px; }
    .type-filter { width: 180px; margin-right: 8px; }
    .read-filters { display: block; margin-bottom: 16px; }
    .list { display: flex; flex-direction: column; gap: 12px; }
    .save-btn mat-icon { color: var(--app-text-muted); }
    .save-btn.saved mat-icon { color: var(--app-primary, #4f6ef7); }
    .card {
      display: flex; align-items: flex-start; gap: 8px; padding: 16px; border-radius: 10px;
      border: 1px solid var(--app-border); background: var(--app-surface); cursor: pointer; transition: box-shadow .15s;
    }
    .card:hover { box-shadow: 0 2px 8px rgba(0,0,0,.06); }
    .card.unread { border-left: 3px solid var(--app-primary, #4f6ef7); background: var(--app-surface-hover, #f6f8ff); }
    .card-main { flex: 1; min-width: 0; }
    .card-title-row { display: flex; align-items: center; gap: 8px; margin-bottom: 4px; }
    .unread-dot { width: 8px; height: 8px; border-radius: 50%; background: var(--app-primary, #4f6ef7); flex-shrink: 0; }
    .title { font-weight: 600; font-size: 15px; }
    .meta { display: flex; align-items: center; gap: 6px; font-size: 12px; color: var(--app-text-muted); margin-bottom: 8px; }
    .excerpt { margin: 0; font-size: 13px; color: var(--app-text-muted); display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
    .danger { color: #c0392b; }
  `]
})
export class AnnouncementsListComponent implements OnInit {
  private readonly announcementService = inject(AnnouncementService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly societies = signal<Society[]>([]);
  readonly societyId = signal(0);
  readonly announcements = signal<AnnouncementDto[]>([]);
  readonly statusFilter = signal<AnnouncementStatus | null>(null);
  readonly typeFilter = signal<AnnouncementType | null>(null);
  readonly readFilter = signal<'all' | 'unread' | 'saved'>('all');

  readonly unreadCount = computed(() => this.announcements().filter((a) => !a.isRead).length);
  readonly visibleAnnouncements = computed(() => {
    const filter = this.readFilter();
    let list = this.announcements();
    if (filter === 'unread') list = list.filter((a) => !a.isRead);
    if (filter === 'saved') list = list.filter((a) => a.isSaved);
    // Admin's Type filter round-trips to the server (see load()); the
    // resident feed is already fully loaded (pageSize 100, same as the
    // Unread/Saved filters above), so it's filtered the same way here.
    if (!this.canManage() && this.typeFilter() !== null) list = list.filter((a) => a.type === this.typeFilter());
    return list;
  });
  readonly readFilterOptions = computed<FilterBarOption[]>(() => [
    { value: 'all', label: 'All' },
    { value: 'unread', label: 'Unread', count: this.unreadCount() },
    { value: 'saved', label: '🔖 Saved' }
  ]);

  onReadFilterChange(value: string): void {
    if (value === 'all' || value === 'unread' || value === 'saved') this.readFilter.set(value);
  }

  readonly statusOptions = Object.entries(ANNOUNCEMENT_STATUS_LABELS).map(([value, label]) => ({ value: Number(value), label }));
  readonly typeOptions = Object.entries(ANNOUNCEMENT_TYPE_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  canManage(): boolean {
    return this.auth.hasPermission('notices.manage');
  }

  typeLabel(a: AnnouncementDto): string {
    return ANNOUNCEMENT_TYPE_LABELS[a.type];
  }
  priorityLabel(a: AnnouncementDto): string {
    return ANNOUNCEMENT_PRIORITY_LABELS[a.priority];
  }
  statusLabel(a: AnnouncementDto): string {
    return ANNOUNCEMENT_STATUS_LABELS[a.status];
  }
  priorityVariant(a: AnnouncementDto): StatusBadgeVariant {
    return ({ 1: 'neutral', 2: 'neutral', 3: 'warning', 4: 'danger' } as Record<number, StatusBadgeVariant>)[a.priority] ?? 'neutral';
  }
  statusVariant(a: AnnouncementDto): StatusBadgeVariant {
    return ({ 1: 'neutral', 2: 'warning', 3: 'success', 4: 'neutral' } as Record<number, StatusBadgeVariant>)[a.status] ?? 'neutral';
  }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      this.societies.set(societies);
      if (societies.length > 0) {
        this.societyId.set(societies[0].id);
        this.load();
      } else {
        this.loading.set(false);
      }
    });
  }

  onSocietyChange(societyId: number): void {
    this.societyId.set(societyId);
    this.load();
  }

  onStatusFilterChange(status: AnnouncementStatus | null): void {
    this.statusFilter.set(status);
    this.load();
  }

  onTypeFilterChange(type: AnnouncementType | null): void {
    this.typeFilter.set(type);
    // Admin's list is server-paginated by type; the resident feed already
    // has everything loaded and just re-runs visibleAnnouncements() below.
    if (this.canManage()) this.load();
  }

  load(): void {
    this.loading.set(true);
    const request$ = this.canManage()
      ? this.announcementService.getAnnouncements({
          societyId: this.societyId(), status: this.statusFilter() ?? undefined, type: this.typeFilter() ?? undefined, pageSize: 100
        })
      : this.announcementService.getPublished({ societyId: this.societyId(), pageSize: 100 });

    request$.subscribe((result) => {
      this.announcements.set(result.items);
      this.loading.set(false);
    });
  }

  openAnnouncement(a: AnnouncementDto): void {
    this.router.navigate(['/announcements', a.id]);
  }

  toggleSaved(a: AnnouncementDto): void {
    this.announcementService.toggleSaved(a.id).subscribe((isSaved) => {
      this.announcements.update((list) => list.map((x) => (x.id === a.id ? { ...x, isSaved } : x)));
    });
  }

  createAnnouncement(): void {
    const ref = this.dialog.open(AnnouncementFormDialogComponent, {
      width: '640px', data: { societyId: this.societyId(), announcement: null }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.announcementService.create(result).subscribe(() => {
        this.toast.success('Announcement saved.');
        this.load();
      });
    });
  }

  editAnnouncement(a: AnnouncementDto): void {
    const ref = this.dialog.open(AnnouncementFormDialogComponent, {
      width: '640px', data: { societyId: this.societyId(), announcement: a }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.announcementService.update(a.id, result).subscribe(() => {
        this.toast.success('Announcement updated.');
        this.load();
      });
    });
  }

  publishAnnouncement(a: AnnouncementDto): void {
    this.announcementService.publish(a.id).subscribe(() => {
      this.toast.success('Announcement published.');
      this.load();
    });
  }

  deleteAnnouncement(a: AnnouncementDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Announcement', destructive: true,
      message: `Delete "${a.title}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.announcementService.delete(a.id).subscribe(() => {
        this.toast.success('Announcement deleted.');
        this.load();
      });
    });
  }
}
