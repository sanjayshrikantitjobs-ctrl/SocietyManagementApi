import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SelectionModel } from '@angular/cdk/collections';
import { concatMap, from, toArray } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import {
  FLAT_CONTRIBUTION_STATUS_LABELS, FestivalContributionDto, FlatContributionDto, FlatContributionKpisDto,
  FlatContributionStatus, PAYMENT_METHOD_LABELS
} from '../models/festival.model';
import { FestivalService } from '../services/festival.service';
import { MOBILE_PATTERN, MOBILE_PATTERN_ERROR } from '../../../shared/validators/mobile.validator';
import { FlatContributionDetailDialogComponent } from './flat-contribution-detail-dialog.component';

const NO_FLAT_OPTION_VALUE = 0;

/** Merged "Contribution" page — replaces the old separate Contribution
 * Targets and Contribution Ledger tabs. Two views over the same toolbar:
 * "By Flat" (the target/paid/outstanding table, click a row for that flat's
 * target + full history) and "All Contributions" (the searchable global
 * ledger). No "Top Contributors" panel — dropped per request. Permission
 * gating added here (canManage/canContribute inputs) — neither source tab
 * had it, a pre-existing gap now fixed to match every other festival tab. */
@Component({
  selector: 'app-festival-contribution-tab',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatButtonToggleModule, MatCheckboxModule, MatChipsModule,
    MatFormFieldModule, MatIconModule, MatSelectModule, MatSortModule, MatTableModule, DataTableComponent, StatCardComponent
  ],
  template: `
    <div class="tab-content">
      <div class="stats-grid">
        <app-stat-card label="Target (All Flats)" [value]="'₹' + (kpis()?.totalTargetAmount ?? 0 | number)" icon="flag" />
        <app-stat-card label="Collected Toward Target" [value]="'₹' + (kpis()?.totalPaidAmount ?? 0 | number)" icon="paid" iconColor="#16a34a" iconBg="#ecfdf5" />
        <app-stat-card label="Outstanding" [value]="'₹' + (kpis()?.totalOutstandingAmount ?? 0 | number)" icon="hourglass_empty" iconColor="#dc2626" iconBg="#fef2f2" />
        <app-stat-card label="Flats Fully Paid" [value]="(kpis()?.flatsPaidCount ?? 0) + ' / ' + (kpis()?.totalFlats ?? 0)" icon="task_alt" iconColor="#2563eb" iconBg="#eff6ff" />
      </div>

      <div class="toolbar">
        <mat-button-toggle-group [value]="view()" (change)="view.set($event.value)">
          <mat-button-toggle value="flats">By Flat</mat-button-toggle>
          <mat-button-toggle value="all">All Contributions</mat-button-toggle>
        </mat-button-toggle-group>
        <div class="actions">
          @if (canManage()) {
            <button mat-stroked-button (click)="setTargetsForAllFlats()"><mat-icon>flag</mat-icon> Set Target for All Flats</button>
          }
          @if (canContribute()) {
            <button mat-flat-button color="primary" (click)="addContribution()"><mat-icon>volunteer_activism</mat-icon> Record Contribution</button>
          }
        </div>
      </div>

      @if (view() === 'flats') {
        @if (canContribute() && flatSelection.selected.length > 0) {
          <div class="bulk-toolbar">
            <span>{{ flatSelection.selected.length }} flat(s) selected</span>
            <button mat-flat-button color="primary" [disabled]="bulkMarkingPaid()" (click)="bulkMarkFlatsPaid()">
              <mat-icon>volunteer_activism</mat-icon> Mark as Paid
            </button>
          </div>
        }
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
          </div>
          <table mat-table [dataSource]="flatContributions()" matSort (matSortChange)="onFlatsSort($event)" table>
            @if (canContribute()) {
              <ng-container matColumnDef="select">
                <th mat-header-cell *matHeaderCellDef>
                  <mat-checkbox (click)="$event.stopPropagation()" (change)="$event ? toggleAllFlats() : null" [checked]="allFlatsSelected()" [indeterminate]="someFlatsSelected()" />
                </th>
                <td mat-cell *matCellDef="let f">
                  @if (f.outstandingAmount > 0) {
                    <mat-checkbox (click)="$event.stopPropagation()" (change)="flatSelection.toggle(f.flatId)" [checked]="flatSelection.isSelected(f.flatId)" />
                  }
                </td>
              </ng-container>
            }
            <ng-container matColumnDef="flat">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Flat</th>
              <td mat-cell *matCellDef="let f"><strong>{{ f.flatNumber }}</strong></td>
            </ng-container>
            <ng-container matColumnDef="target">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Target</th>
              <td mat-cell *matCellDef="let f">₹{{ f.targetAmount | number }}</td>
            </ng-container>
            <ng-container matColumnDef="paid">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Paid</th>
              <td mat-cell *matCellDef="let f">₹{{ f.paidAmount | number }}</td>
            </ng-container>
            <ng-container matColumnDef="outstanding">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Outstanding</th>
              <td mat-cell *matCellDef="let f">₹{{ f.outstandingAmount | number }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
              <td mat-cell *matCellDef="let f">
                <mat-chip-set><mat-chip [class]="'status-' + f.status">{{ statusLabel(f.status) }}</mat-chip></mat-chip-set>
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="flatColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: flatColumns;" class="clickable-row" (click)="openFlatDetail(row)"></tr>
          </table>
        </app-data-table>
      } @else {
        <app-data-table
          [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
          searchPlaceholder="Search donor or receipt no..." emptyTitle="No contributions yet"
          emptyMessage="Record the first contribution to this festival."
          (page)="onPage($event)" (search)="onSearch($event)">
          <table mat-table [dataSource]="contributions()" matSort (matSortChange)="onSort($event)" table>
            <ng-container matColumnDef="donor">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Donor</th>
              <td mat-cell *matCellDef="let c">
                <strong>{{ c.memberName }}</strong>@if (c.flatNumber) { <span class="muted"> · {{ c.flatNumber }}</span> }
              </td>
            </ng-container>
            <ng-container matColumnDef="flat">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Flat</th>
              <td mat-cell *matCellDef="let c">{{ c.flatNumber ?? 'Guest' }}</td>
            </ng-container>
            <ng-container matColumnDef="amount">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Amount</th>
              <td mat-cell *matCellDef="let c">₹{{ c.amount | number }}</td>
            </ng-container>
            <ng-container matColumnDef="method">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Method</th>
              <td mat-cell *matCellDef="let c"><mat-chip-set><mat-chip>{{ methodLabel(c.paymentMethod) }}</mat-chip></mat-chip-set></td>
            </ng-container>
            <ng-container matColumnDef="date">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Date</th>
              <td mat-cell *matCellDef="let c">{{ c.paymentDate | date: 'mediumDate' }}</td>
            </ng-container>
            <ng-container matColumnDef="receipt">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Receipt</th>
              <td mat-cell *matCellDef="let c">
                {{ c.receiptNumber }}
                <button mat-icon-button (click)="downloadReceipt(c)" matTooltip="Download PDF receipt"><mat-icon>download</mat-icon></button>
                @if (canContribute()) {
                  <button mat-icon-button (click)="resendToWhatsApp(c)" matTooltip="Resend receipt to WhatsApp"><mat-icon>chat</mat-icon></button>
                  <button mat-icon-button (click)="editContribution(c)" matTooltip="Edit"><mat-icon>edit</mat-icon></button>
                }
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
          </table>
        </app-data-table>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .stats-grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(220px,1fr)); gap:16px; margin-bottom:20px; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; flex-wrap: wrap; gap: 12px; }
    .toolbar .actions { display: flex; gap: 8px; }
    .bulk-toolbar { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; padding: 10px 14px; background: var(--app-primary-light); border-radius: 8px; }
    .bulk-toolbar span { font-size: 13px; font-weight: 600; color: var(--app-primary); }
    .status-filter { width: 180px; }
    div[toolbar] { display: flex; align-items: center; gap: 12px; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background: var(--app-surface-alt); }
    .status-0 { background: #f1f5f9 !important; }
    .status-1 { background: #fef2f2 !important; color: #b91c1c !important; }
    .status-2 { background: #fffbeb !important; color: #b45309 !important; }
    .status-3 { background: #ecfdf5 !important; color: #15803d !important; }
  `]
})
export class FestivalContributionTabComponent implements OnInit {
  festivalId = input.required<number>();
  canManage = input(false);
  canContribute = input(false);

  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  readonly view = signal<'flats' | 'all'>('flats');
  readonly kpis = signal<FlatContributionKpisDto | null>(null);

