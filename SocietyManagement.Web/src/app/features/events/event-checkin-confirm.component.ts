import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { EVENT_RSVP_STATUS_LABELS, EventRsvpDto } from './models/event.model';
import { EventService } from './services/event.service';

/** Where a phone camera app lands after scanning a flat's QR — this route
 * is the entire "scanner": no camera/decode library needed, the phone's
 * own camera surfaces the link and this page does the rest. Admin-only via
 * roleGuard on the route. */
@Component({
  selector: 'app-event-checkin-confirm',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule,
    MatIconModule, MatInputModule, SkeletonLoaderComponent, EmptyStateComponent
  ],
  template: `
    <div class="confirm-page">
      <div class="app-card">
        @if (loading()) {
          <app-skeleton-loader [rows]="3" [height]="50" />
        } @else if (!rsvp()) {
          <app-empty-state icon="qr_code_scanner" title="RSVP not found" message="This QR code isn't valid or has been removed." />
        } @else if (rsvp()!.status === 3) {
          <app-empty-state icon="cancel" title="RSVP cancelled" message="This flat's RSVP was cancelled and can't be checked in." />
        } @else if (rsvp()!.status === 2) {
          <app-empty-state icon="check_circle" title="Already checked in"
            [message]="'Flat ' + rsvp()!.flatNumber + ' checked in with ' + rsvp()!.checkedInCount + ' member(s).'" />
        } @else {
          <h2>Flat {{ rsvp()!.flatNumber }}</h2>
          <p class="registered-by">{{ rsvp()!.memberName }} — {{ rsvp()!.memberPhone }}</p>
          <p class="registered-count">Registered for <strong>{{ rsvp()!.headCount }}</strong> member(s).</p>

          <form [formGroup]="form" (ngSubmit)="confirm()">
            <mat-form-field appearance="outline">
              <mat-label>Actual Headcount Arrived</mat-label>
              <input matInput type="number" formControlName="actualHeadCount" min="1" />
            </mat-form-field>
            <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || confirming()">
              Confirm Check-in
            </button>
          </form>
        }
      </div>
    </div>
  `,
  styles: [`
    .confirm-page { max-width: 420px; margin: 40px auto; padding: 0 16px; }
    h2 { margin: 0 0 4px; }
    .registered-by { color: var(--app-text-muted); font-size: 13px; margin: 0 0 12px; }
    .registered-count { margin: 0 0 20px; }
    form { display: flex; flex-direction: column; gap: 12px; }
  `]
})
export class EventCheckinConfirmComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly eventService = inject(EventService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly confirming = signal(false);
  readonly rsvp = signal<EventRsvpDto | null>(null);
  readonly rsvpStatusLabels: Record<number, string> = EVENT_RSVP_STATUS_LABELS;

  private qrToken = '';

  form = this.fb.nonNullable.group({
    actualHeadCount: [1, [Validators.required, Validators.min(1)]]
  });

  ngOnInit(): void {
    this.qrToken = this.route.snapshot.paramMap.get('qrToken') ?? '';
    this.eventService.getRsvpByToken(this.qrToken).subscribe({
      next: (rsvp) => {
        this.rsvp.set(rsvp);
        this.form.patchValue({ actualHeadCount: rsvp.headCount });
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  confirm(): void {
    if (this.form.invalid) return;
    this.confirming.set(true);
    this.eventService.checkIn(this.qrToken, this.form.getRawValue().actualHeadCount).subscribe({
      next: (rsvp) => {
        this.rsvp.set(rsvp);
        this.toast.success('Checked in.');
        this.confirming.set(false);
      },
      error: () => this.confirming.set(false)
    });
  }
}
