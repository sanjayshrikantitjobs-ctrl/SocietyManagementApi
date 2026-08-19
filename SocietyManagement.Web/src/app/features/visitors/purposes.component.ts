import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { SocietyService } from '../society-setup/services/society.service';
import { VisitorPurpose } from './models/visitor.model';
import { VisitorService } from './services/visitor.service';

@Component({
  selector: 'app-visitor-purposes',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTableModule, PageHeaderComponent, SkeletonLoaderComponent, EmptyStateComponent],
  template: `
    <div class="app-page">
      <app-page-header title="Visitor Purposes" [breadcrumbs]="[{ label: 'Visitors', link: '/visitors' }, { label: 'Purposes' }]">
        <button mat-flat-button color="primary" (click)="add()" [disabled]="societyId === 0"><mat-icon>add</mat-icon> Add Purpose</button>
      </app-page-header>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="60" />
      } @else if (purposes().length === 0) {
        <app-empty-state icon="list_alt" title="No purposes yet" message="Add a purpose (Delivery, Guest, Vendor, ...) to start creating visitor requests." />
      } @else {
        <table mat-table [dataSource]="purposes()" class="app-card">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let p">{{ p.name }}</td>
          </ng-container>
          <ng-container matColumnDef="requiresApproval">
            <th mat-header-cell *matHeaderCellDef>Approval</th>
            <td mat-cell *matCellDef="let p">{{ p.requiresApproval ? 'Required' : 'Auto-approved' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let p">
              <span class="badge" [class.badge-success]="p.isActive" [class.badge-muted]="!p.isActive">{{ p.isActive ? 'Active' : 'Inactive' }}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let p">
              <button mat-icon-button (click)="edit(p)"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button (click)="remove(p)"><mat-icon>delete_outline</mat-icon></button>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      }
    </div>
  `,
  styles: [`
    table { width: 100%; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .badge-success { background: #dcfce7; color: #15803d; }
    .badge-muted { background: #e2e8f0; color: #475569; }
  `]
})
export class PurposesComponent implements OnInit {
  private readonly visitorService = inject(VisitorService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly purposes = signal<VisitorPurpose[]>([]);
  readonly displayedColumns = ['name', 'requiresApproval', 'status', 'actions'];

  societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.visitorService.getPurposes(this.societyId).subscribe((purposes) => {
      this.purposes.set(purposes);
      this.loading.set(false);
    });
  }

  private fields(purpose?: VisitorPurpose) {
    return [
      { key: 'name', label: 'Name', type: 'text' as const, defaultValue: purpose?.name ?? '' },
      { key: 'requiresApproval', label: 'Requires Resident Approval', type: 'checkbox' as const, defaultValue: purpose?.requiresApproval ?? true },
      { key: 'displayOrder', label: 'Display Order', type: 'number' as const, required: false, defaultValue: purpose?.displayOrder ?? 0 },
      ...(purpose ? [{ key: 'isActive', label: 'Active', type: 'checkbox' as const, defaultValue: purpose.isActive }] : [])
    ];
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '400px', data: { title: 'Add Purpose', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.visitorService.createPurpose({
        ...result, societyId: this.societyId, requiresApproval: !!result.requiresApproval, displayOrder: Number(result.displayOrder) || 0
      }).subscribe(() => {
        this.toast.success('Purpose added.');
        this.load();
      });
    });
  }

  edit(purpose: VisitorPurpose): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '400px', data: { title: 'Edit Purpose', submitLabel: 'Save', fields: this.fields(purpose) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.visitorService.updatePurpose(purpose.id, {
        ...result, requiresApproval: !!result.requiresApproval, isActive: !!result.isActive, displayOrder: Number(result.displayOrder) || 0
      }).subscribe(() => {
        this.toast.success('Purpose updated.');
        this.load();
      });
    });
  }

  remove(purpose: VisitorPurpose): void {
    this.confirmDialog.confirm({ title: 'Delete Purpose', destructive: true, message: `Delete "${purpose.name}"?` }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.visitorService.deletePurpose(purpose.id).subscribe(() => {
        this.toast.success('Purpose deleted.');
        this.load();
      });
    });
  }
}
