import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ToastService } from '../../../core/services/toast.service';
import { RoleService } from '../../roles/role.service';
import { FlatOccupancyDto, PersonLoginDto } from '../models/occupancy.model';
import { OccupancyService } from '../services/occupancy.service';

/** Per-flat login card — one row per current Owner/Tenant member, showing
 * "No login" + a Create Login action, or the existing login's
 * email/role once created. Mirrors members-list.component.ts's
 * createLogin() flow, but keyed off Person (User.PersonId) instead of
 * Member (Member.UserId/User.MemberId). */
@Component({
  selector: 'app-flat-login-card',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTableModule, MatTooltipModule],
  template: `
    <div class="panel">
      <h3>Login Accounts</h3>

      @if (!occupancy || occupancy.members.length === 0) {
        <p class="empty">No members on file for this flat yet.</p>
      } @else {
        <table mat-table [dataSource]="occupancy.members" class="login-table">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let m">{{ m.personName }}</td>
          </ng-container>
          <ng-container matColumnDef="login">
            <th mat-header-cell *matHeaderCellDef>Login</th>
            <td mat-cell *matCellDef="let m">
              @if (logins()[m.personId] === undefined) {
                <span class="muted">Checking…</span>
              } @else if (logins()[m.personId]; as login) {
                <span class="badge badge-success">{{ login.email }} ({{ login.roleName }})</span>
              } @else {
                <span class="badge badge-muted">No login</span>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let m">
              @if (logins()[m.personId] === null) {
                <button mat-icon-button (click)="createLogin(m)" matTooltip="Create Login"><mat-icon>vpn_key</mat-icon></button>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      }
    </div>
  `,
  styles: [`
    .panel { border: 1px solid var(--app-border); border-radius: 10px; padding: 16px; margin-bottom: 16px; }
    .panel h3 { margin: 0 0 12px; font-size: 15px; }
    .empty { color: var(--app-text-muted); font-size: 13px; margin: 0; }
    .login-table { width: 100%; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .badge-success { background: #dcfce7; color: #15803d; }
    .badge-muted { background: #e2e8f0; color: #475569; }
  `]
})
export class FlatLoginCardComponent implements OnChanges {
  @Input() occupancy: FlatOccupancyDto | null = null;
  @Input() flatId!: number;

  private readonly occupancyService = inject(OccupancyService);
  private readonly roleService = inject(RoleService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);

  readonly displayedColumns = ['name', 'login', 'actions'];
  readonly logins = signal<Record<number, PersonLoginDto | null | undefined>>({});

  private roleOptions: { value: number; label: string }[] = [];

  constructor() {
    this.roleService.getRoles().subscribe((roles) => {
      this.roleOptions = roles.map((r) => ({ value: r.id, label: r.name }));
    });
  }

  ngOnChanges(): void {
    if (!this.occupancy) return;
    for (const member of this.occupancy.members) {
      this.occupancyService.getPersonLogin(member.personId).subscribe((login) => {
        this.logins.update((current) => ({ ...current, [member.personId]: login }));
      });
    }
  }

  createLogin(member: { personId: number; personName: string; email?: string | null }): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: `Create Login for ${member.personName}`, submitLabel: 'Create',
        fields: [
          { key: 'roleId', label: 'Role', type: 'select' as const, options: this.roleOptions },
          { key: 'password', label: 'Password (default: Test@12345)', type: 'password' as const, required: false, defaultValue: '' }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.occupancyService.createLoginForPerson(member.personId, this.flatId, Number(result.roleId), result.password || undefined).subscribe(() => {
        this.toast.success('Login account created.');
        this.occupancyService.getPersonLogin(member.personId).subscribe((login) => {
          this.logins.update((current) => ({ ...current, [member.personId]: login }));
        });
      });
    });
  }
}
