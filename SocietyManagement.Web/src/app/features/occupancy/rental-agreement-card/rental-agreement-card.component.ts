import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { AssetUrlPipe } from '../../../shared/pipes/asset-url.pipe';
import { FileUploadService } from '../../../core/services/file-upload.service';
import { ToastService } from '../../../core/services/toast.service';
import { POLICE_VERIFICATION_LABELS, RentalAgreementDto } from '../models/occupancy.model';
import { OccupancyService } from '../services/occupancy.service';

/** The Rental Information Card — display + create/edit, including the
 * agreement document upload. Edit is blocked once the parent occupancy has
 * ended (server-enforced too), so [readOnly] hides the affordance entirely
 * rather than letting the user hit a 409. */
@Component({
  selector: 'app-rental-agreement-card',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule,
    MatInputModule, MatProgressSpinnerModule, MatSelectModule, AssetUrlPipe
  ],
  template: `
    <div class="card">
      <div class="card-header">
        <h4>Rental Information</h4>
        @if (!readOnly && !editing()) {
          <button mat-button (click)="startEdit()">
            <mat-icon>{{ agreement ? 'edit' : 'add' }}</mat-icon>
            {{ agreement ? 'Edit' : 'Add Rental Agreement' }}
          </button>
        }
      </div>

      @if (!editing()) {
        @if (agreement; as a) {
          <div class="fields">
            <div><span class="label">Agreement Period</span><span>{{ a.agreementStartDate | date: 'mediumDate' }} – {{ a.agreementEndDate | date: 'mediumDate' }}</span></div>
            <div><span class="label">Security Deposit</span><span>₹{{ a.securityDeposit | number }}</span></div>
            <div><span class="label">Rent Amount</span><span>{{ a.rentAmount ? '₹' + (a.rentAmount | number) : '—' }}</span></div>
            <div><span class="label">Police Verification</span><span>{{ policeLabels[a.policeVerificationStatus] }}{{ a.policeVerificationReference ? ' (' + a.policeVerificationReference + ')' : '' }}</span></div>
            @if (a.agreementDocumentUrl) {
              <div><span class="label">Document</span><a [href]="a.agreementDocumentUrl | assetUrl" target="_blank">View document</a></div>
            }
          </div>
        } @else {
          <p class="empty">No rental agreement on file yet.</p>
        }
      } @else {
        <form [formGroup]="form" (ngSubmit)="save()" class="edit-grid">
          <mat-form-field appearance="outline"><mat-label>Agreement Start</mat-label><input matInput type="date" formControlName="agreementStartDate" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Agreement End</mat-label><input matInput type="date" formControlName="agreementEndDate" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Security Deposit</mat-label><input matInput type="number" formControlName="securityDeposit" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Rent Amount (optional)</mat-label><input matInput type="number" formControlName="rentAmount" /></mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Police Verification</mat-label>
            <mat-select formControlName="policeVerificationStatus">
              <mat-option [value]="1">Pending</mat-option>
              <mat-option [value]="2">Done</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Verification Reference</mat-label><input matInput formControlName="policeVerificationReference" /></mat-form-field>

          <div class="upload-field span-2">
            <label>Agreement Document</label>
            <div class="upload-row">
              @if (form.value.agreementDocumentUrl) { <a [href]="form.value.agreementDocumentUrl! | assetUrl" target="_blank">Current document</a> }
              <button mat-stroked-button type="button" (click)="fileInput.click()" [disabled]="uploading()">
                @if (uploading()) { <mat-spinner diameter="18" /> } @else { <mat-icon>upload</mat-icon> }
                Upload
              </button>
              <input #fileInput type="file" hidden (change)="onFileSelected($event)" />
            </div>
          </div>

          <div class="span-2 actions">
            <button mat-button type="button" (click)="editing.set(false)">Cancel</button>
            <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Save</button>
          </div>
        </form>
      }
    </div>
  `,
  styles: [`
    .card { border: 1px solid var(--app-border); border-radius: 10px; padding: 16px; margin-bottom: 16px; }
    .card-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; }
    .card-header h4 { margin: 0; font-size: 14px; }
    .fields { display: grid; grid-template-columns: 1fr 1fr; gap: 10px 16px; }
    .fields .label { display: block; font-size: 11px; color: var(--app-text-muted); }
    .empty { color: var(--app-text-muted); font-size: 13px; margin: 0; }
    .edit-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0 16px; }
    .span-2 { grid-column: span 2; }
    .upload-field { margin-bottom: 12px; }
    .upload-field label { display: block; font-size: 12px; color: var(--app-text-muted); margin-bottom: 6px; }
    .upload-row { display: flex; gap: 12px; align-items: center; }
    .actions { display: flex; justify-content: flex-end; gap: 8px; }
  `]
})
export class RentalAgreementCardComponent implements OnChanges {
  @Input() flatOccupancyId!: number;
  @Input() agreement: RentalAgreementDto | null = null;
  @Input() readOnly = false;
  @Output() changed = new EventEmitter<void>();

  private readonly fb = inject(FormBuilder);
  private readonly occupancyService = inject(OccupancyService);
  private readonly fileUploadService = inject(FileUploadService);
  private readonly toast = inject(ToastService);

  readonly editing = signal(false);
  readonly uploading = signal(false);
  readonly policeLabels = POLICE_VERIFICATION_LABELS;

  form = this.fb.nonNullable.group({
    agreementStartDate: ['', Validators.required],
    agreementEndDate: ['', Validators.required],
    securityDeposit: [0, [Validators.required, Validators.min(0)]],
    rentAmount: [null as number | null],
    policeVerificationStatus: [1, Validators.required],
    policeVerificationReference: [''],
    agreementDocumentUrl: ['']
  });

  ngOnChanges(): void {
    this.editing.set(false);
  }

  startEdit(): void {
    const a = this.agreement;
    this.form.reset({
      agreementStartDate: a?.agreementStartDate?.substring(0, 10) ?? new Date().toISOString().substring(0, 10),
      agreementEndDate: a?.agreementEndDate?.substring(0, 10) ?? '',
      securityDeposit: a?.securityDeposit ?? 0,
      rentAmount: a?.rentAmount ?? null,
      policeVerificationStatus: a?.policeVerificationStatus ?? 1,
      policeVerificationReference: a?.policeVerificationReference ?? '',
      agreementDocumentUrl: a?.agreementDocumentUrl ?? ''
    });
    this.editing.set(true);
  }

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.fileUploadService.upload(file, 'occupancy').subscribe({
      next: (url) => {
        this.form.get('agreementDocumentUrl')?.setValue(url);
        this.uploading.set(false);
      },
      error: () => this.uploading.set(false)
    });
  }

  save(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    const payload = {
      agreementStartDate: value.agreementStartDate, agreementEndDate: value.agreementEndDate,
      securityDeposit: Number(value.securityDeposit), rentAmount: value.rentAmount ? Number(value.rentAmount) : null,
      policeVerificationStatus: Number(value.policeVerificationStatus), policeVerificationReference: value.policeVerificationReference || null,
      agreementDocumentUrl: value.agreementDocumentUrl || null
    };

    const onSaved = () => {
      this.toast.success('Rental agreement saved.');
      this.editing.set(false);
      this.changed.emit();
    };

    if (this.agreement) {
      this.occupancyService.updateRentalAgreement(this.agreement.id, payload).subscribe(onSaved);
    } else {
      this.occupancyService.createRentalAgreement({ flatOccupancyId: this.flatOccupancyId, ...payload }).subscribe(onSaved);
    }
  }
}
