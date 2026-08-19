import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTableModule } from '@angular/material/table';
import { RoleListItem } from '../../core/models/user.model';
import { ToastService } from '../../core/services/toast.service';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { RoleFormDialogComponent } from './role-form-dialog.component';
import { RoleService } from './role.service';

@Component({
  selector: 'app-roles-list',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatMenuModule, MatTableModule, DataTableComponent, PageHeaderComponent],
  templateUrl: './roles-list.component.html'
})
export class RolesListComponent implements OnInit {
  private readonly roleService = inject(RoleService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly roles = signal<RoleListItem[]>([]);
  readonly displayedColumns = ['name', 'description', 'userCount', 'actions'];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.roleService.getRoles().subscribe((data) => {
      this.roles.set(data);
      this.loading.set(false);
    });
  }

  add(): void {
    this.dialog.open(RoleFormDialogComponent, { width: '640px', data: null }).afterClosed().subscribe((result) => {
      if (!result) return;
      this.roleService.createRole(result).subscribe(() => {
        this.toast.success('Role created.');
        this.load();
      });
    });
  }

  edit(role: RoleListItem): void {
    this.dialog.open(RoleFormDialogComponent, { width: '640px', data: role }).afterClosed().subscribe((result) => {
      if (!result) return;
      this.roleService.updateRole(role.id, result).subscribe(() => {
        this.toast.success('Role updated.');
        this.load();
      });
    });
  }

  remove(role: RoleListItem): void {
    this.confirmDialog.confirm({
      title: 'Delete Role', destructive: true, message: `Delete role "${role.name}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.roleService.deleteRole(role.id).subscribe(() => {
        this.toast.success('Role deleted.');
        this.load();
      });
    });
  }
}
