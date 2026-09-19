import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, ElementRef, HostListener, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Subject, debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../../core/models/api-response.model';
import { CurrentSocietyService } from '../../../core/services/current-society.service';

interface SearchResult { type: string; id: number; title: string; subtitle?: string | null; route: string; }

const TYPE_ICONS: Record<string, string> = {
  Flat: 'apartment', Vehicle: 'directions_car', Staff: 'engineering', Vendor: 'handyman', Document: 'description', Complaint: 'report_problem', Pet: 'pets'
};

/** Topbar search across the modules the caller may see — the API only
 * returns sources the caller holds the module permission for. */
@Component({
  selector: 'app-global-search',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  template: `
    <div class="search">
      <mat-icon class="icon">search</mat-icon>
      <input type="search" placeholder="Search flats, vehicles, staff, vendors…" [ngModel]="term()" (ngModelChange)="onType($event)"
             (focus)="open.set(true)" aria-label="Search" />
      @if (open() && term().trim().length >= 2) {
        <div class="panel">
          @if (loading()) {
            <div class="empty">Searching…</div>
          } @else if (groups().length === 0) {
            <div class="empty">No matches for "{{ term().trim() }}".</div>
          } @else {
            @for (g of groups(); track g.type) {
              <div class="group-title">{{ g.type }}</div>
              @for (r of g.items; track r.type + r.id) {
                <button type="button" class="result" (click)="go(r)">
                  <mat-icon>{{ icons[r.type] }}</mat-icon>
                  <span class="text"><strong>{{ r.title }}</strong>@if (r.subtitle) { <span class="sub">{{ r.subtitle }}</span> }</span>
                </button>
              }
            }
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .search { position: relative; width: min(420px, 40vw); }
    .icon { position: absolute; left: 10px; top: 50%; transform: translateY(-50%); color: var(--app-text-muted); font-size: 20px; }
    input { width: 100%; box-sizing: border-box; height: 38px; padding: 0 12px 0 38px; border: 1px solid var(--app-border); border-radius: 10px;
      background: var(--app-surface-alt); color: inherit; font-size: 14px; outline: none; }
    input:focus { border-color: var(--app-primary); background: var(--app-surface); }
    .panel { position: absolute; top: 44px; left: 0; right: 0; max-height: 420px; overflow: auto; background: var(--app-surface);
      border: 1px solid var(--app-border); border-radius: 12px; box-shadow: 0 12px 32px rgba(0,0,0,.16); padding: 6px; z-index: 1000; }
    .group-title { font-size: 11px; text-transform: uppercase; letter-spacing: .06em; color: var(--app-text-muted); padding: 8px 10px 4px; }
    .result { display: flex; align-items: center; gap: 10px; width: 100%; border: 0; background: transparent; color: inherit; text-align: left;
      padding: 8px 10px; border-radius: 8px; cursor: pointer; }
    .result:hover { background: var(--app-primary-light); }
    .text { display: flex; flex-direction: column; min-width: 0; }
    .sub { font-size: 12px; color: var(--app-text-muted); }
    .empty { padding: 14px; font-size: 13px; color: var(--app-text-muted); }
    @media (max-width: 768px) { .search { width: 46vw; } }
  `]
})
export class GlobalSearchComponent {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly currentSociety = inject(CurrentSocietyService);

  readonly icons = TYPE_ICONS;
  readonly term = signal('');
  readonly open = signal(false);
  readonly loading = signal(false);
  readonly results = signal<SearchResult[]>([]);
  readonly groups = computed(() => {
    const map = new Map<string, SearchResult[]>();
    this.results().forEach((r) => map.set(r.type, [...(map.get(r.type) ?? []), r]));
    return Array.from(map, ([type, items]) => ({ type, items }));
  });

  private readonly typed$ = new Subject<string>();

  constructor() {
    this.typed$.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      switchMap((term) => {
        const societyId = this.currentSociety.society()?.id;
        if (term.trim().length < 2 || !societyId) { this.loading.set(false); return of([] as SearchResult[]); }
        this.loading.set(true);
        return this.http.get<ApiResponse<SearchResult[]>>(`${environment.apiUrl}/search`, { params: { societyId, q: term.trim() } })
          .pipe(map((r) => r.data ?? []));
      })
    ).subscribe((results) => { this.results.set(results); this.loading.set(false); });
  }

  onType(value: string): void {
    this.term.set(value);
    this.open.set(true);
    if (value.trim().length >= 2) this.loading.set(true);
    this.typed$.next(value);
  }

  go(result: SearchResult): void {
    this.open.set(false);
    this.term.set('');
    this.results.set([]);
    this.router.navigateByUrl(result.route);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    if (!this.host.nativeElement.contains(event.target)) this.open.set(false);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void { this.open.set(false); }
}
