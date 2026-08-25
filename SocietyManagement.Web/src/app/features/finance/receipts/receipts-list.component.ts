import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { ToastService } from '../../../core/services/toast.service';
import { SocietyService } from '../../society-setup/services/society.service';
import { FINANCE_SOURCE_LABELS, FinanceIncomeRowDto } from '../models/finance.model';
import { FinanceService } from '../services/finance.service';

/** Reuses the Income endpoint — every income row already carries a
 * ReceiptNumber (real for Festival, synthesized for Maintenance/Water
 * Tanker), so there's nothing distinct to fetch here; this page is just
 * the reprint/lookup-focused view of the same data. */
@Component({
  selector: 'app-receipts-list',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatTableModule, DataTableComponent],
  template: `
    <div class="tab-content">
      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search receipt no., payer, or flat..." emptyIcon="receipt" emptyTitle="No receipts yet"
        emptyMessage="Receipts are generated automatically for every payment received."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="rows()" table>
          <ng-container matColumnDef="receipt">
            <th mat-header-cell *matHeaderCellDef>Receipt No.</th>
            <td mat-cell *matCellDef="let r"><strong>{{ r.receiptNumber }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="date">
            <th mat-header-cell *matHeaderCellDef>Date</th>
            <td mat-cell *matCellDef="let r">{{ r.date | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="source">
            <th mat-header-cell *matHeaderCellDef>Source</th>
            <td mat-cell *matCellDef="let r"><mat-chip-set><mat-chip>{{ sourceLabels[r.source] }}</mat-chip></mat-chip-set></td>
          </ng-container>
          <ng-container matColumnDef="payer">
            <th mat-header-cell *matHeaderCellDef>Payer</th>
            <td mat-cell *matCellDef="let r">{{ r.payerName }}</td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef>Amount</th>
            <td mat-cell *matCellDef="let r">₹{{ r.amount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let r">
              <button mat-stroked-button (click)="download(r)"><mat-icon>download</mat-icon> Download</button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    table { width: 100%; }
  `]
})
export class ReceiptsListComponent implements OnInit {
  private readonly financeService = inject(FinanceService);
  private readonly societyService = inject(SocietyService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly rows = signal<FinanceIncomeRowDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly displayedColumns = ['receipt', 'date', 'source', 'payer', 'amount', 'actions'];
  readonly sourceLabels: Record<number, string> = FINANCE_SOURCE_LABELS;

  private societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.financeService.getIncome({
      societyId: this.societyId, search: this.searchTerm() || undefined,
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.rows.set(result.items);
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

  download(row: FinanceIncomeRowDto): void {
    this.financeService.downloadReceiptPdf(row.source, row.id).subscribe((blob) => {
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${row.receiptNumber}.pdf`;
      link.click();
      window.URL.revokeObjectURL(url);
    }, () => this.toast.error('Could not generate receipt.'));
  }
}
