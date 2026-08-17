namespace Inventory.Api.Features.Products;

/// <summary>
/// Request payload to register a new product.
/// </summary>
public record CreateProductRequest(string? Code, string? Description, int Balance);

/// <summary>
/// Product data returned by the API. Never exposes the EF Core entity directly.
/// </summary>
public record ProductResponse(int Id, string Code, string Description, int Balance);
