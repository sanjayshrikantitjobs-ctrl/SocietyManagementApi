import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ToastService } from '../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { FlatOccupancyCardComponent } from '../../../shared/components/flat-occupancy-card/flat-occupancy-card.component';
import { SocietyService } from '../../society-setup/services/society.service';
import { EmergencyContactDto, FlatOccupancySummaryDto, FlatResidencyDto, MEMBER_TYPE_LABELS } from '../models/resident.model';
import { ResidentService } from '../services/resident.service';

/** Per-flat occupancy view reached from the flats-list "Residents" row
 * action — the direct UI answer to "is this flat rented, by whom, history
 * of owners, emergency contacts." */
@Component({
  selector: 'app-flat-occupancy',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatIconModule, MatTableModule, MatTooltipModule,
    PageHeaderComponent, FlatOccupancyCardComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header [title]="flatNumber() ? ('Flat ' + flatNumber()) : 'Flat Occupancy'"
        subtitle="Current residents, ownership history, and emergency contacts."
        [breadcrumbs]="[{ label: 'Residents', link: '/residents' }, { label: flatNumber() || '...' }]" />

      @if (summary(); as s) {
        <app-flat-occupancy-card [summary]="s" />
      }

      <div class="section">
        <div class="section-header">
          <h3>Residencies</h3>
          <button mat-flat-button color="primary" (click)="addResidency()"><mat-icon>add</mat-icon> Add Residency</button>
        </div>
        <table mat-table [dataSource]="residencies()" class="app-card">
          <ng-container matColumnDef="memberName">
            <th mat-header-cell *matHeaderCellDef>Member</th>
            <td mat-cell *matCellDef="let r">{{ r.memberName }}</td>
          </ng-container>
          <ng-container matColumnDef="memberType">
            <th mat-header-cell *matHeaderCellDef>Type</th>
            <td mat-cell *matCellDef="let r">{{ memberTypeLabels[r.memberType] }}</td>
          </ng-container>
          <ng-container matColumnDef="phone">
            <th mat-header-cell *matHeaderCellDef>Phone</th>
            <td mat-cell *matCellDef="let r">{{ r.memberPhone }}</td>
          </ng-container>
          <ng-container matColumnDef="period">
            <th mat-header-cell *matHeaderCellDef>Period</th>
            <td mat-cell *matCellDef="let r">
              {{ r.moveInDate | date: 'mediumDate' }} – {{ r.moveOutDate ? (r.moveOutDate | date: 'mediumDate') : 'current' }}
            </td>
          </ng-container>
          <ng-container matColumnDef="primary">
            <th mat-header-cell *matHeaderCellDef>Primary</th>
            <td mat-cell *matCellDef="let r">{{ r.isPrimaryContact ? 'Yes' : '' }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let r">
              @if (!r.moveOutDate) {
                <button mat-icon-button matTooltip="End Residency" (click)="endResidency(r)"><mat-icon>logout</mat-icon></button>
              }
              <button mat-icon-button matTooltip="Delete" (click)="removeResidency(r)"><mat-icon>delete_outline</mat-icon></button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="residencyColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: residencyColumns;"></tr>
        </table>
      </div>

      <div class="section">
        <h3>Ownership History</h3>
        <table mat-table [dataSource]="ownershipHistory()" class="app-card">
          <ng-container matColumnDef="memberName">
            <th mat-header-cell *matHeaderCellDef>Owner</th>
            <td mat-cell *matCellDef="let r">{{ r.memberName }}</td>
          </ng-container>
          <ng-container matColumnDef="period">
            <th mat-header-cell *matHeaderCellDef>Period</th>
            <td mat-cell *matCellDef="let r">
              {{ r.moveInDate | date: 'mediumDate' }} – {{ r.moveOutDate ? (r.moveOutDate | date: 'mediumDate') : 'current' }}
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="ownershipColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: ownershipColumns;"></tr>
        </table>
      </div>

      <div class="section">
        <h3>Emergency Contacts</h3>
        <table mat-table [dataSource]="emergencyContacts()" class="app-card">
          <ng-container matColumnDef="contactName">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let c">{{ c.contactName }}</td>
          </ng-container>
          <ng-container matColumnDef="relationship">
            <th mat-header-cell *matHeaderCellDef>Relationship</th>
            <td mat-cell *matCellDef="let c">{{ c.relationship }}</td>
          </ng-container>
          <ng-container matColumnDef="phone">
            <th mat-header-cell *matHeaderCellDef>Phone</th>
            <td mat-cell *matCellDef="let c">{{ c.phone }}</td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="contactColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: contactColumns;"></tr>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .section { margin-top: 24px; }
    .section-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; }
    h3 { margin: 0 0 8px; font-size: 15px; }
    table { width: 100%; }
  `]
})
export class FlatOccupancyComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly residentService = inject(ResidentService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly flatNumber = signal<string | null>(null);
  readonly summary = signal<FlatOccupancySummaryDto | null>(null);
  readonly residencies = signal<FlatResidencyDto[]>([]);
  readonly ownershipHistory = signal<FlatResidencyDto[]>([]);
  readonly emergencyContacts = signal<EmergencyContactDto[]>([]);

  readonly residencyColumns = ['memberName', 'memberType', 'phone', 'period', 'primary', 'actions'];
  readonly ownershipColumns = ['memberName', 'period'];
  readonly contactColumns = ['contactName', 'relationship', 'phone'];
  readonly memberTypeLabels: Record<number, string> = MEMBER_TYPE_LABELS;

  private flatId = 0;
  private memberOptions: { value: number; label: string }[] = [];

  ngOnInit(): void {
    this.flatId = Number(this.route.snapshot.paramMap.get('flatId'));
    this.societyService.getFlat(this.flatId).subscribe((flat) => this.flatNumber.set(flat.flatNumber));
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) return;
      this.residentService.getMembers({ societyId: societies[0].id, pageSize: 500 }).subscribe((result) => {
        this.memberOptions = result.items.map((m) => ({ value: m.id, label: `${m.firstName} ${m.lastName}` }));
      });
    });
    this.load();
  }

  load(): void {
    this.residentService.getOccupancySummary(this.flatId).subscribe((s) => this.summary.set(s));
    this.residentService.getResidencies(this.flatId).subscribe((r) => this.residencies.set(r));
    this.residentService.getOwnershipHistory(this.flatId).subscribe((r) => this.ownershipHistory.set(r));
    this.residentService.getEmergencyContacts(this.flatId).subscribe((c) => this.emergencyContacts.set(c));
  }

  addResidency(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '440px',
      data: {
        title: 'Add Residency',
        fields: [
          { key: 'memberId', label: 'Member', type: 'select' as const, options: this.memberOptions },
          {
            key: 'memberType', label: 'Type', type: 'select' as const,
            options: [{ value: 1, label: 'Owner' }, { value: 2, label: 'Tenant' }, { value: 3, label: 'Family Member' }]
          },
          { key: 'moveInDate', label: 'Move-In Date', type: 'date' as const, defaultValue: new Date().toISOString().substring(0, 10) },
          { key: 'isPrimaryContact', label: 'Primary Contact for this Flat', type: 'checkbox' as const },
          { key: 'notes', label: 'Notes', type: 'textarea' as const, required: false }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.residentService.createResidency({
        ...result, flatId: this.flatId, memberId: Number(result.memberId), memberType: Number(result.memberType),
        isPrimaryContact: !!result.isPrimaryContact, notes: result.notes || null
      }).subscribe(() => {
        this.toast.success('Residency added.');
        this.load();
      });
    });
  }

  endResidency(residency: FlatResidencyDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '360px',
      data: {
        title: 'End Residency', submitLabel: 'End',
        fields: [{ key: 'moveOutDate', label: 'Move-Out Date', type: 'date' as const, defaultValue: new Date().toISOString().substring(0, 10) }]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.residentService.endResidency(residency.id, result.moveOutDate).subscribe(() => {
        this.toast.success('Residency ended.');
        this.load();
      });
    });
  }

  removeResidency(residency: FlatResidencyDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Residency', destructive: true, message: `Delete this residency record for ${residency.memberName}?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.residentService.deleteResidency(residency.id).subscribe(() => {
        this.toast.success('Residency deleted.');
        this.load();
      });
    });
  }
}
