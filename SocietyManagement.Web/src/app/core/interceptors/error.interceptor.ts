import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { ToastService } from '../services/toast.service';

/** Unwraps the standard ApiResponse error envelope and shows a toast, once,
 * for every failed HTTP call — individual components don't need their own
 * error-toast boilerplate in every subscribe(). */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        return throwError(() => error); // handled by jwtInterceptor's refresh flow
      }

      const body = error.error as ApiResponse<unknown> | undefined;
      const message = body?.errors?.length
        ? body.errors.join(' ')
        : body?.message || error.message || 'Something went wrong. Please try again.';

      if (error.status === 429) {
        toast.error('Too many requests — please slow down and try again shortly.');
      } else if (error.status === 0) {
        toast.error('Unable to reach the server. Check your connection and try again.');
      } else {
        toast.error(message);
      }

      return throwError(() => error);
    })
  );
};