  // "By Flat" view state
  readonly flatsLoading = signal(true);
  readonly flatContributions = signal<FlatContributionDto[]>([]);
  readonly flatsTotalCount = signal(0);
  readonly flatsPageIndex = signal(0);
  readonly flatsPageSize = signal(10);
  readonly flatsSearchTerm = signal('');
  readonly flatsSortState = signal<Sort | null>(null);
  readonly flatSelection = new SelectionModel<number>(true, []);
  readonly bulkMarkingPaid = signal(false);
  get flatColumns(): string[] {
    return this.canContribute()
      ? ['select', 'flat', 'target', 'paid', 'outstanding', 'status']
      : ['flat', 'target', 'paid', 'outstanding', 'status'];
  }
  statusFilter: FlatContributionStatus | null = null;
  readonly statusOptions = [
    { value: 1 as FlatContributionStatus, label: 'Pending' },
    { value: 2 as FlatContributionStatus, label: 'Partially Paid' },
    { value: 3 as FlatContributionStatus, label: 'Paid' },
    { value: 0 as FlatContributionStatus, label: 'No Target' }
  ];

  // "All Contributions" view state
  readonly loading = signal(true);
  readonly contributions = signal<FestivalContributionDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  readonly displayedColumns = ['donor', 'flat', 'amount', 'method', 'date', 'receipt'];

