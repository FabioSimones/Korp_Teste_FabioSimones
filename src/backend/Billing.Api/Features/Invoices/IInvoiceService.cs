using Billing.Api.Common;

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

    /// <summary>
    /// Orchestrates printing: validates the invoice is still open, requests
    /// an atomic stock debit from Inventory (reusing the invoice's
    /// <see cref="Invoice.OperationId"/> across retries), and only closes the
    /// invoice once the debit is confirmed. The invoice is left <c>Open</c>
    /// if the debit call fails for any reason.
    /// </summary>
    /// <exception cref="InvoiceNotFoundException">No invoice with the given id.</exception>
    /// <exception cref="InvoiceAlreadyClosedException">The invoice was already printed/closed.</exception>
    /// <exception cref="InvoiceProductNotFoundException">A product referenced by the invoice no longer exists in Inventory.</exception>
    /// <exception cref="InsufficientStockBalanceException">Inventory reported an insufficient balance for one or more items.</exception>
    /// <exception cref="InventoryServiceUnavailableException">Inventory service unreachable or failing.</exception>
    Task<InvoiceResponse> PrintAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists invoices ordered deterministically by <c>Number</c> descending
    /// then <c>Id</c>, returning a single page of summaries. Never loads the
    /// full table into memory, and never calls the Inventory service (it is
    /// a read of Billing's own database only).
    /// </summary>
    /// <exception cref="InvalidPaginationException">Page number less than 1, or page size outside [1, 100].</exception>
    /// <exception cref="InvalidSortException">Unsupported <c>sortBy</c> field or <c>sortDirection</c> other than asc/desc.</exception>
    Task<PagedResponse<InvoiceSummaryResponse>> GetPagedAsync(InvoicesPageQuery query, CancellationToken cancellationToken);
}
