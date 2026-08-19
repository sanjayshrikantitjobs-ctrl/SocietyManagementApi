import { CommonModule } from '@angular/common';
import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MemberDashboardSummary } from '../../../core/models/dashboard.model';
import { SignalrService } from '../../../core/services/signalr.service';
import { ToastService } from '../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { VisitorVisitDto } from '../../visitors/models/visitor.model';
import { VisitorService } from '../../visitors/services/visitor.service';
import { VisitorApprovalCardComponent } from '../../visitors/visitor-approval-card.component';
import { DashboardService } from '../dashboard.service';

@Component({
  selector: 'app-member-dashboard',
  standalone: true,
  imports: [
    CommonModule, RouterLink, MatButtonModule, PageHeaderComponent, StatCardComponent,
    SkeletonLoaderComponent, VisitorApprovalCardComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="My Dashboard" subtitle="Your maintenance, notices and events at a glance"
                        [breadcrumbs]="[{ label: 'Dashboard' }]" />

      @if (loading()) {
        <app-skeleton-loader [rows]="4" [height]="90" />
      } @else if (summary(); as s) {
        <div class="stats-grid">
          <app-stat-card label="My Maintenance Due" [value]="'₹' + (s.myMaintenanceDue | number)" icon="payments" />
          <app-stat-card label="Unread Notices" value="Coming soon" subtext="Notice Board module" icon="campaign" />
          <app-stat-card label="Upcoming Events" [value]="s.upcomingEventsCount" icon="event" iconColor="#16a34a" iconBg="#ecfdf5" />
          <app-stat-card label="Open Complaints" value="Coming soon" subtext="Complaint Management module" icon="support_agent" />
        </div>

        @if (pendingVisitors().length > 0) {
          <h3>Pending Visitor Approvals</h3>
          <div class="pending-grid">
            @for (visit of pendingVisitors(); track visit.id) {
              <app-visitor-approval-card [visit]="visit" (approve)="approveVisitor($event)" (reject)="rejectVisitor($event)" />
            }
          </div>
        }

        <div class="app-card notice-card">
          <p>View your maintenance bills, download invoices and check payment history on
          <a routerLink="/my-bills">My Bills</a>. Festival contributions and complaint history will appear here as those modules ship.</p>
        </div>
      }
    </div>
  `,
  styles: [`
    .stats-grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(220px,1fr)); gap:16px; margin-bottom:24px; }
    h3 { margin: 0 0 12px; font-size: 15px; }
    .pending-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; margin-bottom: 24px; }
    .notice-card { padding:20px; color: var(--app-text-muted); font-size:14px; }
  `]
})
export class MemberDashboardComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly visitorService = inject(VisitorService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly signalr = inject(SignalrService);

  readonly loading = signal(true);
  readonly summary = signal<MemberDashboardSummary | null>(null);
  readonly pendingVisitors = signal<VisitorVisitDto[]>([]);

  constructor() {
    effect(() => {
      if (this.signalr.notifications().length > 0) {
        this.loadPendingVisitors();
      }
    });
  }

  ngOnInit(): void {
    this.dashboardService.getMemberSummary().subscribe((summary) => {
      this.summary.set(summary);
      this.loading.set(false);
    });
    this.loadPendingVisitors();
  }

  private loadPendingVisitors(): void {
    this.visitorService.getPendingApprovals().subscribe((rows) => this.pendingVisitors.set(rows));
  }

  approveVisitor(id: number): void {
    this.visitorService.approveVisit(id).subscribe(() => {
      this.toast.success('Visitor approved.');
      this.loadPendingVisitors();
    });
  }

  rejectVisitor(id: number): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '360px',
      data: { title: 'Reject Visitor', submitLabel: 'Reject', fields: [{ key: 'reason', label: 'Reason (optional)', type: 'text' as const, required: false, defaultValue: '' }] }
    });
    ref.afterClosed().subscribe((result) => {
      if (result === undefined) return;
      this.visitorService.rejectVisit(id, result.reason || undefined).subscribe(() => {
        this.toast.success('Visitor rejected.');
        this.loadPendingVisitors();
      });
    });
  }
}
