import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { StatCardComponent } from '../../shared/components/stat-card/stat-card.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { SocietyService } from '../society-setup/services/society.service';
import { PET_TYPE_LABELS, PetDto, PetService, PetSummaryDto } from './pets.service';

const PET_TYPE_OPTIONS = Object.entries(PET_TYPE_LABELS).map(([value, label]) => ({ value: Number(value), label }));
const toDateOnly = (value: string | null | undefined) => (value ? value.substring(0, 10) : '');
const nullIfEmpty = (value: unknown) => (value === '' || value === undefined ? null : value);

@Component({
  selector: 'app-pets-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatMenuModule, MatSelectModule, MatSortModule,
    MatTableModule, DataTableComponent, PageHeaderComponent, StatCardComponent, StatusBadgeComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Pets" subtitle="Registered pets by flat, with vaccination status."
        [breadcrumbs]="[{ label: 'Pets' }]">
        @if (canManage()) {
          <button mat-flat-button color="primary" (click)="openForm(null)"><mat-icon>add</mat-icon> Register Pet</button>
        }
      </app-page-header>

      @if (summary(); as s) {
        <div class="stats">
          <app-stat-card label="Registered pets" [value]="s.totalPets" icon="pets" />
          <app-stat-card label="Dogs / Cats" [value]="s.dogs + ' / ' + s.cats" icon="cruelty_free" iconColor="#2563eb" iconBg="#eff6ff" />
          <app-stat-card label="Vaccination overdue" [value]="s.vaccinationOverdue" icon="vaccines" iconColor="#dc2626" iconBg="#fef2f2" />
          <app-stat-card label="No vaccination on record" [value]="s.neverVaccinated" icon="help_outline" iconColor="#b45309" iconBg="#fffbeb" />
        </div>
      }

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search pet, breed or flat..." emptyIcon="pets" emptyTitle="No pets registered"
        emptyMessage="Register a pet against its flat to keep vaccination and emergency details on file."
        (page)="onPage($event)" (search)="onSearch($event)">
        <div toolbar>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="type-filter">
            <mat-select [(ngModel)]="typeFilter" (ngModelChange)="onFilterChange()" placeholder="All types">
              <mat-option [value]="null">All types</mat-option>
              @for (t of typeOptions; track t.value) { <mat-option [value]="t.value">{{ t.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
        </div>
        <table mat-table [dataSource]="rows()" matSort (matSortChange)="onSort($event)" table>
          <ng-container matColumnDef="flat">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="flat">Flat</th>
            <td mat-cell *matCellDef="let p"><strong>{{ p.flatNumber }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="name">Pet</th>
            <td mat-cell *matCellDef="let p">
              {{ p.name }}
              @if (p.identification) { <div class="muted">{{ p.identification }}</div> }
            </td>
          </ng-container>
          <ng-container matColumnDef="type">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="type">Type</th>
            <td mat-cell *matCellDef="let p">{{ typeLabels[p.petType] }}@if (p.breed) { <span class="muted"> · {{ p.breed }}</span> }</td>
          </ng-container>
          <ng-container matColumnDef="vaccination">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="vaccination">Vaccination</th>
            <td mat-cell *matCellDef="let p">
              @if (!p.lastVaccinationDate) {
                <app-status-badge variant="neutral" label="No record" />
              } @else if (isOverdue(p)) {
                <app-status-badge variant="danger" [label]="'Overdue · ' + (p.nextVaccinationDue | date: 'mediumDate')" />
              } @else {
                <app-status-badge variant="success" [label]="p.nextVaccinationDue ? 'Next ' + (p.nextVaccinationDue | date: 'mediumDate') : 'Up to date'" />
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let p"><app-status-badge [variant]="p.isActive ? 'success' : 'neutral'" [label]="p.isActive ? 'Active' : 'Inactive'" /></td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let p">
              @if (canManage()) {
                <button mat-icon-button [matMenuTriggerFor]="menu"><mat-icon>more_vert</mat-icon></button>
                <mat-menu #menu="matMenu">
                  <button mat-menu-item (click)="openForm(p)"><mat-icon>edit</mat-icon> Edit</button>
                  <button mat-menu-item (click)="remove(p)"><mat-icon>delete</mat-icon> Remove</button>
                </mat-menu>
              }
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns;"></tr>
        </table>
      </app-data-table>
    </div>
  `,
  styles: [`
    table { width: 100%; }
    .stats { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .type-filter { width: 160px; }
  `]
})
export class PetsListComponent implements OnInit {
  private readonly petService = inject(PetService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly auth = inject(AuthService);

  readonly typeLabels = PET_TYPE_LABELS;
  readonly typeOptions = PET_TYPE_OPTIONS;
  readonly columns = ['flat', 'name', 'type', 'vaccination', 'status', 'actions'];

  readonly loading = signal(true);
  readonly rows = signal<PetDto[]>([]);
  readonly summary = signal<PetSummaryDto | null>(null);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  readonly sortState = signal<Sort | null>(null);
  typeFilter: number | null = null;

  private societyId = 0;
  private flatOptions: { value: number; label: string }[] = [];

  canManage(): boolean { return this.auth.hasPermission('pets.manage'); }

  isOverdue(p: PetDto): boolean {
    return !!p.nextVaccinationDue && new Date(p.nextVaccinationDue) < new Date(new Date().toDateString());
  }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
      this.societyService.getFlats({ societyId: this.societyId, pageSize: 500 }).subscribe((r) => {
        this.flatOptions = r.items.map((f) => ({ value: f.id, label: f.flatNumber }));
      });
    });
  }

  load(): void {
    this.loading.set(true);
    const sort = this.sortState();
    this.petService.getPets({
      societyId: this.societyId, search: this.searchTerm() || undefined, petType: this.typeFilter ?? undefined,
      sortBy: sort?.direction ? sort.active : undefined, sortDescending: sort?.direction === 'desc',
      pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.rows.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
    this.petService.getSummary(this.societyId).subscribe((s) => this.summary.set(s));
  }

  onPage(event: PageEvent): void { this.pageIndex.set(event.pageIndex); this.pageSize.set(event.pageSize); this.load(); }
  onSearch(term: string): void { this.searchTerm.set(term); this.pageIndex.set(0); this.load(); }
  onSort(sort: Sort): void { this.sortState.set(sort); this.pageIndex.set(0); this.load(); }
  onFilterChange(): void { this.pageIndex.set(0); this.load(); }

  openForm(pet: PetDto | null): void {
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px',
      data: {
        title: pet ? 'Edit Pet' : 'Register Pet',
        submitLabel: 'Save',
        fields: [
          { key: 'flatId', label: 'Flat', type: 'select', options: this.flatOptions, defaultValue: pet?.flatId },
          { key: 'name', label: 'Pet Name', type: 'text', defaultValue: pet?.name ?? '', maxLength: 100 },
          { key: 'petType', label: 'Type', type: 'select', options: this.typeOptions, defaultValue: pet?.petType ?? 1 },
          { key: 'breed', label: 'Breed', type: 'text', required: false, defaultValue: pet?.breed ?? '', maxLength: 100 },
          { key: 'identification', label: 'Identification marks / tag', type: 'text', required: false, defaultValue: pet?.identification ?? '', maxLength: 300 },
          { key: 'registrationNumber', label: 'Registration / licence number', type: 'text', required: false, defaultValue: pet?.registrationNumber ?? '', maxLength: 100 },
          { key: 'lastVaccinationDate', label: 'Last vaccination', type: 'date', required: false, defaultValue: toDateOnly(pet?.lastVaccinationDate) },
          { key: 'nextVaccinationDue', label: 'Next vaccination due', type: 'date', required: false, defaultValue: toDateOnly(pet?.nextVaccinationDue) },
          { key: 'notes', label: 'Notes (temperament, emergency info)', type: 'textarea', required: false, defaultValue: pet?.notes ?? '' },
          ...(pet ? [{ key: 'isActive', label: 'Active', type: 'checkbox' as const, defaultValue: pet.isActive }] : [])
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      const payload = {
        flatId: Number(result.flatId), name: result.name, petType: Number(result.petType), breed: nullIfEmpty(result.breed),
        identification: nullIfEmpty(result.identification), registrationNumber: nullIfEmpty(result.registrationNumber),
        lastVaccinationDate: nullIfEmpty(result.lastVaccinationDate), nextVaccinationDue: nullIfEmpty(result.nextVaccinationDue),
        notes: nullIfEmpty(result.notes), photoUrl: pet?.photoUrl ?? null
      };
      const request$: Observable<unknown> = pet
        ? this.petService.update(pet.id, { ...payload, isActive: !!result.isActive })
        : this.petService.create({ ...payload, societyId: this.societyId });
      request$.subscribe(() => {
        this.toast.success(pet ? 'Pet updated.' : 'Pet registered.');
        this.load();
      });
    });
  }

  remove(pet: PetDto): void {
    this.confirmDialog.confirm({ title: 'Remove Pet', destructive: true, message: `Remove ${pet.name} from flat ${pet.flatNumber}?` })
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.petService.delete(pet.id).subscribe(() => { this.toast.success('Pet removed.'); this.load(); });
      });
  }
}
