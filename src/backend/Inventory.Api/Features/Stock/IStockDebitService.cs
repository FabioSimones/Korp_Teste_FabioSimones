using Inventory.Api.Features.Products;

namespace Inventory.Api.Features.Stock;

/// <summary>
/// Business logic for atomic, idempotent stock debits. Kept out of the HTTP
/// layer so the controller stays thin.
/// </summary>
public interface IStockDebitService
{
    /// <summary>
    /// Debits the balance of every product in <paramref name="request"/>
    /// within a single database transaction: either every item is debited or
    /// none is. Repeating a request with an already-processed
    /// <see cref="StockDebitRequest.OperationId"/> returns the original
    /// result without debiting the balances again.
    /// </summary>
    /// <exception cref="StockDebitValidationException">Invalid payload.</exception>
    /// <exception cref="ProductNotFoundException">One of the referenced products does not exist.</exception>
    /// <exception cref="InsufficientProductBalanceException">One of the items exceeds the available balance.</exception>
    Task<StockDebitResponse> DebitAsync(StockDebitRequest request, CancellationToken cancellationToken);
}
