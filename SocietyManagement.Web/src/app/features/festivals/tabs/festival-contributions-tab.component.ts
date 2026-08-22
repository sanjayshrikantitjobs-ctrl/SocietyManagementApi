import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { SocietyService } from '../../society-setup/services/society.service';
import {
  FLAT_CONTRIBUTION_STATUS_LABELS, FestivalContributionDto, FlatContributionDto, FlatContributionKpisDto,
  FlatContributionStatus, PAYMENT_METHOD_LABELS, TopContributorDto
} from '../models/festival.model';
import { FestivalService } from '../services/festival.service';

const NO_FLAT_OPTION_VALUE = 0;

@Component({
  selector: 'app-festival-contributions-tab',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatChipsModule, MatFormFieldModule, MatIconModule, MatSelectModule,
    MatTableModule, MatTooltipModule, DataTableComponent, StatCardComponent
  ],
  template: `
    <div class="tab-content">
      <h3>Per-Flat Contribution Targets</h3>
      <div class="stats-grid">
        <app-stat-card label="Target (All Flats)" [value]="'₹' + (kpis()?.totalTargetAmount ?? 0 | number)" icon="flag" />
        <app-stat-card label="Collected Toward Target" [value]="'₹' + (kpis()?.totalPaidAmount ?? 0 | number)" icon="paid" iconColor="#16a34a" iconBg="#ecfdf5" />
        <app-stat-card label="Outstanding" [value]="'₹' + (kpis()?.totalOutstandingAmount ?? 0 | number)" icon="hourglass_empty" iconColor="#dc2626" iconBg="#fef2f2" />
        <app-stat-card label="Flats Fully Paid" [value]="(kpis()?.flatsPaidCount ?? 0) + ' / ' + (kpis()?.totalFlats ?? 0)" icon="task_alt" iconColor="#2563eb" iconBg="#eff6ff" />
      </div>

      <app-data-table
        [loading]="flatsLoading()" [totalCount]="flatsTotalCount()" [pageSize]="flatsPageSize()" [pageIndex]="flatsPageIndex()"
        searchPlaceholder="Search flat number..." emptyTitle="No flats found"
        emptyMessage="Set a contribution target to start tracking who's paid."
        (page)="onFlatsPage($event)" (search)="onFlatsSearch($event)">
        <div toolbar>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="status-filter">
            <mat-select [(ngModel)]="statusFilter" (ngModelChange)="onStatusFilterChange()" placeholder="All statuses">
              <mat-option [value]="null">All statuses</mat-option>
              @for (opt of statusOptions; track opt.value) { <mat-option [value]="opt.value">{{ opt.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <button mat-stroked-button (click)="setTargetsForAllFlats()"><mat-icon>flag</mat-icon> Set Target for All Flats</button>
        </div>
        <table mat-table [dataSource]="flatContributions()" table>
          <ng-container matColumnDef="flat">
            <th mat-header-cell *matHeaderCellDef>Flat</th>
            <td mat-cell *matCellDef="let f"><strong>{{ f.flatNumber }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="target">
            <th mat-header-cell *matHeaderCellDef>Target</th>
            <td mat-cell *matCellDef="let f">₹{{ f.targetAmount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="paid">
            <th mat-header-cell *matHeaderCellDef>Paid</th>
            <td mat-cell *matCellDef="let f">₹{{ f.paidAmount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="outstanding">
            <th mat-header-cell *matHeaderCellDef>Outstanding</th>
            <td mat-cell *matCellDef="let f">₹{{ f.outstandingAmount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let f">
              <mat-chip-set><mat-chip [class]="'status-' + f.status">{{ statusLabel(f.status) }}</mat-chip></mat-chip-set>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let f">
              <button mat-icon-button (click)="editFlatTarget(f)" matTooltip="Edit target"><mat-icon>edit</mat-icon></button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="flatColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: flatColumns;"></tr>
        </table>
      </app-data-table>

      <div class="layout">
        <div class="main">
          <div class="toolbar">
            <h3>Contribution Ledger</h3>
            <button mat-flat-button color="primary" (click)="addContribution()"><mat-icon>volunteer_activism</mat-icon> Record Contribution</button>
          </div>
          <app-data-table
            [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
            searchPlaceholder="Search donor or receipt no..." emptyTitle="No contributions yet"
            emptyMessage="Record the first contribution to this festival."
            (page)="onPage($event)" (search)="onSearch($event)">
            <table mat-table [dataSource]="contributions()" table>
              <ng-container matColumnDef="donor">
                <th mat-header-cell *matHeaderCellDef>Donor</th>
                <td mat-cell *matCellDef="let c">
                  <strong>{{ c.memberName }}</strong>@if (c.flatNumber) { <span class="muted"> · {{ c.flatNumber }}</span> }
                </td>
              </ng-container>
              <ng-container matColumnDef="amount">
                <th mat-header-cell *matHeaderCellDef>Amount</th>
                <td mat-cell *matCellDef="let c">₹{{ c.amount | number }}</td>
              </ng-container>
              <ng-container matColumnDef="method">
                <th mat-header-cell *matHeaderCellDef>Method</th>
                <td mat-cell *matCellDef="let c"><mat-chip-set><mat-chip>{{ methodLabel(c.paymentMethod) }}</mat-chip></mat-chip-set></td>
              </ng-container>
              <ng-container matColumnDef="date">
                <th mat-header-cell *matHeaderCellDef>Date</th>
                <td mat-cell *matCellDef="let c">{{ c.paymentDate | date: 'mediumDate' }}</td>
              </ng-container>
              <ng-container matColumnDef="receipt">
                <th mat-header-cell *matHeaderCellDef>Receipt</th>
                <td mat-cell *matCellDef="let c">
                  {{ c.receiptNumber }}
                  <button mat-icon-button (click)="downloadReceipt(c)" matTooltip="Download PDF receipt"><mat-icon>download</mat-icon></button>
                </td>
              </ng-container>

              <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
            </table>
          </app-data-table>
        </div>

        <div class="sidebar app-card">
          <h3>Top Contributors</h3>
          @if (topContributors().length === 0) {
            <p class="muted">No contributions yet.</p>
          } @else {
            <ol class="top-list">
              @for (t of topContributors(); track t.memberName) {
                <li>
                  <span class="name">{{ t.memberName }}@if (t.flatNumber) { <span class="muted"> · {{ t.flatNumber }}</span> }</span>
                  <span class="amount">₹{{ t.totalAmount | number }}</span>
                </li>
              }
            </ol>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .tab-content h3 { margin: 0 0 12px; font-size: 15px; }
    .stats-grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(220px,1fr)); gap:16px; margin-bottom:24px; }
    .layout { display: grid; grid-template-columns: 1fr 280px; gap: 16px; align-items: start; margin-top: 32px; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
    .toolbar h3 { margin: 0; font-size: 15px; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .sidebar { padding: 16px; }
    .sidebar h3 { margin: 0 0 12px; font-size: 14px; }
    .top-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 10px; }
    .top-list li { display: flex; justify-content: space-between; font-size: 13px; }
    .top-list .amount { font-weight: 700; color: #15803d; }
    .status-filter { width: 180px; }
    div[toolbar] { display: flex; align-items: center; gap: 12px; }
    .status-0 { background: #f1f5f9 !important; }
    .status-1 { background: #fef2f2 !important; color: #b91c1c !important; }
    .status-2 { background: #fffbeb !important; color: #b45309 !important; }
    .status-3 { background: #ecfdf5 !important; color: #15803d !important; }
    @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } }
  `]
})
export class FestivalContributionsTabComponent implements OnInit {
  festivalId = input.required<number>();

