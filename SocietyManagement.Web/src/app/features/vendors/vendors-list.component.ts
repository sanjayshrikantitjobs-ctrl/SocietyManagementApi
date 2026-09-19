import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { MOBILE_PATTERN, MOBILE_PATTERN_ERROR } from '../../shared/validators/mobile.validator';
import { SocietyService } from '../society-setup/services/society.service';
import { VENDOR_CATEGORY_LABELS, VendorDto, VendorService } from './vendors.service';

const CATEGORY_OPTIONS = Object.entries(VENDOR_CATEGORY_LABELS).map(([value, label]) => ({ value: Number(value), label }));
const toDateOnly = (value: string | null | undefined) => (value ? value.substring(0, 10) : '');
const nullIfEmpty = (value: unknown) => (value === '' || value === undefined ? null : value);
const RENEWAL_WARNING_DAYS = 30;

@Component({
  selector: 'app-vendors-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatMenuModule, MatSelectModule, MatTableModule,
    DataTableComponent, PageHeaderComponent, StatusBadgeComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Vendors" subtitle="Service providers and contractors, with contract renewal tracking."
        [breadcrumbs]="[{ label: 'Vendors' }]">
        @if (canManage()) {
          <button mat-flat-button color="primary" (click)="openForm(null)"><mat-icon>add</mat-icon> Add Vendor</button>
        }
      </app-page-header>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search vendor, contact or phone..." emptyIcon="handyman" emptyTitle="No vendors yet"
        emptyMessage="Add the electricians, plumbers and service companies your society works with."
        (page)="onPage($event)" (search)="onSearch($event)">
        <div toolbar>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="filter">
            <mat-select [(ngModel)]="categoryFilter" (ngModelChange)="onFilterChange()" placeholder="All categories">
              <mat-option [value]="null">All categories</mat-option>
              @for (c of categoryOptions; track c.value) { <mat-option [value]="c.value">{{ c.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="filter">
            <mat-select [(ngModel)]="contractFilter" (ngModelChange)="onFilterChange()" placeholder="Any contract">
              <mat-option [value]="null">Any contract</mat-option>
              <mat-option [value]="30">Ending within 30 days</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
        <table mat-table [dataSource]="rows()" table>
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Vendor</th>
            <td mat-cell *matCellDef="let v">
              <strong>{{ v.name }}</strong>
              @if (v.contactPerson) { <div class="muted">{{ v.contactPerson }}</div> }
            </td>
          </ng-container>
          <ng-container matColumnDef="category">
            <th mat-header-cell *matHeaderCellDef>Category</th>
            <td mat-cell *matCellDef="let v">{{ categoryLabels[v.category] }}</td>
          </ng-container>
          <ng-container matColumnDef="phone">
            <th mat-header-cell *matHeaderCellDef>Phone</th>
            <td mat-cell *matCellDef="let v"><a [href]="'tel:' + v.phone">{{ v.phone }}</a></td>
          </ng-container>
          <ng-container matColumnDef="contract">
            <th mat-header-cell *matHeaderCellDef>Contract</th>
            <td mat-cell *matCellDef="let v">
              @if (v.contractEnd) {
                <app-status-badge [variant]="contractVariant(v)" [label]="contractLabel(v)" />
              } @else {
                <span class="muted">—</span>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="paid">
            <th mat-header-cell *matHeaderCellDef>Paid (expenses)</th>
            <td mat-cell *matCellDef="let v">{{ v.totalPaid | currency: 'INR' : 'symbol' : '1.0-0' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let v"><app-status-badge [variant]="v.isActive ? 'success' : 'neutral'" [label]="v.isActive ? 'Active' : 'Inactive'" /></td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let v">
              @if (canManage()) {
                <button mat-icon-button [matMenuTriggerFor]="menu"><mat-icon>more_vert</mat-icon></button>
                <mat-menu #menu="matMenu">
                  <button mat-menu-item (click)="openForm(v)"><mat-icon>edit</mat-icon> Edit</button>
                  <button mat-menu-item (click)="remove(v)"><mat-icon>delete</mat-icon> Remove</button>
                </mat-menu>
              }
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .filter { width: 190px; }
    div[toolbar] { display: flex; gap: 12px; }
  `]
})
export class VendorsListComponent implements OnInit {
  private readonly vendorService = inject(VendorService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly auth = inject(AuthService);

  readonly categoryLabels = VENDOR_CATEGORY_LABELS;
  readonly categoryOptions = CATEGORY_OPTIONS;
  readonly columns = ['name', 'category', 'phone', 'contract', 'paid', 'status', 'actions'];

  readonly loading = signal(true);
  readonly rows = signal<VendorDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  categoryFilter: number | null = null;
  contractFilter: number | null = null;

  private societyId = 0;

  canManage(): boolean { return this.auth.hasPermission('vendors.manage'); }

  private daysLeft(v: VendorDto): number {
    return Math.ceil((new Date(v.contractEnd!).getTime() - new Date(new Date().toDateString()).getTime()) / 86400000);
  }
  contractVariant(v: VendorDto): StatusBadgeVariant {
    const days = this.daysLeft(v);
    return days < 0 ? 'danger' : days <= RENEWAL_WARNING_DAYS ? 'warning' : 'success';
  }
  contractLabel(v: VendorDto): string {
    const days = this.daysLeft(v);
    const date = new Date(v.contractEnd!).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
    return days < 0 ? `Expired ${date}` : days <= RENEWAL_WARNING_DAYS ? `Ends in ${days}d` : `Until ${date}`;
  }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.vendorService.getVendors({
      societyId: this.societyId, search: this.searchTerm() || undefined, category: this.categoryFilter ?? undefined,
      expiringWithinDays: this.contractFilter ?? undefined, pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.rows.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  onPage(event: PageEvent): void { this.pageIndex.set(event.pageIndex); this.pageSize.set(event.pageSize); this.load(); }
  onSearch(term: string): void { this.searchTerm.set(term); this.pageIndex.set(0); this.load(); }
  onFilterChange(): void { this.pageIndex.set(0); this.load(); }

  openForm(vendor: VendorDto | null): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px',
      data: {
        title: vendor ? 'Edit Vendor' : 'Add Vendor',
        submitLabel: 'Save',
        fields: [
          { key: 'name', label: 'Vendor / Company Name', type: 'text', defaultValue: vendor?.name ?? '', maxLength: 200 },
          { key: 'category', label: 'Category', type: 'select', options: this.categoryOptions, defaultValue: vendor?.category ?? 1 },
          { key: 'contactPerson', label: 'Contact Person', type: 'text', required: false, defaultValue: vendor?.contactPerson ?? '', maxLength: 150 },
          { key: 'phone', label: 'Phone', type: 'text', defaultValue: vendor?.phone ?? '', pattern: MOBILE_PATTERN, patternError: MOBILE_PATTERN_ERROR, maxLength: 10 },
          { key: 'email', label: 'Email', type: 'text', required: false, defaultValue: vendor?.email ?? '' },
          { key: 'address', label: 'Address', type: 'text', required: false, defaultValue: vendor?.address ?? '', maxLength: 500 },
          { key: 'gstNumber', label: 'GST Number', type: 'text', required: false, defaultValue: vendor?.gstNumber ?? '', maxLength: 20 },
          { key: 'contractStart', label: 'Contract Start', type: 'date', required: false, defaultValue: toDateOnly(vendor?.contractStart) },
          { key: 'contractEnd', label: 'Contract End / Renewal Date', type: 'date', required: false, defaultValue: toDateOnly(vendor?.contractEnd) },
          { key: 'performanceNotes', label: 'Performance notes', type: 'textarea', required: false, defaultValue: vendor?.performanceNotes ?? '' },
          ...(vendor ? [{ key: 'isActive', label: 'Active', type: 'checkbox' as const, defaultValue: vendor.isActive }] : [])
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      const payload = {
        name: result.name, category: Number(result.category), contactPerson: nullIfEmpty(result.contactPerson), phone: result.phone,
        email: nullIfEmpty(result.email), address: nullIfEmpty(result.address), gstNumber: nullIfEmpty(result.gstNumber),
        contractStart: nullIfEmpty(result.contractStart), contractEnd: nullIfEmpty(result.contractEnd),
        performanceNotes: nullIfEmpty(result.performanceNotes)
      };
      const request$: Observable<unknown> = vendor
        ? this.vendorService.update(vendor.id, { ...payload, isActive: !!result.isActive })
        : this.vendorService.create({ ...payload, societyId: this.societyId });
      request$.subscribe(() => {
        this.toast.success(vendor ? 'Vendor updated.' : 'Vendor added.');
        this.load();
      });
    });
  }

  remove(vendor: VendorDto): void {
    this.confirmDialog.confirm({ title: 'Remove Vendor', destructive: true, message: `Remove ${vendor.name}?` }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.vendorService.delete(vendor.id).subscribe(() => { this.toast.success('Vendor removed.'); this.load(); });
    });
  }
}
