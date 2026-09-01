import { CommonModule } from '@angular/common';
import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { FileUploadService } from '../../core/services/file-upload.service';
import { SignalrService } from '../../core/services/signalr.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { AssetUrlPipe } from '../../shared/pipes/asset-url.pipe';
import { mobileValidator } from '../../shared/validators/mobile.validator';
import { SocietyService } from '../society-setup/services/society.service';
import { VisitorVisitDto } from './models/visitor.model';
import { VisitorService } from './services/visitor.service';

/** The watchman's mobile-first flow: capture a photo, enter name/mobile,
 * pick the flat and purpose, submit, then watch the request live-update
 * ("Waiting for approval..." -> Approved/Rejected) via SignalR — no
 * manual refresh, no navigating away to check status. */
@Component({
  selector: 'app-new-visitor',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule,
    MatInputModule, MatProgressSpinnerModule, MatSelectModule, AssetUrlPipe, PageHeaderComponent
  ],
  template: `
    <div class="app-page new-visitor-page">
      <app-page-header title="New Visitor" [breadcrumbs]="[{ label: 'Visitors', link: '/visitors' }, { label: 'New' }]" />

      @if (submittedVisit(); as visit) {
        <div class="app-card status-card">
          @if (visit.status === 1) {
            <mat-spinner diameter="40" />
            <h2>Waiting for approval...</h2>
            <p>{{ visit.visitorName }} is waiting at {{ visit.gateName }} for Flat {{ visit.flatNumber }}.</p>
          } @else if (visit.status === 2) {
            <mat-icon class="big-icon success">check_circle</mat-icon>
            <h2>Visitor Approved</h2>
            <p>{{ visit.visitorName }} may be let in.</p>
          } @else if (visit.status === 3) {
            <mat-icon class="big-icon danger">cancel</mat-icon>
            <h2>Visitor Rejected</h2>
            <p>{{ visit.visitorName }} must not be allowed inside.</p>
          } @else if (visit.status === 6) {
            <mat-icon class="big-icon muted">schedule</mat-icon>
            <h2>Request Expired</h2>
            <p>No response was received in time. Create a new request if the visitor is still waiting.</p>
          }
          <button mat-flat-button color="primary" (click)="reset()">New Visitor</button>
        </div>
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()" class="app-card visitor-form">
          <div class="photo-section">
            @if (photoUrl()) {
              <img [src]="photoUrl() | assetUrl" alt="" class="photo-preview" />
              <button mat-stroked-button type="button" (click)="photoInput.click()" [disabled]="uploadingPhoto()">Retake</button>
            } @else {
              <button mat-flat-button color="primary" type="button" class="capture-btn" (click)="photoInput.click()" [disabled]="uploadingPhoto()">
                @if (uploadingPhoto()) { <mat-spinner diameter="20" /> } @else { <mat-icon>photo_camera</mat-icon> }
                Capture Photo
              </button>
            }
            <input #photoInput type="file" accept="image/*" capture="environment" hidden (change)="onPhotoSelected($event)" />
          </div>

          <mat-form-field appearance="outline"><mat-label>Visitor Name</mat-label><input matInput formControlName="name" /></mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Mobile Number</mat-label>
            <input matInput formControlName="mobile" maxlength="10" />
            @if (form.get('mobile')?.hasError('pattern')) { <mat-error>Enter a valid 10-digit mobile number.</mat-error> }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Flat</mat-label>
            <mat-select formControlName="flatId">
              @for (f of flatOptions; track f.value) { <mat-option [value]="f.value">{{ f.label }}</mat-option> }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Purpose</mat-label>
            <mat-select formControlName="purposeId">
              @for (p of purposeOptions; track p.value) { <mat-option [value]="p.value">{{ p.label }}</mat-option> }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Number of Visitors</mat-label>
            <input matInput type="number" formControlName="numberOfVisitors" min="1" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Gate</mat-label>
            <mat-select formControlName="gateId">
              @for (g of gateOptions; track g.value) { <mat-option [value]="g.value">{{ g.label }}</mat-option> }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline"><mat-label>Vehicle Number (optional)</mat-label><input matInput formControlName="vehicleNumber" /></mat-form-field>

          <button mat-flat-button color="primary" type="submit" class="submit-btn" [disabled]="form.invalid || submitting()">
            @if (submitting()) { <mat-spinner diameter="20" /> } @else { Submit }
          </button>
        </form>
      }
    </div>
  `,
  styles: [`
    .new-visitor-page { max-width: 480px; margin: 0 auto; }
    .visitor-form { display: flex; flex-direction: column; gap: 12px; padding: 20px; }
    .photo-section { display: flex; flex-direction: column; align-items: center; gap: 8px; margin-bottom: 8px; }
    .photo-preview { width: 140px; height: 140px; border-radius: 12px; object-fit: cover; }
    .capture-btn { height: 56px; font-size: 16px; }
    .submit-btn { height: 52px; font-size: 16px; margin-top: 8px; }
    .status-card { display: flex; flex-direction: column; align-items: center; text-align: center; padding: 40px 20px; gap: 8px; }
    .status-card h2 { margin: 8px 0 0; }
    .status-card button { margin-top: 20px; }
    .big-icon { font-size: 56px; width: 56px; height: 56px; }
    .big-icon.success { color: #16a34a; }
    .big-icon.danger { color: #dc2626; }
    .big-icon.muted { color: var(--app-text-muted); }
  `]
})
export class NewVisitorComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly visitorService = inject(VisitorService);
  private readonly societyService = inject(SocietyService);
  private readonly fileUploadService = inject(FileUploadService);
  private readonly toast = inject(ToastService);
  private readonly signalr = inject(SignalrService);
  private readonly router = inject(Router);

  readonly uploadingPhoto = signal(false);
  readonly photoUrl = signal<string | null>(null);
  readonly submitting = signal(false);
  readonly submittedVisit = signal<VisitorVisitDto | null>(null);

  flatOptions: { value: number; label: string }[] = [];
  purposeOptions: { value: number; label: string }[] = [];
  gateOptions: { value: number; label: string }[] = [];

  private societyId = 0;

  form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    mobile: ['', mobileValidator()],
    flatId: [0, Validators.required],
    purposeId: [0, Validators.required],
    numberOfVisitors: [1, [Validators.required, Validators.min(1)]],
    gateId: [0, Validators.required],
    vehicleNumber: ['']
  });

  constructor() {
    // Live-updates the "Waiting for approval..." card when the resident
    // responds — no polling, no manual refresh.
    effect(() => {
      const notifications = this.signalr.notifications();
      const visit = this.submittedVisit();
      if (!visit) return;

      const match = notifications.find((n) =>
        ['VisitorApproved', 'VisitorRejected', 'VisitorRequestExpired'].includes(n.eventName)
        && (n.payload as VisitorVisitDto)?.id === visit.id);

      if (match) {
        this.submittedVisit.set(match.payload as VisitorVisitDto);
      }
    });
  }

  ngOnInit(): void {
    // Pre-fill when arriving from Vehicle Security's "Create Visitor Entry"
    // (see vehicle-scan.component.ts / vehicle-search.component.ts) — read
    // via history.state rather than getCurrentNavigation(), which is only
    // populated during the navigation itself, not by the time ngOnInit runs.
    const state = history.state as { vehicleNumber?: string } | undefined;
    if (state?.vehicleNumber) {
      this.form.patchValue({ vehicleNumber: state.vehicleNumber });
    }

    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) return;
      this.societyId = societies[0].id;

      this.societyService.getFlats({ pageSize: 500 }).subscribe((result) => {
        this.flatOptions = result.items.map((f) => ({ value: f.id, label: f.flatNumber }));
      });
      this.visitorService.getPurposes(this.societyId, true).subscribe((purposes) => {
        this.purposeOptions = purposes.map((p) => ({ value: p.id, label: p.name }));
      });
      this.visitorService.getGates(this.societyId, true).subscribe((gates) => {
        this.gateOptions = gates.map((g) => ({ value: g.id, label: g.name }));
        if (gates.length === 1) this.form.patchValue({ gateId: gates[0].id });
      });
    });
  }

  onPhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploadingPhoto.set(true);
    this.fileUploadService.upload(file, 'visitors').subscribe({
      next: (url) => {
        this.photoUrl.set(url);
        this.uploadingPhoto.set(false);
      },
      error: () => this.uploadingPhoto.set(false)
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();

    this.submitting.set(true);
    this.visitorService.createVisit({
      visitorId: null,
      newVisitorName: value.name,
      newVisitorMobile: value.mobile,
      newVisitorPhotoUrl: this.photoUrl(),
      newVisitorVehicleNumber: value.vehicleNumber || null,
      newVisitorVehicleType: null,
      flatId: Number(value.flatId),
      purposeId: Number(value.purposeId),
      gateId: Number(value.gateId),
      numberOfVisitors: Number(value.numberOfVisitors)
    }).subscribe({
      next: (visit) => {
        this.submittedVisit.set(visit);
        this.submitting.set(false);
      },
      error: () => this.submitting.set(false)
    });
  }

  reset(): void {
    this.submittedVisit.set(null);
    this.photoUrl.set(null);
    this.form.reset({ name: '', mobile: '', flatId: 0, purposeId: 0, numberOfVisitors: 1, gateId: this.form.value.gateId ?? 0, vehicleNumber: '' });
  }
}
