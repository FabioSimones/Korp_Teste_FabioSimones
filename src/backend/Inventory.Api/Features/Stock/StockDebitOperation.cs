namespace Inventory.Api.Features.Stock;

/// <summary>
/// Records a stock debit that was processed for a given <see cref="OperationId"/>.
/// Persisted so that a repeated request with the same <see cref="OperationId"/>
/// can be recognized and answered with the original result instead of being
/// applied a second time (idempotency).
/// </summary>
public class StockDebitOperation
{
    public int Id { get; private set; }

    public Guid OperationId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private readonly List<StockDebitOperationItem> _items = new();

    public IReadOnlyCollection<StockDebitOperationItem> Items => _items;

    // Required by EF Core.
    private StockDebitOperation()
    {
    }

    private StockDebitOperation(Guid operationId, DateTime createdAtUtc)
    {
        OperationId = operationId;
        CreatedAtUtc = createdAtUtc;
    }

    public static StockDebitOperation Create(Guid operationId) => new(operationId, DateTime.UtcNow);

    /// <summary>
    /// Records a debited item as a snapshot (product code and resulting
    /// balance at the time of the operation), so the idempotent replay does
    /// not depend on the current state of the product.
    /// </summary>
    public void AddItem(int productId, string productCode, int quantity, int balanceAfter)
    {
        _items.Add(StockDebitOperationItem.Create(productId, productCode, quantity, balanceAfter));
    }
}
