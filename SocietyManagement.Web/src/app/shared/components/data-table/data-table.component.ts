import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { EmptyStateComponent } from '../empty-state/empty-state.component';
import { SkeletonLoaderComponent } from '../skeleton-loader/skeleton-loader.component';

/**
 * Shell around every Material table in the app: search box, loading skeleton,
 * empty state, and server-side pagination — all wired once here instead of
 * being re-implemented per feature. The actual <table mat-table> is supplied
 * by the caller via content projection (the `table` slot) so this stays
 * generic across completely different column sets (Users, Flats, Roles, ...).
 */
@Component({
  selector: 'app-data-table',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatFormFieldModule, MatInputModule, MatIconModule,
    MatPaginatorModule, SkeletonLoaderComponent, EmptyStateComponent
  ],
  template: `
    <div class="data-table app-card">
      @if (showSearch()) {
        <div class="toolbar">
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search-field">
            <mat-icon matPrefix>search</mat-icon>
            <input matInput [placeholder]="searchPlaceholder()" [(ngModel)]="searchTerm"
                   (ngModelChange)="onSearchChange($event)" />
          </mat-form-field>
          <div class="toolbar-actions"><ng-content select="[toolbar]" /></div>
        </div>
      }

      <div class="table-scroll">
        @if (loading()) {
          <div class="table-loading"><app-skeleton-loader [rows]="5" /></div>
        } @else if (totalCount() === 0) {
          <app-empty-state
            [icon]="emptyIcon()"
            [title]="emptyTitle()"
            [message]="emptyMessage()" />
        } @else {
          <ng-content select="[table]" />
        }
      </div>

      @if (!loading() && totalCount() > 0 && showPaginator()) {
        <mat-paginator
          [length]="totalCount()"
          [pageSize]="pageSize()"
          [pageSizeOptions]="pageSizeOptions()"
          [pageIndex]="pageIndex()"
          (page)="page.emit($event)"
          showFirstLastButtons />
      }
    </div>
  `,
  styles: [`
    .toolbar { display:flex; align-items:center; justify-content:space-between; gap:12px;
      padding: 16px; border-bottom: 1px solid var(--app-border); flex-wrap:wrap; }
    .search-field { width: 320px; max-width: 100%; }
    .table-scroll { overflow-x: auto; }
    .table-loading { padding: 16px; }
    mat-paginator { border-top: 1px solid var(--app-border); background: transparent; }
  `]
})
export class DataTableComponent {
  loading = input(false);
  totalCount = input(0);
  pageSize = input(10);
  pageIndex = input(0);
  pageSizeOptions = input([10, 25, 50, 100]);
  showSearch = input(true);
  showPaginator = input(true);
  searchPlaceholder = input('Search...');
  emptyIcon = input('inbox');
  emptyTitle = input('No records found');
  emptyMessage = input('Try adjusting your search or filters, or add a new record to get started.');

  page = output<PageEvent>();
  search = output<string>();

  searchTerm = '';
  private readonly searchSubject = new Subject<string>();

  constructor() {
    this.searchSubject.pipe(debounceTime(350), distinctUntilChanged()).subscribe((term) => this.search.emit(term));
  }

  onSearchChange(term: string): void {
    this.searchSubject.next(term);
  }
}
