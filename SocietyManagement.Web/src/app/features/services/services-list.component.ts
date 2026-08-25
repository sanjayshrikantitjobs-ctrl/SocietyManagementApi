import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { SocietyService } from '../society-setup/services/society.service';
import { SocietyServiceDto } from './models/society-service.model';
import { SocietyServiceService } from './services/society-service.service';

/** Vendor/service contracts (lift AMC, pest control, ...) with a yearly
 * RenewalDate — rows within 10 days of renewal are highlighted, same set
 * the topbar notification bell counts (main-layout.component.ts). */
@Component({
  selector: 'app-services-list',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatSortModule, MatTableModule,
    MatTooltipModule, DataTableComponent, PageHeaderComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Services" subtitle="Vendor and AMC contracts — lift, pest control, water tank cleaning, and more."
        [breadcrumbs]="[{ label: 'Services' }]">
        <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> Add Service</button>
      </app-page-header>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search service or vendor..." emptyIcon="build" emptyTitle="No services yet"
        emptyMessage="Add a vendor contract to track its renewal date."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="rows()" matSort (matSortChange)="onSort($event)" table>
          <ng-container matColumnDef="serviceName">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Service</th>
            <td mat-cell *matCellDef="let s"><strong>{{ s.serviceName }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="vendorName">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Vendor</th>
            <td mat-cell *matCellDef="let s">{{ s.vendorName }}</td>
          </ng-container>
          <ng-container matColumnDef="contactNumber">
            <th mat-header-cell *matHeaderCellDef>Contact</th>
            <td mat-cell *matCellDef="let s">{{ s.contactNumber }}</td>
          </ng-container>
          <ng-container matColumnDef="renewalDate">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Renewal Date</th>
            <td mat-cell *matCellDef="let s">
              <span [class.expiring]="isExpiring(s)" [class.overdue]="isOverdue(s)">{{ s.renewalDate | date: 'mediumDate' }}</span>
              @if (isOverdue(s)) { <mat-icon class="warn-icon" matTooltip="Overdue">error</mat-icon> }
              @else if (isExpiring(s)) { <mat-icon class="warn-icon" matTooltip="Renewing soon">warning</mat-icon> }
            </td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let s">
              <mat-chip-set><mat-chip [class.active]="s.isActive" [class.inactive]="!s.isActive">{{ s.isActive ? 'Active' : 'Inactive' }}</mat-chip></mat-chip-set>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let s">
              <button mat-icon-button (click)="edit(s); $event.stopPropagation()"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button (click)="remove(s); $event.stopPropagation()"><mat-icon>delete_outline</mat-icon></button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    table { width: 100%; }
    .expiring { color: #b45309; font-weight: 600; }
    .overdue { color: #dc2626; font-weight: 600; }
    .warn-icon { font-size: 18px; width: 18px; height: 18px; vertical-align: middle; margin-left: 4px; }
    .active { background: #dcfce7 !important; color: #15803d !important; }
    .inactive { background: #f1f5f9 !important; color: #64748b !important; }
  `]
})
export class ServicesListComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly societyService = inject(SocietyService);
  private readonly serviceApi = inject(SocietyServiceService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly rows = signal<SocietyServiceDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  readonly displayedColumns = ['serviceName', 'vendorName', 'contactNumber', 'renewalDate', 'status', 'actions'];

  private societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    const sort = this.sortState();
    this.serviceApi.getServices({
      societyId: this.societyId, search: this.searchTerm() || undefined,
      sortBy: sort?.direction ? sort.active : undefined, sortDescending: sort?.direction === 'desc',
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.rows.set(result.items);
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

  onSort(sort: Sort): void {
    this.sortState.set(sort);
    this.pageIndex.set(0);
    this.load();
  }

  isOverdue(s: SocietyServiceDto): boolean {
    return new Date(s.renewalDate) < new Date(new Date().toDateString());
  }

  isExpiring(s: SocietyServiceDto): boolean {
    if (this.isOverdue(s)) return false;
    const days = (new Date(s.renewalDate).getTime() - new Date(new Date().toDateString()).getTime()) / 86400000;
    return days <= 10;
  }

  private fields(service?: SocietyServiceDto) {
    return [
      { key: 'serviceName', label: 'Service Name', type: 'text' as const, defaultValue: service?.serviceName ?? '' },
      { key: 'vendorName', label: 'Vendor Name', type: 'text' as const, defaultValue: service?.vendorName ?? '' },
      { key: 'contactPerson', label: 'Contact Person', type: 'text' as const, required: false, defaultValue: service?.contactPerson ?? '' },
      { key: 'contactNumber', label: 'Contact Number', type: 'text' as const, defaultValue: service?.contactNumber ?? '' },
      { key: 'email', label: 'Email', type: 'text' as const, required: false, defaultValue: service?.email ?? '' },
      { key: 'renewalDate', label: 'Renewal Date', type: 'date' as const, defaultValue: service?.renewalDate?.substring(0, 10) ?? new Date().toISOString().substring(0, 10) },
      { key: 'notes', label: 'Notes', type: 'textarea' as const, required: false, defaultValue: service?.notes ?? '' }
    ];
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '460px', data: { title: 'Add Service', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.serviceApi.createService({
        ...result, societyId: this.societyId, contactPerson: result.contactPerson || null,
        email: result.email || null, notes: result.notes || null
      }).subscribe(() => {
        this.toast.success('Service added.');
        this.load();
      });
    });
  }

  edit(service: SocietyServiceDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '460px', data: { title: 'Edit Service', submitLabel: 'Save', fields: this.fields(service) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.serviceApi.updateService(service.id, {
        ...result, contactPerson: result.contactPerson || null, email: result.email || null,
        notes: result.notes || null, isActive: service.isActive
      }).subscribe(() => {
        this.toast.success('Service updated.');
        this.load();
      });
    });
  }

  remove(service: SocietyServiceDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Service', destructive: true, message: `Delete "${service.serviceName}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.serviceApi.deleteService(service.id).subscribe(() => {
        this.toast.success('Service deleted.');
        this.load();
      });
    });
  }
}
