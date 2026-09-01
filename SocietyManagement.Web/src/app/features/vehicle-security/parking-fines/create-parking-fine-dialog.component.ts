import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { toDateOnlyString } from '../../../shared/utils/date.util';
import { SocietyService } from '../../society-setup/services/society.service';
import { ParkingSlot } from '../../../core/models/society.model';
import { VEHICLE_TYPE_LABELS } from '../../residents/models/resident.model';
import { VehicleSearchItemDto } from '../models/vehicle-scan.model';
import { VehicleScanService } from '../services/vehicle-scan.service';
import { CreateParkingFinePayload, PARKING_FINE_REASON_LABELS } from './models/parking-fine.model';

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve((reader.result as string).split(',')[1]);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

/** Photo evidence is fully optional here — never blocks recording a fine
 * (see ParkingFine.PhotoUrl's backend doc comment). Vehicle lookup reuses
 * the same debounced search VehicleSearchComponent uses. */
@Component({
  selector: 'app-create-parking-fine-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, MatButtonModule, MatDatepickerModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>Record Parking Fine</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="content">
        @if (!selectedVehicle()) {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Search Vehicle</mat-label>
            <input matInput [(ngModel)]="searchTerm" [ngModelOptions]="{ standalone: true }" (ngModelChange)="onSearchChange($event)" placeholder="Reg. no., owner, or flat..." />
            <mat-icon matPrefix>search</mat-icon>
          </mat-form-field>
          @if (results().length > 0) {
            <div class="results">
              @for (item of results(); track item.vehicleId) {
                <button type="button" class="result-row" (click)="selectVehicle(item)">
                  <strong>{{ item.registrationNumber }}</strong>
                  <span class="muted">{{ vehicleTypeLabels[item.vehicleType] }} @if (item.flatNumber) { · Flat {{ item.flatNumber }} }</span>
                </button>
              }
            </div>
          }
        } @else {
          <div class="selected-vehicle">
            <div>
              <strong>{{ selectedVehicle()!.registrationNumber }}</strong>
              <span class="muted">{{ vehicleTypeLabels[selectedVehicle()!.vehicleType] }} @if (selectedVehicle()!.flatNumber) { · Flat {{ selectedVehicle()!.flatNumber }} }</span>
            </div>
            <button type="button" mat-button (click)="clearVehicle()">Change</button>
          </div>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Reason</mat-label>
            <mat-select formControlName="reason">
              @for (r of reasonKeys; track r) {
                <mat-option [value]="r">{{ reasonLabels[r] }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          @if (form.value.reason === 2) {
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Parking Slot (the one it's actually in)</mat-label>
              <mat-select formControlName="parkingSlotId">
                @for (slot of parkingSlots(); track slot.id) {
                  <mat-option [value]="slot.id">{{ slot.slotNumber }}</mat-option>
                }
              </mat-select>
              @if (form.controls.parkingSlotId.hasError('required') && form.controls.parkingSlotId.touched) {
                <mat-error>Required for this reason.</mat-error>
              }
            </mat-form-field>
          }

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Amount (₹)</mat-label>
            <input matInput type="number" formControlName="amount" />
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Fine Date</mat-label>
            <input matInput [matDatepicker]="finePicker" formControlName="fineDate" />
            <mat-datepicker-toggle matSuffix [for]="finePicker"></mat-datepicker-toggle>
            <mat-datepicker #finePicker></mat-datepicker>
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Notes (optional)</mat-label>
            <textarea matInput rows="2" formControlName="notes"></textarea>
          </mat-form-field>

          <div class="photo-field">
            <label>Evidence Photo (optional)</label>
            <div class="photo-row">
              @if (photoPreviewUrl()) {
                <img [src]="photoPreviewUrl()" alt="" class="photo-preview" />
              }
              <button type="button" mat-stroked-button (click)="photoInput.click()">
                <mat-icon>photo_camera</mat-icon> {{ photoPreviewUrl() ? 'Retake' : 'Attach Photo' }}
              </button>
            </div>
            <input #photoInput type="file" accept="image/*" capture="environment" hidden (change)="onPhotoSelected($event)" />
          </div>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="!selectedVehicle() || form.invalid || submitting()">
          @if (submitting()) { <mat-spinner diameter="20" /> } @else { Record Fine }
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .content { display: flex; flex-direction: column; }
    .full-width { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 12px; display: block; }
    .results { display: flex; flex-direction: column; gap: 4px; max-height: 200px; overflow-y: auto; margin-bottom: 12px; }
    .result-row { display: flex; flex-direction: column; align-items: flex-start; text-align: left; padding: 8px 12px;
      border: 1px solid var(--app-border); border-radius: 8px; background: none; cursor: pointer; }
    .result-row:hover { background: var(--app-surface-alt); }
    .selected-vehicle { display: flex; justify-content: space-between; align-items: center;
      padding: 10px 12px; border-radius: 8px; background: var(--app-surface-alt); margin-bottom: 16px; }
    .photo-field { margin-bottom: 16px; }
    .photo-field label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .photo-row { display: flex; gap: 12px; align-items: center; }
    .photo-preview { width: 56px; height: 56px; border-radius: 8px; object-fit: cover; }
  `]
})
export class CreateParkingFineDialogComponent implements OnInit {
  dialogRef = inject(MatDialogRef<CreateParkingFineDialogComponent>);
  data = inject<{ societyId: number }>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly vehicleScanService = inject(VehicleScanService);
  private readonly societyService = inject(SocietyService);

  readonly submitting = signal(false);
  readonly results = signal<VehicleSearchItemDto[]>([]);
  readonly selectedVehicle = signal<VehicleSearchItemDto | null>(null);
  readonly parkingSlots = signal<ParkingSlot[]>([]);
  readonly photoFile = signal<File | null>(null);
  readonly photoPreviewUrl = signal<string | null>(null);

  searchTerm = '';
  readonly vehicleTypeLabels: Record<number, string> = VEHICLE_TYPE_LABELS;
  readonly reasonLabels = PARKING_FINE_REASON_LABELS;
  readonly reasonKeys = [1, 2, 3] as const;

  private readonly searchSubject = new Subject<string>();

  form = this.fb.nonNullable.group({
    reason: [1, Validators.required],
    parkingSlotId: [null as number | null],
    amount: [null as number | null, [Validators.required, Validators.min(1)]],
    fineDate: [new Date(), Validators.required],
    notes: ['']
  });

  constructor() {
    this.searchSubject.pipe(
      debounceTime(300), distinctUntilChanged(),
      switchMap((term) => term.trim().length >= 2 ? this.vehicleScanService.search(this.data.societyId, term.trim()) : [])
    ).subscribe((results) => this.results.set(results));

    this.form.controls.reason.valueChanges.subscribe((reason) => {
      const slotCtrl = this.form.controls.parkingSlotId;
      if (reason === 2) {
        slotCtrl.setValidators(Validators.required);
      } else {
        slotCtrl.clearValidators();
        slotCtrl.setValue(null);
      }
      slotCtrl.updateValueAndValidity();
    });
  }

  ngOnInit(): void {
    this.societyService.getParkingSlots(this.data.societyId).subscribe((slots) => this.parkingSlots.set(slots));
  }

  onSearchChange(term: string): void {
    this.searchSubject.next(term);
  }

  selectVehicle(item: VehicleSearchItemDto): void {
    this.selectedVehicle.set(item);
    this.results.set([]);
  }

  clearVehicle(): void {
    this.selectedVehicle.set(null);
    this.searchTerm = '';
  }

  onPhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.photoFile.set(file);
    this.photoPreviewUrl.set(URL.createObjectURL(file));
  }

  async submit(): Promise<void> {
    if (!this.selectedVehicle() || this.form.invalid) return;

    this.submitting.set(true);
    const raw = this.form.getRawValue();
    const photoFile = this.photoFile();
    const photoBytes = photoFile ? await fileToBase64(photoFile) : null;

    const payload: CreateParkingFinePayload = {
      societyId: this.data.societyId,
      vehicleId: this.selectedVehicle()!.vehicleId,
      parkingSlotId: raw.parkingSlotId,
      reason: raw.reason as 1 | 2 | 3,
      notes: raw.notes || null,
      amount: raw.amount!,
      fineDate: toDateOnlyString(raw.fineDate)!,
      photoBytes
    };

    this.dialogRef.close(payload);
  }
}
