import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { SKIP_ERROR_NOTIFICATION } from '../../core/interceptors/skip-error-notification.token';
import { PagedResponse } from '../../shared/pagination/paged-response';
import { SortDirection } from '../../shared/pagination/sort';
import { CreateInvoiceRequest, Invoice, InvoiceSortField, InvoiceSummary } from './models/invoice';

/**
 * Typed HTTP client for the Billing.Api invoices endpoints. Keeps the
 * feature components free of HttpClient/URL details.
 */
@Injectable({ providedIn: 'root' })
export class InvoicesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.billingApiUrl}/api/invoices`;

  // Every screen that calls this service already renders specific error UI
  // (list error state with retry, detail not-found/error state, form
  // validation/conflict messages), so the interceptor's generic notification
  // is skipped here to avoid a redundant toast on top of it.
  private readonly context = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  getAll(): Observable<Invoice[]> {
    return this.http.get<Invoice[]>(this.baseUrl, { context: this.context });
  }

  /** Server-side paginated listing, used exclusively by `InvoicesListPage`. */
  getPaged(
    pageNumber: number,
    pageSize: number,
    sortBy: InvoiceSortField,
    sortDirection: SortDirection,
  ): Observable<PagedResponse<InvoiceSummary>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize)
      .set('sortBy', sortBy)
      .set('sortDirection', sortDirection);
    return this.http.get<PagedResponse<InvoiceSummary>>(`${this.baseUrl}/paged`, {
      context: this.context,
      params,
    });
  }

  getById(id: number): Observable<Invoice> {
    return this.http.get<Invoice>(`${this.baseUrl}/${id}`, { context: this.context });
  }

  create(request: CreateInvoiceRequest): Observable<Invoice> {
    return this.http.post<Invoice>(this.baseUrl, request, { context: this.context });
  }

  // The print screen renders its own status-specific messages for 404/409/503
  // (see invoice-detail-page), so the generic interceptor notification is
  // skipped here too, same as the other calls in this service.
  print(id: number): Observable<Invoice> {
    return this.http.post<Invoice>(`${this.baseUrl}/${id}/print`, null, {
      context: this.context,
    });
  }
}
