import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { TokenStorageService } from '../services/token-storage.service';

let isRefreshing = false;
const refreshTokenSubject = new BehaviorSubject<string | null>(null);

/**
 * Attaches the access token to every request and transparently retries once
 * with a refreshed token on a 401 (single-flight: concurrent 401s all wait
 * on the same in-flight refresh call instead of each triggering their own).
 */
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const tokenStorage = inject(TokenStorageService);
  const auth = inject(AuthService);
  const router = inject(Router);

  const token = tokenStorage.getAccessToken();
  const authReq = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // /auth/logout excluded too: without this, a 401 here (expired access
      // token) triggers a refresh attempt; if the refresh token is also
      // expired/invalid, the failure handler below calls auth.logout() again
      // to force-clear the session — which re-POSTs /auth/logout, 401s again,
      // and loops forever. A failed logout call means the session was already
      // dead; just let AuthService.logout()'s own error handler clear it locally.
      const isAuthEndpoint = req.url.includes('/auth/login') || req.url.includes('/auth/refresh-token')
        || req.url.includes('/auth/logout');

      if (error.status !== 401 || isAuthEndpoint) {
        return throwError(() => error);
      }

      if (!isRefreshing) {
        isRefreshing = true;
        refreshTokenSubject.next(null);

        return auth.refreshToken().pipe(
          switchMap((res) => {
            isRefreshing = false;
            refreshTokenSubject.next(res.accessToken);
            return next(req.clone({ setHeaders: { Authorization: `Bearer ${res.accessToken}` } }));
          }),
          catchError((refreshError) => {
            isRefreshing = false;
            auth.logout();
            router.navigate(['/auth/login']);
            return throwError(() => refreshError);
          })
        );
      }

      return refreshTokenSubject.pipe(
        filter((newToken) => newToken !== null),
        take(1),
        switchMap((newToken) => next(req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } })))
      );
    })
  );
};
