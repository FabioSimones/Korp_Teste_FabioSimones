using System.Net;
using System.Net.Http.Json;

namespace Billing.Api.Features.Invoices;

/// <summary>
/// Product snapshot returned by the Inventory service, used to build the
/// invoice item snapshot at creation time.
/// </summary>
public record InventoryProductLookupResult(int ProductId, string Code, string Description);

/// <summary>
/// Resilient HTTP client to the Inventory service, used to confirm that a
/// product exists and to capture the code/description snapshot when creating
/// an invoice. Does not validate or reserve balance (that is the stock
/// write-off flow, out of scope for this feature).
/// </summary>
public interface IInventoryProductClient
{
    /// <returns>
    /// The product snapshot, or <c>null</c> when the Inventory service
    /// confirms the product does not exist (HTTP 404).
    /// </returns>
    /// <exception cref="InventoryServiceUnavailableException">
    /// The Inventory service is unreachable, timed out, or returned an
    /// unexpected error.
    /// </exception>
    Task<InventoryProductLookupResult?> GetProductAsync(int productId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IInventoryProductClient" />
public class InventoryProductClient : IInventoryProductClient
{
    private readonly HttpClient _httpClient;

    public InventoryProductClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<InventoryProductLookupResult?> GetProductAsync(int productId, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync($"/api/products/{productId}", cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InventoryServiceUnavailableException("The Inventory service is unavailable.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed (distinct from the caller cancelling the request).
            throw new InventoryServiceUnavailableException("The Inventory service request timed out.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InventoryServiceUnavailableException(
                    $"The Inventory service responded with unexpected status {(int)response.StatusCode}.");
            }

            InventoryProductPayload? product;
            try
            {
                product = await response.Content.ReadFromJsonAsync<InventoryProductPayload>(cancellationToken);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
            {
                throw new InventoryServiceUnavailableException("The Inventory service returned an invalid response.", ex);
            }

            if (product is null)
            {
                throw new InventoryServiceUnavailableException("The Inventory service returned an empty response.");
            }

            return new InventoryProductLookupResult(product.Id, product.Code, product.Description);
        }
    }

    /// <summary>Mirrors Inventory.Api's ProductResponse shape without depending on that assembly.</summary>
    private record InventoryProductPayload(int Id, string Code, string Description, int Balance);
}
