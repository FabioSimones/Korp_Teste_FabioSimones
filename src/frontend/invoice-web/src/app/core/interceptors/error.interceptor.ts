import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { getUserFriendlyErrorMessage } from '../utils/http-error-messages';
import { NotificationService } from '../services/notification.service';
import { SKIP_ERROR_NOTIFICATION } from './skip-error-notification.token';

/**
 * Logs failed HTTP requests and shows a generic error notification, unless
 * the request opts out via `SKIP_ERROR_NOTIFICATION` (see that token for
 * when to use it). This keeps a safety-net notification for requests whose
 * caller has no dedicated error UI, without duplicating messages for
 * features that already render specific validation/conflict/error states.
 *
 * No `context` is passed to `getUserFriendlyErrorMessage` here: this
 * interceptor is the generic, screen-agnostic fallback, so it only ever
 * resolves `errorCode`/status to a message, without per-screen wording.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notification = inject(NotificationService);

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      console.error(`[HTTP] ${request.method} ${request.url} failed`, error);
      if (!request.context.get(SKIP_ERROR_NOTIFICATION)) {
        notification.error(getUserFriendlyErrorMessage(error));
      }
      return throwError(() => error);
    }),
  );
};
