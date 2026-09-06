import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Observable, of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { ToastService } from '../../../core/services/toast.service';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { PERSON_RELATIONSHIP_LABELS, PersonRelationship } from '../../occupancy/models/occupancy.model';
import { SocietyService } from '../../society-setup/services/society.service';
import {
  DISTRIBUTION_CLAIM_STATUS_LABELS, DISTRIBUTION_ELIGIBILITY_TYPE_LABELS, DISTRIBUTION_STATUS_LABELS,
  DistributionClaimDto, FestivalDistributionDetailDto, FlatMemberOptionDto
} from '../models/festival.model';
import { FestivalService } from '../services/festival.service';

const NEW_MEMBER_OPTION = '__new_member__';

interface ClaimFlatGroup {
  flatId: number;
  flatNumber: string;
  claims: DistributionClaimDto[];
  allDistributed: boolean;
  hasDistributed: boolean;
}

export interface FestivalDistributionDetailDialogData {
  distributionId: number;
  societyId: number;
  canManage: boolean;
  canSelect: boolean;
}

/** The full dashboard + claim machinery for one distribution — eligibility
 * generation, variant management, the admin claims table, and (if the
 * signed-in resident has claims of their own) a self-service "My Items"
 * section for picking a variant/size. Closes with `true` if anything
 * changed, so the tab's own list reloads its rollup counts. */
