import { CommonModule } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { AuthService } from '../../core/services/auth.service';
import { CurrentSocietyService } from '../../core/services/current-society.service';
import { CommitteeMember } from '../../core/models/committee.model';
import { CommitteeService } from './committee.service';

/** Chairman/Secretary/Treasurer directory — visible to every logged-in
 * role; only a caller with committee.manage (Admin/Super Admin) sees the
 * add/edit/delete affordances, mirroring the read/manage split asked for
 * ("owner/tenant... can see who is chairman/secretary/treasurer"). */
@Component({
  selector: 'app-committee-list',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatIconModule, MatTableModule, MatTooltipModule, DataTableComponent, PageHeaderComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Committee" subtitle="Your society's Chairman, Secretary, Treasurer and other committee members."
        [breadcrumbs]="[{ label: 'Committee' }]">
        @if (auth.hasPermission('committee.manage')) {
          <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> Add Member</button>
        }
      </app-page-header>

      <app-data-table
        [loading]="loading()" [totalCount]="members().length" [showPaginator]="false" [showSearch]="false"
        emptyIcon="groups" emptyTitle="No committee members yet"
        emptyMessage="Add the Chairman, Secretary, Treasurer and other committee members here.">
        <table mat-table [dataSource]="members()" table>
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let m"><strong>{{ m.name }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="designation">
            <th mat-header-cell *matHeaderCellDef>Designation</th>
            <td mat-cell *matCellDef="let m">{{ m.designation }}</td>
          </ng-container>
          <ng-container matColumnDef="flat">
            <th mat-header-cell *matHeaderCellDef>Flat</th>
            <td mat-cell *matCellDef="let m">{{ m.flatNumber ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="contact">
            <th mat-header-cell *matHeaderCellDef>Contact</th>
            <td mat-cell *matCellDef="let m">{{ m.phone ?? '—' }}<br /><span class="muted">{{ m.email ?? '' }}</span></td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let m">
              @if (auth.hasPermission('committee.manage')) {
                <button mat-icon-button matTooltip="Edit" (click)="edit(m)"><mat-icon>edit</mat-icon></button>
                <button mat-icon-button matTooltip="Delete" (click)="remove(m)"><mat-icon>delete_outline</mat-icon></button>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`table { width: 100%; } .muted { color: var(--app-text-muted); font-size: 12px; }`]
})
export class CommitteeListComponent {
  private readonly committeeService = inject(CommitteeService);
  private readonly currentSociety = inject(CurrentSocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly members = signal<CommitteeMember[]>([]);

  get displayedColumns(): string[] {
    return this.auth.hasPermission('committee.manage')
      ? ['name', 'designation', 'flat', 'contact', 'actions']
      : ['name', 'designation', 'flat', 'contact'];
  }

  constructor() {
    // CurrentSocietyService resolves asynchronously — an effect (not a
    // one-shot ngOnInit call) re-runs load() once the society id actually
    // arrives, same pattern as main-layout's expiring-services load.
    effect(() => {
      const societyId = this.currentSociety.society()?.id;
      if (societyId) this.load(societyId);
    });
  }

  private societyId(): number | null {
    return this.currentSociety.society()?.id ?? null;
  }

  load(societyId: number): void {
    this.loading.set(true);
    this.committeeService.getCommitteeMembers(societyId).subscribe((members) => {
      this.members.set(members);
      this.loading.set(false);
    });
  }

  private fields(member?: CommitteeMember) {
    return [
      { key: 'name', label: 'Name', type: 'text' as const, defaultValue: member?.name ?? '' },
      { key: 'designation', label: 'Designation', type: 'text' as const, defaultValue: member?.designation ?? '' },
      { key: 'flatNumber', label: 'Flat Number', type: 'text' as const, required: false, defaultValue: member?.flatNumber ?? '' },
      { key: 'phone', label: 'Phone', type: 'text' as const, required: false, defaultValue: member?.phone ?? '' },
      { key: 'email', label: 'Email', type: 'text' as const, required: false, defaultValue: member?.email ?? '' }
    ];
  }

  add(): void {
    const societyId = this.societyId();
    if (!societyId) return;
    const ref = this.dialog.open(PromptDialogComponent, { width: '480px', data: { title: 'Add Committee Member', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.committeeService.createCommitteeMember({
        societyId, ...result, flatNumber: result.flatNumber || null, phone: result.phone || null, email: result.email || null
      }).subscribe(() => {
        this.toast.success('Committee member added.');
        this.load(societyId);
      });
    });
  }

  edit(member: CommitteeMember): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px', data: { title: 'Edit Committee Member', submitLabel: 'Save', fields: this.fields(member) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.committeeService.updateCommitteeMember(member.id, {
        ...result, flatNumber: result.flatNumber || null, phone: result.phone || null, email: result.email || null
      }).subscribe(() => {
        this.toast.success('Committee member updated.');
        const societyId = this.societyId();
        if (societyId) this.load(societyId);
      });
    });
  }

  remove(member: CommitteeMember): void {
    this.confirmDialog.confirm({
      title: 'Delete Committee Member', destructive: true, message: `Remove ${member.name} from the committee?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.committeeService.deleteCommitteeMember(member.id).subscribe(() => {
        this.toast.success('Committee member removed.');
        const societyId = this.societyId();
        if (societyId) this.load(societyId);
      });
    });
  }
}
