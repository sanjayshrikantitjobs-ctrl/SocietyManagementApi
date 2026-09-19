import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { Observable } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { SocietyService } from '../society-setup/services/society.service';
import { InventoryItemDto, InventoryService, STOCK_TYPE_LABELS, StockTransactionDto } from './inventory.service';

const today = () => new Date().toISOString().substring(0, 10);
const nullIfEmpty = (value: unknown) => (value === '' || value === undefined ? null : value);

@Component({
  selector: 'app-inventory',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatMenuModule, MatSelectModule, MatTableModule, MatTabsModule,
    DataTableComponent, PageHeaderComponent, StatusBadgeComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Inventory" subtitle="Stock on hand, issues and receipts. Current stock is always the sum of its movements."
        [breadcrumbs]="[{ label: 'Inventory' }]">
        @if (canManage()) {
          <button mat-flat-button color="primary" (click)="openItemForm(null)"><mat-icon>add</mat-icon> New Item</button>
        }
      </app-page-header>

      <mat-tab-group animationDuration="0ms" (selectedTabChange)="onTab($event.index)">
        <mat-tab label="Stock">
          <div class="tab-body">
            <app-data-table
              [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
              searchPlaceholder="Search item or category..." emptyIcon="inventory_2" emptyTitle="No inventory items"
              emptyMessage="Add the items your society keeps in stock (bulbs, cleaning supplies, tools)."
              (page)="onPage($event)" (search)="onSearch($event)">
              <div toolbar>
                <mat-form-field appearance="outline" subscriptSizing="dynamic" class="filter">
                  <mat-select [(ngModel)]="lowOnly" (ngModelChange)="onFilter()">
                    <mat-option [value]="false">All items</mat-option>
                    <mat-option [value]="true">Low stock only</mat-option>
                  </mat-select>
                </mat-form-field>
              </div>
              <table mat-table [dataSource]="items()" table>
                <ng-container matColumnDef="name">
                  <th mat-header-cell *matHeaderCellDef>Item</th>
                  <td mat-cell *matCellDef="let i"><strong>{{ i.name }}</strong>@if (i.category) { <div class="muted">{{ i.category }}</div> }</td>
                </ng-container>
                <ng-container matColumnDef="stock">
                  <th mat-header-cell *matHeaderCellDef>In stock</th>
                  <td mat-cell *matCellDef="let i">{{ i.currentStock | number: '1.0-2' }} {{ i.unit }}</td>
                </ng-container>
                <ng-container matColumnDef="min">
                  <th mat-header-cell *matHeaderCellDef>Minimum</th>
                  <td mat-cell *matCellDef="let i">{{ i.minimumStock ? (i.minimumStock | number: '1.0-2') : '—' }}</td>
                </ng-container>
                <ng-container matColumnDef="status">
                  <th mat-header-cell *matHeaderCellDef>Status</th>
                  <td mat-cell *matCellDef="let i">
                    @if (!i.isActive) { <app-status-badge variant="neutral" label="Inactive" /> }
                    @else if (i.isLow) { <app-status-badge variant="danger" label="Low stock" /> }
                    @else { <app-status-badge variant="success" label="OK" /> }
                  </td>
                </ng-container>
                <ng-container matColumnDef="actions">
                  <th mat-header-cell *matHeaderCellDef></th>
                  <td mat-cell *matCellDef="let i">
                    @if (canManage()) {
                      <button mat-stroked-button (click)="openMove(i, 2)">Issue</button>
                      <button mat-icon-button [matMenuTriggerFor]="menu"><mat-icon>more_vert</mat-icon></button>
                      <mat-menu #menu="matMenu">
                        <button mat-menu-item (click)="openMove(i, 1)"><mat-icon>add_box</mat-icon> Add stock</button>
                        <button mat-menu-item (click)="openMove(i, 3)"><mat-icon>tune</mat-icon> Adjust</button>
                        <button mat-menu-item (click)="openItemForm(i)"><mat-icon>edit</mat-icon> Edit item</button>
                        <button mat-menu-item (click)="removeItem(i)"><mat-icon>delete</mat-icon> Remove</button>
                      </mat-menu>
                    }
                  </td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="itemColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: itemColumns;"></tr>
              </table>
            </app-data-table>
          </div>
        </mat-tab>

        <mat-tab label="Movements">
          <div class="tab-body">
            <app-data-table
              [loading]="txLoading()" [totalCount]="txTotal()" [pageSize]="txPageSize()" [pageIndex]="txPageIndex()"
              [showSearch]="false" emptyIcon="swap_vert" emptyTitle="No stock movements yet"
              emptyMessage="Issuing, receiving and adjusting stock will be listed here."
              (page)="onTxPage($event)">
              <table mat-table [dataSource]="transactions()" table>
                <ng-container matColumnDef="date"><th mat-header-cell *matHeaderCellDef>Date</th><td mat-cell *matCellDef="let t">{{ t.transactionDate | date: 'mediumDate' }}</td></ng-container>
                <ng-container matColumnDef="item"><th mat-header-cell *matHeaderCellDef>Item</th><td mat-cell *matCellDef="let t"><strong>{{ t.itemName }}</strong></td></ng-container>
                <ng-container matColumnDef="type"><th mat-header-cell *matHeaderCellDef>Type</th><td mat-cell *matCellDef="let t">{{ typeLabels[t.type] }}</td></ng-container>
                <ng-container matColumnDef="qty">
                  <th mat-header-cell *matHeaderCellDef>Quantity</th>
                  <td mat-cell *matCellDef="let t"><span [class.neg]="t.quantity < 0">{{ t.quantity > 0 ? '+' : '' }}{{ t.quantity | number: '1.0-2' }} {{ t.unit }}</span></td>
                </ng-container>
                <ng-container matColumnDef="detail">
                  <th mat-header-cell *matHeaderCellDef>Details</th>
                  <td mat-cell *matCellDef="let t">
                    {{ t.issuedTo }}@if (t.issuedTo && t.locationOfUse) { · }{{ t.locationOfUse }}
                    @if (t.notes) { <div class="muted">{{ t.notes }}</div> }
                  </td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="txColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: txColumns;"></tr>
              </table>
            </app-data-table>
          </div>
        </mat-tab>
      </mat-tab-group>
    </div>
  `,
  styles: [`
    .tab-body { padding: 16px 0; }
    table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .filter { width: 170px; }
    .neg { color: #b91c1c; }
  `]
})
export class InventoryComponent implements OnInit {
  private readonly inventoryService = inject(InventoryService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly auth = inject(AuthService);

  readonly typeLabels = STOCK_TYPE_LABELS;
  readonly itemColumns = ['name', 'stock', 'min', 'status', 'actions'];
  readonly txColumns = ['date', 'item', 'type', 'qty', 'detail'];

  readonly loading = signal(true);
  readonly items = signal<InventoryItemDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  lowOnly = false;

  readonly txLoading = signal(false);
  readonly transactions = signal<StockTransactionDto[]>([]);
  readonly txTotal = signal(0);
  readonly txPageIndex = signal(0);
  readonly txPageSize = signal(10);

  private societyId = 0;

  canManage(): boolean { return this.auth.hasPermission('inventory.manage'); }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.loadItems();
    });
  }

  onTab(index: number): void { if (index === 1) this.loadTransactions(); }

  loadItems(): void {
    this.loading.set(true);
    this.inventoryService.getItems({
      societyId: this.societyId, search: this.searchTerm() || undefined, lowStockOnly: this.lowOnly,
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((r) => { this.items.set(r.items); this.totalCount.set(r.totalCount); this.loading.set(false); });
  }

  loadTransactions(): void {
    this.txLoading.set(true);
    this.inventoryService.getTransactions({ societyId: this.societyId, pageNumber: this.txPageIndex() + 1, pageSize: this.txPageSize() })
      .subscribe((r) => { this.transactions.set(r.items); this.txTotal.set(r.totalCount); this.txLoading.set(false); });
  }

  onPage(e: PageEvent): void { this.pageIndex.set(e.pageIndex); this.pageSize.set(e.pageSize); this.loadItems(); }
  onSearch(term: string): void { this.searchTerm.set(term); this.pageIndex.set(0); this.loadItems(); }
  onFilter(): void { this.pageIndex.set(0); this.loadItems(); }
  onTxPage(e: PageEvent): void { this.txPageIndex.set(e.pageIndex); this.txPageSize.set(e.pageSize); this.loadTransactions(); }

  openItemForm(item: InventoryItemDto | null): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '440px',
      data: {
        title: item ? 'Edit Item' : 'New Inventory Item',
        submitLabel: 'Save',
        fields: [
          { key: 'name', label: 'Item name', type: 'text', defaultValue: item?.name ?? '', maxLength: 200 },
          { key: 'category', label: 'Category', type: 'text', required: false, defaultValue: item?.category ?? '', maxLength: 100 },
          { key: 'unit', label: 'Unit (e.g. Units, Litre, Box)', type: 'text', defaultValue: item?.unit ?? 'Units', maxLength: 30 },
          { key: 'minimumStock', label: 'Minimum stock (alerts when at or below)', type: 'number', defaultValue: item?.minimumStock ?? 0 },
          ...(item
            ? [{ key: 'isActive', label: 'Active', type: 'checkbox' as const, defaultValue: item.isActive }]
            : [{ key: 'openingStock', label: 'Opening stock', type: 'number' as const, defaultValue: 0 }])
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      const base = { name: result.name, category: nullIfEmpty(result.category), unit: result.unit, minimumStock: Number(result.minimumStock) };
      const request$: Observable<unknown> = item
        ? this.inventoryService.updateItem(item.id, { ...base, isActive: !!result.isActive })
        : this.inventoryService.createItem({ ...base, societyId: this.societyId, openingStock: Number(result.openingStock) });
      request$.subscribe(() => { this.toast.success(item ? 'Item updated.' : 'Item added.'); this.loadItems(); });
    });
  }

  openMove(item: InventoryItemDto, type: number): void {
    const title = type === 1 ? `Add stock — ${item.name}` : type === 2 ? `Issue — ${item.name}` : `Adjust stock — ${item.name}`;
    const fields: { key: string; label: string; type: 'number' | 'date' | 'text'; required?: boolean; defaultValue?: string | number; hint?: string }[] = [
      {
        key: 'quantity', label: `Quantity (${item.unit})`, type: 'number',
        hint: type === 3 ? 'Use a negative number to reduce stock' : `${item.currentStock} ${item.unit} in stock`
      },
      { key: 'transactionDate', label: 'Date', type: 'date', defaultValue: today() }
    ];
    if (type === 2) {
      fields.push({ key: 'issuedTo', label: 'Issued to', type: 'text', required: false });
      fields.push({ key: 'locationOfUse', label: 'Location of use', type: 'text', required: false });
    }
    fields.push({ key: 'notes', label: 'Notes', type: 'text', required: false });

    const ref = this.dialog.open(PromptDialogComponent, { width: '420px', data: { title, submitLabel: 'Save', fields } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.inventoryService.recordTransaction({
        inventoryItemId: item.id, type, quantity: Number(result.quantity), transactionDate: result.transactionDate,
        issuedTo: nullIfEmpty(result.issuedTo), locationOfUse: nullIfEmpty(result.locationOfUse), notes: nullIfEmpty(result.notes)
      }).subscribe(() => { this.toast.success('Stock updated.'); this.loadItems(); if (this.transactions().length) this.loadTransactions(); });
    });
  }

  removeItem(item: InventoryItemDto): void {
    this.confirmDialog.confirm({ title: 'Remove Item', destructive: true, message: `Remove ${item.name}? Its movement history is kept.` }).subscribe((ok) => {
      if (!ok) return;
      this.inventoryService.deleteItem(item.id).subscribe(() => { this.toast.success('Item removed.'); this.loadItems(); });
    });
  }
}
