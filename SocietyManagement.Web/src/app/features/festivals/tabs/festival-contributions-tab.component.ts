import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { FestivalContributionDto, PAYMENT_METHOD_LABELS, TopContributorDto } from '../models/festival.model';
import { FestivalService } from '../services/festival.service';

/** Records donor name/amount only — FlatId linkage (auto-matching a
 * contribution to a specific flat) is left for a follow-up phase once the
 * Member Management module can supply a proper flat picker; the backend
 * already accepts a null FlatId for exactly this case. */
@Component({
  selector: 'app-festival-contributions-tab',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatTableModule, MatTooltipModule, DataTableComponent],
  template: `
    <div class="tab-content">
      <div class="layout">
        <div class="main">
          <div class="toolbar">
            <h3>Contributions</h3>
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
    .layout { display: grid; grid-template-columns: 1fr 280px; gap: 16px; align-items: start; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
    .toolbar h3 { margin: 0; font-size: 15px; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .sidebar { padding: 16px; }
    .sidebar h3 { margin: 0 0 12px; font-size: 14px; }
    .top-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 10px; }
    .top-list li { display: flex; justify-content: space-between; font-size: 13px; }
    .top-list .amount { font-weight: 700; color: #15803d; }
    @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } }
  `]
})
export class FestivalContributionsTabComponent implements OnInit {
  festivalId = input.required<number>();

  private readonly festivalService = inject(FestivalService);
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

  methodLabel(method: number): string {
    return PAYMENT_METHOD_LABELS[method as 1 | 2 | 3];
  }

  ngOnInit(): void {
    this.load();
    this.loadTopContributors();
  }

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

  addContribution(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '460px',
      data: {
        title: 'Record Contribution',
        submitLabel: 'Save',
        fields: [
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
      this.festivalService.createContribution({
        festivalId: this.festivalId(), flatId: null, memberName: result.memberName, amount: Number(result.amount),
        paymentMethod: Number(result.paymentMethod), paymentDate: result.paymentDate,
        transactionId: result.transactionId, isAnonymous: !!result.isAnonymous
      }).subscribe(() => {
        this.toast.success('Contribution recorded.');
        this.load();
        this.loadTopContributors();
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
}
