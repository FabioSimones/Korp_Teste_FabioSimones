/**
 * Product data as returned by Inventory.Api.
 */
export interface Product {
  readonly id: number;
  readonly code: string;
  readonly description: string;
  readonly balance: number;
}

/**
 * Payload accepted by Inventory.Api to register a new product.
 */
export interface CreateProductRequest {
  readonly code: string;
  readonly description: string;
  readonly balance: number;
}

/**
 * Fields Inventory.Api accepts as `sortBy` on `GET /api/products/paged`. Kept
 * in sync with `ProductService.GetPagedAsync` on the backend (case-sensitive
 * as sent; the backend itself lower-cases before comparing).
 */
export type ProductSortField = 'code' | 'description' | 'balance';

export const PRODUCT_SORT_FIELDS: readonly ProductSortField[] = ['code', 'description', 'balance'];

/** Default sort applied by Inventory.Api when none is specified. */
export const DEFAULT_PRODUCT_SORT_FIELD: ProductSortField = 'code';