  private readonly festivalService = inject(FestivalService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly contributions = signal<FestivalContributionDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly topContributors = signal<TopContributorDto[]>([]);
  readonly displayedColumns = ['donor', 'amount', 'method', 'date', 'receipt'];

  readonly flatsLoading = signal(true);
  readonly flatContributions = signal<FlatContributionDto[]>([]);
  readonly flatsTotalCount = signal(0);
  readonly flatsPageIndex = signal(0);
  readonly flatsPageSize = signal(10);
  readonly flatsSearchTerm = signal('');
  readonly kpis = signal<FlatContributionKpisDto | null>(null);
  readonly flatColumns = ['flat', 'target', 'paid', 'outstanding', 'status', 'actions'];
  statusFilter: FlatContributionStatus | null = null;
  readonly statusOptions = [
    { value: 1 as FlatContributionStatus, label: 'Pending' },
    { value: 2 as FlatContributionStatus, label: 'Partially Paid' },
    { value: 3 as FlatContributionStatus, label: 'Paid' },
    { value: 0 as FlatContributionStatus, label: 'No Target' }
  ];

  private flatOptions: { value: number; label: string }[] = [];

  methodLabel(method: number): string {
    return PAYMENT_METHOD_LABELS[method as 1 | 2 | 3];
  }

  statusLabel(status: number): string {
    return FLAT_CONTRIBUTION_STATUS_LABELS[status as FlatContributionStatus];
  }

  ngOnInit(): void {
    this.load();
    this.loadTopContributors();
    this.loadFlatContributions();
    this.loadKpis();
  }

  // ---- Contribution ledger ----------------------------------------------------
  load(): void {
    this.loading.set(true);
    this.festivalService.getContributions({
      festivalId: this.festivalId(), search: this.searchTerm() || undefined,
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.contributions.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  loadTopContributors(): void {
    this.festivalService.getTopContributors(this.festivalId()).subscribe((data) => this.topContributors.set(data));
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.pageIndex.set(0);
    this.load();
  }

  private ensureFlatOptionsLoaded(callback: () => void): void {
    if (this.flatOptions.length > 0) { callback(); return; }
    this.societyService.getFlats({ pageSize: 500 }).subscribe((result) => {
      this.flatOptions = result.items.map((f) => ({
        value: f.id, label: f.ownerName ? `${f.flatNumber} — ${f.ownerName}` : f.flatNumber
      }));
      callback();
    });
  }

  addContribution(): void {
    this.ensureFlatOptionsLoaded(() => {
      const ref = this.dialog.open(PromptDialogComponent, {
        width: '460px',
        data: {
          title: 'Record Contribution',
          submitLabel: 'Save',
          fields: [
            {
              key: 'flatId', label: 'Flat (optional — leave as Guest for anonymous/outside donors)', type: 'select',
              options: [{ value: NO_FLAT_OPTION_VALUE, label: '— Guest / No Flat —' }, ...this.flatOptions],
              defaultValue: NO_FLAT_OPTION_VALUE
            },
            { key: 'memberName', label: 'Donor Name', type: 'text' },
            { key: 'amount', label: 'Amount', type: 'number' },
            {
              key: 'paymentMethod', label: 'Payment Method', type: 'select',
              options: [{ value: 1, label: 'Cash' }, { value: 2, label: 'UPI' }, { value: 3, label: 'Bank Transfer' }]
            },
            { key: 'paymentDate', label: 'Payment Date', type: 'date' },
            { key: 'transactionId', label: 'Transaction ID', type: 'text', required: false },
            { key: 'isAnonymous', label: 'Keep donor anonymous on public displays', type: 'checkbox' }
          ]
        }
      });
      ref.afterClosed().subscribe((result) => {
        if (!result) return;
        const flatId = Number(result.flatId);
        this.festivalService.createContribution({
          festivalId: this.festivalId(), flatId: flatId === NO_FLAT_OPTION_VALUE ? null : flatId,
          memberName: result.memberName, amount: Number(result.amount),
          paymentMethod: Number(result.paymentMethod), paymentDate: result.paymentDate,
          transactionId: result.transactionId, isAnonymous: !!result.isAnonymous
        }).subscribe(() => {
          this.toast.success('Contribution recorded.');
          this.load();
          this.loadTopContributors();
          this.loadFlatContributions();
          this.loadKpis();
        });
      });
    });
  }

  downloadReceipt(contribution: FestivalContributionDto): void {
    this.festivalService.downloadReceipt(contribution.id).subscribe((blob) => {
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `receipt-${contribution.receiptNumber}.pdf`;
      link.click();
      window.URL.revokeObjectURL(url);
    });
  }

  // ---- Per-flat targets --------------------------------------------------------
  loadFlatContributions(): void {
    this.flatsLoading.set(true);
    this.festivalService.getFlatContributions({
      festivalId: this.festivalId(), search: this.flatsSearchTerm() || undefined,
      status: this.statusFilter ?? undefined, pageNumber: this.flatsPageIndex() + 1, pageSize: this.flatsPageSize()
    }).subscribe((result) => {
      this.flatContributions.set(result.items);
      this.flatsTotalCount.set(result.totalCount);
      this.flatsLoading.set(false);
    });
  }

  loadKpis(): void {
    this.festivalService.getFlatContributionKpis(this.festivalId()).subscribe((data) => this.kpis.set(data));
  }

  onFlatsPage(event: PageEvent): void {
    this.flatsPageIndex.set(event.pageIndex);
    this.flatsPageSize.set(event.pageSize);
    this.loadFlatContributions();
  }

  onFlatsSearch(term: string): void {
    this.flatsSearchTerm.set(term);
    this.flatsPageIndex.set(0);
    this.loadFlatContributions();
  }

  onStatusFilterChange(): void {
    this.flatsPageIndex.set(0);
    this.loadFlatContributions();
  }

  setTargetsForAllFlats(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: 'Set Target for All Flats',
        submitLabel: 'Apply',
        fields: [{ key: 'targetAmount', label: 'Annual Target per Flat (₹)', type: 'number', defaultValue: 0 }]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.setContributionTargets(this.festivalId(), Number(result.targetAmount)).subscribe((count) => {
        this.toast.success(`Target set for ${count} flat(s).`);
        this.loadFlatContributions();
        this.loadKpis();
      });
    });
  }

  editFlatTarget(flat: FlatContributionDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: `Edit Target — ${flat.flatNumber}`,
        submitLabel: 'Save',
        fields: [{ key: 'targetAmount', label: 'Target Amount (₹)', type: 'number', defaultValue: flat.targetAmount }]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.updateFlatContributionTarget(this.festivalId(), flat.flatId, Number(result.targetAmount)).subscribe(() => {
        this.toast.success('Target updated.');
        this.loadFlatContributions();
        this.loadKpis();
      });
    });
  }
}
