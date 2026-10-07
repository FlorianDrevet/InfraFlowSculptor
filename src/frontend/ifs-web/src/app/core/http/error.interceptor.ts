import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { normalizeHttpError } from './api-error';

/**
 * Central place to react to HTTP failures (logging, telemetry, normalizing
 * error shapes). It re-throws so callers still decide how to present the
 * error to the user; it does not swallow errors or redirect on its own —
 * that kind of global side effect belongs in the `auth` schematic's own
 * interceptor, kept separate on purpose.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const normalized = normalizeHttpError(error);
        console.error(
          `[http] ${req.method} ${req.url} failed with ${normalized.status} (${normalized.code})`,
        );
        return throwError(() => normalized);
      }
      return throwError(() => error);
    }),
  );
