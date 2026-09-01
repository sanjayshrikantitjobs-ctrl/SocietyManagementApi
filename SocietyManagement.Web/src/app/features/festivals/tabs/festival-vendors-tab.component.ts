import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { FestivalVendorDto, VENDOR_CATEGORY_LABELS } from '../models/festival.model';
import { FestivalService } from '../services/festival.service';
import { MOBILE_PATTERN, MOBILE_PATTERN_ERROR } from '../../../shared/validators/mobile.validator';

@Component({
  selector: 'app-festival-vendors-tab',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatTableModule, DataTableComponent],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Vendor Directory</h3>
        @if (canManage()) {
          <button mat-flat-button color="primary" (click)="addVendor()"><mat-icon>add</mat-icon> Add Vendor</button>
        }
      </div>
      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search vendors..." emptyTitle="No vendors yet"
        emptyMessage="Add decorators, caterers, sound and lighting vendors to reuse across festivals."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="vendors()" table>
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Vendor</th>
            <td mat-cell *matCellDef="let v"><strong>{{ v.name }}</strong><br /><span class="muted">{{ v.phone }}</span></td>
          </ng-container>
          <ng-container matColumnDef="category">
            <th mat-header-cell *matHeaderCellDef>Category</th>
            <td mat-cell *matCellDef="let v"><mat-chip-set><mat-chip>{{ categoryLabel(v.category) }}</mat-chip></mat-chip-set></td>
          </ng-container>
          <ng-container matColumnDef="rating">
            <th mat-header-cell *matHeaderCellDef>Rating</th>
            <td mat-cell *matCellDef="let v">{{ v.rating }} / 5</td>
          </ng-container>
          <ng-container matColumnDef="payments">
            <th mat-header-cell *matHeaderCellDef>Total Paid</th>
            <td mat-cell *matCellDef="let v">₹{{ v.totalPayments | number }}</td>
          </ng-container>
          <ng-container matColumnDef="outstanding">
            <th mat-header-cell *matHeaderCellDef>Outstanding</th>
            <td mat-cell *matCellDef="let v">₹{{ v.outstandingAmount | number }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let v">
              @if (canManage()) {
                <button mat-icon-button (click)="editVendor(v)"><mat-icon>edit</mat-icon></button>
                <button mat-icon-button (click)="removeVendor(v)"><mat-icon>delete_outline</mat-icon></button>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
    .toolbar h3 { margin: 0; font-size: 15px; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
  `]
})
export class FestivalVendorsTabComponent implements OnInit {
  societyId = input.required<number>();
  canManage = input(false);

  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly vendors = signal<FestivalVendorDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly displayedColumns = ['name', 'category', 'rating', 'payments', 'outstanding', 'actions'];

  private readonly categoryOptions = Object.entries(VENDOR_CATEGORY_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  categoryLabel(category: number): string {
    return VENDOR_CATEGORY_LABELS[category as 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8];
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.festivalService.getVendors({
      societyId: this.societyId(), search: this.searchTerm() || undefined,
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.vendors.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.pageIndex.set(0);
    this.load();
  }

  private fields(vendor?: FestivalVendorDto) {
    return [
      { key: 'name', label: 'Vendor Name', type: 'text' as const, defaultValue: vendor?.name ?? '' },
      { key: 'category', label: 'Category', type: 'select' as const, options: this.categoryOptions, defaultValue: vendor?.category ?? 1 },
      { key: 'phone', label: 'Phone', type: 'text' as const, required: false, defaultValue: vendor?.phone ?? '', pattern: MOBILE_PATTERN, patternError: MOBILE_PATTERN_ERROR, maxLength: 10 },
      { key: 'email', label: 'Email', type: 'text' as const, required: false, defaultValue: vendor?.email ?? '' },
      { key: 'gstNumber', label: 'GST Number', type: 'text' as const, required: false, defaultValue: vendor?.gstNumber ?? '' },
      { key: 'address', label: 'Address', type: 'textarea' as const, required: false, defaultValue: vendor?.address ?? '' },
      { key: 'rating', label: 'Rating (0-5)', type: 'number' as const, defaultValue: vendor?.rating ?? 0 }
    ];
  }

  addVendor(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '480px', data: { title: 'Add Vendor', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.createVendor({
        societyId: this.societyId(), ...result, category: Number(result.category), rating: Number(result.rating)
      }).subscribe(() => {
        this.toast.success('Vendor added.');
        this.load();
      });
    });
  }

  editVendor(vendor: FestivalVendorDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px', data: { title: 'Edit Vendor', submitLabel: 'Save', fields: this.fields(vendor) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.updateVendor(vendor.id, {
        ...result, category: Number(result.category), rating: Number(result.rating)
      }).subscribe(() => {
        this.toast.success('Vendor updated.');
        this.load();
      });
    });
  }

  removeVendor(vendor: FestivalVendorDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Vendor', destructive: true, message: `Delete "${vendor.name}" from the vendor directory?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.festivalService.deleteVendor(vendor.id).subscribe(() => {
        this.toast.success('Vendor deleted.');
        this.load();
      });
    });
  }
}
