import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { ToastService } from '../../../core/services/toast.service';
import { SocietyService } from '../../society-setup/services/society.service';
import { toDateOnlyString } from '../../../shared/utils/date.util';
import { FINANCE_SOURCE_LABELS, FinanceIncomeRowDto } from '../models/finance.model';
import { FinanceService } from '../services/finance.service';

@Component({
  selector: 'app-income-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatChipsModule, MatDatepickerModule, MatFormFieldModule, MatIconModule,
    MatInputModule, MatSelectModule, MatTableModule, MatTooltipModule, DataTableComponent
  ],
  template: `
    <div class="tab-content">
      <div class="filters">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Source</mat-label>
          <mat-select [(ngModel)]="sourceFilter" (selectionChange)="onFilterChange()">
            <mat-option [value]="null">All</mat-option>
            <mat-option [value]="1">Maintenance</mat-option>
            <mat-option [value]="2">Festival</mat-option>
            <mat-option [value]="3">Water Tanker</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>From</mat-label>
          <input matInput [matDatepicker]="fromPicker" [(ngModel)]="dateFrom" (dateChange)="onFilterChange()" />
          <mat-datepicker-toggle matSuffix [for]="fromPicker"></mat-datepicker-toggle>
          <mat-datepicker #fromPicker></mat-datepicker>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>To</mat-label>
          <input matInput [matDatepicker]="toPicker" [(ngModel)]="dateTo" (dateChange)="onFilterChange()" />
          <mat-datepicker-toggle matSuffix [for]="toPicker"></mat-datepicker-toggle>
          <mat-datepicker #toPicker></mat-datepicker>
        </mat-form-field>
      </div>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search payer, flat, or receipt no..." emptyIcon="payments" emptyTitle="No income recorded"
        emptyMessage="Payments from Maintenance, Festivals, and Water Tanker will appear here."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="rows()" table>
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
            <td mat-cell *matCellDef="let r">{{ r.payerName }} @if (r.flatNumber) { <span class="muted">({{ r.flatNumber }})</span> }</td>
          </ng-container>
          <ng-container matColumnDef="description">
            <th mat-header-cell *matHeaderCellDef>Description</th>
            <td mat-cell *matCellDef="let r">{{ r.description }}</td>
          </ng-container>
          <ng-container matColumnDef="method">
            <th mat-header-cell *matHeaderCellDef>Method</th>
            <td mat-cell *matCellDef="let r">{{ r.paymentMethod ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef>Amount</th>
            <td mat-cell *matCellDef="let r" class="income-amount">+₹{{ r.amount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let r">
              <button mat-icon-button matTooltip="Download Receipt" (click)="download(r)"><mat-icon>download</mat-icon></button>
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
    .filters { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; }
    .filters mat-form-field { width: 180px; }
    table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .income-amount { color: #15803d; font-weight: 600; }
  `]
})
export class IncomeListComponent implements OnInit {
  private readonly financeService = inject(FinanceService);
  private readonly societyService = inject(SocietyService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly rows = signal<FinanceIncomeRowDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly displayedColumns = ['date', 'source', 'payer', 'description', 'method', 'amount', 'actions'];
  readonly sourceLabels: Record<number, string> = FINANCE_SOURCE_LABELS;

  sourceFilter: number | null = null;
  dateFrom: Date | null = null;
  dateTo: Date | null = null;

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
      societyId: this.societyId, source: this.sourceFilter ?? undefined, dateFrom: toDateOnlyString(this.dateFrom) ?? undefined,
      dateTo: toDateOnlyString(this.dateTo) ?? undefined, search: this.searchTerm() || undefined,
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

  onFilterChange(): void {
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
