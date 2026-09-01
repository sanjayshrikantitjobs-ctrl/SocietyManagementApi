import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { AssetUrlPipe } from '../../../shared/pipes/asset-url.pipe';
import { FileUploadService } from '../../../core/services/file-upload.service';
import { ToastService } from '../../../core/services/toast.service';
import { parseDateOnly, toDateOnlyString } from '../../../shared/utils/date.util';
import { PERSON_RELATIONSHIP_LABELS, PersonDto } from '../models/occupancy.model';
import { OccupancyService } from '../services/occupancy.service';
import { MOBILE_PATTERN, optionalMobileValidator } from '../../../shared/validators/mobile.validator';

export interface AddOccupancyFamilyMemberDialogData {
  flatOccupancyId: number;
  societyId: number;
}

/** "Additional residents added afterward" to a Tenant occupancy — never
 * Primary. Same person-fields shape as the Add Owner Member / Add Tenant
 * dialogs (including phone-blur reuse search), plus Relationship. */
@Component({
  selector: 'app-add-occupancy-family-member-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatDatepickerModule, MatDialogModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule, AssetUrlPipe
  ],
  template: `
    <h2 mat-dialog-title>Add Family Member</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="grid">
        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Mobile</mat-label>
          <input matInput formControlName="phone" (blur)="onPhoneBlur()" maxlength="10" />
          @if (form.get('phone')?.hasError('pattern')) { <mat-error>Enter a valid 10-digit mobile number.</mat-error> }
        </mat-form-field>

        @if (searching()) {
          <div class="search-status span-2"><mat-spinner diameter="18" /> Checking for an existing person...</div>
        }
        @if (foundPerson(); as found) {
          <div class="found-card span-2">
            <mat-icon>person</mat-icon>
            <span>Found <strong>{{ found.firstName }} {{ found.lastName }}</strong> — reusing this person.</span>
            <button mat-button type="button" (click)="clearFoundPerson()">Not them, create new</button>
          </div>
        }

        <mat-form-field appearance="outline"><mat-label>First Name</mat-label><input matInput formControlName="firstName" [readonly]="!!foundPerson()" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Last Name</mat-label><input matInput formControlName="lastName" [readonly]="!!foundPerson()" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Email</mat-label><input matInput formControlName="email" [readonly]="!!foundPerson()" /></mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>WhatsApp (optional)</mat-label>
          <input matInput formControlName="whatsAppNumber" [readonly]="!!foundPerson()" maxlength="10" />
          @if (form.get('whatsAppNumber')?.hasError('pattern')) { <mat-error>Enter a valid 10-digit mobile number.</mat-error> }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Relationship</mat-label>
          <mat-select formControlName="relationship">
            @for (opt of relationshipOptions; track opt.value) {
              <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Date of Birth</mat-label>
          <input matInput [matDatepicker]="dobPicker" formControlName="dateOfBirth" [readonly]="!!foundPerson()" />
          <mat-datepicker-toggle matSuffix [for]="dobPicker"></mat-datepicker-toggle>
          <mat-datepicker #dobPicker></mat-datepicker>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Move-in Date</mat-label>
          <input matInput [matDatepicker]="moveInPicker" formControlName="moveInDate" />
          <mat-datepicker-toggle matSuffix [for]="moveInPicker"></mat-datepicker-toggle>
          <mat-datepicker #moveInPicker></mat-datepicker>
        </mat-form-field>

        <div class="upload-field span-2">
          <label>Profile Photo</label>
          <div class="upload-row">
            @if (form.value.photoUrl) { <img [src]="form.value.photoUrl | assetUrl" class="photo-preview" alt="" /> }
            <button mat-stroked-button type="button" (click)="photoInput.click()" [disabled]="uploading()">
              @if (uploading()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
              Upload Photo
            </button>
            <input #photoInput type="file" accept="image/*" hidden (change)="onPhotoSelected($event)" />
          </div>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Add Family Member</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .grid { display:grid; grid-template-columns: 1fr 1fr; gap: 0 16px; width: 560px; max-width: 100%; }
    .span-2 { grid-column: span 2; }
    .search-status { display: flex; align-items: center; gap: 8px; font-size: 13px; color: var(--app-text-muted); margin-bottom: 8px; }
    .found-card { display: flex; align-items: center; gap: 8px; background: var(--app-primary-light); padding: 8px 12px; border-radius: 8px; margin-bottom: 8px; font-size: 13px; }
    .found-card mat-icon { color: var(--app-primary); }
    .upload-field { margin-bottom: 16px; }
    .upload-field label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .upload-row { display: flex; gap: 12px; align-items: center; }
    .photo-preview { width: 48px; height: 48px; border-radius: 50%; object-fit: cover; }
  `]
})
export class AddOccupancyFamilyMemberDialogComponent {
  dialogRef = inject(MatDialogRef<AddOccupancyFamilyMemberDialogComponent>);
  data = inject<AddOccupancyFamilyMemberDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly fileUploadService = inject(FileUploadService);
  private readonly occupancyService = inject(OccupancyService);
  private readonly toast = inject(ToastService);

