import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { FestivalCardComponent } from '../../shared/components/festival-card/festival-card.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { Society } from '../../core/models/society.model';
import { SocietyService } from '../society-setup/services/society.service';
import { FestivalFormDialogComponent } from './festival-form-dialog.component';
import { Festival, FestivalStatus } from './models/festival.model';
import { FestivalService } from './services/festival.service';

@Component({
  selector: 'app-festivals-list',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatIconModule, MatSelectModule,
    EmptyStateComponent, FestivalCardComponent, PageHeaderComponent, SkeletonLoaderComponent
  ],
  template: `
    <app-page-header title="Festivals & Events" subtitle="Every festival runs as its own project — budget, contributions, sponsors and expenses.">
      @if (societies().length > 1) {
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="society-picker">
          <mat-select [value]="societyId()" (selectionChange)="onSocietyChange($event.value)">
            @for (s of societies(); track s.id) { <mat-option [value]="s.id">{{ s.name }}</mat-option> }
          </mat-select>
        </mat-form-field>
      }
      @if (canManage()) {
        <button mat-flat-button color="primary" (click)="createFestival()">
          <mat-icon>add</mat-icon> New Festival
        </button>
      }
    </app-page-header>

    <mat-button-toggle-group [value]="statusFilter()" (change)="onStatusFilterChange($event.value)" class="status-filter">
      <mat-button-toggle [value]="null">All</mat-button-toggle>
      <mat-button-toggle [value]="1">Planning</mat-button-toggle>
      <mat-button-toggle [value]="2">Ongoing</mat-button-toggle>
      <mat-button-toggle [value]="3">Completed</mat-button-toggle>
    </mat-button-toggle-group>

    @if (loading()) {
      <app-skeleton-loader [rows]="4" />
    } @else if (festivals().length === 0) {
      <app-empty-state icon="celebration" title="No festivals yet"
        message="Create your first festival to start tracking its budget, contributions and expenses."
        [actionLabel]="canManage() ? 'New Festival' : null" (action)="createFestival()" />
    } @else {
      <div class="grid">
        @for (festival of festivals(); track festival.id) {
          <app-festival-card [festival]="festival" (open)="openFestival(festival)" />
        }
      </div>
    }
  `,
  styles: [`
    .society-picker { width: 220px; margin-right: 8px; }
    .status-filter { margin-bottom: 16px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; }
  `]
})
export class FestivalsListComponent implements OnInit {
  private readonly festivalService = inject(FestivalService);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly societies = signal<Society[]>([]);
  readonly societyId = signal(0);
  readonly festivals = signal<Festival[]>([]);
  readonly statusFilter = signal<FestivalStatus | null>(null);

  canManage(): boolean {
    return this.auth.hasPermission('festivals.manage');
  }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      this.societies.set(societies);
      if (societies.length > 0) {
        this.societyId.set(societies[0].id);
        this.load();
      } else {
        this.loading.set(false);
      }
    });
  }

  onSocietyChange(societyId: number): void {
    this.societyId.set(societyId);
    this.load();
  }

  onStatusFilterChange(status: FestivalStatus | null): void {
    this.statusFilter.set(status);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.festivalService.getFestivals({
      societyId: this.societyId(), status: this.statusFilter() ?? undefined, pageSize: 100
    }).subscribe((result) => {
      this.festivals.set(result.items);
      this.loading.set(false);
    });
  }

  openFestival(festival: Festival): void {
    this.router.navigate(['/festivals', festival.id]);
  }

  createFestival(): void {
    const ref = this.dialog.open(FestivalFormDialogComponent, {
      width: '640px', data: { societyId: this.societyId(), festival: null }
    });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.festivalService.createFestival(result).subscribe(() => {
        this.toast.success('Festival created.');
        this.load();
      });
    });
  }
}
