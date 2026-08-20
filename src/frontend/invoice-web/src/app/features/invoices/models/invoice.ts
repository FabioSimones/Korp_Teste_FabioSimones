/**
 * Lifecycle status of an invoice, as returned by Billing.Api. An invoice
 * transitions `Open` -> `Closed` exactly once, via the print/close flow
 * (`POST /api/invoices/{id}/print`); `Closed` invoices are read-only here.
 */
export type InvoiceStatus = 'Open' | 'Closed';

/**
 * Invoice item data as returned by Billing.Api, including the product
 * snapshot (code/description) captured at creation time.
 */
export interface InvoiceItem {
  readonly id: number;
  readonly productId: number;
  readonly productCode: string;
  readonly productDescription: string;
  readonly quantity: number;
}

/**
 * Invoice data as returned by Billing.Api.
 */
export interface Invoice {
  readonly id: number;
  readonly number: number;
  readonly status: InvoiceStatus;
  readonly createdAtUtc: string;
  /** Set once printing confirms the stock write-off; `null` while `Open`. */
  readonly closedAtUtc: string | null;
  readonly items: readonly InvoiceItem[];
}

/**
 * Single line accepted by Billing.Api when creating an invoice.
 */
export interface CreateInvoiceItemRequest {
  readonly productId: number;
  readonly quantity: number;
}

/**
 * Payload accepted by Billing.Api to register a new invoice.
 */
export interface CreateInvoiceRequest {
  readonly items: CreateInvoiceItemRequest[];
}

/**
 * Summarized invoice data returned by the server-side paginated listing
 * (`GET /api/invoices/paged`). Carries the same header fields as `Invoice`
 * but exposes `itemsCount` instead of the full item list, matching
 * `Billing.Api`'s `InvoiceSummaryResponse`.
 */
export interface InvoiceSummary {
  readonly id: number;
  readonly number: number;
  readonly status: InvoiceStatus;
  readonly createdAtUtc: string;
  readonly closedAtUtc: string | null;
  readonly itemsCount: number;
}

/**
 * Fields Billing.Api accepts as `sortBy` on `GET /api/invoices/paged`. Kept
 * in sync with `InvoiceService.GetPagedAsync` on the backend (case-sensitive
 * as sent; the backend itself lower-cases before comparing).
 */
export type InvoiceSortField = 'number' | 'createdAtUtc' | 'itemsCount' | 'status';

export const INVOICE_SORT_FIELDS: readonly InvoiceSortField[] = [
  'number',
  'createdAtUtc',
  'itemsCount',
  'status',
];

/** Default sort applied by Billing.Api when none is specified: most recent invoices first. */
export const DEFAULT_INVOICE_SORT_FIELD: InvoiceSortField = 'number';
