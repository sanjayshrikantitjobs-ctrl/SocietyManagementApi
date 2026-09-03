import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { SocietyService } from '../../society-setup/services/society.service';
import { AssetUrlPipe } from '../../../shared/pipes/asset-url.pipe';
import { FlatResaleListingDto, LISTING_STATUS_LABELS } from '../models/resident.model';
import { ResidentService } from '../services/resident.service';
import { IssueNocDialogComponent } from './issue-noc-dialog.component';

@Component({
  selector: 'app-resale-listings-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatChipsModule, MatFormFieldModule,
    MatIconModule, MatSelectModule, MatTableModule, MatTooltipModule, DataTableComponent, AssetUrlPipe
  ],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="status-filter">
          <mat-label>Status</mat-label>
          <mat-select [(ngModel)]="statusFilter" (selectionChange)="onFilterChange()">
            <mat-option [value]="null">All</mat-option>
            @for (opt of statusOptions; track opt.value) {
              <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <button mat-flat-button color="primary" (click)="add()" [disabled]="societyId === 0">
          <mat-icon>add</mat-icon> Add Listing
        </button>
      </div>

      <app-data-table
        [loading]="loading()"
        [totalCount]="totalCount()"
        [pageSize]="pageSize()"
        [pageIndex]="pageIndex()"
        [showSearch]="false"
        emptyIcon="sell"
        emptyTitle="No resale listings yet"
        emptyMessage="List a flat for sale to track visit timing, price, and NOC status."
        (page)="onPage($event)">
        <table mat-table [dataSource]="listings()" table>
          <ng-container matColumnDef="flat">
            <th mat-header-cell *matHeaderCellDef>Flat</th>
            <td mat-cell *matCellDef="let l">{{ l.buildingName }} — {{ l.flatNumber }}</td>
          </ng-container>
          <ng-container matColumnDef="listedBy">
            <th mat-header-cell *matHeaderCellDef>Listed By</th>
            <td mat-cell *matCellDef="let l">{{ l.listedByMemberName }}</td>
          </ng-container>
          <ng-container matColumnDef="askingPrice">
            <th mat-header-cell *matHeaderCellDef>Asking Price</th>
            <td mat-cell *matCellDef="let l">₹{{ l.askingPrice | number }}</td>
          </ng-container>
          <ng-container matColumnDef="availableFrom">
            <th mat-header-cell *matHeaderCellDef>Available From</th>
            <td mat-cell *matCellDef="let l">{{ l.availableFrom | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="noc">
            <th mat-header-cell *matHeaderCellDef>NOC</th>
            <td mat-cell *matCellDef="let l">
              @if (!l.nocRequested) {
                <span class="muted">Not requested</span>
              } @else if (l.nocIssuedDate) {
                <a [href]="l.nocDocumentUrl | assetUrl" target="_blank" class="noc-link"><mat-icon>description</mat-icon> Issued {{ l.nocIssuedDate | date: 'mediumDate' }}</a>
              } @else {
                <span class="badge badge-pending">Pending</span>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let l">
              <mat-chip-set><mat-chip [class]="'status-' + l.status">{{ statusLabels[l.status] }}</mat-chip></mat-chip-set>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let l">
              @if (l.status === 1) {
                <button mat-icon-button matTooltip="Mark Under Negotiation" (click)="markUnderNegotiation(l)"><mat-icon>handshake</mat-icon></button>
              }
              @if (l.status === 1 || l.status === 2) {
                <button mat-icon-button matTooltip="Mark Sold" (click)="markSold(l)"><mat-icon>check_circle</mat-icon></button>
                <button mat-icon-button matTooltip="Withdraw" (click)="withdraw(l)"><mat-icon>cancel</mat-icon></button>
              }
              @if (l.nocRequested && !l.nocIssuedDate) {
                <button mat-icon-button matTooltip="Issue NOC" (click)="issueNoc(l)"><mat-icon>fact_check</mat-icon></button>
              }
              <button mat-icon-button matTooltip="Edit" (click)="edit(l)"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button matTooltip="Delete" (click)="remove(l)"><mat-icon>delete_outline</mat-icon></button>
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
    .toolbar { display: flex; align-items: center; justify-content: space-between; gap: 16px; margin-bottom: 16px; }
    .status-filter { width: 200px; }
    table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .noc-link { display: flex; align-items: center; gap: 4px; font-size: 12px; text-decoration: none; }
    .noc-link mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .badge-pending { background: #fef3c7; color: #b45309; }
  `]
})
export class ResaleListingsListComponent implements OnInit {
  private readonly residentService = inject(ResidentService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly listings = signal<FlatResaleListingDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly displayedColumns = ['flat', 'listedBy', 'askingPrice', 'availableFrom', 'noc', 'status', 'actions'];
  readonly statusLabels: Record<number, string> = LISTING_STATUS_LABELS;
  readonly statusOptions = Object.entries(LISTING_STATUS_LABELS).map(([value, label]) => ({ value: Number(value), label }));

  societyId = 0;
  statusFilter: number | null = null;
  private flatOptions: { value: number; label: string }[] = [];
  private memberOptions: { value: number; label: string }[] = [];

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.societyService.getFlats({ societyId: this.societyId, pageSize: 500 }).subscribe((result) => {
        this.flatOptions = result.items.map((f) => ({ value: f.id, label: f.flatNumber }));
      });
      this.residentService.getMembers({ societyId: this.societyId, pageSize: 500 }).subscribe((result) => {
        this.memberOptions = result.items.map((m) => ({ value: m.id, label: `${m.firstName} ${m.lastName}` }));
      });
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.residentService.getListings({
      societyId: this.societyId, status: this.statusFilter ?? undefined,
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.listings.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  onFilterChange(): void {
    this.pageIndex.set(0);
    this.load();
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  private fields(listing?: FlatResaleListingDto) {
    return [
      { key: 'flatId', label: 'Flat', type: 'select' as const, options: this.flatOptions, defaultValue: listing?.flatId },
      { key: 'listedByMemberId', label: 'Listed By', type: 'select' as const, options: this.memberOptions, defaultValue: listing?.listedByMemberId },
      { key: 'askingPrice', label: 'Asking Price', type: 'number' as const, defaultValue: listing?.askingPrice ?? 0 },
      { key: 'availableFrom', label: 'Available From', type: 'date' as const, defaultValue: listing?.availableFrom?.substring(0, 10) ?? new Date().toISOString().substring(0, 10) },
      { key: 'visitTimingNotes', label: 'Visit Timing Notes', type: 'textarea' as const, required: false, defaultValue: listing?.visitTimingNotes ?? '' },
      { key: 'nocRequested', label: 'NOC Requested from Society', type: 'checkbox' as const, defaultValue: listing?.nocRequested ?? false },
      { key: 'notes', label: 'Notes', type: 'textarea' as const, required: false, defaultValue: listing?.notes ?? '' }
    ];
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '480px', data: { title: 'Add Resale Listing', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.residentService.createListing({
        ...result, flatId: Number(result.flatId), listedByMemberId: Number(result.listedByMemberId),
        askingPrice: Number(result.askingPrice), visitTimingNotes: result.visitTimingNotes || null, notes: result.notes || null
      }).subscribe(() => {
        this.toast.success('Listing created.');
        this.load();
      });
    });
  }

  edit(listing: FlatResaleListingDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px', data: { title: 'Edit Resale Listing', submitLabel: 'Save', fields: this.fields(listing) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.residentService.updateListing(listing.id, {
        askingPrice: Number(result.askingPrice), availableFrom: result.availableFrom,
        visitTimingNotes: result.visitTimingNotes || null, nocRequested: !!result.nocRequested, notes: result.notes || null
      }).subscribe(() => {
        this.toast.success('Listing updated.');
        this.load();
      });
    });
  }

  markUnderNegotiation(listing: FlatResaleListingDto): void {
    this.residentService.markUnderNegotiation(listing.id).subscribe(() => {
      this.toast.success('Listing marked under negotiation.');
      this.load();
    });
  }

  markSold(listing: FlatResaleListingDto): void {
    this.confirmDialog.confirm({
      title: 'Mark as Sold', message: `Mark the listing for flat "${listing.flatNumber}" as sold?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.residentService.markSold(listing.id).subscribe(() => {
        this.toast.success('Listing marked sold.');
        this.load();
      });
    });
  }

  withdraw(listing: FlatResaleListingDto): void {
    this.confirmDialog.confirm({
      title: 'Withdraw Listing', destructive: true, message: `Withdraw the listing for flat "${listing.flatNumber}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.residentService.withdrawListing(listing.id).subscribe(() => {
        this.toast.success('Listing withdrawn.');
        this.load();
      });
    });
  }

  issueNoc(listing: FlatResaleListingDto): void {
    const ref = this.dialog.open(IssueNocDialogComponent, { data: {} });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.residentService.issueNoc(listing.id, result.nocIssuedDate, result.nocDocumentUrl).subscribe(() => {
        this.toast.success('NOC issued.');
        this.load();
      });
    });
  }

  remove(listing: FlatResaleListingDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Listing', destructive: true, message: `Delete the resale listing for flat "${listing.flatNumber}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.residentService.deleteListing(listing.id).subscribe(() => {
        this.toast.success('Listing deleted.');
        this.load();
      });
    });
  }
}