  readonly uploading = signal(false);
  readonly searching = signal(false);
  readonly foundPerson = signal<PersonDto | null>(null);
  readonly relationshipOptions = Object.entries(PERSON_RELATIONSHIP_LABELS)
    .filter(([value]) => value !== '1') // "Self" doesn't apply to a family member
    .map(([value, label]) => ({ value: Number(value), label }));

  form = this.fb.nonNullable.group({
    phone: ['', optionalMobileValidator()],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: [''],
    whatsAppNumber: ['', optionalMobileValidator()],
    relationship: [2, Validators.required],
    dateOfBirth: [null as Date | null],
    photoUrl: [''],
    moveInDate: [new Date(), Validators.required]
  });

  onPhoneBlur(): void {
    const phone = this.form.value.phone;
    if (!phone || !MOBILE_PATTERN.test(phone)) return;

    this.searching.set(true);
    this.occupancyService.searchPerson(this.data.societyId, phone).subscribe({
      next: (person) => {
        this.searching.set(false);
        if (person) {
          this.foundPerson.set(person);
          this.form.patchValue({
            firstName: person.firstName, lastName: person.lastName, email: person.email ?? '',
            whatsAppNumber: person.whatsAppNumber ?? '',
            dateOfBirth: parseDateOnly(person.dateOfBirth), photoUrl: person.photoUrl ?? ''
          });
        }
      },
      error: () => this.searching.set(false)
    });
  }

  clearFoundPerson(): void {
    this.foundPerson.set(null);
    this.form.patchValue({ firstName: '', lastName: '', email: '', whatsAppNumber: '', dateOfBirth: null, photoUrl: '' });
  }

  onPhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.fileUploadService.upload(file, 'occupancy').subscribe({
      next: (url) => {
        this.form.get('photoUrl')?.setValue(url);
        this.uploading.set(false);
      },
      error: () => this.uploading.set(false)
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    const found = this.foundPerson();

    this.occupancyService.addFamilyMember(this.data.flatOccupancyId, {
      personId: found?.id ?? null,
      firstName: found ? undefined : value.firstName,
      lastName: found ? undefined : value.lastName,
      phone: found ? undefined : value.phone,
      email: found ? undefined : (value.email || null),
      whatsAppNumber: found ? undefined : (value.whatsAppNumber || null),
      dateOfBirth: found ? undefined : toDateOnlyString(value.dateOfBirth),
      photoUrl: found ? undefined : (value.photoUrl || null),
      relationship: value.relationship as any,
      moveInDate: toDateOnlyString(value.moveInDate)
    }).subscribe({
      next: () => {
        this.toast.success('Family member added.');
        this.dialogRef.close(true);
      }
    });
  }
}
