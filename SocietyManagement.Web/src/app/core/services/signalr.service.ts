import { Injectable, effect, inject, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { ToastService } from './toast.service';
import { TokenStorageService } from './token-storage.service';

export interface RealtimeNotification {
  eventName: string;
  payload: unknown;
  receivedAt: Date;
}

/**
 * Thin wrapper around @microsoft/signalr connecting to
 * Infrastructure.Hubs.NotificationHub (see API's /hubs/notifications). Starts
 * automatically once the user is authenticated and stops on logout. Currently
 * surfaces every event as a toast + appends to `notifications` — feature
 * modules (Notices, Complaints, Maintenance, Festivals, ...) subscribe to
 * specific event names as those modules ship, per spec's "Real-time: Use
 * SignalR" requirement (new notice, complaint update, payment success, ...).
 */
@Injectable({ providedIn: 'root' })
export class SignalrService {
  private readonly auth = inject(AuthService);
  private readonly tokenStorage = inject(TokenStorageService);
  private readonly toast = inject(ToastService);

  private connection: signalR.HubConnection | null = null;
  readonly notifications = signal<RealtimeNotification[]>([]);
  readonly connected = signal(false);

  constructor() {
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.connect();
      } else {
        this.disconnect();
      }
    });
  }

  private connect(): void {
    if (this.connection) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl, { accessTokenFactory: () => this.tokenStorage.getAccessToken() ?? '' })
      .withAutomaticReconnect()
      .build();

    this.connection.onreconnected(() => this.connected.set(true));
    this.connection.onclose(() => this.connected.set(false));

    // Generic catch-all wiring for the events named in the spec; each module
    // adds its own strongly-typed `.on(...)` handler as it ships.
    [
      'NewNotice', 'ComplaintUpdate', 'PaymentSuccess', 'FestivalReminder',
      'FestivalContributionRecorded', 'FestivalExpenseApproved',
      'VisitorApprovalRequested', 'VisitorApproved', 'VisitorRejected', 'VisitorRequestExpired',
      'SupportTicketCreated', 'SupportTicketResolved'
    ].forEach((eventName) => {
      this.connection!.on(eventName, (payload: unknown) => {
        this.notifications.update((list) => [{ eventName, payload, receivedAt: new Date() }, ...list].slice(0, 50));
        this.toast.info(`${eventName}: new update received.`);
      });
    });

    this.connection.start()
      .then(() => this.connected.set(true))
      .catch(() => this.connected.set(false));
  }

  private disconnect(): void {
    this.connection?.stop();
    this.connection = null;
    this.connected.set(false);
  }
}
