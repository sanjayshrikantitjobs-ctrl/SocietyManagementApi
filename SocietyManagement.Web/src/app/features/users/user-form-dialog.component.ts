import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { RoleListItem, UserListItem } from '../../core/models/user.model';
import { RoleService } from '../roles/role.service';

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
        <mat-form-field appearance="outline" [class.span-2]="!!data"><mat-label>Mobile Number</mat-label><input matInput formControlName="mobileNumber" /></mat-form-field>
        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Role</mat-label>
          <mat-select formControlName="roleId">
            @for (role of roles; track role.id) { <mat-option [value]="role.id">{{ role.name }}</mat-option> }
          </mat-select>
        </mat-form-field>
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
  dialogRef = inject(MatDialogRef<UserFormDialogComponent>);
  data = inject<UserListItem | null>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly roleService = inject(RoleService);

  roles: RoleListItem[] = [];

  form = this.fb.nonNullable.group({
    firstName: [this.data?.firstName ?? '', Validators.required],
    lastName: [this.data?.lastName ?? '', Validators.required],
    email: ['', this.data ? [] : [Validators.required, Validators.email]],
    mobileNumber: [this.data?.mobileNumber ?? '', [Validators.required, Validators.pattern(/^[6-9]\d{9}$/)]],
    roleId: [this.data?.roleId ?? null, Validators.required],
    isActive: [this.data?.isActive ?? true]
  });

  ngOnInit(): void {
    this.roleService.getRoles().subscribe((roles) => (this.roles = roles));
  }

  submit(): void {
    if (this.form.invalid) return;
    this.dialogRef.close(this.form.getRawValue());
  }
}
