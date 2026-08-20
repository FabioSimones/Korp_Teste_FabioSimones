namespace Inventory.Api.Features.Stock;

/// <summary>
/// Thrown when the submitted stock debit request is malformed (missing
/// OperationId, no items, non-positive quantities or duplicate products).
/// Maps to HTTP 400.
/// </summary>
public class StockDebitValidationException : Exception
{
    public IReadOnlyCollection<string> Errors { get; }

    public StockDebitValidationException(IEnumerable<string> errors)
        : base("Requisição de baixa de estoque inválida.")
    {
        Errors = errors.ToList();
    }
}
