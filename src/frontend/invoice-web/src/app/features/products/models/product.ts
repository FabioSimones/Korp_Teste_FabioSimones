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
