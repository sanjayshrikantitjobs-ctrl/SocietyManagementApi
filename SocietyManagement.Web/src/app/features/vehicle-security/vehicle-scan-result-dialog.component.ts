import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { VEHICLE_TYPE_LABELS } from '../residents/models/resident.model';
import { VehicleScanResultDto } from './models/vehicle-scan.model';

/** Modal shown once a scanned/searched plate matches a registered vehicle —
 * deliberately mirrors visitor-visit-detail-dialog.component.ts's layout
 * (icon header, label/value field rows, Close button) so the two "look up
 * and see details" flows in the app feel like the same pattern. Owner fields
 * are only rendered when the API actually included them (Watchman logins
 * never receive them — see VehicleScanFeature.cs). */
@Component({
  selector: 'app-vehicle-scan-result-dialog',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatDialogModule, MatIconModule],
  template: `
    <h2 mat-dialog-title>Vehicle Details</h2>
    <mat-dialog-content class="content">
      <div class="icon-wrap">
        <mat-icon class="status-icon">verified</mat-icon>
      </div>

      <div class="fields">
        <div class="field"><span class="label">Registration Number</span><span class="value">{{ visit.registrationNumber }}</span></div>
        <div class="field"><span class="label">Type</span><span class="value">{{ vehicleTypeLabels[visit.vehicleType!] }}</span></div>
        @if (visit.make || visit.model) {
          <div class="field"><span class="label">Make / Model</span><span class="value">{{ visit.make }} {{ visit.model }}</span></div>
        }
        @if (visit.color) {
          <div class="field"><span class="label">Color</span><span class="value">{{ visit.color }}</span></div>
        }
        @if (visit.flatNumber) {
          <div class="field"><span class="label">Flat</span><span class="value">{{ visit.flatNumber }}</span></div>
        }
        @if (visit.wingName || visit.buildingName) {
          <div class="field"><span class="label">Building / Wing</span><span class="value">{{ visit.buildingName }}@if (visit.wingName) {, {{ visit.wingName }}}</span></div>
        }
        @if (visit.parkingSlotNumber) {
          <div class="field"><span class="label">Parking Slot</span><span class="value">{{ visit.parkingSlotNumber }}</span></div>
        }
        @if (visit.ownerName) {
          <div class="field"><span class="label">Owner</span><span class="value">{{ visit.ownerName }}</span></div>
        }
        @if (visit.ownerPhone) {
          <div class="field"><span class="label">Phone</span><span class="value">{{ visit.ownerPhone }}</span></div>
        }
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      @if (canCreateVisitor) {
        <button mat-stroked-button (click)="createVisitor()"><mat-icon>person_add</mat-icon> Create Visitor Entry</button>
      }
      <button mat-button (click)="dialogRef.close()">Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    :host { display: block; width: min(420px, 90vw); }
    .content { display: flex; flex-direction: column; align-items: center; gap: 16px; padding-top: 8px; }
    .icon-wrap { display: flex; justify-content: center; }
    .status-icon { font-size: 64px; width: 64px; height: 64px; color: #16a34a; }
    .fields { width: 100%; display: flex; flex-direction: column; gap: 10px; }
    .field { display: flex; justify-content: space-between; gap: 12px; font-size: 14px; border-bottom: 1px solid var(--app-border); padding-bottom: 8px; }
    .label { color: var(--app-text-muted); }
    .value { font-weight: 600; text-align: right; }
  `]
})
export class VehicleScanResultDialogComponent {
  dialogRef = inject(MatDialogRef<VehicleScanResultDialogComponent>);
  data = inject<{ result: VehicleScanResultDto; canCreateVisitor: boolean }>(MAT_DIALOG_DATA);

  readonly vehicleTypeLabels: Record<number, string> = VEHICLE_TYPE_LABELS;

  get visit(): VehicleScanResultDto {
    return this.data.result;
  }

  get canCreateVisitor(): boolean {
    return this.data.canCreateVisitor;
  }

  createVisitor(): void {
    this.dialogRef.close({ createVisitor: true, vehicleNumber: this.visit.registrationNumber });
  }
}
