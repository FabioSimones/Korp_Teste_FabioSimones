namespace Inventory.Api.Features.Stock;

/// <summary>
/// A single product/quantity pair to debit as part of a stock debit request.
/// </summary>
public record StockDebitItemRequest(int ProductId, int Quantity);

/// <summary>
/// Request payload for an atomic, idempotent stock debit. The
/// <paramref name="OperationId"/> is supplied by the caller (typically the
/// Billing service) so the debit can be safely retried.
/// </summary>
/// <param name="OperationId">Unique identifier of the debit operation, used for idempotency.</param>
/// <param name="Items">Products and quantities to debit. Must not be empty.</param>
public record StockDebitRequest(Guid OperationId, List<StockDebitItemRequest>? Items);

/// <summary>
/// Result of debiting a single product.
/// </summary>
public record StockDebitItemResponse(int ProductId, string ProductCode, int QuantityDebited, int BalanceAfter);

/// <summary>
/// Result of a stock debit operation. Never exposes the EF Core entities directly.
/// </summary>
public record StockDebitResponse(Guid OperationId, IReadOnlyList<StockDebitItemResponse> Items);
