import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SocietyService } from '../society-setup/services/society.service';
import { VehicleOcrReadDto, VehicleScanResultDto } from './models/vehicle-scan.model';
import { VehicleScanResultComponent } from './vehicle-scan-result.component';
import { VehicleScanResultDialogComponent } from './vehicle-scan-result-dialog.component';
import { VehicleScanService } from './services/vehicle-scan.service';
import { VehicleRegisterDialogService } from './services/vehicle-register-dialog.service';

const LOW_CONFIDENCE_THRESHOLD = 0.75;
const MIN_CROP_SIZE = 20;

interface CropBox {
  x: number;
  y: number;
  width: number;
  height: number;
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve((reader.result as string).split(',')[1]);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

/** Mobile-first camera capture -> optional crop -> OCR read -> confirm/edit
 * -> match flow. The phone's own camera app is used via <input
 * capture="environment"> (same pattern as new-visitor.component.ts's photo
 * capture) rather than an in-app getUserMedia video stream.
 *
 * OCR is Aspose.OCR's purpose-built car-plate mode (see
 * AsposeOcrVehicleOcrService.cs), which does its own plate *detection* within
 * the full photo — confirmed live reading the plate correctly straight out of
 * an uncropped gate photo. Earlier attempts needed a crop first: Tesseract
 * only does text *recognition* (no detection, reads noise from an uncropped
 * photo), and a from-scratch PaddleOCR pipeline worked but its native
 * libraries alone added ~540MB to the Linux deployment package and broke
 * Azure App Service deploys. The crop box here is kept only as a manual
 * override for edge cases (multiple plates in frame, heavy clutter), not a
 * required step.
 *
 * Either way, every read — regardless of confidence, including a failed one —
 * always goes through the confirm/edit step below before a search fires:
 * watchmen, admins, and super admins (whoever holds Vehicles.Scan) always see
 * the recognized text in an editable field and must confirm or correct it,
 * never search on a blind auto-accept. Manual entry (typing the plate
 * directly if OCR is unavailable or wrong) is always the reliable fallback. */
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
            <div class="photo-frame"
                 (pointerdown)="onCropStart($event)" (pointermove)="onCropMove($event)"
                 (pointerup)="onCropEnd($event)" (pointerleave)="onCropEnd($event)">
              <img #photoImg [src]="photoPreviewUrl()" alt="" class="photo-preview" (load)="onImageLoad()" />
              @if (cropBox(); as box) {
                <div class="crop-box" [style.left.px]="box.x" [style.top.px]="box.y" [style.width.px]="box.width" [style.height.px]="box.height"></div>
              }
            </div>
            <p class="hint">The plate is usually found automatically. If it picks up the wrong area, draw a box tightly around just the plate.</p>
            <div class="crop-actions">
              <button mat-stroked-button type="button" (click)="photoInput.click()" [disabled]="recognizing()">Retake</button>
              @if (cropBox()) {
                <button mat-stroked-button type="button" (click)="clearCrop()" [disabled]="recognizing()">Clear Box</button>
              }
            </div>
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
                <input matInput [ngModel]="editableNumber()" (ngModelChange)="editableNumber.set($event)" placeholder="e.g. MH04AB1234" />
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
    .photo-frame { position: relative; width: 100%; touch-action: none; user-select: none; cursor: crosshair; }
    .photo-preview { width: 100%; max-height: 260px; object-fit: contain; border-radius: 12px; background: var(--app-surface-alt); display: block; }
    .crop-box { position: absolute; border: 2px solid #4f6ef7; background: rgba(79, 110, 247, 0.15); pointer-events: none; }
    .crop-actions { display: flex; gap: 8px; width: 100%; }
    .crop-actions button { flex: 1; }
    .confirm-block { width: 100%; display: flex; flex-direction: column; gap: 12px; align-items: stretch; }
    .full-width { width: 100%; }
    .hint { margin: 0; font-size: 12px; color: var(--app-text-muted); text-align: center; }
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
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);

  private readonly photoImg = viewChild<ElementRef<HTMLImageElement>>('photoImg');

