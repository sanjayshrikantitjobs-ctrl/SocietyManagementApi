import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { Society } from '../../../core/models/society.model';
import { SocietyService } from '../../society-setup/services/society.service';
import { MaintenanceService } from '../services/maintenance.service';

@Component({
  selector: 'app-maintenance-settings',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule,
    MatProgressSpinnerModule, MatSlideToggleModule
  ],
  template: `
    <div class="tab-content">
      @if (loading()) {
        <div class="loading"><mat-spinner diameter="32" /></div>
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()" class="app-card form-card">
          <h3>Bill Generation &amp; Due Dates</h3>
          <div class="grid">
            <mat-form-field appearance="outline"><mat-label>Bill Generation Day (1-28)</mat-label><input matInput type="number" formControlName="billGenerationDay" /></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Due Day (1-28)</mat-label><input matInput type="number" formControlName="dueDay" /></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Grace Period (days)</mat-label><input matInput type="number" formControlName="gracePeriodDays" /></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Late Fee Amount</mat-label><input matInput type="number" formControlName="lateFeeAmount" /></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Invoice Number Prefix</mat-label><input matInput formControlName="invoiceNumberPrefix" /></mat-form-field>
          </div>

          @if (auth.isSuperAdmin()) {
            <h3>WhatsApp Sending</h3>
            <mat-slide-toggle formControlName="whatsAppEnabled">
              Send WhatsApp messages for maintenance bills
            </mat-slide-toggle>
          }

          <h3>WhatsApp Message Template</h3>
          <p class="hint">Placeholders: {{ '{OwnerName}' }} {{ '{Month}' }} {{ '{Amount}' }} {{ '{DueDate}' }}</p>
          <mat-form-field appearance="outline" class="full-width">
            <textarea matInput rows="5" formControlName="whatsAppMessageTemplate"></textarea>
          </mat-form-field>

          <h3>PDF Invoice Footer</h3>
          <mat-form-field appearance="outline" class="full-width">
            <textarea matInput rows="2" formControlName="pdfFooterMessage"></textarea>
          </mat-form-field>

          <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || saving()">
            @if (saving()) { <mat-spinner diameter="18" /> } @else { Save Settings }
          </button>
        </form>
      }
    </div>
  `,
  styles: [`
    .tab-content { padding: 20px 0; }
    .loading { display: flex; justify-content: center; padding: 60px; }
    .form-card { max-width: 640px; padding: 24px; }
    h3 { margin: 20px 0 8px; font-size: 14px; }
    h3:first-child { margin-top: 0; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0 16px; }
    .full-width { width: 100%; }
    .hint { margin: 0 0 8px; font-size: 12px; color: var(--app-text-muted); }
  `]
})
export class MaintenanceSettingsComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly societyService = inject(SocietyService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly saving = signal(false);

  private societyId = 0;

  form = this.fb.nonNullable.group({
    billGenerationDay: [1, [Validators.required, Validators.min(1), Validators.max(28)]],
    dueDay: [10, [Validators.required, Validators.min(1), Validators.max(28)]],
    gracePeriodDays: [0, [Validators.required, Validators.min(0)]],
    lateFeeAmount: [0, [Validators.required, Validators.min(0)]],
    invoiceNumberPrefix: ['INV', Validators.required],
    whatsAppMessageTemplate: ['', Validators.required],
    pdfFooterMessage: ['', Validators.required],
    whatsAppEnabled: [true]
  });

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies: Society[]) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.maintenanceService.getSettings(this.societyId).subscribe((settings) => {
        this.form.patchValue(settings);
        this.loading.set(false);
      });
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.maintenanceService.saveSettings({ societyId: this.societyId, ...this.form.getRawValue() }).subscribe({
      next: () => {
        this.toast.success('Settings saved.');
        this.saving.set(false);
      },
      error: () => this.saving.set(false)
    });
  }
}
