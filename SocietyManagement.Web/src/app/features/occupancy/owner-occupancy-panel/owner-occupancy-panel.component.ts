import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AssetUrlPipe } from '../../../shared/pipes/asset-url.pipe';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ToastService } from '../../../core/services/toast.service';
import { AddOwnerMemberDialogComponent } from '../add-owner-member-dialog/add-owner-member-dialog.component';
import { FlatOccupancyDto, PERSON_RELATIONSHIP_LABELS } from '../models/occupancy.model';
import { OccupancyService } from '../services/occupancy.service';

@Component({
  selector: 'app-owner-occupancy-panel',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTableModule, MatTooltipModule, AssetUrlPipe],
  template: `
    <div class="panel">
      <div class="panel-header">
        <h3>Owner Occupancy</h3>
        <button mat-flat-button color="primary" (click)="addOwner()"><mat-icon>person_add</mat-icon> Add Owner Member</button>
      </div>

      @if (!occupancy || occupancy.members.length === 0) {
        <p class="empty">No owner on file for this flat yet.</p>
      } @else {
        <table mat-table [dataSource]="occupancy.members" class="members-table">
          <ng-container matColumnDef="photo">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let m">
              @if (m.photoUrl) { <img [src]="m.photoUrl | assetUrl" class="avatar" alt="" /> } @else { <mat-icon class="avatar-placeholder">account_circle</mat-icon> }
            </td>
          </ng-container>
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let m">{{ m.personName }} @if (m.isPrimary) { <span class="badge">Primary</span> }</td>
          </ng-container>
          <ng-container matColumnDef="relationship">
            <th mat-header-cell *matHeaderCellDef>Relationship</th>
            <td mat-cell *matCellDef="let m">{{ relationshipLabels[m.relationship] }}</td>
          </ng-container>
          <ng-container matColumnDef="phone">
            <th mat-header-cell *matHeaderCellDef>Mobile</th>
            <td mat-cell *matCellDef="let m">{{ m.phone }}</td>
          </ng-container>
          <ng-container matColumnDef="whatsApp">
            <th mat-header-cell *matHeaderCellDef>WhatsApp</th>
            <td mat-cell *matCellDef="let m">{{ m.whatsAppNumber ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let m">
              <button mat-icon-button (click)="editMember(m)" matTooltip="Edit"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button (click)="removeMember(m)" matTooltip="Remove"><mat-icon>person_remove</mat-icon></button>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      }
    </div>
  `,
  styles: [`
    .panel { border: 1px solid var(--app-border); border-radius: 10px; padding: 16px; margin-bottom: 16px; }
    .panel-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
    .panel-header h3 { margin: 0; font-size: 15px; }
    .empty { color: var(--app-text-muted); font-size: 13px; margin: 0; }
    .members-table { width: 100%; }
    .avatar { width: 32px; height: 32px; border-radius: 50%; object-fit: cover; }
    .avatar-placeholder { color: var(--app-text-muted); }
    .badge { margin-left: 6px; padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600; background: var(--app-primary-light); color: var(--app-primary); }
  `]
})
export class OwnerOccupancyPanelComponent {
  @Input() flatId!: number;
  @Input() societyId!: number;
  @Input() occupancy: FlatOccupancyDto | null = null;
  @Output() changed = new EventEmitter<void>();

  private readonly dialog = inject(MatDialog);
  private readonly occupancyService = inject(OccupancyService);
  private readonly toast = inject(ToastService);

  readonly displayedColumns = ['photo', 'name', 'relationship', 'phone', 'whatsApp', 'actions'];
  // Widened to a numeric index signature (matches flats-list.component.ts's
  // established fix) so indexing from the table template — where the row
  // type from [dataSource] doesn't narrow to the PersonRelationship literal
  // union — type-checks.
  readonly relationshipLabels: Record<number, string> = PERSON_RELATIONSHIP_LABELS;

  addOwner(): void {
    this.dialog.open(AddOwnerMemberDialogComponent, {
      data: { flatId: this.flatId, societyId: this.societyId }
    }).afterClosed().subscribe((created) => {
      if (created) this.changed.emit();
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
            { key: 'phone', label: 'Phone', type: 'text' as const, defaultValue: person.phone },
            { key: 'email', label: 'Email', type: 'text' as const, required: false, defaultValue: person.email ?? '' },
            { key: 'whatsAppNumber', label: 'WhatsApp Number', type: 'text' as const, required: false, defaultValue: person.whatsAppNumber ?? '' }
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
          this.toast.success('Owner details updated.');
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
