namespace Billing.Api.Features.Invoices;

/// <summary>
/// Thrown when the submitted invoice data violates a domain invariant
/// (no items, non-positive quantity). Maps to HTTP 400.
/// </summary>
public class InvoiceValidationException : Exception
{
    public IReadOnlyCollection<string> Errors { get; }

    public InvoiceValidationException(IEnumerable<string> errors)
        : base("Invalid invoice data.")
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
        : base($"Invoice '{id}' was not found.")
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
        : base($"Product '{productId}' was not found in the Inventory service.")
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
        : base("Invoice number conflict.")
    {
    }
}

/// <summary>
/// Thrown when the Inventory service is unreachable, times out, or returns an
/// unexpected error while validating products for a new invoice. Maps to
/// HTTP 503, since the Billing service depends on Inventory being available
/// to create invoices.
/// </summary>
public class InventoryServiceUnavailableException : Exception
{
    public InventoryServiceUnavailableException(string message)
        : base(message)
    {
    }

    public InventoryServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
