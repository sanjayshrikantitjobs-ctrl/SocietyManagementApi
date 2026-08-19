import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { forkJoin } from 'rxjs';
import { PermissionItem, RoleListItem } from '../../core/models/user.model';
import { RoleService } from './role.service';

/**
 * Dynamic role editor with a Module x Action permission matrix — this is the
 * UI for the spec's "Create dynamic permission management" requirement.
 * Checking a box adds/removes that permission code; the whole selected set is
 * sent as permissionIds and fully replaces the role's permissions on save.
 */
@Component({
  selector: 'app-role-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatDialogModule,
    MatFormFieldModule, MatInputModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Edit Role' : 'Create Role' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="content">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Role Name</mat-label>
          <input matInput formControlName="name" [readonly]="data?.isSystemRole" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Description</mat-label>
          <input matInput formControlName="description" />
        </mat-form-field>

        <h4>Permissions</h4>
        @for (module of moduleNames(); track module) {
          <div class="module-block">
            <div class="module-name">{{ module }}</div>
            <div class="module-perms">
              @for (perm of permissionsByModule()[module]; track perm.id) {
                <mat-checkbox [checked]="selectedIds().has(perm.id)" (change)="toggle(perm.id, $event.checked)">
                  {{ perm.action }}
                </mat-checkbox>
              }
            </div>
          </div>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .content { max-height: 60vh; }
    .full-width { width: 100%; }
    h4 { margin: 16px 0 8px; }
    .module-block { margin-bottom: 12px; }
    .module-name { font-weight: 600; font-size: 13px; color: var(--app-text-muted); margin-bottom: 4px; text-transform: uppercase; }
    .module-perms { display: flex; flex-wrap: wrap; gap: 12px; }
  `]
})
export class RoleFormDialogComponent implements OnInit {
  dialogRef = inject(MatDialogRef<RoleFormDialogComponent>);
  data = inject<RoleListItem | null>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly roleService = inject(RoleService);

  readonly permissionsByModule = signal<Record<string, PermissionItem[]>>({});
  readonly selectedIds = signal<Set<number>>(new Set());

  form = this.fb.nonNullable.group({
    name: [this.data?.name ?? '', Validators.required],
    description: [this.data?.description ?? '']
  });

  moduleNames(): string[] {
    return Object.keys(this.permissionsByModule());
  }

  ngOnInit(): void {
    const permissions$ = this.roleService.getAllPermissions();
    if (this.data) {
      forkJoin([permissions$, this.roleService.getRole(this.data.id)]).subscribe(([perms, detail]) => {
        this.permissionsByModule.set(perms);
        this.selectedIds.set(new Set(detail.permissionIds));
      });
    } else {
      permissions$.subscribe((perms) => this.permissionsByModule.set(perms));
    }
  }

  toggle(permissionId: number, checked: boolean): void {
    const current = new Set(this.selectedIds());
    if (checked) current.add(permissionId); else current.delete(permissionId);
    this.selectedIds.set(current);
  }

  submit(): void {
    if (this.form.invalid) return;
    this.dialogRef.close({ ...this.form.getRawValue(), permissionIds: Array.from(this.selectedIds()) });
  }
}
