/**
 * Lifecycle status of an invoice, as returned by Billing.Api. `Closed`
 * invoices are read-only here; the print/close flow is out of scope for
 * this feature.
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
