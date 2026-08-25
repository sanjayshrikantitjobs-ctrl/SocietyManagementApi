import { CommonModule } from '@angular/common';
import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../core/services/auth.service';
import { SignalrService } from '../../core/services/signalr.service';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatCardComponent } from '../../shared/components/stat-card/stat-card.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { SocietyService } from '../society-setup/services/society.service';
import { AssetUrlPipe } from '../../shared/pipes/asset-url.pipe';
import { VISIT_STATUS_LABELS, VisitorVisitDto } from './models/visitor.model';
import { VisitorService } from './services/visitor.service';
import { VisitorApprovalCardComponent } from './visitor-approval-card.component';
import { VisitorVisitDetailDialogComponent } from './visitor-visit-detail-dialog.component';

/** Role-aware landing: Watchman sees the security dashboard (counts + New
 * Visitor CTA + recent list); Member sees their pending approvals +
 * recent visitors; Admin sees both plus links into Gates/Purposes. */
@Component({
  selector: 'app-visitors-landing',
  standalone: true,
  imports: [
    CommonModule, RouterLink, MatButtonModule, MatIconModule, MatTableModule, AssetUrlPipe,
    PageHeaderComponent, StatCardComponent, SkeletonLoaderComponent, EmptyStateComponent, VisitorApprovalCardComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Visitors" subtitle="Gate security, visitor approvals, and visitor history."
        [breadcrumbs]="[{ label: 'Visitors' }]">
        @if (isWatchman() || auth.isAdmin()) {
          <button mat-flat-button color="primary" routerLink="/visitors/new"><mat-icon>person_add</mat-icon> New Visitor</button>
        }
        @if (auth.isAdmin()) {
          <button mat-stroked-button routerLink="/visitors/gates">Gates</button>
          <button mat-stroked-button routerLink="/visitors/purposes">Purposes</button>
        }
      </app-page-header>

      @if (loading()) {
        <app-skeleton-loader [rows]="4" [height]="70" />
      } @else {
        @if (isWatchman() || auth.isAdmin()) {
          <div class="stats-grid">
            <app-stat-card label="Pending Approvals" [value]="pending().length" icon="hourglass_top" iconColor="#b45309" iconBg="#fef3c7" />
            <app-stat-card label="Currently Inside" [value]="currentlyInsideCount()" icon="how_to_reg" iconColor="#16a34a" iconBg="#ecfdf5" />
            <button mat-stroked-button class="inside-link" routerLink="/visitors/currently-inside">
              <mat-icon>logout</mat-icon> Check Out Visitors
            </button>
          </div>
        }

        @if (pending().length > 0) {
          <h3>Pending Approvals</h3>
          <div class="pending-grid">
            @for (visit of pending(); track visit.id) {
              <app-visitor-approval-card [visit]="visit" (approve)="approveVisit($event)" (reject)="rejectVisit($event)" />
            }
          </div>
        } @else if (!isWatchman() && !auth.isAdmin()) {
          <app-empty-state icon="how_to_reg" title="No pending approvals" message="You'll see visitor requests for your flat here." />
        }

        <h3>Recent Visitors</h3>
        @if (recent().length === 0) {
          <app-empty-state icon="badge" title="No visitors yet" />
        } @else {
            <table mat-table [dataSource]="recent()" class="app-card">
              <ng-container matColumnDef="photo">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let v">
                  @if (v.visitorPhotoUrl) {
                    <img [src]="v.visitorPhotoUrl | assetUrl" alt="" class="thumb" />
                  } @else {
                    <div class="thumb thumb-placeholder"><mat-icon>person</mat-icon></div>
                  }
                </td>
              </ng-container>
              <ng-container matColumnDef="visitor">
                <th mat-header-cell *matHeaderCellDef>Visitor</th>
                <td mat-cell *matCellDef="let v">{{ v.visitorName }}</td>
              </ng-container>
              <ng-container matColumnDef="flat">
                <th mat-header-cell *matHeaderCellDef>Flat</th>
                <td mat-cell *matCellDef="let v">{{ v.flatNumber }}</td>
              </ng-container>
              <ng-container matColumnDef="purpose">
                <th mat-header-cell *matHeaderCellDef>Purpose</th>
                <td mat-cell *matCellDef="let v">{{ v.purposeName }}</td>
              </ng-container>
              <ng-container matColumnDef="time">
                <th mat-header-cell *matHeaderCellDef>Time</th>
                <td mat-cell *matCellDef="let v">{{ v.requestedAt | date: 'short' }}</td>
              </ng-container>
              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>Status</th>
                <td mat-cell *matCellDef="let v">{{ statusLabels[v.status] }}</td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: displayedColumns;" class="clickable-row" (click)="openDetail(row)"></tr>
            </table>
          }
        }
      }
    </div>
  `,
  styles: [`
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; margin-bottom: 24px; align-items: stretch; }
    .inside-link { height: auto; }
    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background: var(--app-primary-light); }
    h3 { margin: 24px 0 12px; font-size: 15px; }
    .pending-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; margin-bottom: 8px; }
    table { width: 100%; }
    .thumb { width: 36px; height: 36px; border-radius: 50%; object-fit: cover; }
    .thumb-placeholder { display: flex; align-items: center; justify-content: center; background: var(--app-primary-light); color: var(--app-primary); }
    .thumb-placeholder mat-icon { font-size: 18px; width: 18px; height: 18px; }
  `]
})
export class VisitorsLandingComponent implements OnInit {
  private readonly visitorService = inject(VisitorService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  private readonly signalr = inject(SignalrService);
  readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly pending = signal<VisitorVisitDto[]>([]);
  readonly recent = signal<VisitorVisitDto[]>([]);
  readonly currentlyInsideCount = signal(0);
  readonly statusLabels: Record<number, string> = VISIT_STATUS_LABELS;
  readonly displayedColumns = ['photo', 'visitor', 'flat', 'purpose', 'time', 'status'];

  private societyId = 0;

  constructor() {
    // Live-refresh when a new request comes in or is resolved elsewhere.
    effect(() => {
      const notifications = this.signalr.notifications();
      if (notifications.length > 0 && this.societyId > 0) {
        this.loadPending();
      }
    });
  }

  isWatchman(): boolean {
    return this.auth.roleName() === 'Watchman';
  }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  private load(): void {
    this.loading.set(true);
    this.loadPending();

    if (this.isWatchman() || this.auth.isAdmin()) {
      this.visitorService.getCurrentlyInside(this.societyId).subscribe((rows) => this.currentlyInsideCount.set(rows.length));
      this.visitorService.getVisits({ societyId: this.societyId, pageNumber: 1, pageSize: 10 }).subscribe((result) => {
        this.recent.set(result.items);
        this.loading.set(false);
      });
    } else {
      this.visitorService.getMyVisits(1, 10).subscribe((result) => {
        this.recent.set(result.items);
        this.loading.set(false);
      });
    }
  }

  private loadPending(): void {
    this.visitorService.getPendingApprovals().subscribe((rows) => this.pending.set(rows));
  }

  approveVisit(id: number): void {
    this.visitorService.approveVisit(id).subscribe(() => {
      this.toast.success('Visitor approved.');
      this.loadPending();
    });
  }

  rejectVisit(id: number): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '360px',
      data: { title: 'Reject Visitor', submitLabel: 'Reject', fields: [{ key: 'reason', label: 'Reason (optional)', type: 'text' as const, required: false, defaultValue: '' }] }
    });
    ref.afterClosed().subscribe((result) => {
      if (result === undefined) return;
      this.visitorService.rejectVisit(id, result.reason || undefined).subscribe(() => {
        this.toast.success('Visitor rejected.');
        this.loadPending();
      });
    });
  }

  openDetail(visit: VisitorVisitDto): void {
    this.dialog.open(VisitorVisitDetailDialogComponent, { data: visit });
  }
}
