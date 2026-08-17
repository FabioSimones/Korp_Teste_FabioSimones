namespace Billing.Api.Features.Invoices;

/// <summary>Single line requested when creating an invoice.</summary>
public record CreateInvoiceItemRequest(int ProductId, int Quantity);

/// <summary>Request payload to register a new invoice.</summary>
public record CreateInvoiceRequest(List<CreateInvoiceItemRequest>? Items);

/// <summary>
/// Invoice item data returned by the API, including the product snapshot
/// (code/description) captured at creation time.
/// </summary>
public record InvoiceItemResponse(int Id, int ProductId, string ProductCode, string ProductDescription, int Quantity);

/// <summary>
/// Invoice data returned by the API. Never exposes the EF Core entity
/// directly.
/// </summary>
public record InvoiceResponse(
    int Id,
    int Number,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<InvoiceItemResponse> Items);
