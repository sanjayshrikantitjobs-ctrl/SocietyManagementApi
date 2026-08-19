import { Injectable, signal } from '@angular/core';

/** Backs a global top-bar progress indicator — incremented/decremented by
 * loading.interceptor.ts around every HTTP call. */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private count = 0;
  readonly isLoading = signal(false);

  start(): void {
    this.count++;
    this.isLoading.set(true);
  }

  stop(): void {
    this.count = Math.max(0, this.count - 1);
    this.isLoading.set(this.count > 0);
  }
}
