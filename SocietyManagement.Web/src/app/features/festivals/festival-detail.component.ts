import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { AssetUrlPipe } from '../../shared/pipes/asset-url.pipe';
import { FestivalBudgetTabComponent } from './tabs/festival-budget-tab.component';
import { FestivalChildFestivalsTabComponent } from './tabs/festival-child-festivals-tab.component';
import { FestivalContributionLedgerTabComponent } from './tabs/festival-contribution-ledger-tab.component';
import { FestivalContributionTargetsTabComponent } from './tabs/festival-contribution-targets-tab.component';
import { FestivalDashboardTabComponent } from './tabs/festival-dashboard-tab.component';
import { FestivalExpensesTabComponent } from './tabs/festival-expenses-tab.component';
import { FestivalSponsorsTabComponent } from './tabs/festival-sponsors-tab.component';
import { FestivalVendorsTabComponent } from './tabs/festival-vendors-tab.component';
import { FestivalFormDialogComponent } from './festival-form-dialog.component';
import {
  ChildPoolStatusDto, Festival, FESTIVAL_KIND_LABELS, FESTIVAL_STATUS_LABELS, FestivalKind, FestivalStatus, PoolSummaryDto
} from './models/festival.model';
import { FestivalService } from './services/festival.service';

@Component({
  selector: 'app-festival-detail',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatIconModule, MatMenuModule, MatProgressSpinnerModule, MatTabsModule, AssetUrlPipe,
    PageHeaderComponent, FestivalDashboardTabComponent, FestivalBudgetTabComponent, FestivalContributionTargetsTabComponent,
    FestivalContributionLedgerTabComponent, FestivalSponsorsTabComponent, FestivalExpensesTabComponent,
    FestivalVendorsTabComponent, FestivalChildFestivalsTabComponent
  ],
  template: `
    @if (loading()) {
      <div class="loading"><mat-spinner diameter="36" /></div>
    } @else if (festival(); as f) {
      <div class="app-page">
        @if (heroImage(f); as hero) {
          <div class="hero" [style.backgroundImage]="'url(' + (hero | assetUrl) + ')'"></div>
        }
        <app-page-header [title]="f.name" [breadcrumbs]="[{ label: 'Festivals', link: '/festivals' }, { label: f.name }]">
          <span class="status-badge" [class]="'status-' + f.status">{{ statusLabel(f.status) }}</span>
          @if (f.kind !== 1) {
            <span class="kind-badge">{{ kindLabel(f.kind) }}@if (f.contributionPoolFestivalName) { : {{ f.contributionPoolFestivalName }} }</span>
          }
          @if (canManage()) {
            <button mat-stroked-button (click)="editFestival(f)"><mat-icon>edit</mat-icon> Edit</button>
            <button mat-icon-button [matMenuTriggerFor]="statusMenu"><mat-icon>more_vert</mat-icon></button>
            <mat-menu #statusMenu="matMenu">
              <button mat-menu-item (click)="setStatus(1)">Mark Planning</button>
              <button mat-menu-item (click)="setStatus(2)">Mark Ongoing</button>
              <button mat-menu-item (click)="setStatus(3)">Close Festival (Completed)</button>
              <button mat-menu-item class="danger" (click)="deleteFestival(f)">Delete</button>
            </mat-menu>
          }
        </app-page-header>

        @if (childPoolStatus(); as pool) {
          <div class="app-card pool-card">
            <mat-icon>savings</mat-icon>
            <span>Funded by <strong>{{ pool.poolFestivalName }}</strong> — <strong>₹{{ pool.poolRemaining | number }}</strong> remaining in the shared pool.</span>
          </div>
        }
        @if (poolSummary(); as summary) {
          <div class="app-card pool-card">
            <mat-icon>savings</mat-icon>
            <span>Contribution Pool — <strong>₹{{ summary.poolCollected | number }}</strong> collected, <strong>₹{{ summary.poolRemaining | number }}</strong> remaining across {{ summary.children.length }} linked festival(s).</span>
          </div>
        }

        <mat-tab-group animationDuration="150ms" preserveContent>
          @if (f.kind === 2) {
            <mat-tab label="Dashboard"><app-festival-dashboard-tab [festivalId]="f.id" /></mat-tab>
            <mat-tab label="Child Festivals & Events"><app-festival-child-festivals-tab [festivalId]="f.id" [poolName]="f.name" [societyId]="f.societyId" /></mat-tab>
            <mat-tab label="Contribution Targets"><app-festival-contribution-targets-tab [festivalId]="f.id" /></mat-tab>
            <mat-tab label="Contribution Ledger"><app-festival-contribution-ledger-tab [festivalId]="f.id" /></mat-tab>
          } @else if (f.kind === 3) {
            <mat-tab label="Dashboard"><app-festival-dashboard-tab [festivalId]="f.id" /></mat-tab>
            <mat-tab label="Budget"><app-festival-budget-tab [festivalId]="f.id" [canManage]="canManage()" /></mat-tab>
            <mat-tab label="Sponsors"><app-festival-sponsors-tab [festivalId]="f.id" [canManage]="canManage()" /></mat-tab>
            <mat-tab label="Expenses"><app-festival-expenses-tab [festivalId]="f.id" [societyId]="f.societyId" [canManage]="canManage()" [canApprove]="canApprove()" /></mat-tab>
            <mat-tab label="Vendors"><app-festival-vendors-tab [societyId]="f.societyId" [canManage]="canManage()" /></mat-tab>
          } @else {
            <mat-tab label="Dashboard"><app-festival-dashboard-tab [festivalId]="f.id" /></mat-tab>
            <mat-tab label="Budget"><app-festival-budget-tab [festivalId]="f.id" [canManage]="canManage()" /></mat-tab>
            <mat-tab label="Contribution Targets"><app-festival-contribution-targets-tab [festivalId]="f.id" /></mat-tab>
            <mat-tab label="Contribution Ledger"><app-festival-contribution-ledger-tab [festivalId]="f.id" /></mat-tab>
            <mat-tab label="Sponsors"><app-festival-sponsors-tab [festivalId]="f.id" [canManage]="canManage()" /></mat-tab>
            <mat-tab label="Expenses"><app-festival-expenses-tab [festivalId]="f.id" [societyId]="f.societyId" [canManage]="canManage()" [canApprove]="canApprove()" /></mat-tab>
            <mat-tab label="Vendors"><app-festival-vendors-tab [societyId]="f.societyId" [canManage]="canManage()" /></mat-tab>
          }
        </mat-tab-group>
      </div>
    }
  `,
  styles: [`
    .loading { display: flex; justify-content: center; padding: 60px; }
    .hero { height: 200px; border-radius: 12px; background-size: cover; background-position: center; margin-bottom: 16px; }
    .status-badge { padding: 4px 12px; border-radius: 12px; font-size: 12px; font-weight: 700; margin-right: 8px; }
    .status-badge.status-1 { background: #fef3c7; color: #b45309; }
    .status-badge.status-2 { background: #dcfce7; color: #15803d; }
    .status-badge.status-3 { background: #e2e8f0; color: #475569; }
    .kind-badge { padding: 4px 12px; border-radius: 12px; font-size: 12px; font-weight: 700; margin-right: 8px; background: #ede9fe; color: #6d28d9; }
    ::ng-deep .danger { color: var(--app-danger); }
    .pool-card { display: flex; align-items: center; gap: 10px; padding: 14px 20px; margin-bottom: 16px; font-size: 13px; }
    .pool-card mat-icon { color: #6d28d9; }
    .children-card { flex-direction: column; align-items: stretch; }
    .children-card h4 { display: flex; align-items: center; gap: 8px; margin: 0 0 10px; font-size: 14px; }
    .children-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
    .children-list li { display: flex; justify-content: space-between; font-size: 13px; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
  `]
})
export class FestivalDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly festival = signal<Festival | null>(null);
  readonly childPoolStatus = signal<ChildPoolStatusDto | null>(null);
  readonly poolSummary = signal<PoolSummaryDto | null>(null);

  canManage(): boolean {
    return this.auth.hasPermission('festivals.manage');
  }

  canApprove(): boolean {
    return this.auth.hasPermission('festivals.expense.approve');
  }

  private currentId = 0;

  constructor() {
    // Subscribed, not snapshot-only — clicking from one festival's detail
    // page straight into another (e.g. a Pool's Child Festivals tab) hits
    // the same route config with just a different :id, so Angular reuses
    // this component instance instead of recreating it; ngOnInit alone
    // would never fire again and the page would silently keep showing the
    // previous festival. takeUntilDestroyed() needs constructor/field-
    // initializer injection context, hence this lives in the constructor.
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.currentId = Number(params.get('id'));
      this.load();
    });
  }

  load(): void {
    const id = this.currentId;
    this.loading.set(true);
    this.festivalService.getFestival(id).subscribe((festival) => {
      this.festival.set(festival);
      this.loading.set(false);
      this.childPoolStatus.set(null);
      this.poolSummary.set(null);
      if (festival.kind === 3) {
        this.festivalService.getChildPoolStatus(id).subscribe((status) => this.childPoolStatus.set(status));
      } else if (festival.kind === 2) {
        this.festivalService.getPoolSummary(id).subscribe((summary) => this.poolSummary.set(summary));
      }
    });
  }

  statusLabel(status: FestivalStatus): string {
    return FESTIVAL_STATUS_LABELS[status];
  }

  kindLabel(kind: FestivalKind): string {
    return FESTIVAL_KIND_LABELS[kind];
  }

  heroImage(f: Festival): string | null {
    return f.bannerImageUrl || f.coverPhotoUrl || null;
  }

  editFestival(festival: Festival): void {
    const ref = this.dialog.open(FestivalFormDialogComponent, {
      width: '640px', data: { societyId: festival.societyId, festival }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.updateFestival(festival.id, result).subscribe(() => {
        this.toast.success('Festival updated.');
        this.load();
      });
    });
  }

  setStatus(status: FestivalStatus): void {
    const f = this.festival();
    if (!f) return;
    this.festivalService.updateFestivalStatus(f.id, status).subscribe(() => {
      this.toast.success('Festival status updated.');
      this.load();
    });
  }

  deleteFestival(festival: Festival): void {
    this.confirmDialog.confirm({
      title: 'Delete Festival', destructive: true,
      message: `Delete "${festival.name}"? This is only possible if it has no recorded contributions or expenses.`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.festivalService.deleteFestival(festival.id).subscribe(() => {
        this.toast.success('Festival deleted.');
        this.router.navigate(['/festivals']);
      });
    });
  }
}
