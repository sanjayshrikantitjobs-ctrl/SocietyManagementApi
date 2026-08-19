import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { SocietyService } from '../society-setup/services/society.service';
import { VisitorVisitDto } from './models/visitor.model';
import { VisitorService } from './services/visitor.service';

@Component({
  selector: 'app-currently-inside',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTableModule, PageHeaderComponent, SkeletonLoaderComponent, EmptyStateComponent],
  template: `
    <div class="app-page">
      <app-page-header title="Currently Inside" [breadcrumbs]="[{ label: 'Visitors', link: '/visitors' }, { label: 'Currently Inside' }]" />

      @if (loading()) {
        <app-skeleton-loader [rows]="4" [height]="60" />
      } @else if (visits().length === 0) {
        <app-empty-state icon="how_to_reg" title="No one is currently inside" />
      } @else {
        <table mat-table [dataSource]="visits()" class="app-card">
          <ng-container matColumnDef="visitor">
            <th mat-header-cell *matHeaderCellDef>Visitor</th>
            <td mat-cell *matCellDef="let v">{{ v.visitorName }}</td>
          </ng-container>
          <ng-container matColumnDef="flat">
            <th mat-header-cell *matHeaderCellDef>Flat</th>
            <td mat-cell *matCellDef="let v">{{ v.flatNumber }}</td>
          </ng-container>
          <ng-container matColumnDef="purpose">
            <th mat-header-cell *matHeaderCellDef>Purpose</th>
            <td mat-cell *matCellDef="let v">{{ v.purposeName }}</td>
          </ng-container>
          <ng-container matColumnDef="checkedIn">
            <th mat-header-cell *matHeaderCellDef>Checked in</th>
            <td mat-cell *matCellDef="let v">{{ v.checkInTime | date: 'shortTime' }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let v">
              <button mat-flat-button color="primary" (click)="checkOut(v)"><mat-icon>logout</mat-icon> Check Out</button>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      }
    </div>
  `,
  styles: [`table { width: 100%; }`]
})
export class CurrentlyInsideComponent implements OnInit {
  private readonly visitorService = inject(VisitorService);
  private readonly societyService = inject(SocietyService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly visits = signal<VisitorVisitDto[]>([]);
  readonly displayedColumns = ['visitor', 'flat', 'purpose', 'checkedIn', 'actions'];

  private societyId = 0;

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  private load(): void {
    this.loading.set(true);
    this.visitorService.getCurrentlyInside(this.societyId).subscribe((rows) => {
      this.visits.set(rows);
      this.loading.set(false);
    });
  }

  checkOut(visit: VisitorVisitDto): void {
    this.visitorService.checkOutVisit(visit.id).subscribe(() => {
      this.toast.success(`${visit.visitorName} checked out.`);
      this.load();
    });
  }
}