  private flatOptions: { value: number; label: string }[] = [];

  statusLabel(status: number): string {
    return FLAT_CONTRIBUTION_STATUS_LABELS[status as FlatContributionStatus];
  }

  methodLabel(method: number): string {
    return PAYMENT_METHOD_LABELS[method as 1 | 2 | 3];
  }

  ngOnInit(): void {
    this.loadFlats();
    this.loadKpis();
    this.load();
  }

  // ---- KPIs + "By Flat" -----------------------------------------------------
  loadKpis(): void {
    this.festivalService.getFlatContributionKpis(this.festivalId()).subscribe((data) => this.kpis.set(data));
  }

  loadFlats(): void {
    this.flatsLoading.set(true);
    this.flatSelection.clear();
    const sort = this.flatsSortState();
    this.festivalService.getFlatContributions({
      festivalId: this.festivalId(), search: this.flatsSearchTerm() || undefined, status: this.statusFilter ?? undefined,
      sortBy: sort?.direction ? sort.active : undefined, sortDescending: sort?.direction === 'desc',
      pageNumber: this.flatsPageIndex() + 1, pageSize: this.flatsPageSize()
    }).subscribe((result) => {
      this.flatContributions.set(result.items);
      this.flatsTotalCount.set(result.totalCount);
      this.flatsLoading.set(false);
    });
  }

  private payableFlats(): FlatContributionDto[] {
    return this.flatContributions().filter((f) => f.outstandingAmount > 0);
  }

  allFlatsSelected(): boolean {
    const payable = this.payableFlats();
    return payable.length > 0 && payable.every((f) => this.flatSelection.isSelected(f.flatId));
  }

  someFlatsSelected(): boolean {
    return this.payableFlats().some((f) => this.flatSelection.isSelected(f.flatId)) && !this.allFlatsSelected();
  }

  toggleAllFlats(): void {
    if (this.allFlatsSelected()) {
      this.payableFlats().forEach((f) => this.flatSelection.deselect(f.flatId));
    } else {
      this.payableFlats().forEach((f) => this.flatSelection.select(f.flatId));
    }
  }

  /** Records a full-outstanding-amount contribution for each selected flat,
   * one at a time (concatMap, not parallel) — CreateContributionCommand
   * already sends the WhatsApp receipt as a side effect, so this is just
   * that same single-flat flow run in sequence per flat, nothing new. */
  bulkMarkFlatsPaid(): void {
    const flats = this.payableFlats().filter((f) => this.flatSelection.isSelected(f.flatId));
    if (flats.length === 0) return;

    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: {
        title: `Mark ${flats.length} Flat(s) as Paid`, submitLabel: 'Mark as Paid',
        fields: [
          { key: 'paymentDate', label: 'Payment Date', type: 'date', defaultValue: new Date().toISOString().substring(0, 10) },
          { key: 'paymentMethod', label: 'Payment Method', type: 'select', options: [{ value: 1, label: 'Cash' }, { value: 2, label: 'UPI' }, { value: 3, label: 'Bank Transfer' }] },
          { key: 'transactionId', label: 'Transaction ID', type: 'text', required: false }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.bulkMarkingPaid.set(true);
      from(flats).pipe(
        concatMap((f) => this.festivalService.createContribution({
          festivalId: this.festivalId(), flatId: f.flatId, memberName: `Flat ${f.flatNumber}`,
          amount: f.outstandingAmount, paymentMethod: Number(result.paymentMethod), paymentDate: result.paymentDate,
          transactionId: result.transactionId || null, isAnonymous: false, whatsAppNumber: null
        })),
        toArray()
      ).subscribe({
        next: () => {
          this.bulkMarkingPaid.set(false);
          this.toast.success(`${flats.length} flat(s) marked as paid — WhatsApp receipts sent.`);
          this.flatSelection.clear();
          this.loadFlats();
          this.loadKpis();
          this.load();
        },
        // One flat's payment failing partway through the sequence still
        // leaves the earlier ones recorded — reload to reflect whatever
        // actually went through instead of leaving the UI stuck mid-batch.
        error: () => {
          this.bulkMarkingPaid.set(false);
          this.flatSelection.clear();
          this.loadFlats();
          this.loadKpis();
          this.load();
        }
      });
    });
  }

