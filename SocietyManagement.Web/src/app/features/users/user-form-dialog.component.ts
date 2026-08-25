import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { RoleListItem, UserListItem } from '../../core/models/user.model';
import { Society } from '../../core/models/society.model';
import { AuthService } from '../../core/services/auth.service';
import { RoleService } from '../roles/role.service';
import { SocietyService } from '../society-setup/services/society.service';

@Component({
  selector: 'app-user-form-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatSlideToggleModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Edit User' : 'Create User' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="grid">
        <mat-form-field appearance="outline"><mat-label>First Name</mat-label><input matInput formControlName="firstName" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Last Name</mat-label><input matInput formControlName="lastName" /></mat-form-field>
        @if (!data) {
          <mat-form-field appearance="outline" class="span-2"><mat-label>Email</mat-label><input matInput formControlName="email" /></mat-form-field>
        }
        @if (!data) {
          <mat-form-field appearance="outline" class="span-2">
            <mat-label>Password (optional)</mat-label>
            <input matInput type="password" formControlName="password" />
            <mat-hint>Leave blank to auto-generate a temporary password and email it. If set, must be 8+ chars with upper, lower, digit and special character.</mat-hint>
            @if (form.get('password')?.hasError('weak')) {
              <mat-error>Password must be 8+ chars with an uppercase letter, a lowercase letter, a digit and a special character.</mat-error>
            }
          </mat-form-field>
        }
        <mat-form-field appearance="outline" [class.span-2]="!!data"><mat-label>Mobile Number</mat-label><input matInput formControlName="mobileNumber" /></mat-form-field>
        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Role</mat-label>
          <mat-select formControlName="roleId">
            @for (role of visibleRoles(); track role.id) { <mat-option [value]="role.id">{{ role.name }}</mat-option> }
          </mat-select>
        </mat-form-field>
        @if (showSocietyPicker()) {
          <mat-form-field appearance="outline" class="span-2">
            <mat-label>Society</mat-label>
            <mat-select formControlName="societyId">
              @for (s of societies; track s.id) { <mat-option [value]="s.id">{{ s.name }}</mat-option> }
            </mat-select>
            <mat-hint>Which society this account belongs to.</mat-hint>
          </mat-form-field>
        }
        @if (data) {
          <mat-slide-toggle formControlName="isActive" class="span-2">Active</mat-slide-toggle>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`.grid { display:grid; grid-template-columns:1fr 1fr; gap:0 16px; } .span-2 { grid-column: span 2; }`]
})
export class UserFormDialogComponent implements OnInit {
  private static passwordStrength(control: AbstractControl): ValidationErrors | null {
    const value = control.value as string;
    if (!value) return null;
    const strong = /^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$/.test(value);
    return strong ? null : { weak: true };
  }

  dialogRef = inject(MatDialogRef<UserFormDialogComponent>);
  data = inject<UserListItem | null>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly roleService = inject(RoleService);
  private readonly societyService = inject(SocietyService);
  readonly auth = inject(AuthService);

  roles: RoleListItem[] = [];
  societies: Society[] = [];

  form = this.fb.nonNullable.group({
    firstName: [this.data?.firstName ?? '', Validators.required],
    lastName: [this.data?.lastName ?? '', Validators.required],
    email: ['', this.data ? [] : [Validators.required, Validators.email]],
    password: ['', this.data ? [] : [UserFormDialogComponent.passwordStrength]],
    mobileNumber: [this.data?.mobileNumber ?? '', [Validators.required, Validators.pattern(/^[6-9]\d{9}$/)]],
    roleId: [this.data?.roleId ?? null, Validators.required],
    societyId: [this.data?.societyId ?? null],
    isActive: [this.data?.isActive ?? true]
  });

  /** Only a Super Admin caller ever sees the Admin/SuperAdmin role options
   * — a scoped Admin can only create Member/Watchman logins for their own
   * society (enforced server-side too; this just keeps the UI honest). */
  visibleRoles(): RoleListItem[] {
    if (this.auth.isSuperAdmin()) return this.roles;
    return this.roles.filter((r) => r.name !== 'Admin' && r.name !== 'SuperAdmin');
  }

  /** Society picker only makes sense for Super Admin assigning a tenant —
   * every role except SuperAdmin itself needs one; a scoped Admin's own
   * society is always implicit, never shown as a picker. */
  showSocietyPicker(): boolean {
    if (!this.auth.isSuperAdmin()) return false;
    const selectedRole = this.roles.find((r) => r.id === this.form.value.roleId);
    return selectedRole?.name !== 'SuperAdmin';
  }

  ngOnInit(): void {
    this.roleService.getRoles().subscribe((roles) => (this.roles = roles));
    if (this.auth.isSuperAdmin()) {
      this.societyService.getSocieties().subscribe((societies) => (this.societies = societies));
    }
  }

  submit(): void {
    if (this.form.invalid) return;
    const raw = this.form.getRawValue();
    this.dialogRef.close({ ...raw, societyId: raw.societyId ? Number(raw.societyId) : null });
  }
}
