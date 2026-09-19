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
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../core/services/auth.service';
import { FileUploadService } from '../../core/services/file-upload.service';
import { ToastService } from '../../core/services/toast.service';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { AssetUrlPipe } from '../../shared/pipes/asset-url.pipe';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { SocietyService } from '../society-setup/services/society.service';
import {
  DOCUMENT_CATEGORY_LABELS, DOCUMENT_VISIBILITY_LABELS, SocietyDocumentDto, SocietyDocumentService
} from './documents.service';

const CATEGORY_OPTIONS = Object.entries(DOCUMENT_CATEGORY_LABELS).map(([value, label]) => ({ value: Number(value), label }));
const VISIBILITY_OPTIONS = Object.entries(DOCUMENT_VISIBILITY_LABELS).map(([value, label]) => ({ value: Number(value), label }));
const toDateOnly = (value: string | null | undefined) => (value ? value.substring(0, 10) : '');
const nullIfEmpty = (value: unknown) => (value === '' || value === undefined ? null : value);
const EXPIRY_WARNING_DAYS = 30;

@Component({
  selector: 'app-documents-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatMenuModule, MatSelectModule, MatTableModule,
    AssetUrlPipe, DataTableComponent, PageHeaderComponent, StatusBadgeComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Documents" subtitle="Bylaws, circulars, meeting minutes, certificates and contracts."
        [breadcrumbs]="[{ label: 'Documents' }]">
        @if (canManage()) {
          <button mat-flat-button color="primary" (click)="fileInput.click()" [disabled]="uploading()">
            <mat-icon>upload_file</mat-icon> {{ uploading() ? 'Uploading…' : 'Upload Document' }}
          </button>
          <input #fileInput type="file" hidden (change)="onFileChosen($event)"
                 accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg" />
        }
      </app-page-header>

      <app-data-table
        [loading]="loading()" [totalCount]="totalCount()" [pageSize]="pageSize()" [pageIndex]="pageIndex()"
        searchPlaceholder="Search title or description..." emptyIcon="folder_open" emptyTitle="No documents"
        [emptyMessage]="canManage() ? 'Upload the first society document.' : 'Nothing has been shared with residents yet.'"
        (page)="onPage($event)" (search)="onSearch($event)">
        <div toolbar>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="filter">
            <mat-select [(ngModel)]="categoryFilter" (ngModelChange)="onFilterChange()" placeholder="All categories">
              <mat-option [value]="null">All categories</mat-option>
              @for (c of categoryOptions; track c.value) { <mat-option [value]="c.value">{{ c.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="filter">
            <mat-select [(ngModel)]="expiryFilter" (ngModelChange)="onFilterChange()" placeholder="Any expiry">
              <mat-option [value]="false">Any expiry</mat-option>
              <mat-option [value]="true">Expiring / expired</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
        <table mat-table [dataSource]="rows()" table>
          <ng-container matColumnDef="title">
            <th mat-header-cell *matHeaderCellDef>Document</th>
            <td mat-cell *matCellDef="let d">
              <a [href]="d.fileUrl | assetUrl" target="_blank" rel="noopener"><strong>{{ d.title }}</strong></a>
              @if (d.description) { <div class="muted">{{ d.description }}</div> }
            </td>
          </ng-container>
          <ng-container matColumnDef="category">
            <th mat-header-cell *matHeaderCellDef>Category</th>
            <td mat-cell *matCellDef="let d">{{ categoryLabels[d.category] }}</td>
          </ng-container>
          <ng-container matColumnDef="visibility">
            <th mat-header-cell *matHeaderCellDef>Visible to</th>
            <td mat-cell *matCellDef="let d">
              <app-status-badge [variant]="d.visibility === 2 ? 'info' : 'neutral'" [label]="visibilityLabels[d.visibility]" />
            </td>
          </ng-container>
          <ng-container matColumnDef="expiry">
            <th mat-header-cell *matHeaderCellDef>Expiry</th>
            <td mat-cell *matCellDef="let d">
              @if (d.expiryDate) {
                <app-status-badge [variant]="expiryVariant(d)" [label]="(d.expiryDate | date: 'mediumDate') ?? ''" />
              } @else {
                <span class="muted">—</span>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="added">
            <th mat-header-cell *matHeaderCellDef>Added</th>
            <td mat-cell *matCellDef="let d">{{ d.createdAt | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let d">
              <a mat-icon-button [href]="d.fileUrl | assetUrl" target="_blank" rel="noopener" aria-label="Open"><mat-icon>open_in_new</mat-icon></a>
              @if (canManage()) {
                <button mat-icon-button [matMenuTriggerFor]="menu"><mat-icon>more_vert</mat-icon></button>
                <mat-menu #menu="matMenu">
                  <button mat-menu-item (click)="openForm(d)"><mat-icon>edit</mat-icon> Edit details</button>
                  <button mat-menu-item (click)="remove(d)"><mat-icon>delete</mat-icon> Remove</button>
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
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .filter { width: 190px; }
    div[toolbar] { display: flex; gap: 12px; }
    a { color: inherit; text-decoration: none; }
    a:hover { text-decoration: underline; }
  `]
})
export class DocumentsListComponent implements OnInit {
  private readonly documentService = inject(SocietyDocumentService);
  private readonly societyService = inject(SocietyService);
  private readonly fileUpload = inject(FileUploadService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly auth = inject(AuthService);

  readonly categoryLabels = DOCUMENT_CATEGORY_LABELS;
  readonly visibilityLabels = DOCUMENT_VISIBILITY_LABELS;
  readonly categoryOptions = CATEGORY_OPTIONS;
  readonly columns = ['title', 'category', 'visibility', 'expiry', 'added', 'actions'];

  readonly loading = signal(true);
  readonly uploading = signal(false);
  readonly rows = signal<SocietyDocumentDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly searchTerm = signal('');
  categoryFilter: number | null = null;
  expiryFilter = false;

  private societyId = 0;

  canManage(): boolean { return this.auth.hasPermission('documents.manage'); }

  expiryVariant(d: SocietyDocumentDto): StatusBadgeVariant {
    const days = Math.ceil((new Date(d.expiryDate!).getTime() - new Date(new Date().toDateString()).getTime()) / 86400000);
    return days < 0 ? 'danger' : days <= EXPIRY_WARNING_DAYS ? 'warning' : 'success';
  }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.documentService.getDocuments({
      societyId: this.societyId, search: this.searchTerm() || undefined, category: this.categoryFilter ?? undefined,
      expiringOnly: this.expiryFilter, pageNumber: this.pageIndex() + 1, pageSize: this.pageSize()
    }).subscribe((result) => {
      this.rows.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  onPage(event: PageEvent): void { this.pageIndex.set(event.pageIndex); this.pageSize.set(event.pageSize); this.load(); }
  onSearch(term: string): void { this.searchTerm.set(term); this.pageIndex.set(0); this.load(); }
  onFilterChange(): void { this.pageIndex.set(0); this.load(); }

  // Two-step, same as every other upload in the app: push the bytes first,
  // then save the record with the returned URL once the user has filled in
  // its details.
  onFileChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.uploading.set(true);
    this.fileUpload.upload(file, 'society-documents').subscribe({
      next: (url) => {
        this.uploading.set(false);
        this.openForm(null, { fileUrl: url, fileName: file.name });
      },
      error: () => this.uploading.set(false)
    });
  }

  openForm(doc: SocietyDocumentDto | null, upload?: { fileUrl: string; fileName: string }): void {
    const fileUrl = doc?.fileUrl ?? upload!.fileUrl;
    const fileName = doc?.fileName ?? upload!.fileName;
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '480px',
      data: {
        title: doc ? 'Edit Document' : 'Document Details',
        submitLabel: 'Save',
        fields: [
          { key: 'title', label: 'Title', type: 'text', defaultValue: doc?.title ?? fileName.replace(/\.[^.]+$/, ''), maxLength: 200 },
          { key: 'category', label: 'Category', type: 'select', options: this.categoryOptions, defaultValue: doc?.category ?? 8 },
          { key: 'visibility', label: 'Visible to', type: 'select', options: VISIBILITY_OPTIONS, defaultValue: doc?.visibility ?? 1 },
          { key: 'expiryDate', label: 'Expiry date (optional)', type: 'date', required: false, defaultValue: toDateOnly(doc?.expiryDate) },
          { key: 'description', label: 'Description', type: 'textarea', required: false, defaultValue: doc?.description ?? '' }
        ]
      }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      const payload = {
        title: result.title, category: Number(result.category), visibility: Number(result.visibility),
        expiryDate: nullIfEmpty(result.expiryDate), description: nullIfEmpty(result.description), fileUrl, fileName
      };
      const request$: Observable<unknown> = doc
        ? this.documentService.update(doc.id, payload)
        : this.documentService.create({ ...payload, societyId: this.societyId });
      request$.subscribe(() => {
        this.toast.success(doc ? 'Document updated.' : 'Document added.');
        this.load();
      });
    });
  }

  remove(doc: SocietyDocumentDto): void {
    this.confirmDialog.confirm({ title: 'Remove Document', destructive: true, message: `Remove "${doc.title}"?` }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.documentService.delete(doc.id).subscribe(() => { this.toast.success('Document removed.'); this.load(); });
    });
  }
}