  onFlatsPage(event: PageEvent): void {
    this.flatsPageIndex.set(event.pageIndex);
    this.flatsPageSize.set(event.pageSize);
    this.loadFlats();
  }

  onFlatsSearch(term: string): void {
    this.flatsSearchTerm.set(term);
    this.flatsPageIndex.set(0);
    this.loadFlats();
  }

  onFlatsSort(sort: Sort): void {
    this.flatsSortState.set(sort);
    this.flatsPageIndex.set(0);
    this.loadFlats();
  }

  onStatusFilterChange(): void {
    this.flatsPageIndex.set(0);
    this.loadFlats();
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
        this.loadFlats();
        this.loadKpis();
      });
    });
  }

  openFlatDetail(flat: FlatContributionDto): void {
    const ref = this.dialog.open(FlatContributionDetailDialogComponent, {
      width: '560px',
      data: { festivalId: this.festivalId(), flat, canManage: this.canManage(), canContribute: this.canContribute() }
    });
    ref.afterClosed().subscribe((changed) => {
      if (!changed) return;
      this.loadFlats();
      this.loadKpis();
      this.load();
    });
  }

  // ---- "All Contributions" ---------------------------------------------------
  load(): void {
    this.loading.set(true);
    const sort = this.sortState();
    this.festivalService.getContributions({
      festivalId: this.festivalId(), search: this.searchTerm() || undefined,
      sortBy: sort?.direction ? sort.active : undefined, sortDescending: sort?.direction === 'desc',
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.contributions.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
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

  onSort(sort: Sort): void {
    this.sortState.set(sort);
    this.pageIndex.set(0);
    this.load();
  }

  addContribution(): void {
    // Always re-fetch: a flat that just got fully paid must drop off the
    // list, and one that just gained an outstanding balance must reappear.
    this.festivalService.getContributableFlats(this.festivalId()).subscribe((flats) => {
      this.flatOptions = flats.map((f) => ({ value: f.flatId, label: f.flatNumber }));

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
            { key: 'whatsAppNumber', label: 'WhatsApp Number (optional — defaults to the flat\'s number on file)', type: 'text', required: false, pattern: MOBILE_PATTERN, patternError: MOBILE_PATTERN_ERROR, maxLength: 10 },
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
          transactionId: result.transactionId, isAnonymous: !!result.isAnonymous,
          whatsAppNumber: result.whatsAppNumber || null
        }).subscribe(() => {
          this.toast.success('Contribution recorded.');
          this.load();
          this.loadFlats();
          this.loadKpis();
        });
      });
    });
  }

  editContribution(contribution: FestivalContributionDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '460px',
      data: {
        title: 'Edit Contribution',
        submitLabel: 'Save',
        fields: [
          { key: 'memberName', label: 'Donor Name', type: 'text', defaultValue: contribution.memberName },
          { key: 'amount', label: 'Amount', type: 'number', defaultValue: contribution.amount },
          {
            key: 'paymentMethod', label: 'Payment Method', type: 'select', defaultValue: contribution.paymentMethod,
            options: [{ value: 1, label: 'Cash' }, { value: 2, label: 'UPI' }, { value: 3, label: 'Bank Transfer' }]
          },
          { key: 'paymentDate', label: 'Payment Date', type: 'date', defaultValue: contribution.paymentDate.substring(0, 10) },
          { key: 'transactionId', label: 'Transaction ID', type: 'text', required: false, defaultValue: contribution.transactionId ?? '' },
          { key: 'isAnonymous', label: 'Keep donor anonymous on public displays', type: 'checkbox', defaultValue: contribution.isAnonymous }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.updateContribution(contribution.id, {
        memberName: result.memberName, amount: Number(result.amount), paymentMethod: Number(result.paymentMethod),
        paymentDate: result.paymentDate, transactionId: result.transactionId || null, isAnonymous: !!result.isAnonymous
      }).subscribe(() => {
        this.toast.success('Contribution updated.');
        this.load();
        this.loadFlats();
        this.loadKpis();
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

  resendToWhatsApp(contribution: FestivalContributionDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '400px',
      data: {
        title: 'Resend Receipt to WhatsApp',
        submitLabel: 'Send',
        fields: [
          {
            key: 'whatsAppNumber', label: 'WhatsApp Number', type: 'text',
            defaultValue: contribution.whatsAppNumber ?? '', pattern: MOBILE_PATTERN, patternError: MOBILE_PATTERN_ERROR, maxLength: 10
          }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.resendContributionReceipt(contribution.id, result.whatsAppNumber).subscribe(() => {
        this.toast.success('Receipt resent to WhatsApp.');
        this.load();
      });
    });
  }
}
