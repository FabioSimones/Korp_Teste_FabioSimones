namespace Billing.Api.Features.Invoices;

/// <summary>
/// Business logic for invoice registration and queries. Kept out of the HTTP
/// layer so controllers stay thin.
/// </summary>
public interface IInvoiceService
{
    /// <exception cref="InvoiceValidationException">No items, or a non-positive quantity.</exception>
    /// <exception cref="InvoiceProductNotFoundException">A referenced product does not exist in Inventory.</exception>
    /// <exception cref="InventoryServiceUnavailableException">Inventory service unreachable or failing.</exception>
    /// <exception cref="DuplicateInvoiceNumberException">Number collision (defensive, should not occur organically).</exception>
    Task<InvoiceResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<InvoiceResponse>> GetAllAsync(CancellationToken cancellationToken);

    /// <exception cref="InvoiceNotFoundException">No invoice with the given id.</exception>
    Task<InvoiceResponse> GetByIdAsync(int id, CancellationToken cancellationToken);
}
