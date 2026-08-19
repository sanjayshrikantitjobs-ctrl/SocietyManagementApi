import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PageEvent } from '@angular/material/paginator';
import { FLAT_STATUS_LABELS, FLAT_TYPE_LABELS, Flat } from '../../../core/models/society.model';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { QuickAddFlatDialogComponent } from '../../residents/quick-add-flat/quick-add-flat-dialog.component';
import { SocietyService } from '../services/society.service';
import { FlatFormDialogComponent } from './flat-form-dialog.component';

/** Flats are always managed in the context of a floor (spec's hierarchy is
 * Society -> Building -> Wing -> Floor -> Flat) — reached from the "View
 * flats" action on a floor row in Structure. */
@Component({
  selector: 'app-flats-list',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule, MatTableModule, MatTooltipModule, DataTableComponent, PageHeaderComponent],
  templateUrl: './flats-list.component.html',
  styleUrl: './flats-list.component.scss'
})
export class FlatsListComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly societyId = signal(0);
  readonly floorId = signal<number | null>(null);
  readonly loading = signal(true);
  readonly flats = signal<Flat[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly displayedColumns = ['flatNumber', 'flatType', 'areaSqFt', 'status', 'actions'];

  // Widened to a numeric index signature (vs. the exported Record<FlatType/FlatStatus, string>
  // mapped types) so indexing from the table template — where the row type from
  // [dataSource] doesn't narrow to the FlatType/FlatStatus literal unions — type-checks.
  readonly flatTypeLabels: Record<number, string> = FLAT_TYPE_LABELS;
  readonly flatStatusLabels: Record<number, string> = FLAT_STATUS_LABELS;

  ngOnInit(): void {
    this.societyId.set(Number(this.route.snapshot.paramMap.get('societyId')));
    const floorIdParam = this.route.snapshot.queryParamMap.get('floorId');
    this.floorId.set(floorIdParam ? Number(floorIdParam) : null);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.societyService.getFlats({
      floorId: this.floorId() ?? undefined,
      search: this.searchTerm() || undefined,
      pageNumber: this.pageIndex() + 1,
      pageSize: this.pageSize()
    }).subscribe((result) => {
      this.flats.set(result.items);
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
    if (!this.floorId()) {
      this.toast.info('Open a specific floor from Structure to add a flat.');
      return;
    }
    this.dialog.open(FlatFormDialogComponent, {
      width: '520px', data: { flat: null, floorId: this.floorId() }
    }).afterClosed().subscribe((result) => {
      if (!result) return;
      this.societyService.createFlat({ floorId: this.floorId()!, ...result }).subscribe(() => {
        this.toast.success('Flat added.');
        this.load();
      });
    });
  }

  addFlatWithResident(): void {
    this.dialog.open(QuickAddFlatDialogComponent, { maxWidth: '95vw' }).afterClosed().subscribe((created) => {
      if (!created) return;
      this.load();
    });
  }

  edit(flat: Flat): void {
    this.dialog.open(FlatFormDialogComponent, {
      width: '520px', data: { flat, floorId: flat.floorId }
    }).afterClosed().subscribe((result) => {
      if (!result) return;
      this.societyService.updateFlat(flat.id, result).subscribe(() => {
        this.toast.success('Flat updated.');
        this.load();
      });
    });
  }

  remove(flat: Flat): void {
    this.confirmDialog.confirm({
      title: 'Delete Flat', destructive: true, message: `Delete flat "${flat.flatNumber}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.societyService.deleteFlat(flat.id).subscribe(() => {
        this.toast.success('Flat deleted.');
        this.load();
      });
    });
  }
}
