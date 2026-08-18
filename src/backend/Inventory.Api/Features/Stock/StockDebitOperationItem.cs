namespace Inventory.Api.Features.Stock;

/// <summary>
/// A single product debited by a <see cref="StockDebitOperation"/>. Stores a
/// snapshot of the product code and the resulting balance so the operation
/// result can be replayed exactly on a repeated request.
/// </summary>
public class StockDebitOperationItem
{
    public int Id { get; private set; }

    public int StockDebitOperationId { get; private set; }

    public int ProductId { get; private set; }

    public string ProductCode { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public int BalanceAfter { get; private set; }

    // Required by EF Core.
    private StockDebitOperationItem()
    {
    }

    private StockDebitOperationItem(int productId, string productCode, int quantity, int balanceAfter)
    {
        ProductId = productId;
        ProductCode = productCode;
        Quantity = quantity;
        BalanceAfter = balanceAfter;
    }

    internal static StockDebitOperationItem Create(int productId, string productCode, int quantity, int balanceAfter) =>
        new(productId, productCode, quantity, balanceAfter);
}
