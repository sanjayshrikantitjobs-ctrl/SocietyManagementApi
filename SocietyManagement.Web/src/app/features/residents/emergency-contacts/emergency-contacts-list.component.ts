import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { SocietyService } from '../../society-setup/services/society.service';
import { EmergencyContactDto } from '../models/resident.model';
import { ResidentService } from '../services/resident.service';
import { MOBILE_PATTERN, MOBILE_PATTERN_ERROR } from '../../../shared/validators/mobile.validator';

@Component({
  selector: 'app-emergency-contacts-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule,
    MatSelectModule, MatTableModule, EmptyStateComponent, SkeletonLoaderComponent
  ],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="flat-picker">
          <mat-label>Flat</mat-label>
          <mat-select [(ngModel)]="selectedFlatId" (selectionChange)="load()">
            @for (opt of flatOptions; track opt.value) {
              <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <button mat-flat-button color="primary" (click)="add()" [disabled]="!selectedFlatId">
          <mat-icon>add</mat-icon> Add Contact
        </button>
      </div>

      @if (!selectedFlatId) {
        <app-empty-state icon="emergency" title="Pick a flat" message="Select a flat above to view or add its emergency contacts." />
      } @else if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="60" />
      } @else if (contacts().length === 0) {
        <app-empty-state icon="emergency" title="No emergency contacts yet"
          message="Add a contact person for this flat, for use in case of emergencies." actionLabel="Add Contact" (action)="add()" />
      } @else {
        <table mat-table [dataSource]="contacts()" class="app-card">
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
    .tab-content { padding: 20px 0; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; gap: 16px; margin-bottom: 16px; }
    .flat-picker { width: 240px; }
    table { width: 100%; }
  `]
})
export class EmergencyContactsListComponent implements OnInit {
  private readonly residentService = inject(ResidentService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(false);
  readonly contacts = signal<EmergencyContactDto[]>([]);
  readonly displayedColumns = ['contactName', 'relationship', 'phone', 'alternatePhone', 'actions'];

  flatOptions: { value: number; label: string }[] = [];
  selectedFlatId: number | null = null;

  ngOnInit(): void {
    this.societyService.getFlats({ pageSize: 500 }).subscribe((result) => {
      this.flatOptions = result.items.map((f) => ({ value: f.id, label: f.flatNumber }));
    });
  }

  load(): void {
    if (!this.selectedFlatId) return;
    this.loading.set(true);
    this.residentService.getEmergencyContacts(this.selectedFlatId).subscribe((data) => {
      this.contacts.set(data);
      this.loading.set(false);
    });
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
        ...result, flatId: this.selectedFlatId, alternatePhone: result.alternatePhone || null
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
