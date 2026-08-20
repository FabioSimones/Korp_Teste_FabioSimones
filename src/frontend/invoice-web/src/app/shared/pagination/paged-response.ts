/**
 * Generic envelope returned by the server-side paginated listing endpoints
 * (`GET /api/products/paged`, `GET /api/invoices/paged`). Both `Inventory.Api`
 * and `Billing.Api` expose the same JSON shape from their own independent
 * `PagedResponse<T>` type (see `docs/technical-details.md`, "Paginação
 * server-side"), so a single generic frontend interface covers both.
 */
export interface PagedResponse<T> {
  readonly items: T[];
  readonly pageNumber: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
  readonly hasPreviousPage: boolean;
  readonly hasNextPage: boolean;
}

/** Page sizes offered by the pagination control, in this fixed order. */
export const PAGE_SIZE_OPTIONS = [5, 10, 25, 50] as const;

/** Default page number/size used when none is provided or valid in the route. */
export const DEFAULT_PAGE_NUMBER = 1;
export const DEFAULT_PAGE_SIZE = 5;
