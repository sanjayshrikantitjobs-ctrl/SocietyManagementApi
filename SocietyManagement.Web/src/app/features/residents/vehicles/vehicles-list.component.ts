import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { SocietyService } from '../../society-setup/services/society.service';
import { VEHICLE_TYPE_LABELS, VehicleDto } from '../models/resident.model';
import { ResidentService } from '../services/resident.service';

/** Society-wide vehicle list — a vehicle can be assigned to either a Member
 * or a Flat (or both fields left as-is on edit); the Owner/Flat column
 * shows whichever is set. */
@Component({
  selector: 'app-vehicles-list',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatSortModule, MatTableModule, DataTableComponent],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Vehicles</h3>
        <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> Add Vehicle</button>
      </div>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search reg. no., owner, or flat..." emptyIcon="directions_car" emptyTitle="No vehicles yet"
        emptyMessage="Register a two-wheeler or four-wheeler and assign it to a member or a flat."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="vehicles()" matSort (matSortChange)="onSort($event)" table>
          <ng-container matColumnDef="registrationNumber">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Reg. No.</th>
            <td mat-cell *matCellDef="let v"><strong>{{ v.registrationNumber }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="memberName">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="membername">Owner / Flat</th>
            <td mat-cell *matCellDef="let v">{{ v.memberName ?? (v.flatNumber ? 'Flat ' + v.flatNumber : '—') }}</td>
          </ng-container>
          <ng-container matColumnDef="vehicleType">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="vehicletype">Type</th>
            <td mat-cell *matCellDef="let v">{{ vehicleTypeLabels[v.vehicleType] }}</td>
          </ng-container>
          <ng-container matColumnDef="makeModel">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="makemodel">Make / Model</th>
            <td mat-cell *matCellDef="let v">{{ v.make }} {{ v.model }}</td>
          </ng-container>
          <ng-container matColumnDef="parking">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Parking Slot</th>
            <td mat-cell *matCellDef="let v">{{ v.parkingSlotNumber || '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let v">
              <button mat-icon-button (click)="edit(v)"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button (click)="remove(v)"><mat-icon>delete_outline</mat-icon></button>
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
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
    .toolbar h3 { margin: 0; font-size: 15px; }
    table { width: 100%; }
  `]
})
export class VehiclesListComponent implements OnInit {
  private readonly residentService = inject(ResidentService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly vehicles = signal<VehicleDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  readonly displayedColumns = ['registrationNumber', 'memberName', 'vehicleType', 'makeModel', 'parking', 'actions'];
  readonly vehicleTypeLabels: Record<number, string> = VEHICLE_TYPE_LABELS;

  private societyId = 0;
  private parkingSlotOptions: { value: number; label: string }[] = [];
  private memberOptions: { value: number; label: string }[] = [];
  private flatOptions: { value: number; label: string }[] = [];

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.residentService.getMembers({ societyId: this.societyId, pageSize: 500 }).subscribe((result) => {
        this.memberOptions = result.items.map((m) => ({ value: m.id, label: `${m.firstName} ${m.lastName}` }));
      });
      this.societyService.getFlats({ pageSize: 500 }).subscribe((result) => {
        this.flatOptions = result.items.map((f) => ({ value: f.id, label: f.flatNumber }));
      });
      this.societyService.getParkingSlots(this.societyId).subscribe((slots) => {
        this.parkingSlotOptions = slots.map((s) => ({ value: s.id, label: s.slotNumber }));
      });
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    const sort = this.sortState();
    this.residentService.getVehicles({
      societyId: this.societyId, search: this.searchTerm() || undefined,
      sortBy: sort?.direction ? sort.active : undefined, sortDescending: sort?.direction === 'desc',
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.vehicles.set(result.items);
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

  private fields(vehicle?: VehicleDto) {
    return [
      {
        key: 'memberId', label: 'Owner (Member)', type: 'select' as const, required: false,
        options: [{ value: '', label: 'None' }, ...this.memberOptions], defaultValue: vehicle?.memberId ?? ''
      },
      {
        key: 'flatId', label: 'Flat', type: 'select' as const, required: false,
        options: [{ value: '', label: 'None' }, ...this.flatOptions], defaultValue: vehicle?.flatId ?? ''
      },
      {
        key: 'vehicleType', label: 'Vehicle Type', type: 'select' as const,
        options: [{ value: 1, label: 'Two Wheeler' }, { value: 2, label: 'Four Wheeler' }],
        defaultValue: vehicle?.vehicleType ?? 1
      },
      { key: 'registrationNumber', label: 'Registration Number', type: 'text' as const, defaultValue: vehicle?.registrationNumber ?? '' },
      { key: 'make', label: 'Make', type: 'text' as const, required: false, defaultValue: vehicle?.make ?? '' },
      { key: 'model', label: 'Model', type: 'text' as const, required: false, defaultValue: vehicle?.model ?? '' },
      { key: 'color', label: 'Color', type: 'text' as const, required: false, defaultValue: vehicle?.color ?? '' },
      {
        key: 'parkingSlotId', label: 'Parking Slot (optional)', type: 'select' as const, required: false,
        options: [{ value: '', label: 'None' }, ...this.parkingSlotOptions], defaultValue: vehicle?.parkingSlotId ?? ''
      }
    ];
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '460px', data: { title: 'Add Vehicle', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      if (!result.memberId && !result.flatId) {
        this.toast.error('Assign the vehicle to an Owner (Member) or a Flat.');
        return;
      }
      this.residentService.createVehicle({
        ...result, memberId: result.memberId ? Number(result.memberId) : null,
        flatId: result.flatId ? Number(result.flatId) : null, vehicleType: Number(result.vehicleType),
        parkingSlotId: result.parkingSlotId ? Number(result.parkingSlotId) : null,
        make: result.make || null, model: result.model || null, color: result.color || null
      }).subscribe(() => {
        this.toast.success('Vehicle added.');
        this.load();
      });
    });
  }

  edit(vehicle: VehicleDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '460px', data: { title: 'Edit Vehicle', submitLabel: 'Save', fields: this.fields(vehicle) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.residentService.updateVehicle(vehicle.id, {
        ...result, vehicleType: Number(result.vehicleType),
        parkingSlotId: result.parkingSlotId ? Number(result.parkingSlotId) : null,
        make: result.make || null, model: result.model || null, color: result.color || null
      }).subscribe(() => {
        this.toast.success('Vehicle updated.');
        this.load();
      });
    });
  }

  remove(vehicle: VehicleDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Vehicle', destructive: true, message: `Delete vehicle "${vehicle.registrationNumber}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.residentService.deleteVehicle(vehicle.id).subscribe(() => {
        this.toast.success('Vehicle deleted.');
        this.load();
      });
    });
  }
}
