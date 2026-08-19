import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { LoginResponse, UserProfile } from '../models/user.model';
import { TokenStorageService } from './token-storage.service';

/**
 * Single source of truth for "who is logged in" — a signal rather than a
 * BehaviorSubject so templates and guards can read `currentUser()` directly
 * without an async pipe, per the Angular Signals requirement in the spec.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokenStorage = inject(TokenStorageService);
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  readonly currentUser = signal<UserProfile | null>(null);
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly permissions = computed(() => this.currentUser()?.permissions ?? []);
  readonly roleName = computed(() => this.currentUser()?.roleName ?? null);
  readonly isAdmin = computed(() => this.roleName() === 'Admin');

  hasPermission(code: string): boolean {
    return this.permissions().includes(code);
  }

  login(identifier: string, password: string): Observable<LoginResponse> {
    return this.http.post<ApiResponse<LoginResponse>>(`${this.baseUrl}/login`, { identifier, password }).pipe(
      map((res) => res.data!),
      tap((data) => this.applySession(data))
    );
  }

  /** Rehydrates session on app start if a refresh token is present — see
   * app.config.ts's APP_INITIALIZER-equivalent provideAppInitializer. */
  restoreSession(): Observable<UserProfile | null> {
    if (!this.tokenStorage.getRefreshToken()) {
      return of(null);
    }
    return this.http.get<ApiResponse<UserProfile>>(`${this.baseUrl}/me`).pipe(
      map((res) => res.data),
      tap((data) => this.currentUser.set(data)),
      catchError(() => {
        this.clearSession();
        return of(null);
      })
    );
  }

  refreshToken(): Observable<LoginResponse> {
    const refreshToken = this.tokenStorage.getRefreshToken();
    const accessToken = this.tokenStorage.getAccessToken();
    return this.http.post<ApiResponse<LoginResponse>>(`${this.baseUrl}/refresh-token`, {
      accessToken, refreshToken
    }).pipe(
      map((res) => res.data!),
      tap((data) => this.applySession(data))
    );
  }

  logout(): void {
    const refreshToken = this.tokenStorage.getRefreshToken();
    this.http.post(`${this.baseUrl}/logout`, { refreshToken }).subscribe({
      complete: () => this.clearSession(),
      error: () => this.clearSession()
    });
  }

  forgotPassword(identifier: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.baseUrl}/forgot-password`, { identifier });
  }

  resetPassword(identifier: string, otpCode: string, newPassword: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.baseUrl}/reset-password`, {
      identifier, otpCode, newPassword
    });
  }

  changePassword(currentPassword: string, newPassword: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.baseUrl}/change-password`, {
      currentPassword, newPassword
    }).pipe(
      tap(() => {
        const user = this.currentUser();
        if (user) this.currentUser.set({ ...user, mustChangePassword: false });
      })
    );
  }

  private applySession(data: LoginResponse): void {
    this.tokenStorage.setTokens(data.accessToken, data.refreshToken);
    this.currentUser.set(data.user);
  }

  private clearSession(): void {
    this.tokenStorage.clear();
    this.currentUser.set(null);
  }
}
