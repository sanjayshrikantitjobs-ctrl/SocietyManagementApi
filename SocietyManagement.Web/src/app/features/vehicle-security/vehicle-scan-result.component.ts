import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { VEHICLE_TYPE_LABELS } from '../residents/models/resident.model';
import { VehicleScanResultDto } from './models/vehicle-scan.model';

/** Shared result display for both the camera-scan and manual-search flows —
 * a clear "Vehicle Not Registered" state (never auto-creates anything),
 * vehicle/flat/building/wing/parking fields, and owner contact info only
 * when the API actually returned it (it's omitted entirely, not redacted
 * client-side, for a Watchman caller — see VehicleScanFeature.cs). */
@Component({
  selector: 'app-vehicle-scan-result',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule],
  template: `
    @if (result(); as r) {
      <div class="app-card result-card" [class.not-registered]="r.result === 2">
        @if (r.result === 2) {
          <div class="status-block">
            <mat-icon class="status-icon danger">no_crash</mat-icon>
            <h3>Vehicle Not Registered</h3>
            <p class="muted">"{{ r.registrationNumber }}" doesn't match any vehicle on file.</p>
          </div>
          @if (canRegister()) {
            <button mat-flat-button color="primary" (click)="registerVehicle.emit(r.registrationNumber)">
              <mat-icon>add</mat-icon> Register This Vehicle
            </button>
          }
        } @else {
          <div class="status-block">
            <mat-icon class="status-icon success">verified</mat-icon>
            <h3>{{ r.registrationNumber }}</h3>
            <p class="muted">{{ vehicleTypeLabels[r.vehicleType!] }} @if (r.make || r.model) { — {{ r.make }} {{ r.model }} }</p>
          </div>

          <div class="fields">
            @if (r.flatNumber) {
              <div><span class="label">Flat</span><span>{{ r.flatNumber }}@if (r.wingName) {, {{ r.wingName }}} @if (r.buildingName) {, {{ r.buildingName }}}</span></div>
            }
            @if (r.parkingSlotNumber) {
              <div><span class="label">Parking Slot</span><span>{{ r.parkingSlotNumber }}</span></div>
            }
            @if (r.color) {
              <div><span class="label">Color</span><span>{{ r.color }}</span></div>
            }
            @if (r.ownerName) {
              <div><span class="label">Owner</span><span>{{ r.ownerName }}</span></div>
            }
            @if (r.ownerPhone) {
              <div><span class="label">Phone</span><span>{{ r.ownerPhone }}</span></div>
            }
          </div>
        }

        @if (canCreateVisitor()) {
          <button mat-stroked-button (click)="createVisitor.emit(r.registrationNumber)">
            <mat-icon>person_add</mat-icon> Create Visitor Entry
          </button>
        }
      </div>
    }
  `,
  styles: [`
    .result-card { padding: 20px; display: flex; flex-direction: column; gap: 16px; }
    .result-card.not-registered { border: 1px solid #fecaca; }
    .status-block { display: flex; flex-direction: column; align-items: center; text-align: center; gap: 4px; }
    .status-icon { font-size: 40px; width: 40px; height: 40px; }
    .status-icon.success { color: #16a34a; }
    .status-icon.danger { color: #dc2626; }
    .status-block h3 { margin: 4px 0 0; }
    .muted { color: var(--app-text-muted); font-size: 13px; margin: 0; }
    .fields { display: flex; flex-direction: column; gap: 8px; }
    .fields div { display: flex; justify-content: space-between; font-size: 14px; border-bottom: 1px solid var(--app-border); padding-bottom: 6px; }
    .label { color: var(--app-text-muted); }
  `]
})
export class VehicleScanResultComponent {
  result = input<VehicleScanResultDto | null>(null);
  canRegister = input(false);
  canCreateVisitor = input(false);

  registerVehicle = output<string>();
  createVisitor = output<string>();

  readonly vehicleTypeLabels: Record<number, string> = VEHICLE_TYPE_LABELS;
}
