import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { FestivalBudgetRevisionDto } from '../models/festival.model';
import { FestivalService } from '../services/festival.service';

/** Read-only revision history list — the "Budget Revision History" the spec
 * requires, opened from a budget category's history button. */
@Component({
  selector: 'app-budget-revisions-dialog',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatDialogModule, EmptyStateComponent],
  template: `
    <h2 mat-dialog-title>Budget Revision History</h2>
    <mat-dialog-content>
      @if (revisions().length === 0) {
        <app-empty-state icon="history" title="No revisions yet" message="Changes to estimated/approved amounts will be logged here." />
      } @else {
        <table class="revisions-table">
          <thead><tr><th>Date</th><th>Estimated</th><th>Approved</th><th>Reason</th><th>By</th></tr></thead>
          <tbody>
            @for (r of revisions(); track r.id) {
              <tr>
                <td>{{ r.createdAt | date: 'short' }}</td>
                <td>₹{{ r.previousEstimatedAmount | number }} → ₹{{ r.newEstimatedAmount | number }}</td>
                <td>₹{{ r.previousApprovedAmount | number }} → ₹{{ r.newApprovedAmount | number }}</td>
                <td>{{ r.reason || '—' }}</td>
                <td>{{ r.createdBy || '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end"><button mat-button mat-dialog-close>Close</button></mat-dialog-actions>
  `,
  styles: [`
    .revisions-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .revisions-table th, .revisions-table td { text-align: left; padding: 8px; border-bottom: 1px solid var(--app-border); }
  `]
})
export class BudgetRevisionsDialogComponent implements OnInit {
  private readonly categoryId = inject<number>(MAT_DIALOG_DATA);
  private readonly festivalService = inject(FestivalService);

  readonly revisions = signal<FestivalBudgetRevisionDto[]>([]);

  ngOnInit(): void {
    this.festivalService.getBudgetRevisions(this.categoryId).subscribe((data) => this.revisions.set(data));
  }
}
