import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { SKIP_ERROR_NOTIFICATION } from '../../core/interceptors/skip-error-notification.token';
import { PagedResponse } from '../../shared/pagination/paged-response';
import { SortDirection } from '../../shared/pagination/sort';
import { CreateProductRequest, Product, ProductSortField } from './models/product';

/**
 * Typed HTTP client for the Inventory.Api products endpoints. Keeps
 * the feature components free of HttpClient/URL details.
 */
@Injectable({ providedIn: 'root' })
export class ProductsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.inventoryApiUrl}/api/products`;

  // ProductsPage already renders specific error UI for both requests
  // (list error state with retry, field/form validation and conflict
  // messages), so the interceptor's generic notification is skipped here
  // to avoid a redundant toast on top of it.
  private readonly context = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  getAll(): Observable<Product[]> {
    return this.http.get<Product[]>(this.baseUrl, { context: this.context });
  }

  /**
   * Server-side paginated listing, used exclusively by `ProductsPage`. Kept
   * separate from `getAll()`, which continues to be used by the new-invoice
   * form's product picker (no pagination there).
   */
  getPaged(
    pageNumber: number,
    pageSize: number,
    sortBy: ProductSortField,
    sortDirection: SortDirection,
  ): Observable<PagedResponse<Product>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize)
      .set('sortBy', sortBy)
      .set('sortDirection', sortDirection);
    return this.http.get<PagedResponse<Product>>(`${this.baseUrl}/paged`, {
      context: this.context,
      params,
    });
  }

  create(request: CreateProductRequest): Observable<Product> {
    return this.http.post<Product>(this.baseUrl, request, { context: this.context });
  }
}
