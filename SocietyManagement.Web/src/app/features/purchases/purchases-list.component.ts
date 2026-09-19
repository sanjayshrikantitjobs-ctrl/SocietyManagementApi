import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { FilterBarComponent, FilterBarOption } from '../../shared/components/filter-bar/filter-bar.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { InventoryService } from '../inventory/inventory.service';
import { SocietyService } from '../society-setup/services/society.service';
import { VendorService } from '../vendors/vendors.service';
import { PurchaseFormDialogComponent, PurchaseFormData, PurchaseFormResult } from './purchase-form-dialog.component';
import {
  PURCHASE_PRIORITY_LABELS, PURCHASE_STATUS, PURCHASE_STATUS_LABELS, PurchaseRequestDto, PurchaseService
} from './purchases.service';

const S = PURCHASE_STATUS;
const STATUS_VARIANT: Record<number, StatusBadgeVariant> = {
  1: 'neutral', 2: 'warning', 3: 'info', 4: 'danger', 5: 'info', 6: 'warning', 7: 'success', 8: 'neutral'
};
const STATUS_FILTERS: FilterBarOption[] = [
  { value: 'all', label: 'All' },
  ...Object.entries(PURCHASE_STATUS_LABELS).map(([value, label]) => ({ value, label }))
];

