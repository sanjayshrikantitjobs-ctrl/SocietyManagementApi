import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { AssetUrlPipe } from '../../shared/pipes/asset-url.pipe';
import { toDateOnlyString } from '../../shared/utils/date.util';
import { SocietyService } from '../society-setup/services/society.service';
import { SCAN_RESULT_LABELS, SCAN_SOURCE_LABELS, VehicleScanHistoryDto } from './models/vehicle-scan.model';
import { VehicleScanService } from './services/vehicle-scan.service';

/** Watchman sees only their own scans; Admin/Super Admin see the whole
 * society's — enforced server-side in GetScanHistoryQuery, this component
 * just renders whatever the API returns. */
@Component({
  selector: 'app-vehicle-scan-history',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatChipsModule, MatDatepickerModule, MatFormFieldModule, MatInputModule,
    MatTableModule, PageHeaderComponent, DataTableComponent, AssetUrlPipe
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Scan History" subtitle="Every plate scan and looked-up search." [breadcrumbs]="[{ label: 'Vehicle Security', link: '/vehicle-security' }, { label: 'History' }]" />

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        [showSearch]="false" emptyIcon="history" emptyTitle="No scans yet"
        (page)="onPage($event)">
        <div toolbar class="date-filters">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>From</mat-label>
            <input matInput [matDatepicker]="fromPicker" [(ngModel)]="fromDate" (dateChange)="onFilterChange()" />
            <mat-datepicker-toggle matSuffix [for]="fromPicker"></mat-datepicker-toggle>
            <mat-datepicker #fromPicker></mat-datepicker>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>To</mat-label>
            <input matInput [matDatepicker]="toPicker" [(ngModel)]="toDate" (dateChange)="onFilterChange()" />
            <mat-datepicker-toggle matSuffix [for]="toPicker"></mat-datepicker-toggle>
            <mat-datepicker #toPicker></mat-datepicker>
          </mat-form-field>
        </div>
        <table mat-table [dataSource]="items()" table>
          <ng-container matColumnDef="photo">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let h">
              @if (h.imageUrl) { <img [src]="h.imageUrl | assetUrl" alt="" class="thumb" /> }
            </td>
          </ng-container>
          <ng-container matColumnDef="registrationNumber">
            <th mat-header-cell *matHeaderCellDef>Reg. No.</th>
            <td mat-cell *matCellDef="let h"><strong>{{ h.normalizedRegistrationNumber }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="source">
            <th mat-header-cell *matHeaderCellDef>Source</th>
            <td mat-cell *matCellDef="let h">{{ sourceLabels[h.source] }}</td>
          </ng-container>
          <ng-container matColumnDef="confidence">
            <th mat-header-cell *matHeaderCellDef>Confidence</th>
            <td mat-cell *matCellDef="let h">{{ h.confidence != null ? (h.confidence * 100 | number: '1.0-0') + '%' : '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="result">
            <th mat-header-cell *matHeaderCellDef>Result</th>
            <td mat-cell *matCellDef="let h">
              <mat-chip-set><mat-chip [class.registered]="h.result === 1" [class.not-registered]="h.result === 2">{{ resultLabels[h.result] }}</mat-chip></mat-chip-set>
            </td>
          </ng-container>
          <ng-container matColumnDef="scannedByName">
            <th mat-header-cell *matHeaderCellDef>Scanned By</th>
            <td mat-cell *matCellDef="let h">{{ h.scannedByName }}</td>
          </ng-container>
          <ng-container matColumnDef="scannedAt">
            <th mat-header-cell *matHeaderCellDef>Time</th>
            <td mat-cell *matCellDef="let h">{{ h.scannedAt | date: 'short' }}</td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    .date-filters { display: flex; gap: 12px; }
    .date-filters mat-form-field { width: 160px; }
    table { width: 100%; }
    .thumb { width: 36px; height: 36px; border-radius: 6px; object-fit: cover; }
    .registered { background: #ecfdf5 !important; color: #16a34a !important; }
    .not-registered { background: #fef2f2 !important; color: #dc2626 !important; }
  `]
})
export class VehicleScanHistoryComponent implements OnInit {
  private readonly vehicleScanService = inject(VehicleScanService);
  private readonly societyService = inject(SocietyService);

  readonly loading = signal(true);
  readonly items = signal<VehicleScanHistoryDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);
  readonly displayedColumns = ['photo', 'registrationNumber', 'source', 'confidence', 'result', 'scannedByName', 'scannedAt'];
  readonly sourceLabels: Record<number, string> = SCAN_SOURCE_LABELS;
  readonly resultLabels: Record<number, string> = SCAN_RESULT_LABELS;

  fromDate: Date | null = null;
  toDate: Date | null = null;

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
    this.vehicleScanService.getHistory({
      societyId: this.societyId, fromDate: toDateOnlyString(this.fromDate) ?? undefined,
      toDate: toDateOnlyString(this.toDate) ?? undefined, pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.items.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  onFilterChange(): void {
    this.pageIndex.set(0);
    this.load();
  }
}
