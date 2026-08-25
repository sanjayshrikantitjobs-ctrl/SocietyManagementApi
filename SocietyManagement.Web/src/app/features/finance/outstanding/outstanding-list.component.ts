import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { SocietyService } from '../../society-setup/services/society.service';
import { FINANCE_SOURCE_LABELS, FinanceOutstandingRowDto } from '../models/finance.model';
import { FinanceService } from '../services/finance.service';

@Component({
  selector: 'app-outstanding-list',
  standalone: true,
  imports: [CommonModule, FormsModule, MatChipsModule, MatFormFieldModule, MatSelectModule, MatTableModule, DataTableComponent],
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
      </div>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search flat or payer..." emptyIcon="check_circle" emptyTitle="Nothing outstanding"
        emptyMessage="Every flat is fully paid up."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="rows()" table>
          <ng-container matColumnDef="source">
            <th mat-header-cell *matHeaderCellDef>Source</th>
            <td mat-cell *matCellDef="let r"><mat-chip-set><mat-chip>{{ sourceLabels[r.source] }}</mat-chip></mat-chip-set></td>
          </ng-container>
          <ng-container matColumnDef="flat">
            <th mat-header-cell *matHeaderCellDef>Flat</th>
            <td mat-cell *matCellDef="let r">{{ r.flatNumber ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="payer">
            <th mat-header-cell *matHeaderCellDef>Payer</th>
            <td mat-cell *matCellDef="let r">{{ r.payerName }}</td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef>Amount Due</th>
            <td mat-cell *matCellDef="let r" class="due-amount">₹{{ r.amount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="daysOverdue">
            <th mat-header-cell *matHeaderCellDef>Days Overdue</th>
            <td mat-cell *matCellDef="let r">
              @if (r.daysOverdue) { <span class="overdue-days">{{ r.daysOverdue }}</span> } @else { — }
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
    .filters { display: flex; gap: 12px; margin-bottom: 16px; }
    .filters mat-form-field { width: 180px; }
    table { width: 100%; }
    .due-amount { color: #b45309; font-weight: 600; }
    .overdue-days { color: #dc2626; font-weight: 700; }
  `]
})
export class OutstandingListComponent implements OnInit {
  private readonly financeService = inject(FinanceService);
  private readonly societyService = inject(SocietyService);

  readonly loading = signal(true);
  readonly rows = signal<FinanceOutstandingRowDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly displayedColumns = ['source', 'flat', 'payer', 'amount', 'daysOverdue'];
  readonly sourceLabels: Record<number, string> = FINANCE_SOURCE_LABELS;

  sourceFilter: number | null = null;

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
    this.financeService.getOutstanding({
      societyId: this.societyId, source: this.sourceFilter ?? undefined, search: this.searchTerm() || undefined,
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
}
