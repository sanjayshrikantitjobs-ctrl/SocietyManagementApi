import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { ToastService } from '../../../core/services/toast.service';
import { OccupancyService } from '../services/occupancy.service';

export interface OccupancySettingsDialogData {
  societyId: number;
}

/** AllowMultiplePrimaryOwners toggle — gated on Occupancy.ManageSettings. */
@Component({
  selector: 'app-occupancy-settings',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatSlideToggleModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>Occupancy Settings</h2>
    <mat-dialog-content class="content">
      @if (loading()) {
        <p>Loading...</p>
      } @else {
        <mat-slide-toggle [checked]="allowMultiplePrimaryOwners()" (change)="allowMultiplePrimaryOwners.set($event.checked)">
          Allow multiple Primary Owners per flat
        </mat-slide-toggle>
        <p class="hint">When off (default), only one owner per flat can be marked Primary Owner at a time.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close()">Cancel</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="loading()">Save</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .content { width: 380px; max-width: 100%; }
    .hint { font-size: 12px; color: var(--app-text-muted); margin-top: 8px; }
  `]
})
export class OccupancySettingsComponent {
  dialogRef = inject(MatDialogRef<OccupancySettingsComponent>);
  data = inject<OccupancySettingsDialogData>(MAT_DIALOG_DATA);
  private readonly occupancyService = inject(OccupancyService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly allowMultiplePrimaryOwners = signal(false);

  constructor() {
    this.occupancyService.getSettings(this.data.societyId).subscribe((settings) => {
      this.allowMultiplePrimaryOwners.set(settings.allowMultiplePrimaryOwners);
      this.loading.set(false);
    });
  }

  save(): void {
    this.occupancyService.updateSettings(this.data.societyId, this.allowMultiplePrimaryOwners()).subscribe(() => {
      this.toast.success('Occupancy settings updated.');
      this.dialogRef.close(true);
    });
  }
}
