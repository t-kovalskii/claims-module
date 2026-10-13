import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';

/** Shape of the API's RFC 7807 ProblemDetails body (see GlobalExceptionHandler). */
interface ApiProblemDetails {
  detail?: string;
  unmetConditions?: string[];
}

/** Turns a failed API call into a user-friendly snackbar instead of a silent failure. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const snackBar = inject(MatSnackBar);
  const authService = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const isLoginRequest = req.url.endsWith('/auth/login');

        if (error.status === 401 && isLoginRequest) {
          snackBar.open('Login failed: that username is not recognised.', 'Dismiss', { duration: 6000 });
        } else if (error.status === 401) {
          authService.logout();
          router.navigate(['/login']);
          snackBar.open('Your session has expired. Please log in again.', 'Dismiss', { duration: 6000 });
        } else {
          const body = error.error as ApiProblemDetails | null;
          const detail = typeof body?.detail === 'string' ? body.detail : error.message;
          const message = body?.unmetConditions?.length
            ? `${detail} (${body.unmetConditions.join('; ')})`
            : detail;
          snackBar.open(`Something went wrong: ${message}`, 'Dismiss', { duration: 6000 });
        }
      }

      return throwError(() => error);
    })
  );
};
