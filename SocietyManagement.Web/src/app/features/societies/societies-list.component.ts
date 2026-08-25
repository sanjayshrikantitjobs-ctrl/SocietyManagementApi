import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { Society } from '../../core/models/society.model';
import { SocietyService } from '../society-setup/services/society.service';

/** Super-Admin-only — create/manage every society on the platform. Mirrors
 * services-list.component.ts's simple PromptDialogComponent-based CRUD
 * pattern, since every Society field is plain text/date. */
@Component({
  selector: 'app-societies-list',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatIconModule, MatTableModule, MatTooltipModule, DataTableComponent, PageHeaderComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Societies" subtitle="Every society on the platform — create new ones and manage existing details."
        [breadcrumbs]="[{ label: 'Societies' }]">
        <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> Add Society</button>
      </app-page-header>

      <app-data-table
        [loading]="loading()" [totalCount]="societies().length" [showPaginator]="false" [showSearch]="false"
        emptyIcon="domain" emptyTitle="No societies yet" emptyMessage="Create the first society to get started.">
        <table mat-table [dataSource]="societies()" table>
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let s"><strong>{{ s.name }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="code">
            <th mat-header-cell *matHeaderCellDef>Code</th>
            <td mat-cell *matCellDef="let s">{{ s.code ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="location">
            <th mat-header-cell *matHeaderCellDef>Location</th>
            <td mat-cell *matCellDef="let s">{{ s.city }}, {{ s.state }}</td>
          </ng-container>
          <ng-container matColumnDef="contact">
            <th mat-header-cell *matHeaderCellDef>Contact</th>
            <td mat-cell *matCellDef="let s">{{ s.contactPhone ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="buildings">
            <th mat-header-cell *matHeaderCellDef>Buildings</th>
            <td mat-cell *matCellDef="let s">{{ s.buildingCount }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let s">
              <button mat-icon-button matTooltip="Edit" (click)="edit(s)"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button matTooltip="Delete" (click)="remove(s)"><mat-icon>delete_outline</mat-icon></button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`table { width: 100%; }`]
})
export class SocietiesListComponent implements OnInit {
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly societies = signal<Society[]>([]);
  readonly displayedColumns = ['name', 'code', 'location', 'contact', 'buildings', 'actions'];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.societyService.getSocieties().subscribe((result) => {
      this.societies.set(result);
      this.loading.set(false);
    });
  }

  private fields(society?: Society) {
    return [
      { key: 'name', label: 'Society Name', type: 'text' as const, defaultValue: society?.name ?? '' },
      ...(society
        ? [{ key: 'code', label: 'Society Code', type: 'text' as const, required: false, defaultValue: society.code ?? '' }]
        : []),
      { key: 'registrationNumber', label: 'Registration Number', type: 'text' as const, required: false, defaultValue: society?.registrationNumber ?? '' },
      { key: 'address', label: 'Address', type: 'textarea' as const, defaultValue: society?.address ?? '' },
      { key: 'city', label: 'City', type: 'text' as const, defaultValue: society?.city ?? '' },
      { key: 'state', label: 'State', type: 'text' as const, defaultValue: society?.state ?? '' },
      { key: 'pincode', label: 'Pincode', type: 'text' as const, defaultValue: society?.pincode ?? '' },
      { key: 'contactEmail', label: 'Contact Email', type: 'text' as const, required: false, defaultValue: society?.contactEmail ?? '' },
      { key: 'contactPhone', label: 'Contact Phone', type: 'text' as const, required: false, defaultValue: society?.contactPhone ?? '' }
    ];
  }

  add(): void {
    const ref = this.dialog.open(PromptDialogComponent, { width: '480px', data: { title: 'Add Society', fields: this.fields() } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.societyService.createSociety({
        ...result, contactEmail: result.contactEmail || null, contactPhone: result.contactPhone || null,
        registrationNumber: result.registrationNumber || null
      }).subscribe(() => {
        this.toast.success('Society created.');
        this.load();
      });
    });
  }

  edit(society: Society): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px', data: { title: 'Edit Society', submitLabel: 'Save', fields: this.fields(society) }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.societyService.updateSociety(society.id, {
        ...result, contactEmail: result.contactEmail || null, contactPhone: result.contactPhone || null,
        registrationNumber: result.registrationNumber || null
      }).subscribe(() => {
        this.toast.success('Society updated.');
        this.load();
      });
    });
  }

  remove(society: Society): void {
    this.confirmDialog.confirm({
      title: 'Delete Society', destructive: true, message: `Delete "${society.name}"? This is only possible if it has no buildings.`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.societyService.deleteSociety(society.id).subscribe(() => {
        this.toast.success('Society deleted.');
        this.load();
      });
    });
  }
}
