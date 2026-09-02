import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { Society } from '../../core/models/society.model';
import { SocietyService } from '../society-setup/services/society.service';
import { VisitorService } from './services/visitor.service';

/** Admin/Super Admin-only — how long a pending approval stays open, and how
 * long visitor gate-entry history (and photos) are kept before automatic
 * cleanup. Gated by Permissions.Visitors.Manage at the route/controller
 * level, same as Gates/Purposes. */
@Component({
  selector: 'app-visitor-settings',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule,
    MatProgressSpinnerModule, PageHeaderComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Visitor Settings" subtitle="Approval timeouts and how long visitor history is kept."
        [breadcrumbs]="[{ label: 'Visitors', link: '/visitors' }, { label: 'Settings' }]" />

      @if (loading()) {
        <div class="loading"><mat-spinner diameter="32" /></div>
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()" class="app-card form-card">
          <h3>Approval Requests</h3>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Approval Request Expiry (minutes)</mat-label>
            <input matInput type="number" formControlName="approvalRequestExpiryMinutes" />
            <mat-hint>A pending visitor request auto-expires after this many minutes with no response.</mat-hint>
          </mat-form-field>

          <h3>Data Retention</h3>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Keep Visitor History (days)</mat-label>
            <input matInput type="number" formControlName="retentionDays" />
            <mat-hint>Visitor gate-entry records and photos older than this are automatically deleted from the database and storage. Default 30 days.</mat-hint>
          </mat-form-field>

          <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || saving()">
            @if (saving()) { <mat-spinner diameter="18" /> } @else { Save Settings }
          </button>
        </form>
      }
    </div>
  `,
  styles: [`
    .loading { display: flex; justify-content: center; padding: 60px; }
    .form-card { max-width: 480px; padding: 24px; }
    h3 { margin: 20px 0 8px; font-size: 14px; }
    h3:first-child { margin-top: 0; }
    .full-width { width: 100%; }
  `]
})
export class VisitorSettingsComponent implements OnInit {
  private readonly visitorService = inject(VisitorService);
  private readonly societyService = inject(SocietyService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly saving = signal(false);

  private societyId = 0;

  form = this.fb.nonNullable.group({
    approvalRequestExpiryMinutes: [30, [Validators.required, Validators.min(1), Validators.max(1440)]],
    retentionDays: [30, [Validators.required, Validators.min(1), Validators.max(3650)]]
  });

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies: Society[]) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.visitorService.getSettings(this.societyId).subscribe((settings) => {
        this.form.patchValue(settings);
        this.loading.set(false);
      });
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.visitorService.saveSettings({ societyId: this.societyId, ...this.form.getRawValue() }).subscribe({
      next: () => {
        this.toast.success('Settings saved.');
        this.saving.set(false);
      },
      error: () => this.saving.set(false)
    });
  }
}
