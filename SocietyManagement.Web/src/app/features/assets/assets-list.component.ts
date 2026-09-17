import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { map } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { AuthService } from '../../core/services/auth.service';
import { Flat } from '../../core/models/society.model';
import { ToastService } from '../../core/services/toast.service';
import { AssetUrlPipe } from '../../shared/pipes/asset-url.pipe';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { Society } from '../../core/models/society.model';
import { SocietyService } from '../society-setup/services/society.service';
import { AssetFormDialogComponent } from './asset-form-dialog.component';
import { ASSET_CATEGORY_LABELS, ASSET_PRICING_TYPE_LABELS, AssetDto } from './models/asset.model';
import { AssetService } from './services/asset.service';

interface CartLine {
  asset: AssetDto;
  quantity: number;
}

@Component({
  selector: 'app-assets-list',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatDatepickerModule, MatFormFieldModule, MatIconModule, MatInputModule, MatMenuModule, MatSelectModule,
    AssetUrlPipe, EmptyStateComponent, PageHeaderComponent, SkeletonLoaderComponent
  ],
  template: `
    <div class="app-page">
    <app-page-header title="Assets" subtitle="Pick a date range, choose quantities and submit a rental request — Admins can book on behalf of any flat.">
      @if (societies().length > 1) {
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="picker">
          <mat-select [value]="societyId()" (selectionChange)="onSocietyChange($event.value)">
            @for (s of societies(); track s.id) { <mat-option [value]="s.id">{{ s.name }}</mat-option> }
          </mat-select>
        </mat-form-field>
      }
      @if (canManage()) {
        <button mat-flat-button color="primary" (click)="createAsset()"><mat-icon>add</mat-icon> New Asset</button>
      }
    </app-page-header>

    <div class="rent-bar">
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Start Date</mat-label>
        <input matInput [matDatepicker]="startPicker" [value]="startDate()" (dateChange)="onStartDateChange($event.value)" />
        <mat-datepicker-toggle matSuffix [for]="startPicker"></mat-datepicker-toggle>
        <mat-datepicker #startPicker></mat-datepicker>
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>End Date</mat-label>
        <input matInput [matDatepicker]="endPicker" [value]="endDate()" (dateChange)="onEndDateChange($event.value)" />
        <mat-datepicker-toggle matSuffix [for]="endPicker"></mat-datepicker-toggle>
        <mat-datepicker #endPicker></mat-datepicker>
      </mat-form-field>
    </div>

    @if (loading()) {
      <app-skeleton-loader [rows]="4" />
    } @else if (assets().length === 0) {
      <app-empty-state icon="chair" title="No assets yet" message="Add society equipment to make it available for rent."
        [actionLabel]="canManage() ? 'New Asset' : null" (action)="createAsset()" />
    } @else {
      <div class="layout with-cart">
        <div class="grid">
          @for (a of assets(); track a.id) {
            <div class="card" [class.inactive]="!a.isActive">
              <div class="thumb" [style.backgroundImage]="'url(' + (a.imageUrl | assetUrl) + ')'"></div>
              <div class="body">
                <div class="title-row">
                  <span class="title">{{ a.name }}</span>
                  @if (canManage()) {
                    <button mat-icon-button (click)="$event.stopPropagation()" [matMenuTriggerFor]="menu"><mat-icon>more_vert</mat-icon></button>
                    <mat-menu #menu="matMenu">
                      <button mat-menu-item (click)="editAsset(a)">Edit</button>
                      <button mat-menu-item class="danger" (click)="deleteAsset(a)">Delete</button>
                    </mat-menu>
                  }
                </div>
                <div class="meta">{{ categoryLabel(a) }} &middot; {{ a.totalQuantity }} available</div>
                <div class="price">{{ a.rentalPrice | currency: 'INR' }} / {{ pricingLabel(a) }}</div>
                @if (a.isActive) {
                  <div class="qty-row">
                    <input type="number" min="0" [max]="a.totalQuantity" [value]="cartQuantity(a.id)" (change)="setCartQuantity(a, $event)" />
                    <button mat-stroked-button (click)="addToCart(a)">Add</button>
                  </div>
                }
                @if (!a.isActive) { <span class="badge">Inactive</span> }
              </div>
            </div>
          }
        </div>

        <div class="cart">
          <h3>{{ canManage() ? 'Book a Rental For a Flat' : 'Your Rental Request' }}</h3>
          @if (canManage() || flats().length > 1) {
            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="span">
              <mat-label>{{ canManage() ? 'Rent For Flat' : 'Flat' }}</mat-label>
              <mat-select [value]="selectedFlatId()" (selectionChange)="selectedFlatId.set($event.value)">
                @for (f of flats(); track f.id) { <mat-option [value]="f.id">{{ f.flatNumber }}</mat-option> }
              </mat-select>
            </mat-form-field>
          }
          @if (cartLines().length === 0) {
            <p class="empty">No items added yet.</p>
          } @else {
            @for (line of cartLines(); track line.asset.id) {
              <div class="cart-line">
                <span>{{ line.quantity }} &times; {{ line.asset.name }}</span>
                <span>{{ estimateLine(line) | currency: 'INR' }}</span>
                <button mat-icon-button (click)="removeFromCart(line.asset.id)"><mat-icon>close</mat-icon></button>
              </div>
            }
            <div class="cart-total">Estimated Total: {{ estimateTotal() | currency: 'INR' }}</div>
            <p class="hint">Final total is calculated and confirmed after you submit.</p>
            <button mat-flat-button color="primary" class="span" [disabled]="!canSubmit()" (click)="submitRequest()">Submit Rental Request</button>
          }
        </div>
      </div>
    }
    </div>
  `,
  styles: [`
    .picker { width: 200px; margin-right: 8px; }
    .rent-bar { display: flex; gap: 12px; margin-bottom: 16px; }
    .layout { display: grid; grid-template-columns: 1fr; gap: 24px; }
    .layout.with-cart { grid-template-columns: 2fr 1fr; align-items: start; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 16px; }
    .card { border-radius: 10px; overflow: hidden; border: 1px solid var(--app-border); background: var(--app-surface); }
    .card.inactive { opacity: .6; }
    .thumb { height: 100px; background-size: cover; background-position: center; background-color: var(--app-primary-light); }
    .body { padding: 12px; }
    .title-row { display: flex; align-items: center; justify-content: space-between; }
    .title { font-weight: 600; font-size: 14px; }
    .meta { font-size: 12px; color: var(--app-text-muted); margin-top: 4px; }
    .price { font-size: 13px; font-weight: 600; margin-top: 4px; }
    .qty-row { display: flex; gap: 8px; margin-top: 8px; align-items: center; }
    .qty-row input { width: 60px; padding: 6px; border: 1px solid var(--app-border); border-radius: 4px; }
    .badge { display: inline-block; margin-top: 6px; font-size: 11px; padding: 2px 8px; border-radius: 10px; background: #f0f0f0; color: #888; }
    .danger { color: #c0392b; }
    .cart { position: sticky; top: 16px; border: 1px solid var(--app-border); border-radius: 10px; padding: 16px; background: var(--app-surface); }
    .cart h3 { margin: 0 0 12px; font-size: 15px; }
    .cart .span { width: 100%; margin-bottom: 12px; }
    .cart-line { display: flex; align-items: center; gap: 6px; font-size: 13px; padding: 4px 0; }
    .cart-line span:first-child { flex: 1; }
    .cart-total { font-weight: 700; margin-top: 8px; border-top: 1px solid var(--app-border); padding-top: 8px; }
    .hint { font-size: 12px; color: var(--app-text-muted); margin: 4px 0 12px; }
    .empty { color: var(--app-text-muted); font-size: 13px; }
  `]
})
export class AssetsListComponent implements OnInit {
  private readonly assetService = inject(AssetService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly societies = signal<Society[]>([]);
  readonly societyId = signal(0);
  readonly assets = signal<AssetDto[]>([]);
  readonly flats = signal<Flat[]>([]);
  readonly selectedFlatId = signal(0);

  readonly startDate = signal(new Date());
  readonly endDate = signal(new Date());
  readonly cart = signal<Map<number, CartLine>>(new Map());
  readonly cartLines = computed(() => Array.from(this.cart().values()));

  canManage(): boolean { return this.auth.hasPermission('assets.manage'); }
  categoryLabel(a: AssetDto): string { return ASSET_CATEGORY_LABELS[a.category]; }
  pricingLabel(a: AssetDto): string { return ASSET_PRICING_TYPE_LABELS[a.pricingType]; }

  private days(): number {
    const ms = this.endDate().setHours(0, 0, 0, 0) - this.startDate().setHours(0, 0, 0, 0);
    return Math.max(Math.round(ms / 86400000) + 1, 1);
  }

  estimateLine(line: CartLine): number {
    const multiplier = line.asset.pricingType === 1 || line.asset.pricingType === 4 ? 1 : this.days();
    return line.asset.rentalPrice * line.quantity * multiplier;
  }
  estimateTotal(): number {
    return this.cartLines().reduce((sum, line) => sum + this.estimateLine(line), 0);
  }
  cartQuantity(assetId: number): number {
    return this.cart().get(assetId)?.quantity ?? 0;
  }
  canSubmit(): boolean {
    return this.cartLines().length > 0 && this.selectedFlatId() > 0;
  }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      this.societies.set(societies);
      if (societies.length > 0) {
        this.societyId.set(societies[0].id);
        this.load();
        this.loadFlats();
      } else {
        this.loading.set(false);
      }
    });
  }

  // Admin/SuperAdmin can rent on behalf of any flat in the society — the
  // backend already allows this (CreateAssetBookingCommandHandler only
  // enforces "must be a current resident of this flat" when the caller
  // lacks assets.manage); a resident only ever sees their own flat(s).
  private loadFlats(): void {
    const flats$ = this.canManage()
      ? this.societyService.getFlats({ societyId: this.societyId(), pageSize: 500 }).pipe(map((r) => r.items))
      : this.societyService.getMyFlats();

    flats$.subscribe((flats) => {
      this.flats.set(flats);
      if (flats.length > 0) this.selectedFlatId.set(flats[0].id);
    });
  }

  onSocietyChange(id: number): void { this.societyId.set(id); this.load(); this.loadFlats(); }
  onStartDateChange(date: Date | null): void { if (date) this.startDate.set(date); }
  onEndDateChange(date: Date | null): void { if (date) this.endDate.set(date); }

  load(): void {
    this.loading.set(true);
    this.assetService.getAssets(this.societyId(), !this.canManage()).subscribe((assets) => {
      this.assets.set(assets);
      this.loading.set(false);
    });
  }

  setCartQuantity(asset: AssetDto, event: Event): void {
    const value = Number((event.target as HTMLInputElement).value) || 0;
    const map = new Map(this.cart());
    if (value <= 0) map.delete(asset.id);
    else map.set(asset.id, { asset, quantity: value });
    this.cart.set(map);
  }

  addToCart(asset: AssetDto): void {
    const current = this.cart().get(asset.id);
    if (current) return; // already set via the quantity input directly
    const map = new Map(this.cart());
    map.set(asset.id, { asset, quantity: 1 });
    this.cart.set(map);
  }

  removeFromCart(assetId: number): void {
    const map = new Map(this.cart());
    map.delete(assetId);
    this.cart.set(map);
  }

  createAsset(): void {
    const ref = this.dialog.open(AssetFormDialogComponent, { width: '660px', data: { societyId: this.societyId(), asset: null } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.assetService.createAsset(result).subscribe(() => {
        this.toast.success('Asset created.');
        this.load();
      });
    });
  }

  editAsset(a: AssetDto): void {
    const ref = this.dialog.open(AssetFormDialogComponent, { width: '660px', data: { societyId: this.societyId(), asset: a } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.assetService.updateAsset(a.id, result).subscribe(() => {
        this.toast.success('Asset updated.');
        this.load();
      });
    });
  }

  deleteAsset(a: AssetDto): void {
    this.confirmDialog.confirm({ title: 'Delete Asset', destructive: true, message: `Delete "${a.name}"?` }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.assetService.deleteAsset(a.id).subscribe(() => {
        this.toast.success('Asset deleted.');
        this.load();
      });
    });
  }

  submitRequest(): void {
    if (!this.canSubmit()) return;
    const toDateOnly = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    const payload = {
      flatId: this.selectedFlatId(),
      startDate: toDateOnly(this.startDate()),
      endDate: toDateOnly(this.endDate()),
      notes: null,
      facilityBookingId: null,
      items: this.cartLines().map((line) => ({ assetId: line.asset.id, quantity: line.quantity }))
    };
    this.assetService.createBooking(payload).subscribe({
      next: () => {
        this.toast.success('Rental request submitted.');
        this.cart.set(new Map());
        // An admin booking on behalf of another flat has no stake in their
        // own "My Rentals" — send them to the admin list where the new
        // request actually shows up instead.
        this.router.navigate([this.canManage() ? '/asset-rentals' : '/my-rentals']);
      },
      error: (err) => this.toast.error(err?.error?.message || 'Could not submit the rental request.')
    });
  }
}
