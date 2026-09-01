import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ToastService } from '../../../core/services/toast.service';
import { OccupancyMember, PERSON_RELATIONSHIP_LABELS } from '../../../core/models/occupancy-member.model';
import { MOBILE_PATTERN, MOBILE_PATTERN_ERROR } from '../../../shared/validators/mobile.validator';
import { MyFamilyService } from './my-family.service';

/** Resident self-service: view who currently resides at your flat and add
 * a new family member. Deliberately no delete/remove action anywhere on
 * this page — removing a resident stays Admin/Super-Admin-only via the
 * existing Residents module (enforced server-side too, not just hidden
 * here). */
@Component({
  selector: 'app-my-family',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatTableModule, DataTableComponent, PageHeaderComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="My Family" subtitle="Everyone currently residing at your flat."
        [breadcrumbs]="[{ label: 'My Family' }]">
        <button mat-flat-button color="primary" (click)="add()"><mat-icon>person_add</mat-icon> Add Family Member</button>
      </app-page-header>

      <app-data-table
        [loading]="loading()" [totalCount]="members().length" [showPaginator]="false" [showSearch]="false"
        emptyIcon="family_restroom" emptyTitle="No one listed yet"
        emptyMessage="Add your family members so they're on record for your flat.">
        <table mat-table [dataSource]="members()" table>
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let m">
              <strong>{{ m.personName }}</strong>
              @if (m.isPrimary) { <mat-chip-set><mat-chip>Primary</mat-chip></mat-chip-set> }
            </td>
          </ng-container>
          <ng-container matColumnDef="relationship">
            <th mat-header-cell *matHeaderCellDef>Relationship</th>
            <td mat-cell *matCellDef="let m">{{ relationshipLabels[m.relationship] }}</td>
          </ng-container>
          <ng-container matColumnDef="contact">
            <th mat-header-cell *matHeaderCellDef>Contact</th>
            <td mat-cell *matCellDef="let m">{{ m.phone }}<br /><span class="muted">{{ m.email ?? '' }}</span></td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`table { width: 100%; } .muted { color: var(--app-text-muted); font-size: 12px; }`]
})
export class MyFamilyComponent implements OnInit {
  private readonly myFamilyService = inject(MyFamilyService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly members = signal<OccupancyMember[]>([]);
  readonly displayedColumns = ['name', 'relationship', 'contact'];
  readonly relationshipLabels: Record<number, string> = PERSON_RELATIONSHIP_LABELS;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.myFamilyService.getMyFamilyMembers().subscribe((members) => {
      this.members.set(members);
      this.loading.set(false);
    });
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px',
      data: {
        title: 'Add Family Member',
        fields: [
          { key: 'firstName', label: 'First Name', type: 'text' as const },
          { key: 'lastName', label: 'Last Name', type: 'text' as const },
          { key: 'phone', label: 'Phone', type: 'text' as const, required: false, pattern: MOBILE_PATTERN, patternError: MOBILE_PATTERN_ERROR, maxLength: 10 },
          { key: 'email', label: 'Email', type: 'text' as const, required: false },
          {
            key: 'relationship', label: 'Relationship', type: 'select' as const,
            options: [
              { value: 2, label: 'Spouse' }, { value: 3, label: 'Son' }, { value: 4, label: 'Daughter' },
              { value: 5, label: 'Parent' }, { value: 6, label: 'Grandparent' }, { value: 7, label: 'Sibling' },
              { value: 8, label: 'Other' }
            ]
          }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.myFamilyService.addFamilyMember({ ...result, phone: result.phone || undefined, email: result.email || undefined }).subscribe(() => {
        this.toast.success('Family member added.');
        this.load();
      });
    });
  }
}
