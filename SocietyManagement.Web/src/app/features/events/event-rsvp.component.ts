import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import * as QRCode from 'qrcode';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { EVENT_RSVP_STATUS_LABELS, EventCapacitySummaryDto, EventDto, EventRsvpDto } from './models/event.model';
import { EventService } from './services/event.service';

/** Member-facing self-registration screen: pick a headcount, see remaining
 * seats live, submit, and get a QR code back that encodes the check-in URL
 * for this flat's RSVP. Generated entirely client-side (the `qrcode` npm
 * package) — the token just needs to be opaque, nothing about it needs to
 * be verifiable without a server round-trip anyway. */
@Component({
  selector: 'app-event-rsvp',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule,
    MatIconModule, MatInputModule, PageHeaderComponent, SkeletonLoaderComponent, EmptyStateComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="RSVP" [subtitle]="event()?.name ?? ''"
        [breadcrumbs]="[{ label: 'Events', link: '/events' }, { label: 'RSVP' }]" />

      @if (loading()) {
        <app-skeleton-loader [rows]="3" [height]="80" />
      } @else if (!event()) {
        <app-empty-state icon="event_busy" title="Event not found" />
      } @else if (event()!.status !== 2) {
        <app-empty-state icon="event_busy" title="RSVPs aren't open"
          message="This event isn't currently accepting RSVPs." />
      } @else {
        <div class="grid">
          <div class="app-card details">
            <h3>{{ event()!.name }}</h3>
            @if (event()!.description) { <p>{{ event()!.description }}</p> }
            <div class="meta">
              <span><mat-icon>event</mat-icon> {{ event()!.eventDateTime | date: 'medium' }}</span>
              @if (event()!.venue) { <span><mat-icon>place</mat-icon> {{ event()!.venue }}</span> }
            </div>
            @if (capacity(); as c) {
              <div class="seats" [class.full]="c.remainingSeats !== null && c.remainingSeats !== undefined && c.remainingSeats <= 0">
                @if (c.remainingSeats === null || c.remainingSeats === undefined) {
                  Unlimited seats available
                } @else {
                  {{ c.remainingSeats > 0 ? c.remainingSeats + ' seat(s) left' : 'This event is full' }}
                }
              </div>
            }
          </div>

          <div class="app-card">
            @if (myRsvp(); as rsvp) {
              <div class="rsvp-status">
                <span class="badge" [class.badge-success]="rsvp.status === 2" [class.badge-info]="rsvp.status === 1">
                  {{ rsvpStatusLabels[rsvp.status] }}
                </span>
                <p>Registered for <strong>{{ rsvp.headCount }}</strong> member(s).</p>
              </div>

              @if (rsvp.status === 1) {
                <div class="qr-block">
                  <p class="hint">Show this QR at the door — the admin scans it to check your flat in.</p>
                  @if (qrDataUrl()) { <img [src]="qrDataUrl()" alt="Check-in QR code" class="qr-image" /> }
                </div>

                <form [formGroup]="form" (ngSubmit)="submit()" class="update-form">
                  <mat-form-field appearance="outline">
                    <mat-label>Update Headcount</mat-label>
                    <input matInput type="number" formControlName="headCount" min="1" />
                  </mat-form-field>
                  <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || submitting()">Update</button>
                  <button mat-button type="button" color="warn" (click)="cancelRsvp(rsvp.id)" [disabled]="submitting()">Cancel RSVP</button>
                </form>
              }
            } @else {
              <form [formGroup]="form" (ngSubmit)="submit()" class="rsvp-form">
                <mat-form-field appearance="outline">
                  <mat-label>How many members are attending?</mat-label>
                  <input matInput type="number" formControlName="headCount" min="1" />
                </mat-form-field>
                <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || submitting()">
                  Confirm RSVP
                </button>
              </form>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; align-items: start; }
    .details h3 { margin: 0 0 8px; }
    .meta { display: flex; gap: 16px; margin: 12px 0; font-size: 13px; color: var(--app-text-muted); }
    .meta span { display: flex; align-items: center; gap: 4px; }
    .meta mat-icon { font-size: 18px; width: 18px; height: 18px; }
    .seats { margin-top: 12px; padding: 8px 12px; border-radius: 8px; background: #ecfdf5; color: #15803d; font-weight: 600; font-size: 14px; }
    .seats.full { background: #fef2f2; color: #dc2626; }
    .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .badge-success { background: #dcfce7; color: #15803d; }
    .badge-info { background: #dbeafe; color: #1d4ed8; }
    .rsvp-status p { margin: 8px 0 16px; }
    .qr-block { text-align: center; margin: 16px 0; }
    .qr-block .hint { font-size: 12px; color: var(--app-text-muted); }
    .qr-image { width: 200px; height: 200px; border: 1px solid var(--app-border); border-radius: 8px; padding: 8px; }
    .rsvp-form, .update-form { display: flex; flex-direction: column; gap: 8px; }
    .update-form { flex-direction: row; align-items: flex-start; flex-wrap: wrap; }
  `]
})
export class EventRsvpComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly eventService = inject(EventService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly event = signal<EventDto | null>(null);
  readonly capacity = signal<EventCapacitySummaryDto | null>(null);
  readonly myRsvp = signal<EventRsvpDto | null>(null);
  readonly qrDataUrl = signal<string | null>(null);
  readonly rsvpStatusLabels: Record<number, string> = EVENT_RSVP_STATUS_LABELS;

  private eventId = 0;

  form = this.fb.nonNullable.group({
    headCount: [1, [Validators.required, Validators.min(1)]]
  });

  ngOnInit(): void {
    this.eventId = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.eventService.getEventById(this.eventId).subscribe({
      next: (e) => {
        this.event.set(e);
        this.eventService.getCapacitySummary(this.eventId).subscribe((c) => this.capacity.set(c));
        this.eventService.getMyRsvp(this.eventId).subscribe((rsvp) => {
          this.myRsvp.set(rsvp);
          if (rsvp) {
            this.form.patchValue({ headCount: rsvp.headCount });
            this.renderQr(rsvp.qrToken);
          }
          this.loading.set(false);
        });
      },
      error: () => this.loading.set(false)
    });
  }

  private renderQr(qrToken: string): void {
    const url = `${window.location.origin}/events/check-in/${qrToken}`;
    QRCode.toDataURL(url, { width: 240, margin: 1 }).then((dataUrl) => this.qrDataUrl.set(dataUrl));
  }

  submit(): void {
    if (this.form.invalid) return;
    this.submitting.set(true);
    this.eventService.createOrUpdateRsvp(this.eventId, this.form.getRawValue().headCount).subscribe({
      next: (rsvp) => {
        this.myRsvp.set(rsvp);
        this.renderQr(rsvp.qrToken);
        this.eventService.getCapacitySummary(this.eventId).subscribe((c) => this.capacity.set(c));
        this.toast.success('RSVP saved.');
        this.submitting.set(false);
      },
      error: () => this.submitting.set(false)
    });
  }

  cancelRsvp(id: number): void {
    this.confirmDialog.confirm({
      title: 'Cancel RSVP', destructive: true, message: 'Cancel your RSVP for this event?'
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.eventService.cancelRsvp(id).subscribe(() => {
        this.myRsvp.set(null);
        this.qrDataUrl.set(null);
        this.form.reset({ headCount: 1 });
        this.eventService.getCapacitySummary(this.eventId).subscribe((c) => this.capacity.set(c));
        this.toast.success('RSVP cancelled.');
      });
    });
  }
}
