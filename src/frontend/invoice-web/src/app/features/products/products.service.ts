import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { SKIP_ERROR_NOTIFICATION } from '../../core/interceptors/skip-error-notification.token';
import { CreateProductRequest, Product } from './models/product';

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

  create(request: CreateProductRequest): Observable<Product> {
    return this.http.post<Product>(this.baseUrl, request, { context: this.context });
  }
}
