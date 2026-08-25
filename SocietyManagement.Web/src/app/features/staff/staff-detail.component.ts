import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { AssetUrlPipe } from '../../shared/pipes/asset-url.pipe';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { StaffFormDialogComponent } from './staff-form-dialog.component';
import { STAFF_CATEGORY_LABELS, StaffDto } from './models/staff.model';
import { StaffService } from './services/staff.service';

@Component({
  selector: 'app-staff-detail',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatChipsModule, MatIconModule, AssetUrlPipe, PageHeaderComponent],
  template: `
    @if (staff(); as s) {
      <div class="app-page">
        <app-page-header [title]="s.firstName + ' ' + s.lastName" [subtitle]="categoryLabels[s.category]"
          [breadcrumbs]="[{ label: 'Staff', link: '/staff' }, { label: s.firstName + ' ' + s.lastName }]">
          <mat-chip-set><mat-chip [class.active]="s.isActive" [class.inactive]="!s.isActive">{{ s.isActive ? 'Active' : 'Inactive' }}</mat-chip></mat-chip-set>
          <button mat-stroked-button (click)="edit()"><mat-icon>edit</mat-icon> Edit</button>
          <button mat-stroked-button color="warn" (click)="remove()"><mat-icon>delete_outline</mat-icon> Delete</button>
        </app-page-header>

        <div class="panel">
          @if (s.photoUrl) { <img [src]="s.photoUrl | assetUrl" class="photo" alt="" /> }
          <table class="details">
            <tr><td>Phone</td><td>{{ s.phone }}</td></tr>
            <tr><td>Email</td><td>{{ s.email ?? '—' }}</td></tr>
            <tr><td>Address</td><td>{{ s.address ?? '—' }}</td></tr>
            <tr><td>Joining Date</td><td>{{ s.joiningDate | date: 'mediumDate' }}</td></tr>
            <tr><td>Salary</td><td>₹{{ s.salary | number }}</td></tr>
            <tr><td>Salary Pay Day</td><td>{{ s.salaryPayDay }} of every month</td></tr>
            <tr>
              <td>Joining Document</td>
              <td>
                @if (s.joiningDocumentUrl) {
                  <a [href]="s.joiningDocumentUrl | assetUrl" target="_blank">View Document</a>
                } @else { — }
              </td>
            </tr>
          </table>
        </div>
      </div>
    }
  `,
  styles: [`
    .panel { border: 1px solid var(--app-border); border-radius: 10px; padding: 20px; background: var(--app-surface); }
    .photo { width: 96px; height: 96px; border-radius: 50%; object-fit: cover; margin-bottom: 16px; }
    .details { width: 100%; border-collapse: collapse; }
    .details td { padding: 10px 8px; border-top: 1px solid var(--app-border); font-size: 14px; }
    .details td:first-child { color: var(--app-text-muted); width: 180px; }
    .active { background: #dcfce7 !important; color: #15803d !important; }
    .inactive { background: #f1f5f9 !important; color: #64748b !important; }
  `]
})
export class StaffDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly staffService = inject(StaffService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly staff = signal<StaffDto | null>(null);
  readonly categoryLabels: Record<number, string> = STAFF_CATEGORY_LABELS;

  private id = 0;

  ngOnInit(): void {
    this.id = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
  }

  load(): void {
    this.staffService.getStaffById(this.id).subscribe((s) => this.staff.set(s));
  }

  edit(): void {
    const current = this.staff();
    if (!current) return;
    const ref = this.dialog.open(StaffFormDialogComponent, { data: { staff: current, societyId: current.societyId } });
    ref.afterClosed().subscribe((result) => {
      if (!result) return;
      this.staffService.updateStaff(this.id, { ...result, isActive: current.isActive }).subscribe(() => {
        this.toast.success('Staff member updated.');
        this.load();
      });
    });
  }

  remove(): void {
    const current = this.staff();
    if (!current) return;
    this.confirmDialog.confirm({
      title: 'Delete Staff Member', destructive: true, message: `Delete ${current.firstName} ${current.lastName}?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.staffService.deleteStaff(this.id).subscribe(() => {
        this.toast.success('Staff member deleted.');
        this.router.navigate(['/staff']);
      });
    });
  }
}
