import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { AssetUrlPipe } from '../../../shared/pipes/asset-url.pipe';
import { FileUploadService } from '../../../core/services/file-upload.service';
import { ToastService } from '../../../core/services/toast.service';
import { PERSON_RELATIONSHIP_LABELS, PersonDto } from '../models/occupancy.model';
import { OccupancyService } from '../services/occupancy.service';

export interface AddOwnerMemberDialogData {
  flatId: number;
  societyId: number;
}

/** Dedicated dialog (not the generic app-prompt-dialog, which has no file
 * upload) for the "Add Owner Member" flow. Phone-blur searches for an
 * existing Person in this society first, so the same person is never
 * duplicated across flats/records. */
@Component({
  selector: 'app-add-owner-member-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSelectModule, AssetUrlPipe
  ],
  template: `
    <h2 mat-dialog-title>Add Owner Member</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="grid">
        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Mobile</mat-label>
          <input matInput formControlName="phone" (blur)="onPhoneBlur()" maxlength="10" />
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
          <mat-label>Gender</mat-label>
          <mat-select formControlName="gender" [disabled]="!!foundPerson()">
            <mat-option [value]="1">Male</mat-option>
            <mat-option [value]="2">Female</mat-option>
            <mat-option [value]="3">Other</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Date of Birth</mat-label><input matInput type="date" formControlName="dateOfBirth" [readonly]="!!foundPerson()" /></mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Relationship</mat-label>
          <mat-select formControlName="relationship">
            @for (opt of relationshipOptions; track opt.value) {
              <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Aadhaar (optional)</mat-label><input matInput formControlName="aadhaarNumber" [readonly]="!!foundPerson()" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>PAN (optional)</mat-label><input matInput formControlName="panNumber" [readonly]="!!foundPerson()" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Move-in Date</mat-label><input matInput type="date" formControlName="moveInDate" /></mat-form-field>

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

        <mat-checkbox formControlName="isPrimary" class="span-2">Is Primary Owner</mat-checkbox>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Add Owner</button>
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
export class AddOwnerMemberDialogComponent {
  dialogRef = inject(MatDialogRef<AddOwnerMemberDialogComponent>);
  data = inject<AddOwnerMemberDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly fileUploadService = inject(FileUploadService);
  private readonly occupancyService = inject(OccupancyService);
  private readonly toast = inject(ToastService);

  readonly uploading = signal(false);
  readonly searching = signal(false);
  readonly foundPerson = signal<PersonDto | null>(null);
  readonly relationshipOptions = Object.entries(PERSON_RELATIONSHIP_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  form = this.fb.nonNullable.group({
    phone: ['', [Validators.required, Validators.pattern(/^\d{10}$/)]],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: [''],
    gender: [null as number | null],
    dateOfBirth: [''],
    relationship: [1, Validators.required],
    aadhaarNumber: [''],
    panNumber: [''],
    photoUrl: [''],
    isPrimary: [false],
    moveInDate: [new Date().toISOString().substring(0, 10), Validators.required]
  });

  onPhoneBlur(): void {
    const phone = this.form.value.phone;
    if (!phone || !/^\d{10}$/.test(phone)) return;

    this.searching.set(true);
    this.occupancyService.searchPerson(this.data.societyId, phone).subscribe({
      next: (person) => {
        this.searching.set(false);
        if (person) {
          this.foundPerson.set(person);
          this.form.patchValue({
            firstName: person.firstName, lastName: person.lastName, email: person.email ?? '',
            gender: person.gender ?? null, dateOfBirth: person.dateOfBirth?.substring(0, 10) ?? '',
            aadhaarNumber: person.aadhaarNumber ?? '', panNumber: person.panNumber ?? '', photoUrl: person.photoUrl ?? ''
          });
        }
      },
      error: () => this.searching.set(false)
    });
  }

  clearFoundPerson(): void {
    this.foundPerson.set(null);
    this.form.patchValue({ firstName: '', lastName: '', email: '', gender: null, dateOfBirth: '', aadhaarNumber: '', panNumber: '', photoUrl: '' });
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

    this.occupancyService.addOwnerMember({
      flatId: this.data.flatId,
      personId: found?.id ?? null,
      firstName: found ? undefined : value.firstName,
      lastName: found ? undefined : value.lastName,
      phone: found ? undefined : value.phone,
      email: found ? undefined : (value.email || null),
      gender: found ? undefined : value.gender,
      dateOfBirth: found ? undefined : (value.dateOfBirth || null),
      photoUrl: found ? undefined : (value.photoUrl || null),
      aadhaarNumber: found ? undefined : (value.aadhaarNumber || null),
      panNumber: found ? undefined : (value.panNumber || null),
      relationship: value.relationship as any,
      isPrimary: value.isPrimary,
      moveInDate: value.moveInDate
    }).subscribe({
      next: () => {
        this.toast.success('Owner added.');
        this.dialogRef.close(true);
      }
    });
  }
}
