import { Injectable, effect, inject, signal } from '@angular/core';
import { Society } from '../models/society.model';
import { SocietyService } from '../../features/society-setup/services/society.service';
import { AuthService } from './auth.service';

/**
 * Holds the logged-in user's society for shell branding (logo/name in the
 * sidebar). There's no per-user SocietyId yet — every screen in this app
 * resolves "the" society the same way, by taking the first (only) row from
 * GetSocieties — so this does the same, ready to swap for real per-user
 * resolution once multi-tenancy ships. Starts/clears on auth state exactly
 * like SignalrService's connection lifecycle.
 */
@Injectable({ providedIn: 'root' })
export class CurrentSocietyService {
  private readonly auth = inject(AuthService);
  private readonly societyService = inject(SocietyService);

  readonly society = signal<Society | null>(null);

  constructor() {
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.societyService.getSocieties().subscribe((societies) => {
          this.society.set(societies[0] ?? null);
        });
      } else {
        this.society.set(null);
      }
    });
  }
}
