namespace Billing.Api.Features.Invoices;

/// <summary>
/// Thrown when the submitted invoice data violates a domain invariant
/// (no items, non-positive quantity). Maps to HTTP 400.
/// </summary>
public class InvoiceValidationException : Exception
{
    public IReadOnlyCollection<string> Errors { get; }

    public InvoiceValidationException(IEnumerable<string> errors)
        : base("Dados da nota fiscal inválidos.")
    {
        Errors = errors.ToList();
    }
}

/// <summary>
/// Thrown when an invoice cannot be found by its identifier. Maps to HTTP 404.
/// </summary>
public class InvoiceNotFoundException : Exception
{
    public int Id { get; }

    public InvoiceNotFoundException(int id)
        : base("Nota fiscal não encontrada.")
    {
        Id = id;
    }
}

/// <summary>
/// Thrown when a product referenced by an invoice item does not exist in the
/// Inventory service. Maps to HTTP 404.
/// </summary>
public class InvoiceProductNotFoundException : Exception
{
    public int ProductId { get; }

    public InvoiceProductNotFoundException(int productId)
        : base("Produto não encontrado.")
    {
        ProductId = productId;
    }
}

/// <summary>
/// Thrown as a defensive net when the generated invoice number collides with
/// an existing one (protected at the database level by a unique index).
/// Should not happen organically since the number is assigned by a database
/// sequence, but is mapped to HTTP 409 in case it ever does.
/// </summary>
public class DuplicateInvoiceNumberException : Exception
{
    public DuplicateInvoiceNumberException()
        : base("Conflito na numeração da nota fiscal. Tente novamente.")
    {
    }
}

/// <summary>
/// Distinguishes why <see cref="InventoryServiceUnavailableException"/> was
/// raised, so the HTTP layer can report a more specific
/// <c>errorCode</c> (<c>INVENTORY_TIMEOUT</c> vs <c>INVENTORY_UNAVAILABLE</c>)
/// while both still map to the same HTTP 503 status.
/// </summary>
public enum InventoryUnavailableReason
{
    /// <summary>Unreachable, refused the connection, circuit open, or returned an unexpected/invalid response.</summary>
    Unavailable,

    /// <summary>The resilience pipeline's per-attempt or total timeout tripped.</summary>
    Timeout,
}

/// <summary>
/// Thrown when the Inventory service is unreachable, times out, or returns an
/// unexpected error while validating products for a new invoice or debiting
/// stock during printing. Maps to HTTP 503, since the Billing service
/// depends on Inventory being available for both flows.
/// </summary>
public class InventoryServiceUnavailableException : Exception
{
    public InventoryUnavailableReason Reason { get; }

    public InventoryServiceUnavailableException(string message, InventoryUnavailableReason reason = InventoryUnavailableReason.Unavailable)
        : base(message)
    {
        Reason = reason;
    }

    public InventoryServiceUnavailableException(string message, Exception innerException, InventoryUnavailableReason reason = InventoryUnavailableReason.Unavailable)
        : base(message, innerException)
    {
        Reason = reason;
    }
}

/// <summary>
/// Thrown when <c>POST /api/invoices/{id}/print</c> is requested for an
/// invoice that is already <see cref="InvoiceStatus.Closed"/>. Maps to HTTP
/// 409, since printing is only allowed for open invoices.
/// </summary>
public class InvoiceAlreadyClosedException : Exception
{
    public int Id { get; }

    public InvoiceAlreadyClosedException(int id)
        : base("Esta nota fiscal já foi fechada.")
    {
        Id = id;
    }
}

/// <summary>
/// Thrown when the pagination parameters for a paged listing are invalid
/// (page number less than 1, or page size outside the allowed range).
/// Maps to HTTP 400.
/// </summary>
public class InvalidPaginationException : Exception
{
    public IReadOnlyCollection<string> Errors { get; }

    public InvalidPaginationException(IEnumerable<string> errors)
        : base("Parâmetros de paginação inválidos.")
    {
        Errors = errors.ToList();
    }
}

/// <summary>
/// Thrown when the sort parameters for a paged listing are invalid: an
/// unsupported <c>sortBy</c> field, or a <c>sortDirection</c> other than
/// <c>asc</c>/<c>desc</c>. Maps to HTTP 400.
/// </summary>
public class InvalidSortException : Exception
{
    public IReadOnlyCollection<string> Errors { get; }

    public InvalidSortException(IEnumerable<string> errors)
        : base("Parâmetros de ordenação inválidos.")
    {
        Errors = errors.ToList();
    }
}

/// <summary>
/// Thrown when the Inventory service rejects a stock debit requested during
/// printing because the balance of one or more products is insufficient.
/// Maps to HTTP 409; the invoice is left <see cref="InvoiceStatus.Open"/> so
/// the caller can retry after the balance is corrected.
/// </summary>
public class InsufficientStockBalanceException : Exception
{
    public InsufficientStockBalanceException(string message)
        : base(message)
    {
    }
}
