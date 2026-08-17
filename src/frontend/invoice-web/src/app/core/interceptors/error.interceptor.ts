import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { NotificationService } from '../services/notification.service';

/**
 * Skeleton interceptor: logs failed HTTP requests and shows a generic
 * notification. Feature-specific error handling (validation, conflicts,
 * business messages) will be added when the corresponding screens are built.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notification = inject(NotificationService);

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      console.error(`[HTTP] ${request.method} ${request.url} failed`, error);
      notification.error('Não foi possível concluir a operação. Tente novamente.');
      return throwError(() => error);
    }),
  );
};
