namespace Inventory.Api.Features.Products;

/// <summary>
/// Request payload to register a new product.
/// </summary>
public record CreateProductRequest(string? Code, string? Description, int Balance);

/// <summary>
/// Product data returned by the API. Never exposes the EF Core entity directly.
/// </summary>
public record ProductResponse(int Id, string Code, string Description, int Balance);

/// <summary>
/// Query parameters accepted by the paginated products listing.
/// </summary>
/// <param name="SortBy">
/// One of <c>code</c>, <c>description</c> or <c>balance</c> (case-insensitive).
/// Defaults to <c>code</c> when not provided.
/// </param>
/// <param name="SortDirection">
/// One of <c>asc</c> or <c>desc</c> (case-insensitive). Defaults to <c>asc</c>
/// when not provided.
/// </param>
public record ProductsPageQuery(int PageNumber, int PageSize, string? SortBy = null, string? SortDirection = null);
