using Inventory.Api.Common;

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

    /// <summary>
    /// Lists products ordered deterministically by <c>Code</c> then <c>Id</c>,
    /// returning a single page. Never loads the full table into memory: the
    /// total count and the page are both computed in the database.
    /// </summary>
    /// <exception cref="InvalidPaginationException">Page number less than 1, or page size outside [1, 100].</exception>
    /// <exception cref="InvalidSortException">Unsupported <c>sortBy</c> field or <c>sortDirection</c> other than asc/desc.</exception>
    Task<PagedResponse<ProductResponse>> GetPagedAsync(ProductsPageQuery query, CancellationToken cancellationToken);
}
