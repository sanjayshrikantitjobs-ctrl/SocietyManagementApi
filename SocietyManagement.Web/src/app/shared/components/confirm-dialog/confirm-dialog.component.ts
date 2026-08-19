import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
}

/** Reusable confirmation dialog — used before every destructive action
 * (delete user, delete flat, revoke role, ...) app-wide. */
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatDialogModule, MatIconModule],
  template: `
    <div class="confirm-dialog">
      <mat-icon [class.warn]="data.destructive">{{ data.destructive ? 'warning' : 'help' }}</mat-icon>
      <h2 mat-dialog-title>{{ data.title }}</h2>
      <mat-dialog-content>{{ data.message }}</mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button (click)="dialogRef.close(false)">{{ data.cancelLabel ?? 'Cancel' }}</button>
        <button mat-flat-button [color]="data.destructive ? 'warn' : 'primary'" (click)="dialogRef.close(true)">
          {{ data.confirmLabel ?? 'Confirm' }}
        </button>
      </mat-dialog-actions>
    </div>
  `,
  styles: [`
    .confirm-dialog { text-align:center; padding: 8px; }
    mat-icon { font-size:40px; width:40px; height:40px; color: var(--app-primary); }
    mat-icon.warn { color: var(--app-danger); }
    h2 { margin: 8px 0; }
    mat-dialog-actions { justify-content:center !important; }
  `]
})
export class ConfirmDialogComponent {
  dialogRef = inject(MatDialogRef<ConfirmDialogComponent>);
  data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}
