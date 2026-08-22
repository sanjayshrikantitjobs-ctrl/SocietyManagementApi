import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatTableModule } from '@angular/material/table';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { WaterTankerCollectionDto } from '../models/maintenance.model';
import { MaintenanceService } from '../services/maintenance.service';

/** Resident self-service view of their own flat's water tanker charge
 * history — the flat is resolved server-side from the caller's own
 * Member/FlatResidency record (see GetMyWaterTankerCollectionsQuery), same
 * as My Bills, so there's nothing here to pick or configure. */
@Component({
  selector: 'app-my-water-tanker',
  standalone: true,
  imports: [CommonModule, MatChipsModule, MatTableModule, EmptyStateComponent, PageHeaderComponent, SkeletonLoaderComponent],
  template: `
    <div class="app-page">
      <app-page-header title="My Water Tanker Charges" subtitle="Monthly water tanker contribution for your flat."
        [breadcrumbs]="[{ label: 'My Water Tanker' }]" />

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="60" />
      } @else if (collections().length === 0) {
        <app-empty-state icon="water_drop" title="No charges yet" message="Water tanker charges for your flat will appear here once generated." />
      } @else {
        <table mat-table [dataSource]="collections()" class="app-card">
          <ng-container matColumnDef="flat">
            <th mat-header-cell *matHeaderCellDef>Flat</th>
            <td mat-cell *matCellDef="let c">{{ c.flatNumber }}</td>
          </ng-container>
          <ng-container matColumnDef="month">
            <th mat-header-cell *matHeaderCellDef>Month</th>
            <td mat-cell *matCellDef="let c">{{ c.month | date: 'MMMM yyyy' }}</td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef>Amount</th>
            <td mat-cell *matCellDef="let c">₹{{ c.amount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let c">
              <mat-chip-set><mat-chip [class]="c.isPaid ? 'status-paid' : 'status-pending'">{{ c.isPaid ? 'Paid' : 'Pending' }}</mat-chip></mat-chip-set>
            </td>
          </ng-container>
          <ng-container matColumnDef="paymentDate">
            <th mat-header-cell *matHeaderCellDef>Payment Date</th>
            <td mat-cell *matCellDef="let c">{{ c.paymentDate ? (c.paymentDate | date: 'mediumDate') : '—' }}</td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      }
    </div>
  `,
  styles: [`
    table { width: 100%; }
    .status-paid { background: #ecfdf5 !important; color: #15803d !important; }
    .status-pending { background: #fef2f2 !important; color: #b91c1c !important; }
  `]
})
export class MyWaterTankerComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);

  readonly loading = signal(true);
  readonly collections = signal<WaterTankerCollectionDto[]>([]);
  readonly displayedColumns = ['flat', 'month', 'amount', 'status', 'paymentDate'];

  ngOnInit(): void {
    this.maintenanceService.getMyWaterTankerCollections().subscribe((data) => {
      this.collections.set(data);
      this.loading.set(false);
    });
  }
}
