using Billing.Api.Features.Invoices;

namespace Billing.Tests;

/// <summary>
/// Test double for <see cref="IInventoryProductClient"/> that behaves like
/// <see cref="FakeInventoryProductClient"/> but also counts every call it
/// receives, so tests can assert that a given flow (e.g. the paginated
/// invoices listing) never calls out to the Inventory service.
/// </summary>
internal class CountingInventoryProductClient : IInventoryProductClient
{
    private readonly IReadOnlyDictionary<int, InventoryProductLookupResult> _products;

    public int CallCount { get; set; }

    public CountingInventoryProductClient(IReadOnlyDictionary<int, InventoryProductLookupResult> products)
    {
        _products = products;
    }

    public Task<InventoryProductLookupResult?> GetProductAsync(int productId, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(_products.TryGetValue(productId, out var product) ? product : null);
    }
}
