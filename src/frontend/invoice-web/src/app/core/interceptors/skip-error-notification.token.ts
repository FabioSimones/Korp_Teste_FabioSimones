import { HttpContextToken } from '@angular/common/http';

/**
 * When set to `true` in a request's `HttpContext`, `errorInterceptor` skips
 * showing its generic error notification for that request.
 *
 * Use this on requests whose calling feature already renders a specific
 * error message (field validation, conflict, empty/error states with
 * retry, etc.) so users are not shown a redundant generic toast on top of
 * the specific one.
 */
export const SKIP_ERROR_NOTIFICATION = new HttpContextToken<boolean>(() => false);
