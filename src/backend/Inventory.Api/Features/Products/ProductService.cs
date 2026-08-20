using Inventory.Api.Common;
using Inventory.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inventory.Api.Features.Products;

/// <inheritdoc cref="IProductService" />
public class ProductService : IProductService
{
    private const string UniqueViolationSqlState = "23505";
    private const int MinPageSize = 1;
    private const int MaxPageSize = 100;
    private const string DefaultSortBy = "code";
    private const string DefaultSortDirection = "asc";

    private readonly InventoryDbContext _dbContext;

    public ProductService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = Product.Create(request.Code, request.Description, request.Balance);

        var codeAlreadyExists = await _dbContext.Products
            .AsNoTracking()
            .AnyAsync(p => p.Code == product.Code, cancellationToken);

        if (codeAlreadyExists)
        {
            throw new DuplicateProductCodeException(product.Code);
        }

        _dbContext.Products.Add(product);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Database-level invariant, protects against a race between the
            // existence check above and the insert (belt and suspenders).
            throw new DuplicateProductCodeException(product.Code);
        }

        return ToResponse(product);
    }

    public async Task<IReadOnlyList<ProductResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new ProductResponse(p.Id, p.Code, p.Description, p.Balance))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductResponse(p.Id, p.Code, p.Description, p.Balance))
            .FirstOrDefaultAsync(cancellationToken);

        return product ?? throw new ProductNotFoundException(id);
    }

    public async Task<PagedResponse<ProductResponse>> GetPagedAsync(ProductsPageQuery query, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (query.PageNumber < 1)
        {
            errors.Add("O número da página deve ser maior ou igual a 1.");
        }

        if (query.PageSize < MinPageSize || query.PageSize > MaxPageSize)
        {
            errors.Add($"O tamanho da página deve estar entre {MinPageSize} e {MaxPageSize}.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidPaginationException(errors);
        }

        var sortBy = (query.SortBy ?? DefaultSortBy).Trim().ToLowerInvariant();
        var sortDirection = (query.SortDirection ?? DefaultSortDirection).Trim().ToLowerInvariant();

        var sortErrors = new List<string>();

        if (sortBy is not ("code" or "description" or "balance"))
        {
            sortErrors.Add("O campo de ordenação informado não é válido.");
        }

        if (sortDirection is not ("asc" or "desc"))
        {
            sortErrors.Add("A direção de ordenação deve ser 'asc' ou 'desc'.");
        }

        if (sortErrors.Count > 0)
        {
            throw new InvalidSortException(sortErrors);
        }

        var ascending = sortDirection == "asc";

        var totalCount = await _dbContext.Products
            .AsNoTracking()
            .CountAsync(cancellationToken);

        IQueryable<Product> orderedQuery = (sortBy, ascending) switch
        {
            ("code", true) => _dbContext.Products.AsNoTracking().OrderBy(p => p.Code).ThenBy(p => p.Id),
            ("code", false) => _dbContext.Products.AsNoTracking().OrderByDescending(p => p.Code).ThenByDescending(p => p.Id),
            ("description", true) => _dbContext.Products.AsNoTracking().OrderBy(p => p.Description).ThenBy(p => p.Id),
            ("description", false) => _dbContext.Products.AsNoTracking().OrderByDescending(p => p.Description).ThenByDescending(p => p.Id),
            ("balance", true) => _dbContext.Products.AsNoTracking().OrderBy(p => p.Balance).ThenBy(p => p.Code).ThenBy(p => p.Id),
            ("balance", false) => _dbContext.Products.AsNoTracking().OrderByDescending(p => p.Balance).ThenBy(p => p.Code).ThenBy(p => p.Id),
            // Unreachable: sortBy/ascending were already validated above.
            _ => throw new InvalidSortException(["O campo de ordenação informado não é válido."]),
        };

        var items = await orderedQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductResponse(p.Id, p.Code, p.Description, p.Balance))
            .ToListAsync(cancellationToken);

        return PagedResponse<ProductResponse>.Create(items, query.PageNumber, query.PageSize, totalCount);
    }

    private static ProductResponse ToResponse(Product product) =>
        new(product.Id, product.Code, product.Description, product.Balance);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };
}
