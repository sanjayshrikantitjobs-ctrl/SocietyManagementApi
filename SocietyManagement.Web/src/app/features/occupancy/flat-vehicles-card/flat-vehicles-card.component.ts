import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { SocietyService } from '../../society-setup/services/society.service';
import { VEHICLE_TYPE_LABELS, VehicleDto } from '../../residents/models/resident.model';
import { ResidentService } from '../../residents/services/resident.service';

/** Vehicles assigned directly to a flat (Vehicle.FlatId) — a flat may have
 * more than one, shown identically from both the Owner and Tenant detail
 * views since a flat's vehicles are shared context, not owner/tenant-specific. */
@Component({
  selector: 'app-flat-vehicles-card',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTableModule],
  template: `
    <div class="panel">
      <div class="panel-header">
        <h3>Vehicles</h3>
        <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> Add Vehicle</button>
      </div>

      @if (vehicles().length === 0) {
        <p class="empty">No vehicles assigned to this flat yet.</p>
      } @else {
        <table mat-table [dataSource]="vehicles()" class="vehicles-table">
          <ng-container matColumnDef="registrationNumber">
            <th mat-header-cell *matHeaderCellDef>Reg. No.</th>
            <td mat-cell *matCellDef="let v"><strong>{{ v.registrationNumber }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="vehicleType">
            <th mat-header-cell *matHeaderCellDef>Type</th>
            <td mat-cell *matCellDef="let v">{{ vehicleTypeLabels[v.vehicleType] }}</td>
          </ng-container>
          <ng-container matColumnDef="makeModel">
            <th mat-header-cell *matHeaderCellDef>Make / Model</th>
            <td mat-cell *matCellDef="let v">{{ v.make }} {{ v.model }}</td>
          </ng-container>
          <ng-container matColumnDef="parking">
            <th mat-header-cell *matHeaderCellDef>Parking Slot</th>
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
      }
    </div>
  `,
  styles: [`
    .panel { border: 1px solid var(--app-border); border-radius: 10px; padding: 16px; margin-bottom: 16px; }
    .panel-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
    .panel-header h3 { margin: 0; font-size: 15px; }
    .empty { color: var(--app-text-muted); font-size: 13px; margin: 0; }
    .vehicles-table { width: 100%; }
  `]
})
export class FlatVehiclesCardComponent implements OnChanges {
  @Input() flatId!: number;
  @Input() societyId!: number;

  private readonly residentService = inject(ResidentService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly vehicles = signal<VehicleDto[]>([]);
  readonly displayedColumns = ['registrationNumber', 'vehicleType', 'makeModel', 'parking', 'actions'];
  readonly vehicleTypeLabels: Record<number, string> = VEHICLE_TYPE_LABELS;

  private parkingSlotOptions: { value: number; label: string }[] = [];

  ngOnChanges(): void {
    if (!this.flatId) return;
    if (this.societyId && this.parkingSlotOptions.length === 0) {
      this.societyService.getParkingSlots(this.societyId).subscribe((slots) => {
        this.parkingSlotOptions = slots.map((s) => ({ value: s.id, label: s.slotNumber }));
      });
    }
    this.load();
  }

  load(): void {
    this.residentService.getVehicles({ flatId: this.flatId, pageSize: 100 }).subscribe((result) => this.vehicles.set(result.items));
  }

  private fields(vehicle?: VehicleDto) {
    return [
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
      this.residentService.createVehicle({
        ...result, flatId: this.flatId, vehicleType: Number(result.vehicleType),
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
