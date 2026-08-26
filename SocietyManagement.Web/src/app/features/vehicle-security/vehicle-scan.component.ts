import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SocietyService } from '../society-setup/services/society.service';
import { VehicleScanResultDto } from './models/vehicle-scan.model';
import { VehicleScanResultComponent } from './vehicle-scan-result.component';
import { VehicleScanService } from './services/vehicle-scan.service';
import { VehicleRegisterDialogService } from './services/vehicle-register-dialog.service';

const LOW_CONFIDENCE_THRESHOLD = 0.75;

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve((reader.result as string).split(',')[1]);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

/** Mobile-first camera capture -> OCR read -> confirm/edit -> match flow.
 * The phone's own camera app is used via <input capture="environment">
 * (same pattern as new-visitor.component.ts's photo capture) rather than an
 * in-app getUserMedia video stream — more reliable across iOS/Android
 * browsers, far less code, and this app has no existing in-app-camera
 * precedent to diverge from (event-checkin also deep-links out). */
@Component({
  selector: 'app-vehicle-scan',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatProgressSpinnerModule, PageHeaderComponent, VehicleScanResultComponent
  ],
  template: `
    <div class="app-page scan-page">
      <app-page-header title="Scan Vehicle" subtitle="Capture a plate photo to look it up." [breadcrumbs]="[{ label: 'Vehicle Security', link: '/vehicle-security' }, { label: 'Scan' }]" />

      @if (!scanResult()) {
        <div class="app-card capture-card">
          @if (photoPreviewUrl()) {
            <img [src]="photoPreviewUrl()" alt="" class="photo-preview" />
            <button mat-stroked-button type="button" (click)="photoInput.click()" [disabled]="recognizing()">Retake</button>
          } @else {
            <button mat-flat-button color="primary" class="capture-btn" (click)="photoInput.click()">
              <mat-icon>photo_camera</mat-icon> Capture Plate Photo
            </button>
          }
          <input #photoInput type="file" accept="image/*" capture="environment" hidden (change)="onPhotoSelected($event)" />

          @if (photoFile() && !ocrRead()) {
            <button mat-flat-button color="primary" (click)="recognize()" [disabled]="recognizing()">
              @if (recognizing()) { <mat-spinner diameter="20" /> } @else { Recognize Plate }
            </button>
          }

          @if (ocrRead(); as read) {
            <div class="confirm-block">
              <div class="confidence-badge" [class.low]="read.confidence < lowConfidenceThreshold">
                <mat-icon>{{ read.confidence < lowConfidenceThreshold ? 'warning' : 'check_circle' }}</mat-icon>
                {{ read.confidence < lowConfidenceThreshold ? 'Low confidence' : 'Confidence' }} — {{ (read.confidence * 100) | number: '1.0-0' }}%
              </div>
              <p class="hint">Confirm or correct the registration number before searching.</p>
              <mat-form-field appearance="outline" class="full-width">
                <mat-label>Registration Number</mat-label>
                <input matInput [(ngModel)]="editableNumber" placeholder="e.g. MH04AB1234" />
              </mat-form-field>
              <button mat-flat-button color="primary" (click)="confirmAndSearch()" [disabled]="!editableNumber().trim() || confirming()">
                @if (confirming()) { <mat-spinner diameter="20" /> } @else { Search }
              </button>
            </div>
          }
        </div>
      } @else {
        <app-vehicle-scan-result
          [result]="scanResult()" [canRegister]="canRegister()" [canCreateVisitor]="canCreateVisitor()"
          (registerVehicle)="onRegister($event)" (createVisitor)="onCreateVisitor($event)" />
        <button mat-stroked-button class="scan-again" (click)="reset()"><mat-icon>refresh</mat-icon> Scan Another</button>
      }
    </div>
  `,
  styles: [`
    .scan-page { max-width: 480px; margin: 0 auto; }
    .capture-card { display: flex; flex-direction: column; align-items: center; gap: 16px; padding: 24px; }
    .capture-btn { height: 56px; font-size: 16px; width: 100%; }
    .photo-preview { width: 100%; max-height: 260px; object-fit: contain; border-radius: 12px; background: var(--app-surface-alt); }
    .confirm-block { width: 100%; display: flex; flex-direction: column; gap: 12px; align-items: stretch; }
    .full-width { width: 100%; }
    .hint { margin: 0; font-size: 12px; color: var(--app-text-muted); }
    .confidence-badge { display: flex; align-items: center; gap: 6px; font-size: 13px; font-weight: 600; align-self: center;
      padding: 4px 12px; border-radius: 999px; background: #ecfdf5; color: #16a34a; }
    .confidence-badge.low { background: #fffbeb; color: #b45309; }
    .confidence-badge mat-icon { font-size: 18px; width: 18px; height: 18px; }
    .scan-again { width: 100%; margin-top: 12px; }
  `]
})
export class VehicleScanComponent implements OnInit {
  private readonly vehicleScanService = inject(VehicleScanService);
  private readonly societyService = inject(SocietyService);
  private readonly registerDialog = inject(VehicleRegisterDialogService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);

  readonly lowConfidenceThreshold = LOW_CONFIDENCE_THRESHOLD;
  readonly photoFile = signal<File | null>(null);
  readonly photoPreviewUrl = signal<string | null>(null);
  readonly recognizing = signal(false);
  readonly confirming = signal(false);
  readonly ocrRead = signal<import('./models/vehicle-scan.model').VehicleOcrReadDto | null>(null);
  readonly scanResult = signal<VehicleScanResultDto | null>(null);
  editableNumber = signal('');

  private societyId = 0;

  readonly canRegister = () => this.auth.hasPermission('vehicles.register');
  readonly canCreateVisitor = () => this.auth.hasPermission('visitors.create');

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length > 0) this.societyId = societies[0].id;
    });
  }

  onPhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.photoFile.set(file);
    this.photoPreviewUrl.set(URL.createObjectURL(file));
    this.ocrRead.set(null);
  }

  recognize(): void {
    const file = this.photoFile();
    if (!file) return;

    this.recognizing.set(true);
    this.vehicleScanService.recognize(this.societyId, file).subscribe({
      next: (read) => {
        this.ocrRead.set(read);
        this.editableNumber.set(read.normalizedText);
        this.recognizing.set(false);
      },
      error: () => this.recognizing.set(false)
    });
  }

  async confirmAndSearch(): Promise<void> {
    const number = this.editableNumber().trim();
    if (!number) return;

    this.confirming.set(true);
    const file = this.photoFile();
    const imageBytes = file ? await fileToBase64(file) : null;
    const read = this.ocrRead();

    this.vehicleScanService.confirm({
      societyId: this.societyId, normalizedRegistrationNumber: number, rawOcrText: read?.rawText ?? null,
      confidence: read?.confidence ?? null, source: 1, gateId: null, imageBytes
    }).subscribe({
      next: (result) => {
        this.scanResult.set(result);
        this.confirming.set(false);
      },
      error: () => this.confirming.set(false)
    });
  }

  onRegister(registrationNumber: string): void {
    this.registerDialog.open(this.societyId, registrationNumber).subscribe((created) => {
      if (created) this.toast.success('Vehicle registered.');
    });
  }

  onCreateVisitor(vehicleNumber: string): void {
    this.router.navigateByUrl('/visitors/new', { state: { vehicleNumber } });
  }

  reset(): void {
    this.photoFile.set(null);
    this.photoPreviewUrl.set(null);
    this.ocrRead.set(null);
    this.scanResult.set(null);
    this.editableNumber.set('');
  }
}
