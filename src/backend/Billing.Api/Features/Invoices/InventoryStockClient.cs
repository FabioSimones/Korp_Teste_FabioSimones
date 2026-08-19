using System.Net;
using System.Net.Http.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Billing.Api.Features.Invoices;

/// <summary>Single product/quantity pair to debit, mirrors Inventory.Api's stock debit contract.</summary>
public record StockDebitItemRequestDto(int ProductId, int Quantity);

/// <summary>
/// Request payload for <c>POST /api/stock/debits</c> on the Inventory
/// service. Recreated here (rather than shared) because each microservice
/// owns its own contracts; the shape must stay in sync with
/// <c>Inventory.Api.Features.Stock.StockDebitRequest</c>.
/// </summary>
public record StockDebitRequestDto(Guid OperationId, List<StockDebitItemRequestDto> Items);

/// <summary>Result of debiting a single product, as returned by Inventory.Api.</summary>
public record StockDebitItemResultDto(int ProductId, string ProductCode, int QuantityDebited, int BalanceAfter);

/// <summary>Result of a stock debit operation, as returned by Inventory.Api.</summary>
public record StockDebitResultDto(Guid OperationId, IReadOnlyList<StockDebitItemResultDto> Items);

/// <summary>
/// HTTP client to Inventory.Api's atomic, idempotent stock debit endpoint,
/// used by the invoice print/close flow. Kept separate from
/// <see cref="IInventoryProductClient"/> (which only reads product data) so
/// each client has a single, focused responsibility, even though both target
/// the same Inventory.Api base address.
/// </summary>
public interface IInventoryStockClient
{
    /// <returns>The debit result, either freshly applied or replayed for a repeated <c>OperationId</c>.</returns>
    /// <exception cref="InvoiceProductNotFoundException">A product referenced by the request does not exist in Inventory.</exception>
    /// <exception cref="InsufficientStockBalanceException">One or more products do not have enough balance.</exception>
    /// <exception cref="InventoryServiceUnavailableException">Inventory service unreachable, timed out, or returned an unexpected/invalid response.</exception>
    Task<StockDebitResultDto> DebitAsync(StockDebitRequestDto request, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IInventoryStockClient" />
public class InventoryStockClient : IInventoryStockClient
{
    private readonly HttpClient _httpClient;

    public InventoryStockClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<StockDebitResultDto> DebitAsync(StockDebitRequestDto request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("/api/stock/debits", request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InventoryServiceUnavailableException("The Inventory service is unavailable.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            // The resilience pipeline's total or per-attempt timeout tripped
            // (Task 11), distinct from the caller cancelling the request.
            // The invoice stays Open: InvoiceService only closes it after
            // DebitAsync returns successfully.
            throw new InventoryServiceUnavailableException("The Inventory service request timed out.", ex);
        }
        catch (BrokenCircuitException ex)
        {
            // The circuit breaker is open and short-circuited the call
            // before reaching Inventory.Api (Task 11).
            throw new InventoryServiceUnavailableException("The Inventory service circuit breaker is open.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout safety-net elapsed; should not normally
            // trigger since it sits well above the pipeline's own total
            // timeout (see Program.cs).
            throw new InventoryServiceUnavailableException("The Inventory service request timed out.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvoiceProductNotFoundException(await ExtractProductIdOrDefaultAsync(request, cancellationToken));
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var detail = await ExtractProblemDetailAsync(response, cancellationToken)
                    ?? "The Inventory service reported an insufficient stock balance.";
                throw new InsufficientStockBalanceException(detail);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InventoryServiceUnavailableException(
                    $"The Inventory service responded with unexpected status {(int)response.StatusCode}.");
            }

            StockDebitResultDto? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<StockDebitResultDto>(cancellationToken);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
            {
                throw new InventoryServiceUnavailableException("The Inventory service returned an invalid response.", ex);
            }

            if (result is null)
            {
                throw new InventoryServiceUnavailableException("The Inventory service returned an empty response.");
            }

            return result;
        }
    }

    /// <summary>
    /// Inventory.Api's 404 only reports the offending product's id inside its
    /// ProblemDetails message, not as a structured field; since a product not
    /// existing at print time should not happen organically (Billing already
    /// validated it at invoice creation and Inventory has no delete
    /// endpoint), the id is best-effort and falls back to the first item
    /// requested instead of failing the error path itself.
    /// </summary>
    private static Task<int> ExtractProductIdOrDefaultAsync(StockDebitRequestDto request, CancellationToken _) =>
        Task.FromResult(request.Items.Count > 0 ? request.Items[0].ProductId : 0);

    private static async Task<string?> ExtractProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(cancellationToken);
            return problem?.Detail;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Minimal shape used to read the "detail" field out of a ProblemDetails error response.</summary>
    private record ProblemDetailsPayload(string? Detail);
}