  readonly lowConfidenceThreshold = LOW_CONFIDENCE_THRESHOLD;
  readonly photoFile = signal<File | null>(null);
  readonly photoPreviewUrl = signal<string | null>(null);
  readonly recognizing = signal(false);
  readonly confirming = signal(false);
  readonly ocrRead = signal<VehicleOcrReadDto | null>(null);
  readonly scanResult = signal<VehicleScanResultDto | null>(null);
  readonly cropBox = signal<CropBox | null>(null);
  editableNumber = signal('');

  private societyId = 0;
  private cropOrigin: { x: number; y: number } | null = null;

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
    this.cropBox.set(null);
  }

  onImageLoad(): void {
    this.cropBox.set(null);
  }

  private framePoint(event: PointerEvent): { x: number; y: number } | null {
    const el = this.photoImg()?.nativeElement;
    if (!el) return null;
    const rect = el.getBoundingClientRect();
    return {
      x: Math.min(Math.max(event.clientX - rect.left, 0), rect.width),
      y: Math.min(Math.max(event.clientY - rect.top, 0), rect.height)
    };
  }

  onCropStart(event: PointerEvent): void {
    const point = this.framePoint(event);
    if (!point) return;
    this.cropOrigin = point;
    this.cropBox.set({ x: point.x, y: point.y, width: 0, height: 0 });
  }

  onCropMove(event: PointerEvent): void {
    if (!this.cropOrigin) return;
    const point = this.framePoint(event);
    if (!point) return;
    this.cropBox.set({
      x: Math.min(this.cropOrigin.x, point.x),
      y: Math.min(this.cropOrigin.y, point.y),
      width: Math.abs(point.x - this.cropOrigin.x),
      height: Math.abs(point.y - this.cropOrigin.y)
    });
  }

  onCropEnd(_event: PointerEvent): void {
    this.cropOrigin = null;
    const box = this.cropBox();
    if (box && (box.width < MIN_CROP_SIZE || box.height < MIN_CROP_SIZE)) {
      this.cropBox.set(null);
    }
  }

  clearCrop(): void {
    this.cropBox.set(null);
  }

  private async cropToFile(box: CropBox): Promise<File | null> {
    const el = this.photoImg()?.nativeElement;
    if (!el || !el.naturalWidth) return null;

    const scaleX = el.naturalWidth / el.clientWidth;
    const scaleY = el.naturalHeight / el.clientHeight;
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(box.width * scaleX));
    canvas.height = Math.max(1, Math.round(box.height * scaleY));
    const ctx = canvas.getContext('2d');
    if (!ctx) return null;
    ctx.drawImage(el, box.x * scaleX, box.y * scaleY, box.width * scaleX, box.height * scaleY, 0, 0, canvas.width, canvas.height);

    return new Promise((resolve) => {
      canvas.toBlob((blob) => resolve(blob ? new File([blob], 'plate-crop.jpg', { type: 'image/jpeg' }) : null), 'image/jpeg', 0.92);
    });
  }

  async recognize(): Promise<void> {
    let file = this.photoFile();
    if (!file) return;

    const box = this.cropBox();
    if (box) {
      const cropped = await this.cropToFile(box);
      if (cropped) {
        file = cropped;
        this.photoFile.set(cropped);
        this.photoPreviewUrl.set(URL.createObjectURL(cropped));
        this.cropBox.set(null);
      }
    }

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
        this.confirming.set(false);
        if (result.result === 1) {
          this.openMatchedVehicleDialog(result);
        } else {
          this.scanResult.set(result);
        }
      },
      error: () => this.confirming.set(false)
    });
  }

  private openMatchedVehicleDialog(result: VehicleScanResultDto): void {
    const ref = this.dialog.open(VehicleScanResultDialogComponent, {
      data: { result, canCreateVisitor: this.canCreateVisitor() }
    });
    ref.afterClosed().subscribe((closeResult) => {
      if (closeResult?.createVisitor) {
        this.onCreateVisitor(closeResult.vehicleNumber);
      } else {
        this.reset();
      }
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
    this.cropBox.set(null);
    this.editableNumber.set('');
  }
}
