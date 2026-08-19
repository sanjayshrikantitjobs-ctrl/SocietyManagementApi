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
import { Gate } from './models/visitor.model';
import { VisitorService } from './services/visitor.service';

@Component({
  selector: 'app-gates',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTableModule, PageHeaderComponent, SkeletonLoaderComponent, EmptyStateComponent],
  template: `
    <div class="app-page">
      <app-page-header title="Gates" [breadcrumbs]="[{ label: 'Visitors', link: '/visitors' }, { label: 'Gates' }]">
        <button mat-flat-button color="primary" (click)="add()" [disabled]="societyId === 0"><mat-icon>add</mat-icon> Add Gate</button>
      </app-page-header>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="60" />
      } @else if (gates().length === 0) {
        <app-empty-state icon="door_front" title="No gates yet" message="Add a gate to start recording visitor entries." />
      } @else {
        <table mat-table [dataSource]="gates()" class="app-card">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let g">{{ g.name }}</td>
          </ng-container>
          <ng-container matColumnDef="code">
            <th mat-header-cell *matHeaderCellDef>Code</th>
            <td mat-cell *matCellDef="let g">{{ g.code }}</td>
          </ng-container>
          <ng-container matColumnDef="location">
            <th mat-header-cell *matHeaderCellDef>Location</th>
            <td mat-cell *matCellDef="let g">{{ g.location || '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let g">
              <span class="badge" [class.badge-success]="g.isActive" [class.badge-muted]="!g.isActive">{{ g.isActive ? 'Active' : 'Inactive' }}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let g">
              <button mat-icon-button (click)="edit(g)"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button (click)="remove(g)"><mat-icon>delete_outline</mat-icon></button>
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
export class GatesComponent implements OnInit {
  private readonly visitorService = inject(VisitorService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly gates = signal<Gate[]>([]);
  readonly displayedColumns = ['name', 'code', 'location', 'status', 'actions'];

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
    this.visitorService.getGates(this.societyId).subscribe((gates) => {
      this.gates.set(gates);
      this.loading.set(false);
    });
  }

  private fields(gate?: Gate) {
    return [
      { key: 'name', label: 'Name', type: 'text' as const, defaultValue: gate?.name ?? '' },
      { key: 'code', label: 'Code', type: 'text' as const, defaultValue: gate?.code ?? '' },
      { key: 'location', label: 'Location', type: 'text' as const, required: false, defaultValue: gate?.location ?? '' },
      ...(gate ? [{ key: 'isActive', label: 'Active', type: 'checkbox' as const, defaultValue: gate.isActive }] : [])
    ];
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '400px', data: { title: 'Add Gate', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.visitorService.createGate({ ...result, societyId: this.societyId, location: result.location || null }).subscribe(() => {
        this.toast.success('Gate added.');
        this.load();
      });
    });
  }

  edit(gate: Gate): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '400px', data: { title: 'Edit Gate', submitLabel: 'Save', fields: this.fields(gate) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.visitorService.updateGate(gate.id, { ...result, location: result.location || null, isActive: !!result.isActive }).subscribe(() => {
        this.toast.success('Gate updated.');
        this.load();
      });
    });
  }

  remove(gate: Gate): void {
    this.confirmDialog.confirm({ title: 'Delete Gate', destructive: true, message: `Delete "${gate.name}"?` }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.visitorService.deleteGate(gate.id).subscribe(() => {
        this.toast.success('Gate deleted.');
        this.load();
      });
    });
  }
}
