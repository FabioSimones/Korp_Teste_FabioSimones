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
/// directly. <see cref="ClosedAtUtc"/> is <c>null</c> while the invoice is
/// <c>Open</c> and set once printing confirms the stock write-off.
/// </summary>
public record InvoiceResponse(
    int Id,
    int Number,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    IReadOnlyList<InvoiceItemResponse> Items);

/// <summary>
/// Query parameters accepted by the paginated invoices listing.
/// </summary>
/// <param name="SortBy">
/// One of <c>number</c>, <c>createdAtUtc</c>, <c>itemsCount</c> or <c>status</c>
/// (case-insensitive). Defaults to <c>number</c> when not provided.
/// </param>
/// <param name="SortDirection">
/// One of <c>asc</c> or <c>desc</c> (case-insensitive). Defaults to <c>desc</c>
/// when not provided.
/// </param>
public record InvoicesPageQuery(int PageNumber, int PageSize, string? SortBy = null, string? SortDirection = null);

/// <summary>
/// Summarized invoice data returned by the paginated listing. Carries the
/// same header fields as <see cref="InvoiceResponse"/> but exposes the item
/// count instead of the full item snapshot, so the query can be answered
/// with a single projection (translated to a SQL <c>COUNT</c> subquery)
/// without loading every item row for every invoice on the page.
/// </summary>
public record InvoiceSummaryResponse(
    int Id,
    int Number,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    int ItemsCount);