@Component({
  selector: 'app-festival-distribution-detail-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatChipsModule, MatDialogModule, MatFormFieldModule, MatIconModule,
    MatInputModule, MatMenuModule, MatPaginatorModule, MatProgressSpinnerModule, MatSelectModule, MatTableModule
  ],
  template: `
    <h2 mat-dialog-title class="title-row">
      <span>🎁 {{ distribution()?.itemName }}</span>
      @if (distribution(); as d) {
        <mat-chip-set><mat-chip [class]="'status-' + d.status">{{ statusLabels[d.status] }}</mat-chip></mat-chip-set>
      }
      @if (data.canManage) {
        <button mat-icon-button (click)="editDistribution()" matTooltip="Edit"><mat-icon>edit</mat-icon></button>
      }
    </h2>

    <mat-dialog-content class="content">
      @if (loading()) {
        <div class="loading"><mat-spinner diameter="32" /></div>
      } @else if (distribution(); as d) {
        <p class="description">{{ d.description || 'No description.' }}</p>
        <p class="eligibility">
          <mat-icon inline>info</mat-icon>
          @switch (d.eligibilityType) {
            @case (2) { <span>Flats that have partially or fully paid their contribution are eligible.</span> }
            @case (3) { <span>Flats that have contributed ₹{{ d.eligibilityMinContribution | number }}+ toward this festival are eligible.</span> }
            @default { <span>Every flat is eligible.</span> }
          }
          {{ d.quantityPerFlat }} unit(s) per eligible flat.
        </p>

        <div class="stats">
          <div class="app-card stat"><span class="value">{{ d.eligibleFlatsCount }}</span><span class="label">Eligible Flats</span></div>
          <div class="app-card stat"><span class="value">{{ d.totalQuantity }}</span><span class="label">Total Quantity</span></div>
          <div class="app-card stat"><span class="value pending">{{ d.pendingCount }}</span><span class="label">Pending</span></div>
          <div class="app-card stat"><span class="value confirmed">{{ d.confirmedCount }}</span><span class="label">Confirmed</span></div>
          <div class="app-card stat"><span class="value distributed">{{ d.distributedCount }}</span><span class="label">Distributed</span></div>
        </div>

        @if (data.canManage) {
          <div class="section-header">
            <h3>Variants / Sizes</h3>
            <button mat-stroked-button (click)="addVariant()"><mat-icon>add</mat-icon> Add Variant</button>
          </div>
          <div class="variant-chips">
            @if (d.variants.length === 0) {
              <span class="muted">No variants — residents just claim their slot, no size choice.</span>
            }
            @for (v of d.variants; track v.id) {
              <mat-chip (removed)="removeVariant(v.id)">{{ v.label }}<button matChipRemove><mat-icon>cancel</mat-icon></button></mat-chip>
            }
          </div>

          <div class="section-header">
            <h3>Requirement by Variant</h3>
          </div>
          <table mat-table [dataSource]="d.variantBreakdown" class="app-card breakdown-table">
            <ng-container matColumnDef="variant">
              <th mat-header-cell *matHeaderCellDef>Variant</th>
              <td mat-cell *matCellDef="let b">{{ b.variantLabel }}</td>
            </ng-container>
            <ng-container matColumnDef="pending">
              <th mat-header-cell *matHeaderCellDef>Pending</th>
              <td mat-cell *matCellDef="let b">{{ b.pendingCount }}</td>
            </ng-container>
            <ng-container matColumnDef="confirmed">
              <th mat-header-cell *matHeaderCellDef>Confirmed</th>
              <td mat-cell *matCellDef="let b">{{ b.confirmedCount }}</td>
            </ng-container>
            <ng-container matColumnDef="distributed">
              <th mat-header-cell *matHeaderCellDef>Distributed</th>
              <td mat-cell *matCellDef="let b">{{ b.distributedCount }}</td>
            </ng-container>
            <ng-container matColumnDef="total">
              <th mat-header-cell *matHeaderCellDef>Total</th>
              <td mat-cell *matCellDef="let b"><strong>{{ b.totalCount }}</strong></td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="breakdownColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: breakdownColumns;"></tr>
          </table>

          <div class="section-header">
            <h3>Eligible Flats & Claims ({{ groupedClaims().length }} flats, {{ filteredClaims().length }} items)</h3>
            <div class="header-actions">
              <button mat-stroked-button (click)="addFlat()"><mat-icon>add_home</mat-icon> Add Flat</button>
              <button mat-flat-button color="primary" (click)="generateEligibility()"><mat-icon>bolt</mat-icon> Generate Eligibility</button>
            </div>
          </div>
          <div class="filters">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Search flat</mat-label>
              <input matInput [ngModel]="flatSearch()" (ngModelChange)="onSearchChange($event)" />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Status</mat-label>
              <mat-select [ngModel]="statusFilter()" (ngModelChange)="onStatusChange($event)">
                <mat-option [value]="null">All</mat-option>
                <mat-option [value]="1">Pending</mat-option>
                <mat-option [value]="2">Confirmed</mat-option>
                <mat-option [value]="3">Distributed</mat-option>
              </mat-select>
            </mat-form-field>
          </div>

          @if (claims().length === 0) {
            <p class="muted">No claims yet — click "Generate Eligibility" once flats qualify, or "Add Flat" to add one manually.</p>
          } @else if (groupedClaims().length === 0) {
            <p class="muted">No flats match this search/filter.</p>
          } @else {
            <div class="flat-groups">
              @for (group of pagedGroups(); track group.flatId) {
                <div class="app-card flat-group">
                  <div class="flat-group-header">
                    <strong>Flat {{ group.flatNumber }}</strong>
                    <span class="muted">{{ group.claims.length }} item(s)</span>
                    <div class="flat-group-actions">
                      <button mat-stroked-button (click)="addExtra(group)">
                        <mat-icon>add_shopping_cart</mat-icon> Add Extra (Paid)
                      </button>
                      @if (!group.allDistributed) {
                        <button mat-stroked-button (click)="markFlatDistributed(group)">
                          <mat-icon>done_all</mat-icon> Mark All Distributed
                        </button>
                      }
                      @if (!group.hasDistributed) {
                        <button mat-stroked-button color="warn" (click)="removeFlat(group)">
                          <mat-icon>block</mat-icon> Not Qualified
                        </button>
                      }
                    </div>
                  </div>
                  <div class="slot-row" *ngFor="let c of group.claims">
                    <span class="slot-number">#{{ c.slotNumber }}</span>
                    <span class="slot-member">{{ c.memberName || 'Unassigned' }}</span>
                    <span class="slot-variant">{{ c.variantLabel || '—' }}</span>
                    @if (c.amount) { <span class="slot-amount">+₹{{ c.amount | number }}</span> }
                    <mat-chip-set><mat-chip [class]="'status-' + c.status">{{ claimStatusLabels[c.status] }}</mat-chip></mat-chip-set>
                    <button mat-icon-button [matMenuTriggerFor]="menu"><mat-icon>more_vert</mat-icon></button>
                    <mat-menu #menu="matMenu">
                      <button mat-menu-item (click)="assignMember(c)"><mat-icon>person</mat-icon><span>Assign Member</span></button>
                      <button mat-menu-item (click)="selectVariant(c, true)"><mat-icon>checklist</mat-icon><span>Select Variant</span></button>
                      @if (c.status !== 3) {
                        <button mat-menu-item (click)="markDistributed(c)"><mat-icon>done_all</mat-icon><span>Mark as Distributed</span></button>
                      }
                    </mat-menu>
                  </div>
                </div>
              }
            </div>
            <mat-paginator
              [length]="groupedClaims().length" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
              [pageSizeOptions]="[10, 20, 50]" (page)="onPageChange($event)">
            </mat-paginator>
          }
        }

        @if (myClaims().length > 0) {
          <div class="section-header">
            <h3>My Items</h3>
          </div>
          <div class="my-claims">
            @for (c of myClaims(); track c.id) {
              <div class="app-card my-claim-row">
                <div>
                  <strong>Flat {{ c.flatNumber }} — Slot #{{ c.slotNumber }}</strong>
                  <div class="muted">{{ c.memberName ? 'For ' + c.memberName : 'Not yet assigned to a member' }}
                    @if (c.variantLabel) { · {{ c.variantLabel }} }
                  </div>
                </div>
                <div class="my-claim-actions">
                  <mat-chip-set><mat-chip [class]="'status-' + c.status">{{ claimStatusLabels[c.status] }}</mat-chip></mat-chip-set>
                  @if (c.status !== 3) {
                    <button mat-stroked-button (click)="selectVariant(c, false)">{{ c.status === 1 ? 'Select' : 'Change' }}</button>
                  }
                </div>
              </div>
            }
          </div>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="close()">Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .title-row { display: flex; align-items: center; gap: 10px; padding: 0 24px; }
    .title-row span:first-child { flex: 1; }
    .content { min-width: 560px; max-width: 100%; }
    .loading { display: flex; justify-content: center; padding: 40px; }
    .description { color: var(--app-text-muted); font-size: 13px; margin: 4px 0; }
    .eligibility { display: flex; align-items: center; gap: 6px; font-size: 12px; color: var(--app-text-muted); margin-bottom: 16px; }
    .stats { display: grid; grid-template-columns: repeat(5, 1fr); gap: 10px; margin-bottom: 20px; }
    .stats .stat { padding: 10px; text-align: center; }
    .stats .value { display: block; font-size: 18px; font-weight: 700; }
    .stats .value.pending { color: #b45309; }
    .stats .value.confirmed { color: #1d4ed8; }
    .stats .value.distributed { color: #15803d; }
    .stats .label { font-size: 10px; color: var(--app-text-muted); text-transform: uppercase; }
    .section-header { display: flex; align-items: center; justify-content: space-between; margin: 20px 0 10px; }
    .section-header h3 { margin: 0; font-size: 13px; }
    .header-actions { display: flex; gap: 8px; }
    .variant-chips { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 8px; }
    .filters { display: flex; gap: 12px; margin-bottom: 10px; }
    table { width: 100%; margin-bottom: 8px; }
    .breakdown-table { margin-bottom: 4px; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .flat-groups { display: flex; flex-direction: column; gap: 10px; margin-bottom: 8px; }
    .flat-group { padding: 12px 16px; }
    .flat-group-header { display: flex; align-items: center; gap: 10px; margin-bottom: 8px; }
    .flat-group-header strong { font-size: 13px; }
    .flat-group-actions { display: flex; gap: 8px; margin-left: auto; }
    .slot-row { display: flex; align-items: center; gap: 12px; padding: 6px 0; border-top: 1px solid var(--app-border-color, #e2e8f0); font-size: 12px; }
    .slot-row:first-of-type { border-top: none; }
    .slot-number { color: var(--app-text-muted); min-width: 32px; }
    .slot-member { flex: 1; }
    .slot-variant { color: var(--app-text-muted); min-width: 60px; }
    .slot-amount { color: #b45309; font-weight: 600; min-width: 60px; }
    .my-claims { display: flex; flex-direction: column; gap: 8px; }
    .my-claim-row { display: flex; align-items: center; justify-content: space-between; padding: 12px 16px; }
    .my-claim-actions { display: flex; align-items: center; gap: 10px; }
    .status-1 { background: #fef3c7; color: #b45309; }
    .status-2 { background: #dbeafe; color: #1d4ed8; }
    .status-3 { background: #dcfce7; color: #15803d; }
  `]
})
export class FestivalDistributionDetailDialogComponent implements OnInit {
  dialogRef = inject(MatDialogRef<FestivalDistributionDetailDialogComponent>);
  data = inject<FestivalDistributionDetailDialogData>(MAT_DIALOG_DATA);
  private readonly festivalService = inject(FestivalService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly distribution = signal<FestivalDistributionDetailDto | null>(null);
  readonly claims = signal<DistributionClaimDto[]>([]);
  readonly myClaims = signal<DistributionClaimDto[]>([]);
  readonly statusLabels: Record<number, string> = DISTRIBUTION_STATUS_LABELS;
  readonly claimStatusLabels: Record<number, string> = DISTRIBUTION_CLAIM_STATUS_LABELS;
  readonly breakdownColumns = ['variant', 'pending', 'confirmed', 'distributed', 'total'];

  readonly flatSearch = signal('');
  readonly statusFilter = signal<number | null>(null);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);

