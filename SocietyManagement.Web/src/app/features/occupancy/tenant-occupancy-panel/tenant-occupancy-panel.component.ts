import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AssetUrlPipe } from '../../../shared/pipes/asset-url.pipe';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { ToastService } from '../../../core/services/toast.service';
import { AddOccupancyFamilyMemberDialogComponent } from '../add-occupancy-family-member-dialog/add-occupancy-family-member-dialog.component';
import { AddTenantDialogComponent } from '../add-tenant-dialog/add-tenant-dialog.component';
import { RentalAgreementCardComponent } from '../rental-agreement-card/rental-agreement-card.component';
import { ResidentDocumentsCardComponent } from '../resident-documents-card/resident-documents-card.component';
import { FlatOccupancyDto, PERSON_RELATIONSHIP_LABELS, RESIDENT_STATUS_LABELS } from '../models/occupancy.model';
import { OccupancyService } from '../services/occupancy.service';
import { MOBILE_PATTERN, MOBILE_PATTERN_ERROR } from '../../../shared/validators/mobile.validator';

@Component({
  selector: 'app-tenant-occupancy-panel',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatIconModule, MatTableModule, MatTooltipModule,
    AssetUrlPipe, RentalAgreementCardComponent, ResidentDocumentsCardComponent
  ],
  template: `
    <div class="panel">
      <div class="panel-header">
        <h3>Tenant Occupancy</h3>
        @if (!occupancy) {
          <button mat-flat-button color="primary" (click)="addTenant()"><mat-icon>person_add</mat-icon> Add Tenant</button>
        } @else {
          <div class="actions">
            <button mat-stroked-button (click)="addFamilyMember()"><mat-icon>group_add</mat-icon> Add Family Member</button>
            <button mat-stroked-button color="warn" (click)="endTenancy()"><mat-icon>logout</mat-icon> End Tenancy</button>
          </div>
        }
      </div>

      @if (!occupancy) {
        <p class="empty">This flat currently has no tenant on file.</p>
      } @else {
        <app-rental-agreement-card [flatOccupancyId]="occupancy.id" [agreement]="occupancy.rentalAgreement ?? null" (changed)="changed.emit()" />
        <app-resident-documents-card [flatOccupancyId]="occupancy.id" />

        <h4 class="section-title">Family Members</h4>
        <table mat-table [dataSource]="occupancy.members" class="members-table">
          <ng-container matColumnDef="photo">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let m">
              @if (m.photoUrl) { <img [src]="m.photoUrl | assetUrl" class="avatar" alt="" /> } @else { <mat-icon class="avatar-placeholder">account_circle</mat-icon> }
            </td>
          </ng-container>
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let m">{{ m.personName }} @if (m.isPrimary) { <span class="badge">Primary Tenant</span> }</td>
          </ng-container>
          <ng-container matColumnDef="relationship">
            <th mat-header-cell *matHeaderCellDef>Relationship</th>
            <td mat-cell *matCellDef="let m">{{ relationshipLabels[m.relationship] }}</td>
          </ng-container>
          <ng-container matColumnDef="phone">
            <th mat-header-cell *matHeaderCellDef>Mobile</th>
            <td mat-cell *matCellDef="let m">{{ m.phone }}</td>
          </ng-container>
          <ng-container matColumnDef="email">
            <th mat-header-cell *matHeaderCellDef>Email</th>
            <td mat-cell *matCellDef="let m">{{ m.email ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="whatsApp">
            <th mat-header-cell *matHeaderCellDef>WhatsApp</th>
            <td mat-cell *matCellDef="let m">{{ m.whatsAppNumber ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="residentStatus">
            <th mat-header-cell *matHeaderCellDef>Resident Status</th>
            <td mat-cell *matCellDef="let m">{{ residentStatusLabels[m.residentStatus] }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let m">
              <button mat-icon-button (click)="editMember(m)" matTooltip="Edit"><mat-icon>edit</mat-icon></button>
              @if (!m.isPrimary) {
                <button mat-icon-button (click)="removeMember(m)" matTooltip="Remove"><mat-icon>person_remove</mat-icon></button>
              }
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      }
    </div>
  `,
  styles: [`
    .panel { border: 1px solid var(--app-border); border-radius: 10px; padding: 16px; }
    .panel-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
    .panel-header h3 { margin: 0; font-size: 15px; }
    .panel-header .actions { display: flex; gap: 8px; }
    .section-title { font-size: 13px; color: var(--app-text-muted); margin: 16px 0 8px; }
    .empty { color: var(--app-text-muted); font-size: 13px; margin: 0; }
    .members-table { width: 100%; }
    .avatar { width: 32px; height: 32px; border-radius: 50%; object-fit: cover; }
    .avatar-placeholder { color: var(--app-text-muted); }
    .badge { margin-left: 6px; padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600; background: var(--app-primary-light); color: var(--app-primary); }
  `]
})
export class TenantOccupancyPanelComponent {
  @Input() flatId!: number;
  @Input() societyId!: number;
  @Input() occupancy: FlatOccupancyDto | null = null;
  @Output() changed = new EventEmitter<void>();

