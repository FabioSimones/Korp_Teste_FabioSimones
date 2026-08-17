using Billing.Api.Features.Invoices;

namespace Billing.Tests;

/// <summary>
/// Test double for <see cref="IInventoryProductClient"/>, used to isolate the
/// Billing API tests from the Inventory service's real HTTP endpoint while
/// still exercising the full Billing.Api pipeline (controller, service,
/// domain, real PostgreSQL database).
/// </summary>
internal class FakeInventoryProductClient : IInventoryProductClient
{
    private readonly IReadOnlyDictionary<int, InventoryProductLookupResult> _products;
    private readonly bool _throwUnavailable;

    public FakeInventoryProductClient(
        IReadOnlyDictionary<int, InventoryProductLookupResult> products,
        bool throwUnavailable = false)
    {
        _products = products;
        _throwUnavailable = throwUnavailable;
    }

    public Task<InventoryProductLookupResult?> GetProductAsync(int productId, CancellationToken cancellationToken)
    {
        if (_throwUnavailable)
        {
            throw new InventoryServiceUnavailableException("Simulated Inventory outage.");
        }

        return Task.FromResult(_products.TryGetValue(productId, out var product) ? product : null);
    }
}
