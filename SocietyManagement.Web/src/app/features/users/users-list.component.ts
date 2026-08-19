import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { UserListItem } from '../../core/models/user.model';
import { ToastService } from '../../core/services/toast.service';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { UserFormDialogComponent } from './user-form-dialog.component';
import { UserService } from './user.service';

/** User Management module — spec: "Admin can Create User, Assign Role, Reset
 * Password, Lock Account." */
@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatIconModule, MatMenuModule, MatChipsModule, MatPaginatorModule,
    MatTableModule, DataTableComponent, PageHeaderComponent
  ],
  templateUrl: './users-list.component.html',
  styleUrl: './users-list.component.scss'
})
export class UsersListComponent implements OnInit {
  private readonly userService = inject(UserService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly users = signal<UserListItem[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly displayedColumns = ['name', 'role', 'status', 'lastLogin', 'actions'];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.userService.getUsers({
      search: this.searchTerm() || undefined,
      pageNumber: this.pageIndex() + 1,
      pageSize: this.pageSize()
    }).subscribe((result) => {
      this.users.set(result.items);
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

  add(): void {
    this.dialog.open(UserFormDialogComponent, { width: '560px', data: null }).afterClosed().subscribe((result) => {
      if (!result) return;
      this.userService.createUser(result).subscribe(() => {
        this.toast.success('User created and a temporary password emailed.');
        this.load();
      });
    });
  }

  edit(user: UserListItem): void {
    this.dialog.open(UserFormDialogComponent, { width: '560px', data: user }).afterClosed().subscribe((result) => {
      if (!result) return;
      this.userService.updateUser(user.id, result).subscribe(() => {
        this.toast.success('User updated.');
        this.load();
      });
    });
  }

  toggleLock(user: UserListItem): void {
    this.userService.toggleLock(user.id, !user.isLocked).subscribe(() => {
      this.toast.success(user.isLocked ? 'User unlocked.' : 'User locked.');
      this.load();
    });
  }

  resetPassword(user: UserListItem): void {
    this.confirmDialog.confirm({
      title: 'Reset Password', message: `Send a new temporary password to ${user.email}?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.userService.resetPassword(user.id).subscribe(() => this.toast.success('Temporary password emailed.'));
    });
  }

  remove(user: UserListItem): void {
    this.confirmDialog.confirm({
      title: 'Delete User', destructive: true, message: `Delete ${user.firstName} ${user.lastName}?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.userService.deleteUser(user.id).subscribe(() => {
        this.toast.success('User deleted.');
        this.load();
      });
    });
  }
}
