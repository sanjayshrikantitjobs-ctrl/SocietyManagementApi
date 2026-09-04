import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
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
import { PlateOcrResultDto, VehicleScanResultDto } from './models/vehicle-scan.model';
import { LiveScanAcceptedEvent, VehicleLiveScanComponent } from './vehicle-live-scan.component';
import { VehicleScanResultComponent } from './vehicle-scan-result.component';
import { VehicleScanResultDialogComponent } from './vehicle-scan-result-dialog.component';
import { VehicleScanService } from './services/vehicle-scan.service';
import { VehicleRegisterDialogService } from './services/vehicle-register-dialog.service';

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve((reader.result as string).split(',')[1]);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

/** Mirrors VehicleLiveScanComponent's own normalizePlate — strips
 * everything but letters/digits and uppercases, so a stored "MH 04 AB 1234"
 * can be compared directly against an OCR/search-term "MH04AB1234". */
function normalizePlate(text: string): string {
  return text.toUpperCase().replace(/[^A-Z0-9]/g, '');
}

/** Manual plate entry -> match flow, with an optional OCR assist.
 *
 * OCR history in this codebase: several server-side engines were tried
 * (Tesseract, PaddleOCR, Aspose.OCR) and a photo-capture+drag-to-crop+
 * server-OCR flow (OpenCvSharp + TesseractOCR) shipped and worked locally —
 * but silently failed once deployed, because ~180MB of native OCR binaries
 * didn't survive intact onto a 1GB-quota shared App Service tier. OCR now
 * runs entirely client-side via Tesseract.js (VehicleLiveScanComponent): a
 * continuous camera feed, no server round-trip, no native binaries to
 * deploy. It's still purely an ASSIST — the registration field stays fully
 * editable and manual entry remains authoritative; see tryAutoMatch() for
 * the one case that skips the manual Search click: an exact match on the
 * non-persisting /search endpoint auto-opens the details popup directly. */
@Component({
  selector: 'app-vehicle-scan',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatProgressSpinnerModule, PageHeaderComponent, VehicleLiveScanComponent, VehicleScanResultComponent
  ],
  template: `
    <div class="app-page scan-page">
      <app-page-header title="Scan Vehicle" subtitle="Enter the plate number to look it up." [breadcrumbs]="[{ label: 'Vehicle Security', link: '/vehicle-security' }, { label: 'Scan' }]" />

      @if (!scanResult()) {
        <div class="app-card capture-card">
          @if (liveScanActive()) {
            <app-vehicle-live-scan (accepted)="onLiveScanAccepted($event)" (cancelled)="liveScanActive.set(false)" />
          } @else {
            @if (photoPreviewUrl()) {
              <img [src]="photoPreviewUrl()" alt="" class="photo-preview" />
              <button mat-stroked-button type="button" (click)="photoInput.click()">Retake Photo</button>
              @if (checkingMatch()) {
                <div class="ocr-status"><mat-spinner diameter="16" /> Checking records…</div>
              }
            } @else {
              <button mat-flat-button color="primary" type="button" class="capture-btn" (click)="liveScanActive.set(true)">
                <mat-icon>videocam</mat-icon> Live Scan Plate
              </button>
              <button mat-stroked-button type="button" class="capture-btn" (click)="photoInput.click()">
                <mat-icon>photo_camera</mat-icon> Attach a Photo (optional, for the record)
              </button>
            }
            <input #photoInput type="file" accept="image/*" capture="environment" hidden (change)="onPhotoSelected($event)" />

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Registration Number</mat-label>
              <input matInput [ngModel]="registrationNumber()" (ngModelChange)="registrationNumber.set($event)" placeholder="e.g. MH04AB1234" />
            </mat-form-field>
            <button mat-flat-button color="primary" (click)="confirmAndSearch()" [disabled]="!registrationNumber().trim() || confirming()">
              @if (confirming()) { <mat-spinner diameter="20" /> } @else { Search }
            </button>
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
    .photo-preview { width: 100%; max-height: 260px; object-fit: contain; border-radius: 12px; background: var(--app-surface-alt); display: block; }
    .full-width { width: 100%; }
    .scan-again { width: 100%; margin-top: 12px; }
    .ocr-status { display: flex; align-items: center; gap: 8px; font-size: 12px; color: var(--app-text-muted); }
    .ocr-status.muted { color: var(--app-text-muted); }
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

  readonly photoFile = signal<File | null>(null);
  readonly photoPreviewUrl = signal<string | null>(null);
  readonly confirming = signal(false);
  readonly scanResult = signal<VehicleScanResultDto | null>(null);
  registrationNumber = signal('');

  readonly liveScanActive = signal(false);
  readonly checkingMatch = signal(false);
  readonly ocrResult = signal<PlateOcrResultDto | null>(null);
  /** Snapshot captured by VehicleLiveScanComponent at the moment it accepted
   * a plate — kept as the audit photo for /confirm, same role photoFile
   * plays for the manual-attach path. */
  private liveScanImageBase64: string | null = null;

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
    this.liveScanImageBase64 = null;
    this.ocrResult.set(null);
  }

  onLiveScanAccepted(event: LiveScanAcceptedEvent): void {
    this.liveScanActive.set(false);
    this.photoFile.set(null);
    this.liveScanImageBase64 = event.imageBase64;
    this.photoPreviewUrl.set(`data:image/jpeg;base64,${event.imageBase64}`);
    this.ocrResult.set({ recognizedText: event.recognizedText, normalizedText: event.normalizedText, confidence: event.confidence });
    this.registrationNumber.set(event.normalizedText);
    this.tryAutoMatch(event.normalizedText);
  }

  /** OCR-assist convenience: if the guess exactly matches a registered
   * vehicle, skip straight to the details popup instead of making the guard
   * click Search for what's already an unambiguous hit. Uses the
   * non-persisting /search lookup first (not /confirm) so a plain typo in
   * the OCR guess doesn't log a false "Not Registered" scan — only a
   * confirmed exact match triggers the real confirm+log flow. If nothing
   * matches, the field is simply left prefilled and editable, same as
   * today. */
  private tryAutoMatch(normalizedGuess: string): void {
    this.checkingMatch.set(true);
    this.vehicleScanService.search(this.societyId, normalizedGuess).subscribe({
      next: (items) => {
        this.checkingMatch.set(false);
        // The user may have already edited the field while this was in
        // flight — don't auto-submit something they've since changed.
        if (this.registrationNumber().trim() !== normalizedGuess) return;

        const exactMatch = items.some((i) => normalizePlate(i.registrationNumber) === normalizedGuess);
        if (exactMatch) {
          this.confirmAndSearch();
        }
      },
      error: () => this.checkingMatch.set(false)
    });
  }

  async confirmAndSearch(): Promise<void> {
    const number = this.registrationNumber().trim();
    if (!number) return;

    this.confirming.set(true);
    const file = this.photoFile();
    const imageBytes = this.liveScanImageBase64 ?? (file ? await fileToBase64(file) : null);
    const ocr = this.ocrResult();

    this.vehicleScanService.confirm({
      societyId: this.societyId, normalizedRegistrationNumber: number,
      rawOcrText: ocr?.recognizedText || null, confidence: ocr ? ocr.confidence : null,
      source: ocr ? 1 : 2, gateId: null, imageBytes
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
    this.scanResult.set(null);
    this.registrationNumber.set('');
    this.liveScanActive.set(false);
    this.liveScanImageBase64 = null;
    this.checkingMatch.set(false);
    this.ocrResult.set(null);
  }
}
