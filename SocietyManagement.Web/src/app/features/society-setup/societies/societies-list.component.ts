import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTableModule } from '@angular/material/table';
import { Society } from '../../../core/models/society.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { ToastService } from '../../../core/services/toast.service';
import { SocietyService } from '../services/society.service';
import { SocietyFormDialogComponent } from './society-form-dialog.component';

@Component({
  selector: 'app-societies-list',
  standalone: true,
  imports: [
    CommonModule, RouterLink, MatButtonModule, MatIconModule, MatMenuModule, MatTableModule,
    PageHeaderComponent, DataTableComponent
  ],
  templateUrl: './societies-list.component.html'
})
export class SocietiesListComponent implements OnInit {
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly societies = signal<Society[]>([]);
  readonly displayedColumns = ['name', 'code', 'city', 'buildingCount', 'contact', 'actions'];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.societyService.getSocieties().subscribe((data) => {
      this.societies.set(data);
      this.loading.set(false);
    });
  }

  add(): void {
    const ref = this.dialog.open(SocietyFormDialogComponent, { width: '640px', data: null });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.societyService.createSociety(result).subscribe(() => {
        this.toast.success('Society created.');
        this.load();
      });
    });
  }

  edit(society: Society): void {
    const ref = this.dialog.open(SocietyFormDialogComponent, { width: '640px', data: society });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.societyService.updateSociety(society.id, result).subscribe(() => {
        this.toast.success('Society updated.');
        this.load();
      });
    });
  }

  remove(society: Society): void {
    this.confirmDialog.confirm({
      title: 'Delete Society', destructive: true,
      message: `Delete "${society.name}"? This cannot be undone.`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.societyService.deleteSociety(society.id).subscribe(() => {
        this.toast.success('Society deleted.');
        this.load();
      });
    });
  }
}
