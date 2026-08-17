namespace Inventory.Api.Features.Products;

/// <summary>
/// Business logic for product registration and queries. Kept out of the
/// HTTP layer so controllers stay thin.
/// </summary>
public interface IProductService
{
    /// <exception cref="ProductValidationException">Invalid code, description or balance.</exception>
    /// <exception cref="DuplicateProductCodeException">Code already registered.</exception>
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductResponse>> GetAllAsync(CancellationToken cancellationToken);

    /// <exception cref="ProductNotFoundException">No product with the given id.</exception>
    Task<ProductResponse> GetByIdAsync(int id, CancellationToken cancellationToken);
}
