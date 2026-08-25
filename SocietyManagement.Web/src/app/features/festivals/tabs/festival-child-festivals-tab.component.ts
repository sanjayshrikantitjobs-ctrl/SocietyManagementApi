import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { ToastService } from '../../../core/services/toast.service';
import { FestivalFormDialogComponent } from '../festival-form-dialog.component';
import { FESTIVAL_STATUS_LABELS, PoolChildSummaryDto } from '../models/festival.model';
import { FestivalService } from '../services/festival.service';

/** Pool-only tab: every Child festival drawing from this pool, with a
 * quick "Add Child Festival" action that pre-links the new festival to
 * this pool (see FestivalFormDialogComponent's locked-kind mode). */
@Component({
  selector: 'app-festival-child-festivals-tab',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatChipsModule, MatIconModule, MatTableModule, EmptyStateComponent, SkeletonLoaderComponent],
  template: `
    <div class="tab-content">
      <div class="toolbar">
        <h3>Child Festivals & Events</h3>
        <button mat-flat-button color="primary" (click)="addChild()"><mat-icon>add</mat-icon> Add Child Festival</button>
      </div>

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="60" />
      } @else if (children().length === 0) {
        <app-empty-state icon="celebration" title="No child festivals yet"
          message="Add Ganpati, Navratri, or any other festival that should draw from this shared pool."
          actionLabel="Add Child Festival" (action)="addChild()" />
      } @else {
        <table mat-table [dataSource]="children()" class="children-table">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Festival</th>
            <td mat-cell *matCellDef="let c"><a>{{ c.name }}</a></td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let c"><mat-chip-set><mat-chip [class]="'status-' + c.status">{{ statusLabel(c.status) }}</mat-chip></mat-chip-set></td>
          </ng-container>
          <ng-container matColumnDef="budget">
            <th mat-header-cell *matHeaderCellDef>Budget</th>
            <td mat-cell *matCellDef="let c">₹{{ c.budget | number }}</td>
          </ng-container>
          <ng-container matColumnDef="spent">
            <th mat-header-cell *matHeaderCellDef>Spent</th>
            <td mat-cell *matCellDef="let c">₹{{ c.spent | number }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;" (click)="open(row)" class="clickable-row"></tr>
        </table>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
    .toolbar h3 { margin: 0; font-size: 15px; }
    .children-table { width: 100%; }
    .children-table a { cursor: pointer; color: var(--app-primary); font-weight: 600; }
    .clickable-row { cursor: pointer; }
    .status-1 { background: #fef3c7; color: #b45309; }
    .status-2 { background: #dcfce7; color: #15803d; }
    .status-3 { background: #e2e8f0; color: #475569; }
  `]
})
export class FestivalChildFestivalsTabComponent implements OnInit {
  festivalId = input.required<number>();
  poolName = input.required<string>();
  societyId = input.required<number>();

  private readonly festivalService = inject(FestivalService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly children = signal<PoolChildSummaryDto[]>([]);
  readonly displayedColumns = ['name', 'status', 'budget', 'spent'];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.festivalService.getPoolSummary(this.festivalId()).subscribe((summary) => {
      this.children.set(summary.children);
      this.loading.set(false);
    });
  }

  statusLabel(status: number): string {
    return FESTIVAL_STATUS_LABELS[status as 1 | 2 | 3];
  }

  open(child: PoolChildSummaryDto): void {
    this.router.navigate(['/festivals', child.festivalId]);
  }

  addChild(): void {
    const ref = this.dialog.open(FestivalFormDialogComponent, {
      width: '640px',
      data: {
        societyId: this.societyId(), festival: null,
        lockedKind: 3, lockedPoolFestivalId: this.festivalId(), lockedPoolFestivalName: this.poolName()
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.createFestival(result).subscribe(() => {
        this.toast.success('Child festival added.');
        this.load();
      });
    });
  }
}
