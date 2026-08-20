/**
 * Sort direction accepted by the server-side paginated listing endpoints
 * (`GET /api/products/paged`, `GET /api/invoices/paged`). Generic — shared by
 * every sortable listing, unlike the sortable *fields*, which are specific to
 * each resource (see `ProductSortField` in `features/products/models/product.ts`
 * and `InvoiceSortField` in `features/invoices/models/invoice.ts`).
 */
export type SortDirection = 'asc' | 'desc';

/** Flips `asc` <-> `desc`, used when the user clicks the already-active column. */
export function toggleSortDirection(direction: SortDirection): SortDirection {
  return direction === 'asc' ? 'desc' : 'asc';
}

/**
 * Validates a `sortBy`/`sortDirection` pair read from the route's query
 * params against the resource's own list of sortable fields. Returns `null`
 * when either value is missing or not recognized, so the caller can fall
 * back to its defaults and normalize the URL (`replaceUrl: true`), the same
 * pattern already used for `page`/`pageSize`.
 */
export function resolveSort<TField extends string>(
  rawSortBy: string | null,
  rawSortDirection: string | null,
  validFields: readonly TField[],
): { sortBy: TField; sortDirection: SortDirection } | null {
  const isValidField = rawSortBy !== null && (validFields as readonly string[]).includes(rawSortBy);
  const isValidDirection = rawSortDirection === 'asc' || rawSortDirection === 'desc';

  if (!isValidField || !isValidDirection) {
    return null;
  }

  return { sortBy: rawSortBy as TField, sortDirection: rawSortDirection };
}