@Component({
  selector: 'app-purchases-list',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatIconModule, MatMenuModule, MatTableModule, DataTableComponent, FilterBarComponent,
    PageHeaderComponent, StatusBadgeComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Purchase Requests" subtitle="Request, approve, order and receive what the society needs."
        [breadcrumbs]="[{ label: 'Purchases' }]">
        @if (canManage()) {
          <button mat-flat-button color="primary" (click)="openForm(null)"><mat-icon>add</mat-icon> New Request</button>
        }
      </app-page-header>

      <app-filter-bar class="filters" [options]="filters" [selected]="statusFilter()" (selectedChange)="onFilter($event)" />

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search title or requester..." emptyIcon="shopping_cart" emptyTitle="No purchase requests"
        emptyMessage="Raise a request when the society needs to buy something."
        (page)="onPage($event)" (search)="onSearch($event)">
        <table mat-table [dataSource]="rows()" table>
          <ng-container matColumnDef="title">
            <th mat-header-cell *matHeaderCellDef>Request</th>
            <td mat-cell *matCellDef="let p">
              <a class="link" (click)="toggle(p.id)"><strong>#{{ p.id }} · {{ p.title }}</strong></a>
              <div class="muted">{{ p.requestedByName }} · {{ p.createdAt | date: 'mediumDate' }}</div>
              @if (expanded() === p.id) {
                <div class="detail">
                  @if (p.description) { <p>{{ p.description }}</p> }
                  @if (p.status === ${S.Rejected} && p.rejectionReason) { <p class="reject">Rejected: {{ p.rejectionReason }}</p> }
                  <table class="items">
                    <tr><th>Item</th><th>Qty</th><th>Est. price</th><th>Received</th></tr>
                    @for (i of p.items; track i.id) {
                      <tr>
                        <td>{{ i.itemName }}</td>
                        <td>{{ i.quantity | number: '1.0-2' }} {{ i.unit }}</td>
                        <td>{{ i.estimatedUnitPrice | currency: 'INR' : 'symbol' : '1.0-0' }}</td>
                        <td>{{ i.receivedQuantity | number: '1.0-2' }}</td>
                      </tr>
                    }
                  </table>
                </div>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="priority">
            <th mat-header-cell *matHeaderCellDef>Priority</th>
            <td mat-cell *matCellDef="let p"><app-status-badge [variant]="p.priority === 3 ? 'danger' : p.priority === 1 ? 'neutral' : 'info'" [label]="priorityLabels[p.priority]" /></td>
          </ng-container>
          <ng-container matColumnDef="vendor">
            <th mat-header-cell *matHeaderCellDef>Vendor</th>
            <td mat-cell *matCellDef="let p">{{ p.vendorName || '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="total">
            <th mat-header-cell *matHeaderCellDef>Estimate</th>
            <td mat-cell *matCellDef="let p">{{ p.estimatedTotal | currency: 'INR' : 'symbol' : '1.0-0' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let p"><app-status-badge [variant]="statusVariant[p.status]" [label]="statusLabels[p.status]" /></td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let p">
              @if (hasActions(p)) {
                <button mat-icon-button [matMenuTriggerFor]="menu"><mat-icon>more_vert</mat-icon></button>
                <mat-menu #menu="matMenu">
                  @if (canManage() && (p.status === ${S.Draft} || p.status === ${S.Rejected})) {
                    <button mat-menu-item (click)="openForm(p)"><mat-icon>edit</mat-icon> Edit</button>
                  }
                  @if (canManage() && p.status === ${S.Draft}) {
                    <button mat-menu-item (click)="submit(p)"><mat-icon>send</mat-icon> Submit for approval</button>
                  }
                  @if (canApprove() && p.status === ${S.PendingApproval}) {
                    <button mat-menu-item (click)="approve(p)"><mat-icon>check_circle</mat-icon> Approve</button>
                    <button mat-menu-item (click)="reject(p)"><mat-icon>cancel</mat-icon> Reject</button>
                  }
                  @if (canManage() && p.status === ${S.Approved}) {
                    <button mat-menu-item (click)="order(p)"><mat-icon>local_shipping</mat-icon> Place order</button>
                  }
                  @if (canManage() && (p.status === ${S.Ordered} || p.status === ${S.PartiallyReceived})) {
                    <button mat-menu-item (click)="receive(p)"><mat-icon>inventory</mat-icon> Receive goods</button>
                  }
                  @if (canManage() && p.status !== ${S.Received} && p.status !== ${S.Cancelled} && p.status !== ${S.PartiallyReceived}) {
                    <button mat-menu-item (click)="cancel(p)"><mat-icon>block</mat-icon> Cancel request</button>
                  }
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
    .filters { display: block; margin-bottom: 12px; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .link { cursor: pointer; }
    .detail { margin-top: 8px; padding: 10px 12px; background: var(--app-surface-alt); border-radius: 8px; font-size: 13px; }
    .detail p { margin: 0 0 8px; }
    .reject { color: #b91c1c; }
    .items { width: 100%; border-collapse: collapse; }
    .items th, .items td { text-align: left; padding: 4px 8px 4px 0; font-size: 12px; }
  `]
})
export class PurchasesListComponent implements OnInit {
  private readonly purchaseService = inject(PurchaseService);
  private readonly societyService = inject(SocietyService);
  private readonly vendorService = inject(VendorService);
  private readonly inventoryService = inject(InventoryService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly auth = inject(AuthService);

  readonly filters = STATUS_FILTERS;
  readonly statusLabels = PURCHASE_STATUS_LABELS;
  readonly statusVariant = STATUS_VARIANT;
  readonly priorityLabels = PURCHASE_PRIORITY_LABELS;
  readonly columns = ['title', 'priority', 'vendor', 'total', 'status', 'actions'];

  readonly loading = signal(true);
  readonly rows = signal<PurchaseRequestDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly statusFilter = signal<string>('all');
  readonly expanded = signal<number | null>(null);

  private societyId = 0;

  canManage(): boolean { return this.auth.hasPermission('purchases.manage'); }
  canApprove(): boolean { return this.auth.hasPermission('purchases.approve'); }
  hasActions(p: PurchaseRequestDto): boolean {
    if (this.canApprove() && p.status === S.PendingApproval) return true;
    return this.canManage() && p.status !== S.Received && p.status !== S.Cancelled;
  }
  toggle(id: number): void { this.expanded.set(this.expanded() === id ? null : id); }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    const status = this.statusFilter();
    this.purchaseService.getAll({
      societyId: this.societyId, search: this.searchTerm() || undefined, status: status === 'all' ? undefined : Number(status),
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((r) => { this.rows.set(r.items); this.totalCount.set(r.totalCount); this.loading.set(false); });
  }

  onPage(e: PageEvent): void { this.pageIndex.set(e.pageIndex); this.pageSize.set(e.pageSize); this.load(); }
  onSearch(term: string): void { this.searchTerm.set(term); this.pageIndex.set(0); this.load(); }
  onFilter(value: string): void { this.statusFilter.set(value); this.pageIndex.set(0); this.load(); }

  private run(action: () => import('rxjs').Observable<unknown>, message: string): void {
    action().subscribe(() => { this.toast.success(message); this.load(); });
  }

  // Vendor / inventory pickers are optional — a role without those
  // permissions still gets a working form, just without the pickers.
  private loadPickers(cb: (vendors: { id: number; name: string }[], items: { id: number; name: string; unit: string }[]) => void): void {
    this.vendorService.getVendors({ societyId: this.societyId, isActive: true, pageSize: 100 })
      .pipe(catchError(() => of({ items: [] })))
      .subscribe((v) => {
        this.inventoryService.getItems({ societyId: this.societyId, activeOnly: true, pageSize: 100 })
          .pipe(catchError(() => of({ items: [] })))
          .subscribe((i) => cb(
            v.items.map((x) => ({ id: x.id, name: x.name })),
            i.items.map((x) => ({ id: x.id, name: x.name, unit: x.unit }))));
      });
  }

  openForm(request: PurchaseRequestDto | null): void {
    this.loadPickers((vendors, inventoryItems) => {
      const data: PurchaseFormData = { request, vendors, inventoryItems };
      this.dialog.open(PurchaseFormDialogComponent, { data, maxWidth: '95vw' }).afterClosed().subscribe((result: PurchaseFormResult | undefined) => {
        if (!result) return;
        if (request) {
          const { submit, ...payload } = result;
          this.purchaseService.update(request.id, payload).subscribe(() => {
            const done = () => { this.toast.success(submit ? 'Submitted for approval.' : 'Request saved.'); this.load(); };
            if (submit) this.purchaseService.submit(request.id).subscribe(done); else done();
          });
        } else {
          this.purchaseService.create({ ...result, societyId: this.societyId }).subscribe(() => {
            this.toast.success(result.submit ? 'Submitted for approval.' : 'Request saved as draft.');
            this.load();
          });
        }
      });
    });
  }

  submit(p: PurchaseRequestDto): void { this.run(() => this.purchaseService.submit(p.id), 'Submitted for approval.'); }
  approve(p: PurchaseRequestDto): void { this.run(() => this.purchaseService.approve(p.id), 'Request approved.'); }

  reject(p: PurchaseRequestDto): void {
    this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: { title: `Reject #${p.id}`, submitLabel: 'Reject', fields: [{ key: 'reason', label: 'Reason', type: 'textarea', maxLength: 500 }] }
    }).afterClosed().subscribe((r) => { if (r) this.run(() => this.purchaseService.reject(p.id, r.reason), 'Request rejected.'); });
  }

  order(p: PurchaseRequestDto): void {
    this.loadPickers((vendors) => {
      this.dialog.open(PromptDialogComponent, {
        width: '420px',
        data: {
          title: `Place order — #${p.id}`, submitLabel: 'Place order',
          fields: [{ key: 'vendorId', label: 'Vendor', type: 'select', options: vendors.map((v) => ({ value: v.id, label: v.name })), defaultValue: p.vendorId ?? '' }]
        }
      }).afterClosed().subscribe((r) => { if (r) this.run(() => this.purchaseService.order(p.id, Number(r.vendorId)), 'Order placed.'); });
    });
  }

  receive(p: PurchaseRequestDto): void {
    const open = p.items.filter((i) => i.receivedQuantity < i.quantity);
    this.dialog.open(PromptDialogComponent, {
      width: '440px',
      data: {
        title: `Receive goods — #${p.id}`, submitLabel: 'Receive',
        fields: open.map((i) => ({
          key: `line_${i.id}`, label: `${i.itemName} (${i.quantity - i.receivedQuantity} ${i.unit} outstanding)`, type: 'number' as const,
          required: false, defaultValue: i.quantity - i.receivedQuantity,
          hint: i.inventoryItemId ? 'Adds to stock' : 'Not tracked in inventory'
        }))
      }
    }).afterClosed().subscribe((r) => {
      if (!r) return;
      const lines = open.map((i) => ({ itemId: i.id, quantity: Number(r[`line_${i.id}`] || 0) })).filter((l) => l.quantity > 0);
      if (lines.length === 0) return;
      this.run(() => this.purchaseService.receive(p.id, lines), 'Goods received.');
    });
  }

  cancel(p: PurchaseRequestDto): void {
    this.confirmDialog.confirm({ title: 'Cancel Request', destructive: true, message: `Cancel "${p.title}"?` }).subscribe((ok) => {
      if (ok) this.run(() => this.purchaseService.cancel(p.id), 'Request cancelled.');
    });
  }
}
