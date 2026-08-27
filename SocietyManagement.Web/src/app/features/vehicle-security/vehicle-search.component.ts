import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SocietyService } from '../society-setup/services/society.service';
import { VEHICLE_TYPE_LABELS } from '../residents/models/resident.model';
import { VehicleScanResultDto, VehicleSearchItemDto } from './models/vehicle-scan.model';
import { VehicleScanResultComponent } from './vehicle-scan-result.component';
import { VehicleScanResultDialogComponent } from './vehicle-scan-result-dialog.component';
import { VehicleScanService } from './services/vehicle-scan.service';
import { VehicleRegisterDialogService } from './services/vehicle-register-dialog.service';

/** Manual lookup by plate/owner-name/flat number — opening a result calls
 * /confirm with Source=ManualSearch (no image) so the same one endpoint
 * that logs OCR scans also logs "this record was looked up", once, on
 * open — never per keystroke. */
@Component({
  selector: 'app-vehicle-search',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatListModule, PageHeaderComponent, VehicleScanResultComponent
  ],
  template: `
    <div class="app-page search-page">
      <app-page-header title="Search Vehicles" subtitle="Look up by registration number, owner, or flat." [breadcrumbs]="[{ label: 'Vehicle Security', link: '/vehicle-security' }, { label: 'Search' }]" />

      @if (!scanResult()) {
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Search</mat-label>
          <input matInput [ngModel]="searchTerm()" (ngModelChange)="onSearchChange($event)" placeholder="Reg. no., owner, or flat..." />
          <mat-icon matPrefix>search</mat-icon>
        </mat-form-field>

        @if (results().length > 0) {
          <mat-nav-list class="app-card results-list">
            @for (item of results(); track item.vehicleId) {
              <a mat-list-item (click)="openResult(item)">
                <span matListItemTitle>{{ item.registrationNumber }}</span>
                <span matListItemLine>{{ vehicleTypeLabels[item.vehicleType] }} @if (item.flatNumber) { · Flat {{ item.flatNumber }} }</span>
              </a>
            }
          </mat-nav-list>
        } @else if (searchTerm().length >= 2) {
          <p class="muted">No matches.</p>
        }
      } @else {
        <app-vehicle-scan-result
          [result]="scanResult()" [canRegister]="canRegister()" [canCreateVisitor]="canCreateVisitor()"
          (registerVehicle)="onRegister($event)" (createVisitor)="onCreateVisitor($event)" />
        <button mat-stroked-button class="search-again" (click)="reset()"><mat-icon>arrow_back</mat-icon> Back to Search</button>
      }
    </div>
  `,
  styles: [`
    .search-page { max-width: 480px; margin: 0 auto; }
    .full-width { width: 100%; }
    .results-list { padding: 4px 0; }
    .muted { color: var(--app-text-muted); font-size: 13px; text-align: center; margin-top: 24px; }
    .search-again { width: 100%; margin-top: 12px; }
  `]
})
export class VehicleSearchComponent implements OnInit {
  private readonly vehicleScanService = inject(VehicleScanService);
  private readonly societyService = inject(SocietyService);
  private readonly registerDialog = inject(VehicleRegisterDialogService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);

  searchTerm = signal('');
  readonly results = signal<VehicleSearchItemDto[]>([]);
  readonly scanResult = signal<VehicleScanResultDto | null>(null);
  readonly vehicleTypeLabels: Record<number, string> = VEHICLE_TYPE_LABELS;

  private societyId = 0;
  private readonly searchSubject = new Subject<string>();

  readonly canRegister = () => this.auth.hasPermission('vehicles.register');
  readonly canCreateVisitor = () => this.auth.hasPermission('visitors.create');

  constructor() {
    this.searchSubject.pipe(
      debounceTime(300), distinctUntilChanged(),
      switchMap((term) => term.trim().length >= 2 ? this.vehicleScanService.search(this.societyId, term.trim()) : [])
    ).subscribe((results) => this.results.set(results));
  }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length > 0) this.societyId = societies[0].id;
    });
  }

  onSearchChange(term: string): void {
    this.searchTerm.set(term);
    this.searchSubject.next(term);
  }

  openResult(item: VehicleSearchItemDto): void {
    this.vehicleScanService.confirm({
      societyId: this.societyId, normalizedRegistrationNumber: item.registrationNumber,
      source: 2, gateId: null
    }).subscribe((result) => {
      if (result.result === 1) {
        this.openMatchedVehicleDialog(result);
      } else {
        this.scanResult.set(result);
      }
    });
  }

  private openMatchedVehicleDialog(result: VehicleScanResultDto): void {
    const ref = this.dialog.open(VehicleScanResultDialogComponent, {
      data: { result, canCreateVisitor: this.canCreateVisitor() }
    });
    ref.afterClosed().subscribe((closeResult) => {
      if (closeResult?.createVisitor) {
        this.onCreateVisitor(closeResult.vehicleNumber);
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
    this.scanResult.set(null);
  }
}