  private readonly dialog = inject(MatDialog);
  private readonly occupancyService = inject(OccupancyService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly displayedColumns = ['photo', 'name', 'relationship', 'phone', 'email', 'whatsApp', 'residentStatus', 'actions'];
  // See owner-occupancy-panel.component.ts for why these are widened to a
  // numeric index signature.
  readonly relationshipLabels: Record<number, string> = PERSON_RELATIONSHIP_LABELS;
  readonly residentStatusLabels: Record<number, string> = RESIDENT_STATUS_LABELS;

  addTenant(): void {
    this.dialog.open(AddTenantDialogComponent, {
      data: { flatId: this.flatId, societyId: this.societyId }
    }).afterClosed().subscribe((created) => {
      if (created) this.changed.emit();
    });
  }

  addFamilyMember(): void {
    if (!this.occupancy) return;
    this.dialog.open(AddOccupancyFamilyMemberDialogComponent, {
      data: { flatOccupancyId: this.occupancy.id, societyId: this.societyId }
    }).afterClosed().subscribe((created) => {
      if (created) this.changed.emit();
    });
  }

  endTenancy(): void {
    if (!this.occupancy) return;
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: 'End Tenancy',
        submitLabel: 'End Tenancy',
        fields: [{ key: 'endDate', label: 'Move-out Date', type: 'date' as const, defaultValue: new Date().toISOString().substring(0, 10) }]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.occupancyService.endOccupancy(this.occupancy!.id, result.endDate).subscribe(() => {
        this.toast.success('Tenancy ended — the whole family has moved out.');
        this.changed.emit();
      });
    });
  }

  editMember(member: { personId: number; personName: string }): void {
    this.occupancyService.getPersonById(member.personId).subscribe((person) => {
      const ref = this.dialog.open(PromptDialogComponent, {
        width: '420px',
        data: {
          title: `Edit ${member.personName}`, submitLabel: 'Save',
          fields: [
            { key: 'firstName', label: 'First Name', type: 'text' as const, defaultValue: person.firstName },
            { key: 'lastName', label: 'Last Name', type: 'text' as const, defaultValue: person.lastName },
            { key: 'phone', label: 'Phone', type: 'text' as const, defaultValue: person.phone, pattern: MOBILE_PATTERN, patternError: MOBILE_PATTERN_ERROR, maxLength: 10 },
            { key: 'email', label: 'Email', type: 'text' as const, required: false, defaultValue: person.email ?? '' },
            { key: 'whatsAppNumber', label: 'WhatsApp Number', type: 'text' as const, required: false, defaultValue: person.whatsAppNumber ?? '', pattern: MOBILE_PATTERN, patternError: MOBILE_PATTERN_ERROR, maxLength: 10 }
          ]
        }
      });
      ref.afterClosed().subscribe((result) => {
        if (!result) return;
        this.occupancyService.updatePerson(person.id, {
          firstName: result.firstName, lastName: result.lastName, phone: result.phone,
          email: result.email || null, whatsAppNumber: result.whatsAppNumber || null,
          gender: person.gender, dateOfBirth: person.dateOfBirth, photoUrl: person.photoUrl,
          aadhaarNumber: person.aadhaarNumber, panNumber: person.panNumber
        }).subscribe(() => {
          this.toast.success('Tenant details updated.');
          this.changed.emit();
        });
      });
    });
  }

  removeMember(member: { id: number; personName: string }): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '360px',
      data: {
        title: `Remove ${member.personName}`,
        submitLabel: 'Remove',
        fields: [{ key: 'leftDate', label: 'Move-out Date', type: 'date' as const, defaultValue: new Date().toISOString().substring(0, 10) }]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.occupancyService.removeMember(member.id, result.leftDate).subscribe(() => {
        this.toast.success('Member removed.');
        this.changed.emit();
      });
    });
  }
}
