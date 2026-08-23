import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { RoleService } from '../../roles/role.service';
import { SocietyService } from '../../society-setup/services/society.service';
import { GENDER_LABELS, MemberDto } from '../models/resident.model';
import { ImportResidentsDialogComponent } from '../import/import-residents-dialog.component';
import { QuickAddFlatDialogComponent } from '../quick-add-flat/quick-add-flat-dialog.component';
import { ResidentService } from '../services/resident.service';
import { MemberDetailDialogComponent } from './member-detail-dialog.component';

@Component({
  selector: 'app-members-list',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatSortModule, MatTableModule, MatTooltipModule, DataTableComponent],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <button mat-stroked-button color="primary" (click)="addFlatWithResident()" [disabled]="societyId === 0">
          <mat-icon>domain_add</mat-icon> Add Flat + Resident
        </button>
        <button mat-flat-button color="primary" (click)="add()" [disabled]="societyId === 0">
          <mat-icon>person_add</mat-icon> Add Member
        </button>
        <button mat-stroked-button (click)="importFromExcel()" [disabled]="societyId === 0">
          <mat-icon>upload_file</mat-icon> Import from Excel
        </button>
      </div>

      <app-data-table
        [loading]="loading()"
        [totalCount]="totalCount()"
        [pageSize]="pageSize()"
        [pageIndex]="pageIndex()"
        searchPlaceholder="Search name or phone..."
        emptyIcon="people"
        emptyTitle="No members yet"
        emptyMessage="Add a member to start tracking residents, vehicles, and occupancy."
        (page)="onPage($event)"
        (search)="onSearch($event)">
        <table mat-table [dataSource]="members()" matSort (matSortChange)="onSort($event)" table>
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Name</th>
            <td mat-cell *matCellDef="let m">{{ m.firstName }} {{ m.lastName }}</td>
          </ng-container>
          <ng-container matColumnDef="phone">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Phone</th>
            <td mat-cell *matCellDef="let m">{{ m.phone }}</td>
          </ng-container>
          <ng-container matColumnDef="email">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Email</th>
            <td mat-cell *matCellDef="let m">{{ m.email || '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="gender">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Gender</th>
            <td mat-cell *matCellDef="let m">{{ m.gender ? genderLabels[m.gender] : '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="login">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Login</th>
            <td mat-cell *matCellDef="let m">
              <span class="badge" [class.badge-success]="m.userId" [class.badge-muted]="!m.userId">
                {{ m.userId ? 'Active' : 'None' }}
              </span>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let m">
              <button mat-icon-button matTooltip="View" (click)="view(m)"><mat-icon>visibility</mat-icon></button>
              <button mat-icon-button matTooltip="Edit" (click)="edit(m)"><mat-icon>edit</mat-icon></button>
              @if (!m.userId) {
                <button mat-icon-button matTooltip="Create Login" (click)="createLogin(m)"><mat-icon>vpn_key</mat-icon></button>
              }
              <button mat-icon-button matTooltip="Delete" (click)="remove(m)"><mat-icon>delete_outline</mat-icon></button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
    table { width: 100%; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .badge-success { background: #dcfce7; color: #15803d; }
    .badge-muted { background: #e2e8f0; color: #475569; }
  `]
})
export class MembersListComponent implements OnInit {
  private readonly residentService = inject(ResidentService);
  private readonly societyService = inject(SocietyService);
  private readonly roleService = inject(RoleService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly members = signal<MemberDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  readonly displayedColumns = ['name', 'phone', 'email', 'gender', 'login', 'actions'];
  readonly genderLabels: Record<number, string> = GENDER_LABELS;

  societyId = 0;
  private roleOptions: { value: number; label: string }[] = [];

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
    this.roleService.getRoles().subscribe((roles) => {
      this.roleOptions = roles.map((r) => ({ value: r.id, label: r.name }));
    });
  }

  load(): void {
    this.loading.set(true);
    const sort = this.sortState();
    this.residentService.getMembers({
      societyId: this.societyId,
      search: this.searchTerm() || undefined,
      sortBy: sort?.direction ? sort.active : undefined,
      sortDescending: sort?.direction === 'desc',
      pageNumber: this.pageIndex() + 1,
      pageSize: this.pageSize()
    }).subscribe((result) => {
      this.members.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.pageIndex.set(0);
    this.load();
  }

  onSort(sort: Sort): void {
    this.sortState.set(sort);
    this.pageIndex.set(0);
    this.load();
  }

  private fields(member?: MemberDto) {
    return [
      { key: 'firstName', label: 'First Name', type: 'text' as const, defaultValue: member?.firstName ?? '' },
      { key: 'lastName', label: 'Last Name', type: 'text' as const, defaultValue: member?.lastName ?? '' },
      { key: 'phone', label: 'Phone', type: 'text' as const, defaultValue: member?.phone ?? '' },
      { key: 'email', label: 'Email', type: 'text' as const, required: false, defaultValue: member?.email ?? '' },
      {
        key: 'gender', label: 'Gender', type: 'select' as const, required: false,
        options: [{ value: 1, label: 'Male' }, { value: 2, label: 'Female' }, { value: 3, label: 'Other' }],
        defaultValue: member?.gender ?? ''
      },
      { key: 'dateOfBirth', label: 'Date of Birth', type: 'date' as const, required: false, defaultValue: member?.dateOfBirth?.substring(0, 10) ?? '' }
    ];
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '460px', data: { title: 'Add Member', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.residentService.createMember({
        ...result, societyId: this.societyId, gender: result.gender ? Number(result.gender) : null,
        dateOfBirth: result.dateOfBirth || null, email: result.email || null
      }).subscribe(() => {
        this.toast.success('Member added.');
        this.load();
      });
    });
  }

  edit(member: MemberDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '460px', data: { title: 'Edit Member', submitLabel: 'Save', fields: this.fields(member) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.residentService.updateMember(member.id, {
        ...result, gender: result.gender ? Number(result.gender) : null,
        dateOfBirth: result.dateOfBirth || null, email: result.email || null
      }).subscribe(() => {
        this.toast.success('Member updated.');
        this.load();
      });
    });
  }

  addFlatWithResident(): void {
    this.dialog.open(QuickAddFlatDialogComponent, { maxWidth: '95vw' }).afterClosed().subscribe((created) => {
      if (!created) return;
      this.load();
    });
  }

  importFromExcel(): void {
    this.dialog.open(ImportResidentsDialogComponent, {
      maxWidth: '95vw', data: { societyId: this.societyId }
    }).afterClosed().subscribe((imported) => {
      if (!imported) return;
      this.load();
    });
  }

  view(member: MemberDto): void {
    this.residentService.getMemberById(member.id).subscribe((detail) => {
      this.dialog.open(MemberDetailDialogComponent, { width: '480px', data: detail });
    });
  }

  createLogin(member: MemberDto): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: 'Create Login', submitLabel: 'Create',
        fields: [
          { key: 'roleId', label: 'Role', type: 'select' as const, options: this.roleOptions },
          { key: 'password', label: 'Password (optional)', type: 'password' as const, required: false, defaultValue: '' }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.residentService.createLogin(member.id, Number(result.roleId), result.password || undefined).subscribe(() => {
        this.toast.success(
          member.email
            ? 'Login account created.'
            : 'Login account created. This member has no email on file — share the password with them directly.'
        );
        this.load();
      });
    });
  }

  remove(member: MemberDto): void {
    this.confirmDialog.confirm({
      title: 'Delete Member', destructive: true, message: `Delete "${member.firstName} ${member.lastName}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.residentService.deleteMember(member.id).subscribe(() => {
        this.toast.success('Member deleted.');
        this.load();
      });
    });
  }
}
