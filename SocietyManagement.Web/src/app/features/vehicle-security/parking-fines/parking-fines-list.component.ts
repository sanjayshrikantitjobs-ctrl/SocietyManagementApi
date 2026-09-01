import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AssetUrlPipe } from '../../../shared/pipes/asset-url.pipe';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { SocietyService } from '../../society-setup/services/society.service';
import { CreateParkingFineDialogComponent } from './create-parking-fine-dialog.component';
import { CreateParkingFinePayload, PARKING_FINE_REASON_LABELS, ParkingFine } from './models/parking-fine.model';
import { ParkingFineService } from './services/parking-fine.service';

/** All-society scope for Watchman too (unlike Scan History's per-actor
 * scoping) — useful for handing off between shifts. Delete is hidden for
 * Watchman via the same hasPermission-gate pattern used across the app
 * (e.g. societies-list.component.html). */
@Component({
  selector: 'app-parking-fines-list',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatTableModule,
    PageHeaderComponent, DataTableComponent, AssetUrlPipe
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Parking Fines" subtitle="Fines for vehicles parked in a no-parking zone or someone else's slot."
                        [breadcrumbs]="[{ label: 'Vehicle Security', link: '/vehicle-security' }, { label: 'Parking Fines' }]">
        @if (auth.hasPermission('parking_fines.create')) {
          <button mat-flat-button color="primary" (click)="add()">
            <mat-icon>add</mat-icon> Record Fine
          </button>
        }
      </app-page-header>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        [showSearch]="false" emptyIcon="local_parking" emptyTitle="No parking fines yet"
        (page)="onPage($event)">
        <table mat-table [dataSource]="items()" table>
          <ng-container matColumnDef="photo">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let f">
              @if (f.photoUrl) { <img [src]="f.photoUrl | assetUrl" alt="" class="thumb" /> }
            </td>
          </ng-container>
          <ng-container matColumnDef="registrationNumber">
            <th mat-header-cell *matHeaderCellDef>Reg. No.</th>
            <td mat-cell *matCellDef="let f">
              <strong>{{ f.registrationNumber }}</strong><br />
              @if (f.flatNumber) { <span class="muted">Flat {{ f.flatNumber }}</span> }
            </td>
          </ng-container>
          <ng-container matColumnDef="reason">
            <th mat-header-cell *matHeaderCellDef>Reason</th>
            <td mat-cell *matCellDef="let f">
              <mat-chip-set><mat-chip>{{ reasonLabels[f.reason] }}</mat-chip></mat-chip-set>
              @if (f.parkingSlotNumber) { <span class="muted">Slot {{ f.parkingSlotNumber }}</span> }
            </td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef>Amount</th>
            <td mat-cell *matCellDef="let f">₹{{ f.amount }}</td>
          </ng-container>
          <ng-container matColumnDef="fineDate">
            <th mat-header-cell *matHeaderCellDef>Date</th>
            <td mat-cell *matCellDef="let f">{{ f.fineDate | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="issuedByName">
            <th mat-header-cell *matHeaderCellDef>Issued By</th>
            <td mat-cell *matCellDef="let f">{{ f.issuedByName }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let f">
              @if (auth.hasPermission('parking_fines.delete')) {
                <button mat-icon-button (click)="remove(f)"><mat-icon>delete</mat-icon></button>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    table { width: 100%; }
    .thumb { width: 36px; height: 36px; border-radius: 6px; object-fit: cover; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
  `]
})
export class ParkingFinesListComponent implements OnInit {
  private readonly parkingFineService = inject(ParkingFineService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly items = signal<ParkingFine[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);
  readonly reasonLabels = PARKING_FINE_REASON_LABELS;

  get displayedColumns(): string[] {
    const base = ['photo', 'registrationNumber', 'reason', 'amount', 'fineDate', 'issuedByName'];
    return this.auth.hasPermission('parking_fines.delete') ? [...base, 'actions'] : base;
  }

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
    this.parkingFineService.getFines({
      societyId: this.societyId, pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
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

  add(): void {
    this.dialog.open(CreateParkingFineDialogComponent, { width: '480px', data: { societyId: this.societyId } })
      .afterClosed().subscribe((payload: CreateParkingFinePayload | undefined) => {
        if (!payload) return;
        this.parkingFineService.createFine(payload).subscribe(() => {
          this.toast.success('Parking fine recorded.');
          this.load();
        });
      });
  }

  remove(fine: ParkingFine): void {
    this.confirmDialog.confirm({
      title: 'Remove Fine', destructive: true,
      message: `Remove the fine for ${fine.registrationNumber}?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.parkingFineService.deleteFine(fine.id).subscribe(() => {
        this.toast.success('Fine removed.');
        this.load();
      });
    });
  }
}
