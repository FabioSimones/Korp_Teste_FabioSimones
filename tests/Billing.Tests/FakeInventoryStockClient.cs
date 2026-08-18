using Billing.Api.Features.Invoices;

namespace Billing.Tests;

/// <summary>
/// Test double for <see cref="IInventoryStockClient"/>, used to isolate
/// print-flow tests that do not need a real Inventory.Api (e.g. "invoice not
/// found"/"already closed" guards, which never reach the stock client) from
/// the real, in-process integration exercised by
/// <see cref="InvoicesPrintRealInventoryIntegrationTests"/>. Records every
/// call so tests can assert the same <c>OperationId</c> is reused across
/// print attempts for the same invoice.
/// </summary>
internal class FakeInventoryStockClient : IInventoryStockClient
{
    private readonly Exception? _throwOnDebit;

    public List<StockDebitRequestDto> Calls { get; } = new();

    public FakeInventoryStockClient(Exception? throwOnDebit = null)
    {
        _throwOnDebit = throwOnDebit;
    }

    public Task<StockDebitResultDto> DebitAsync(StockDebitRequestDto request, CancellationToken cancellationToken)
    {
        Calls.Add(request);

        if (_throwOnDebit is not null)
        {
            throw _throwOnDebit;
        }

        var items = request.Items
            .Select(i => new StockDebitItemResultDto(i.ProductId, $"SKU-{i.ProductId}", i.Quantity, 0))
            .ToList();

        return Task.FromResult(new StockDebitResultDto(request.OperationId, items));
    }
}
