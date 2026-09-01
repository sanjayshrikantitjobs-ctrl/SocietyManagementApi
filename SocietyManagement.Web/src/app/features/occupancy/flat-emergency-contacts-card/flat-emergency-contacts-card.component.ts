import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { EmergencyContactDto } from '../../residents/models/resident.model';
import { ResidentService } from '../../residents/services/resident.service';
import { MOBILE_PATTERN, MOBILE_PATTERN_ERROR } from '../../../shared/validators/mobile.validator';

/** Thin wrapper around ResidentService's existing, already flat-scoped
 * emergency-contacts endpoints — no backend change needed, this data was
 * never Member/Person-specific to begin with. */
@Component({
  selector: 'app-flat-emergency-contacts-card',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTableModule],
  template: `
    <div class="panel">
      <div class="panel-header">
        <h3>Emergency Contacts</h3>
        <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> Add Contact</button>
      </div>

      @if (contacts().length === 0) {
        <p class="empty">No emergency contacts on file for this flat yet.</p>
      } @else {
        <table mat-table [dataSource]="contacts()" class="contacts-table">
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
          <ng-container matColumnDef="alternatePhone">
            <th mat-header-cell *matHeaderCellDef>Alternate Phone</th>
            <td mat-cell *matCellDef="let c">{{ c.alternatePhone || '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let c">
              <button mat-icon-button (click)="edit(c)"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button (click)="remove(c)"><mat-icon>delete_outline</mat-icon></button>
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
    .contacts-table { width: 100%; }
  `]
})
export class FlatEmergencyContactsCardComponent implements OnChanges {
  @Input() flatId!: number;

  private readonly residentService = inject(ResidentService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly contacts = signal<EmergencyContactDto[]>([]);
  readonly displayedColumns = ['contactName', 'relationship', 'phone', 'alternatePhone', 'actions'];

  ngOnChanges(): void {
    if (!this.flatId) return;
    this.load();
  }

  load(): void {
    this.residentService.getEmergencyContacts(this.flatId).subscribe((data) => this.contacts.set(data));
  }

  private fields(contact?: EmergencyContactDto) {
    return [
      { key: 'contactName', label: 'Contact Name', type: 'text' as const, defaultValue: contact?.contactName ?? '' },
      { key: 'relationship', label: 'Relationship', type: 'text' as const, defaultValue: contact?.relationship ?? '' },
      { key: 'phone', label: 'Phone', type: 'text' as const, defaultValue: contact?.phone ?? '', pattern: MOBILE_PATTERN, patternError: MOBILE_PATTERN_ERROR, maxLength: 10 },
      { key: 'alternatePhone', label: 'Alternate Phone', type: 'text' as const, required: false, defaultValue: contact?.alternatePhone ?? '', pattern: MOBILE_PATTERN, patternError: MOBILE_PATTERN_ERROR, maxLength: 10 }
    ];
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '420px', data: { title: 'Add Emergency Contact', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.residentService.createEmergencyContact({
        ...result, flatId: this.flatId, alternatePhone: result.alternatePhone || null
      }).subscribe(() => {
        this.toast.success('Emergency contact added.');
        this.load();
      });
    });
  }

  edit(contact: EmergencyContactDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '420px', data: { title: 'Edit Emergency Contact', submitLabel: 'Save', fields: this.fields(contact) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.residentService.updateEmergencyContact(contact.id, {
        ...result, alternatePhone: result.alternatePhone || null
      }).subscribe(() => {
        this.toast.success('Emergency contact updated.');
        this.load();
      });
    });
  }

  remove(contact: EmergencyContactDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Emergency Contact', destructive: true, message: `Delete contact "${contact.contactName}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.residentService.deleteEmergencyContact(contact.id).subscribe(() => {
        this.toast.success('Emergency contact deleted.');
        this.load();
      });
    });
  }
}
