import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
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
import { FestivalBudgetTabComponent } from './tabs/festival-budget-tab.component';
import { FestivalContributionsTabComponent } from './tabs/festival-contributions-tab.component';
import { FestivalDashboardTabComponent } from './tabs/festival-dashboard-tab.component';
import { FestivalExpensesTabComponent } from './tabs/festival-expenses-tab.component';
import { FestivalSponsorsTabComponent } from './tabs/festival-sponsors-tab.component';
import { FestivalVendorsTabComponent } from './tabs/festival-vendors-tab.component';
import { FestivalFormDialogComponent } from './festival-form-dialog.component';
import { Festival, FESTIVAL_STATUS_LABELS, FestivalStatus } from './models/festival.model';
import { FestivalService } from './services/festival.service';

@Component({
  selector: 'app-festival-detail',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatIconModule, MatMenuModule, MatProgressSpinnerModule, MatTabsModule,
    PageHeaderComponent, FestivalDashboardTabComponent, FestivalBudgetTabComponent, FestivalContributionsTabComponent,
    FestivalSponsorsTabComponent, FestivalExpensesTabComponent, FestivalVendorsTabComponent
  ],
  template: `
    @if (loading()) {
      <div class="loading"><mat-spinner diameter="36" /></div>
    } @else if (festival(); as f) {
      <app-page-header [title]="f.name" [breadcrumbs]="[{ label: 'Festivals', link: '/festivals' }, { label: f.name }]">
        <span class="status-badge" [class]="'status-' + f.status">{{ statusLabel(f.status) }}</span>
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

      <mat-tab-group animationDuration="150ms" preserveContent>
        <mat-tab label="Dashboard"><app-festival-dashboard-tab [festivalId]="f.id" /></mat-tab>
        <mat-tab label="Budget"><app-festival-budget-tab [festivalId]="f.id" [canManage]="canManage()" /></mat-tab>
        <mat-tab label="Contributions"><app-festival-contributions-tab [festivalId]="f.id" /></mat-tab>
        <mat-tab label="Sponsors"><app-festival-sponsors-tab [festivalId]="f.id" [canManage]="canManage()" /></mat-tab>
        <mat-tab label="Expenses"><app-festival-expenses-tab [festivalId]="f.id" [societyId]="f.societyId" [canManage]="canManage()" [canApprove]="canApprove()" /></mat-tab>
        <mat-tab label="Vendors"><app-festival-vendors-tab [societyId]="f.societyId" [canManage]="canManage()" /></mat-tab>
      </mat-tab-group>
    }
  `,
  styles: [`
    .loading { display: flex; justify-content: center; padding: 60px; }
    .status-badge { padding: 4px 12px; border-radius: 12px; font-size: 12px; font-weight: 700; margin-right: 8px; }
    .status-badge.status-1 { background: #fef3c7; color: #b45309; }
    .status-badge.status-2 { background: #dcfce7; color: #15803d; }
    .status-badge.status-3 { background: #e2e8f0; color: #475569; }
    ::ng-deep .danger { color: var(--app-danger); }
  `]
})
export class FestivalDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly festival = signal<Festival | null>(null);

  canManage(): boolean {
    return this.auth.hasPermission('festivals.manage');
  }

  canApprove(): boolean {
    return this.auth.hasPermission('festivals.expense.approve');
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loading.set(true);
    this.festivalService.getFestival(id).subscribe((festival) => {
      this.festival.set(festival);
      this.loading.set(false);
    });
  }

  statusLabel(status: FestivalStatus): string {
    return FESTIVAL_STATUS_LABELS[status];
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
