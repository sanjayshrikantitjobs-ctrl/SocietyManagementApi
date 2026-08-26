import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatStepperModule } from '@angular/material/stepper';
import { Building, FLAT_TYPE_LABELS, Floor, Wing } from '../../../core/models/society.model';
import { ToastService } from '../../../core/services/toast.service';
import { SocietyService } from '../../society-setup/services/society.service';
import { toDateOnlyString } from '../../../shared/utils/date.util';
import { GENDER_LABELS } from '../models/resident.model';
import { ResidentService } from '../services/resident.service';

/** One-dialog "add a flat and move someone in" flow — collapses the old
 * Society Setup -> Structure -> Flats click-path plus a separate trip to
 * Residents -> Members into a single stepper (location+flat, owner/tenant,
 * optional family members). Reuses the existing createFlat/createMember/
 * createResidency endpoints; no new backend needed. */
@Component({
  selector: 'app-quick-add-flat-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatDatepickerModule, MatDialogModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule, MatStepperModule
  ],
  template: `
    <h2 mat-dialog-title>Add Flat + Resident</h2>
    <mat-dialog-content>
      <mat-stepper [linear]="true" #stepper>
        <mat-step [stepControl]="locationForm" label="Flat">
          <form [formGroup]="locationForm" class="grid">
            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Society</mat-label>
              <mat-select formControlName="societyId" (selectionChange)="onSocietyChange($event.value)">
                @for (s of societies(); track s.id) { <mat-option [value]="s.id">{{ s.name }}</mat-option> }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Building</mat-label>
              <mat-select formControlName="buildingId" (selectionChange)="onBuildingChange($event.value)">
                @for (b of buildings(); track b.id) { <mat-option [value]="b.id">{{ b.name }}</mat-option> }
              </mat-select>
              <button mat-icon-button matSuffix type="button" matTooltip="Add new building"
                [disabled]="!locationForm.get('societyId')?.value"
                (click)="$event.stopPropagation(); addingBuilding.set(!addingBuilding())">
                <mat-icon>add_circle_outline</mat-icon>
              </button>
            </mat-form-field>
            @if (addingBuilding()) {
              <div class="inline-add span-2">
                <input #newBuildingName placeholder="New building name"
                  (keydown.enter)="addBuilding(newBuildingName.value); newBuildingName.value = ''" />
                <button mat-stroked-button type="button" (click)="addBuilding(newBuildingName.value); newBuildingName.value = ''">Add</button>
              </div>
            }

            <mat-form-field appearance="outline">
              <mat-label>Wing</mat-label>
              <mat-select formControlName="wingId" (selectionChange)="onWingChange($event.value)" [disabled]="!locationForm.get('buildingId')?.value">
                @for (w of wings(); track w.id) { <mat-option [value]="w.id">{{ w.name }}</mat-option> }
              </mat-select>
              <button mat-icon-button matSuffix type="button" matTooltip="Add new wing"
                [disabled]="!locationForm.get('buildingId')?.value"
                (click)="$event.stopPropagation(); addingWing.set(!addingWing())">
                <mat-icon>add_circle_outline</mat-icon>
              </button>
            </mat-form-field>
            @if (addingWing()) {
              <div class="inline-add span-2">
                <input #newWingName placeholder="New wing name"
                  (keydown.enter)="addWing(newWingName.value); newWingName.value = ''" />
                <button mat-stroked-button type="button" (click)="addWing(newWingName.value); newWingName.value = ''">Add</button>
              </div>
            }

            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Floor</mat-label>
              <mat-select formControlName="floorId" [disabled]="!locationForm.get('wingId')?.value">
                @for (f of floors(); track f.id) { <mat-option [value]="f.id">{{ f.name || ('Floor ' + f.floorNumber) }}</mat-option> }
              </mat-select>
              <button mat-icon-button matSuffix type="button" matTooltip="Add new floor"
                [disabled]="!locationForm.get('wingId')?.value"
                (click)="$event.stopPropagation(); addingFloor.set(!addingFloor())">
                <mat-icon>add_circle_outline</mat-icon>
              </button>
            </mat-form-field>
            @if (addingFloor()) {
              <div class="inline-add span-2">
                <input #newFloorNumber type="number" placeholder="Floor number (e.g. 1)"
                  (keydown.enter)="addFloor(newFloorNumber.value); newFloorNumber.value = ''" />
                <button mat-stroked-button type="button" (click)="addFloor(newFloorNumber.value); newFloorNumber.value = ''">Add</button>
              </div>
            }

            <mat-form-field appearance="outline"><mat-label>Flat No.</mat-label><input matInput formControlName="flatNumber" /></mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Type</mat-label>
              <mat-select formControlName="flatType">
                @for (t of flatTypeOptions; track t.value) { <mat-option [value]="t.value">{{ t.label }}</mat-option> }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Area (sq.ft, optional)</mat-label>
              <input matInput type="number" formControlName="areaSqFt" />
            </mat-form-field>
          </form>
          <div class="step-actions">
            <button mat-flat-button color="primary" matStepperNext [disabled]="locationForm.invalid">Next</button>
          </div>
        </mat-step>

        <mat-step [stepControl]="residentForm" label="Owner / Tenant">
          <form [formGroup]="residentForm" class="grid">
            <div class="span-2 radio-row">
              <label><input type="radio" formControlName="residencyType" [value]="1" /> Owner</label>
              <label><input type="radio" formControlName="residencyType" [value]="2" /> Tenant</label>
            </div>
            <mat-form-field appearance="outline"><mat-label>First Name</mat-label><input matInput formControlName="firstName" /></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Last Name</mat-label><input matInput formControlName="lastName" /></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Phone</mat-label><input matInput formControlName="phone" /></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Email (optional)</mat-label><input matInput formControlName="email" /></mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Gender (optional)</mat-label>
              <mat-select formControlName="gender">
                @for (g of genderOptions; track g.value) { <mat-option [value]="g.value">{{ g.label }}</mat-option> }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Date of Birth (optional)</mat-label>
              <input matInput [matDatepicker]="dobPicker" formControlName="dateOfBirth" />
              <mat-datepicker-toggle matSuffix [for]="dobPicker"></mat-datepicker-toggle>
              <mat-datepicker #dobPicker></mat-datepicker>
            </mat-form-field>
          </form>
          <div class="step-actions">
            <button mat-button matStepperPrevious>Back</button>
            <button mat-flat-button color="primary" matStepperNext [disabled]="residentForm.invalid">Next</button>
          </div>
        </mat-step>

        <mat-step label="Family Members (optional)">
          <form [formGroup]="familyForm">
            <div formArrayName="familyMembers" class="family-list">
              @for (row of familyMembers.controls; track row; let i = $index) {
                <div [formGroupName]="i" class="family-row">
                  <mat-form-field appearance="outline"><mat-label>First Name</mat-label><input matInput formControlName="firstName" /></mat-form-field>
                  <mat-form-field appearance="outline"><mat-label>Last Name</mat-label><input matInput formControlName="lastName" /></mat-form-field>
                  <mat-form-field appearance="outline"><mat-label>Age</mat-label><input matInput type="number" formControlName="age" /></mat-form-field>
                  <mat-form-field appearance="outline"><mat-label>Phone (defaults to primary)</mat-label><input matInput formControlName="phone" /></mat-form-field>
                  <button mat-icon-button type="button" (click)="removeFamilyMember(i)"><mat-icon>close</mat-icon></button>
                </div>
              }
            </div>
          </form>
          <button mat-stroked-button type="button" (click)="addFamilyMember()"><mat-icon>add</mat-icon> Add Family Member</button>

          <div class="step-actions">
            <button mat-button matStepperPrevious [disabled]="submitting()">Back</button>
            <button mat-flat-button color="primary" (click)="finish()" [disabled]="submitting()">
              @if (submitting()) { <mat-spinner diameter="18" /> } @else { Create Flat & Add Resident }
            </button>
          </div>
        </mat-step>
      </mat-stepper>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="dialogRef.close()" [disabled]="submitting()">Cancel</button>
    </mat-dialog-actions>
  `,
  styles: [`
    :host { display: block; width: min(620px, 90vw); max-width: 100%; box-sizing: border-box; }
    mat-dialog-content { max-width: 100%; overflow-x: hidden; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0 16px; padding-top: 8px; max-width: 100%; }
    .span-2 { grid-column: span 2; }
    mat-form-field { min-width: 0; }
    .inline-add { display: flex; gap: 8px; margin: -8px 0 12px; }
    .inline-add input { flex: 1; min-width: 0; padding: 8px 12px; border: 1px solid var(--app-border); border-radius: 4px; font-size: 14px; }
    .radio-row { display: flex; gap: 24px; margin: 4px 0 12px; }
    .radio-row label { display: flex; align-items: center; gap: 6px; font-size: 14px; }
    .step-actions { display: flex; gap: 8px; justify-content: flex-end; padding: 16px 0 8px; }
    .family-list { display: flex; flex-direction: column; gap: 4px; margin-bottom: 8px; }
    .family-row { display: grid; grid-template-columns: 1fr 1fr 80px 1fr auto; gap: 8px; align-items: center; }
  `]
})
export class QuickAddFlatDialogComponent {
  dialogRef = inject(MatDialogRef<QuickAddFlatDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly societyService = inject(SocietyService);
  private readonly residentService = inject(ResidentService);
  private readonly toast = inject(ToastService);

  readonly societies = signal<{ id: number; name: string }[]>([]);
  readonly buildings = signal<Building[]>([]);
  readonly wings = signal<Wing[]>([]);
  readonly floors = signal<Floor[]>([]);
  readonly submitting = signal(false);

  readonly addingBuilding = signal(false);
  readonly addingWing = signal(false);
  readonly addingFloor = signal(false);

  readonly flatTypeOptions = Object.entries(FLAT_TYPE_LABELS).map(([value, label]) => ({ value: Number(value), label }));
  readonly genderOptions = Object.entries(GENDER_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  locationForm = this.fb.nonNullable.group({
    societyId: [0, Validators.required],
    buildingId: [0, Validators.required],
    wingId: [0, Validators.required],
    floorId: [0, Validators.required],
    flatNumber: ['', Validators.required],
    flatType: [3, Validators.required],
    areaSqFt: [null as number | null]
  });

  residentForm = this.fb.nonNullable.group({
    residencyType: [1, Validators.required],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    phone: ['', Validators.required],
    email: [''],
    gender: [null as number | null],
    dateOfBirth: [null as Date | null]
  });

  familyForm = this.fb.group({ familyMembers: this.fb.array<ReturnType<typeof this.newFamilyRow>>([]) });

  get familyMembers(): FormArray {
    return this.familyForm.get('familyMembers') as FormArray;
  }

  constructor() {
    this.societyService.getSocieties().subscribe((societies) => {
      this.societies.set(societies.map((s) => ({ id: s.id, name: s.name })));
      if (societies.length === 1) {
        this.locationForm.patchValue({ societyId: societies[0].id });
        this.onSocietyChange(societies[0].id);
      }
    });
  }

  private newFamilyRow() {
    return this.fb.group({
      firstName: [''], lastName: [''], age: [null as number | null], phone: ['']
    });
  }

  addFamilyMember(): void {
    this.familyMembers.push(this.newFamilyRow());
  }

  removeFamilyMember(index: number): void {
    this.familyMembers.removeAt(index);
  }

  private loadBuildings(societyId: number): void {
    this.societyService.getBuildings(societyId).subscribe((buildings) => this.buildings.set(buildings));
  }

  private loadWings(buildingId: number): void {
    this.societyService.getWings(buildingId).subscribe((wings) => this.wings.set(wings));
  }

  private loadFloors(wingId: number): void {
    this.societyService.getFloors(wingId).subscribe((floors) => this.floors.set(floors));
  }

  onSocietyChange(societyId: number): void {
    this.buildings.set([]); this.wings.set([]); this.floors.set([]);
    this.locationForm.patchValue({ buildingId: 0, wingId: 0, floorId: 0 });
    this.loadBuildings(societyId);
  }

  onBuildingChange(buildingId: number): void {
    this.wings.set([]); this.floors.set([]);
    this.locationForm.patchValue({ wingId: 0, floorId: 0 });
    this.loadWings(buildingId);
  }

  onWingChange(wingId: number): void {
    this.floors.set([]);
    this.locationForm.patchValue({ floorId: 0 });
    this.loadFloors(wingId);
  }

  addBuilding(name: string): void {
    const societyId = this.locationForm.get('societyId')!.value;
    if (!name.trim() || !societyId) return;
    this.societyService.createBuilding(societyId, name.trim()).subscribe((id) => {
      this.loadBuildings(societyId);
      this.locationForm.patchValue({ buildingId: id });
      this.onBuildingChange(id);
      this.addingBuilding.set(false);
      this.toast.success('Building added.');
    });
  }

  addWing(name: string): void {
    const buildingId = this.locationForm.get('buildingId')!.value;
    if (!name.trim() || !buildingId) return;
    this.societyService.createWing(buildingId, name.trim()).subscribe((id) => {
      this.loadWings(buildingId);
      this.locationForm.patchValue({ wingId: id });
      this.onWingChange(id);
      this.addingWing.set(false);
      this.toast.success('Wing added.');
    });
  }

  addFloor(floorNumberInput: string): void {
    const wingId = this.locationForm.get('wingId')!.value;
    const floorNumber = Number(floorNumberInput);
    if (!floorNumber || !wingId) return;
    this.societyService.createFloor(wingId, floorNumber).subscribe((id) => {
      this.loadFloors(wingId);
      this.locationForm.patchValue({ floorId: id });
      this.addingFloor.set(false);
      this.toast.success('Floor added.');
    });
  }

  private ageToDateOfBirth(age: number): string {
    const year = new Date().getFullYear() - age;
    return `${year}-01-01`;
  }

  async finish(): Promise<void> {
    if (this.locationForm.invalid || this.residentForm.invalid) return;

    this.submitting.set(true);
    let flatCreated = false;
    try {
      const location = this.locationForm.getRawValue();
      const flatId = await firstValueFrom(this.societyService.createFlat({
        floorId: location.floorId, flatNumber: location.flatNumber, flatType: location.flatType,
        areaSqFt: location.areaSqFt ?? undefined
      }));
      flatCreated = true;

      const resident = this.residentForm.getRawValue();
      const primaryMemberId = await firstValueFrom(this.residentService.createMember({
        societyId: location.societyId, firstName: resident.firstName, lastName: resident.lastName,
        phone: resident.phone, email: resident.email || null, gender: resident.gender, dateOfBirth: toDateOnlyString(resident.dateOfBirth)
      }));
      await firstValueFrom(this.residentService.createResidency({
        flatId, memberId: primaryMemberId, memberType: resident.residencyType,
        moveInDate: new Date().toISOString().substring(0, 10), isPrimaryContact: true, notes: null
      }));

      for (const row of this.familyMembers.controls) {
        const value = row.getRawValue() as { firstName: string; lastName: string; age: number | null; phone: string };
        if (!value.firstName.trim()) continue;

        const memberId = await firstValueFrom(this.residentService.createMember({
          societyId: location.societyId, firstName: value.firstName, lastName: value.lastName || '',
          phone: value.phone || resident.phone, email: null, gender: null,
          dateOfBirth: value.age ? this.ageToDateOfBirth(value.age) : null
        }));
        await firstValueFrom(this.residentService.createResidency({
          flatId, memberId, memberType: 3, moveInDate: new Date().toISOString().substring(0, 10),
          isPrimaryContact: false, notes: null
        }));
      }

      this.toast.success('Flat and resident added.');
      this.dialogRef.close(true);
    } catch {
      // The flat/member/residency calls aren't wrapped in one transaction, so a
      // failure partway through (e.g. a dropped connection) can still leave the
      // flat and earlier residents committed server-side. The interceptor's toast
      // already surfaced the specific error; closing (instead of leaving the form
      // open to retry) avoids a confusing "flat number already exists" on resubmit
      // and lets the caller's list refresh to reflect whatever did get created.
      if (flatCreated) {
        this.toast.info('Some of this may not have saved — check the Members and Flats lists before retrying.');
        this.dialogRef.close(true);
      }
    } finally {
      this.submitting.set(false);
    }
  }
}
