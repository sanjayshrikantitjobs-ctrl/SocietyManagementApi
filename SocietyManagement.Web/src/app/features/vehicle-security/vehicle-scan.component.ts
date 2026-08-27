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
import { VehicleScanResultDto } from './models/vehicle-scan.model';
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

/** Manual plate entry -> match flow, with an optional photo attached for the
 * record. OCR was tried across several engines this session (Tesseract,
 * PaddleOCR, Aspose.OCR) — each either misread plates unreliably, broke the
 * production deployment pipeline (PaddleOCR's native libraries alone added
 * ~540MB to the package), or ran into other integration issues, so the
 * feature was simplified to manual entry: the guard/resident always types
 * the registration number themselves, with a photo capture kept purely as an
 * optional visual record attached to the scan log — never used for
 * recognition. */
@Component({
  selector: 'app-vehicle-scan',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatProgressSpinnerModule, PageHeaderComponent, VehicleScanResultComponent
  ],
  template: `
    <div class="app-page scan-page">
      <app-page-header title="Scan Vehicle" subtitle="Enter the plate number to look it up." [breadcrumbs]="[{ label: 'Vehicle Security', link: '/vehicle-security' }, { label: 'Scan' }]" />

      @if (!scanResult()) {
        <div class="app-card capture-card">
          @if (photoPreviewUrl()) {
            <img [src]="photoPreviewUrl()" alt="" class="photo-preview" />
            <button mat-stroked-button type="button" (click)="photoInput.click()">Retake Photo</button>
          } @else {
            <button mat-stroked-button type="button" class="capture-btn" (click)="photoInput.click()">
              <mat-icon>photo_camera</mat-icon> Attach a Photo (optional)
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
  }

  async confirmAndSearch(): Promise<void> {
    const number = this.registrationNumber().trim();
    if (!number) return;

    this.confirming.set(true);
    const file = this.photoFile();
    const imageBytes = file ? await fileToBase64(file) : null;

    this.vehicleScanService.confirm({
      societyId: this.societyId, normalizedRegistrationNumber: number, rawOcrText: null,
      confidence: null, source: 1, gateId: null, imageBytes
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
  }
}
