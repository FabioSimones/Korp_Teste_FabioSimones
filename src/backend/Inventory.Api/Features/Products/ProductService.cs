using Inventory.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inventory.Api.Features.Products;

/// <inheritdoc cref="IProductService" />
public class ProductService : IProductService
{
    private const string UniqueViolationSqlState = "23505";

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

    private static ProductResponse ToResponse(Product product) =>
        new(product.Id, product.Code, product.Description, product.Balance);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };
}