  readonly filteredClaims = computed(() => {
    const search = this.flatSearch().trim().toLowerCase();
    const status = this.statusFilter();
    return this.claims().filter((c) =>
      (!search || c.flatNumber.toLowerCase().includes(search)) && (status === null || c.status === status));
  });

  readonly groupedClaims = computed<ClaimFlatGroup[]>(() => {
    const groups = new Map<number, ClaimFlatGroup>();
    for (const c of this.filteredClaims()) {
      let group = groups.get(c.flatId);
      if (!group) {
        group = { flatId: c.flatId, flatNumber: c.flatNumber, claims: [], allDistributed: true, hasDistributed: false };
        groups.set(c.flatId, group);
      }
      group.claims.push(c);
      if (c.status !== 3) group.allDistributed = false;
      if (c.status === 3) group.hasDistributed = true;
    }
    return Array.from(groups.values()).sort((a, b) => a.flatNumber.localeCompare(b.flatNumber, undefined, { numeric: true }));
  });

  readonly pagedGroups = computed<ClaimFlatGroup[]>(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.groupedClaims().slice(start, start + this.pageSize());
  });

  private changed = false;

  onSearchChange(value: string): void {
    this.flatSearch.set(value);
    this.pageIndex.set(0);
  }

  onStatusChange(value: number | null): void {
    this.statusFilter.set(value);
    this.pageIndex.set(0);
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.festivalService.getDistribution(this.data.distributionId).subscribe((distribution) => {
      this.distribution.set(distribution);
      this.loading.set(false);
    });
    if (this.data.canManage) {
      this.festivalService.getDistributionClaims(this.data.distributionId).subscribe((claims) => this.claims.set(claims));
    }
    if (this.data.canSelect) {
      this.festivalService.getMyDistributionClaims(this.data.distributionId).subscribe((claims) => this.myClaims.set(claims));
    }
  }

  editDistribution(): void {
    const d = this.distribution();
    if (!d) return;
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px',
      data: {
        title: 'Edit Distribution', submitLabel: 'Save',
        fields: [
          { key: 'itemName', label: 'Item Name', type: 'text', defaultValue: d.itemName },
          { key: 'description', label: 'Description (optional)', type: 'textarea', required: false, defaultValue: d.description ?? '' },
          { key: 'quantityPerFlat', label: 'Quantity per Eligible Flat', type: 'number', defaultValue: d.quantityPerFlat },
          {
            key: 'eligibilityType', label: 'Eligibility', type: 'select', defaultValue: d.eligibilityType,
            options: [
              { value: 1, label: 'Every Flat' },
              { value: 2, label: 'Partially or Fully Paid Contribution' },
              { value: 3, label: 'Minimum Contribution Amount' }
            ]
          },
          {
            key: 'eligibilityMinContribution', label: 'Minimum Contribution Amount (₹)', type: 'number', required: false,
            defaultValue: d.eligibilityMinContribution ?? '', hint: 'Only used when Eligibility is "Minimum Contribution Amount" above.'
          }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.updateDistribution(d.id, {
        itemName: result.itemName, description: result.description || null,
        eligibilityType: Number(result.eligibilityType),
        eligibilityMinContribution: result.eligibilityMinContribution ? Number(result.eligibilityMinContribution) : null,
        quantityPerFlat: Number(result.quantityPerFlat)
      }).subscribe(() => {
        this.toast.success('Distribution updated.');
        this.changed = true;
        this.load();
      });
    });
  }

  addFlat(): void {
    this.societyService.getFlats({ societyId: this.data.societyId, pageSize: 500 }).subscribe((result) => {
      const ref = this.dialog.open(PromptDialogComponent, {
        width: '380px',
        data: {
          title: 'Add Flat', submitLabel: 'Add',
          fields: [
            {
              key: 'flatId', label: 'Flat', type: 'select',
              options: result.items.map((f) => ({ value: f.id, label: f.flatNumber }))
            },
            { key: 'quantity', label: 'Quantity', type: 'number', defaultValue: 1 }
          ]
        }
      });
      ref.afterClosed().subscribe((formResult) => {
        if (!formResult) return;
        this.festivalService.addManualDistributionClaim(
          this.data.distributionId, Number(formResult.flatId), Number(formResult.quantity)
        ).subscribe(() => {
          this.toast.success('Flat added.');
          this.changed = true;
          this.load();
        });
      });
    });
  }

  markFlatDistributed(group: ClaimFlatGroup): void {
    this.confirmDialog.confirm({
      title: 'Mark All Distributed', message: `Confirm all ${group.claims.length} item(s) for Flat ${group.flatNumber} have been handed over?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.festivalService.markFlatDistributed(this.data.distributionId, group.flatId).subscribe((count) => {
        this.toast.success(`${count} item(s) marked as distributed.`);
        this.changed = true;
        this.load();
      });
    });
  }

  removeFlat(group: ClaimFlatGroup): void {
    this.confirmDialog.confirm({
      title: 'Not Qualified', destructive: true,
      message: `Remove Flat ${group.flatNumber} from this distribution? Its ${group.claims.length} pending/confirmed slot(s) will be deleted.`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.festivalService.removeDistributionFlat(this.data.distributionId, group.flatId).subscribe({
        next: () => {
          this.toast.success(`Flat ${group.flatNumber} removed.`);
          this.changed = true;
          this.load();
        },
        error: (err) => this.toast.error(err?.error?.message || 'Could not remove that flat.')
      });
    });
  }

  addVariant(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '360px',
      data: { title: 'Add Variant', submitLabel: 'Add', fields: [{ key: 'label', label: 'Label (e.g. L, XL, XXL)', type: 'text' }] }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.addDistributionVariant(this.data.distributionId, result.label).subscribe(() => {
        this.toast.success('Variant added.');
        this.changed = true;
        this.load();
      });
    });
  }

  removeVariant(variantId: number): void {
    this.festivalService.deleteDistributionVariant(variantId).subscribe({
      next: () => {
        this.toast.success('Variant removed.');
        this.changed = true;
        this.load();
      },
      error: (err) => this.toast.error(err?.error?.message || 'Could not remove that variant.')
    });
  }

  generateEligibility(): void {
    this.confirmDialog.confirm({
      title: 'Generate Eligibility',
      message: 'Find every newly-eligible flat and create its claim slots? Flats already processed are skipped.'
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.festivalService.generateDistributionClaims(this.data.distributionId).subscribe((count) => {
        this.toast.success(`${count} slot(s) generated.`);
        this.changed = true;
        this.load();
      });
    });
  }

  /** Opens a small "add a person to this flat's household" form and, on
   * submit, persists them via the real Occupancy model (so they show up
   * everywhere else too — Residents, My Family, future distributions —
   * not just as a one-off tag on this claim). Resolves to null if the user
   * cancelled or the add failed. */
  private promptAddResident(flatId: number, flatNumber: string): Observable<FlatMemberOptionDto | null> {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '360px',
      data: {
        title: `Add Member — Flat ${flatNumber}`, submitLabel: 'Add',
        fields: [
          { key: 'firstName', label: 'First Name', type: 'text' },
          { key: 'lastName', label: 'Last Name', type: 'text' },
          { key: 'phone', label: 'Phone (optional)', type: 'text', required: false },
          {
            key: 'relationship', label: 'Relationship', type: 'select', defaultValue: 8,
            options: Object.entries(PERSON_RELATIONSHIP_LABELS).map(([value, label]) => ({ value: Number(value), label }))
          }
        ]
      }
    });
    return ref.afterClosed().pipe(
      switchMap((result) => {
        if (!result) return of(null);
        return this.festivalService.addFlatResident(
          flatId, result.firstName, result.lastName, result.phone || null, Number(result.relationship) as PersonRelationship
        ).pipe(
          catchError((err) => {
            this.toast.error(err?.error?.message || 'Could not add that member.');
            return of(null);
          })
        );
      })
    );
  }

  private memberOptions(members: FlatMemberOptionDto[], defaultValue: number | null | undefined) {
    return {
      defaultValue: defaultValue ?? '',
      options: [
        { value: '', label: '— Unassigned —' },
        ...members.map((m) => ({ value: m.memberId, label: m.name })),
        { value: NEW_MEMBER_OPTION, label: '+ Add New Member…' }
      ]
    };
  }

  private saveAssignMember(claimId: number, memberId: number | null): void {
    this.festivalService.assignClaimMember(claimId, memberId).subscribe(() => {
      this.toast.success('Assignment updated.');
      this.changed = true;
      this.load();
    });
  }

  assignMember(claim: DistributionClaimDto): void {
    this.festivalService.getFlatMembersForDistribution(claim.flatId).subscribe((members) => {
      const memberField = this.memberOptions(members, claim.memberId);
      const ref = this.dialog.open(PromptDialogComponent, {
        width: '360px',
        data: {
          title: `Assign Member — Flat ${claim.flatNumber}`, submitLabel: 'Save',
          fields: [{ key: 'memberId', label: 'Member', type: 'select', ...memberField }]
        }
      });
      ref.afterClosed().subscribe((result) => {
        if (!result) return;
        if (result.memberId === NEW_MEMBER_OPTION) {
          this.promptAddResident(claim.flatId, claim.flatNumber).subscribe((newMember) => {
            if (newMember) this.saveAssignMember(claim.id, newMember.memberId);
          });
          return;
        }
        this.saveAssignMember(claim.id, result.memberId ? Number(result.memberId) : null);
      });
    });
  }

  private saveSelectVariant(claimId: number, variantId: number | null, memberId: number | null): void {
    this.festivalService.selectClaimVariant(claimId, variantId, memberId).subscribe(() => {
      this.toast.success('Selection saved.');
      this.changed = true;
      this.load();
    });
  }

  selectVariant(claim: DistributionClaimDto, isAdminAction: boolean): void {
    const d = this.distribution();
    if (!d) return;

    this.festivalService.getFlatMembersForDistribution(claim.flatId).subscribe((members) => {
      const fields = [];
      if (d.variants.length > 0) {
        fields.push({
          key: 'variantId', label: 'Size / Variant', type: 'select' as const, defaultValue: claim.variantId ?? '',
          options: d.variants.map((v) => ({ value: v.id, label: v.label }))
        });
      }
      const memberField = this.memberOptions(members, claim.memberId);
      fields.push({
        key: 'memberId', label: 'For which member? (optional)', type: 'select' as const, required: false, ...memberField
      });

      const ref = this.dialog.open(PromptDialogComponent, {
        width: '380px',
        data: { title: `Select Item — Flat ${claim.flatNumber}`, submitLabel: 'Confirm', fields }
      });
      ref.afterClosed().subscribe((result) => {
        if (!result) return;
        const variantId = result.variantId ? Number(result.variantId) : null;
        if (result.memberId === NEW_MEMBER_OPTION) {
          this.promptAddResident(claim.flatId, claim.flatNumber).subscribe((newMember) => {
            if (newMember) this.saveSelectVariant(claim.id, variantId, newMember.memberId);
          });
          return;
        }
        this.saveSelectVariant(claim.id, variantId, result.memberId ? Number(result.memberId) : null);
      });
    });
  }

  addExtra(group: ClaimFlatGroup): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: `Add Extra — Flat ${group.flatNumber}`, submitLabel: 'Add & Charge',
        fields: [
          { key: 'quantity', label: 'Quantity', type: 'number', defaultValue: 1 },
          { key: 'amountPerUnit', label: 'Amount per Unit (₹)', type: 'number' },
          {
            key: 'notes', label: 'Notes (optional)', type: 'textarea', required: false,
            hint: `Adds a Special Charge for Flat ${group.flatNumber} in Maintenance for the total amount.`
          }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.addChargeableExtraClaim(
        this.data.distributionId, group.flatId, Number(result.quantity), Number(result.amountPerUnit), result.notes || null
      ).subscribe(() => {
        this.toast.success(`Extra item(s) added and charged to Flat ${group.flatNumber}.`);
        this.changed = true;
        this.load();
      });
    });
  }

  markDistributed(claim: DistributionClaimDto): void {
    this.confirmDialog.confirm({
      title: 'Mark as Distributed', message: `Confirm this item has been physically handed over for Flat ${claim.flatNumber}?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.festivalService.markClaimDistributed(claim.id).subscribe(() => {
        this.toast.success('Marked as distributed.');
        this.changed = true;
        this.load();
      });
    });
  }

  close(): void {
    this.dialogRef.close(this.changed);
  }
}
