using Inventory.Api.Data;
using Inventory.Api.Features.Products;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inventory.Api.Features.Stock;

/// <inheritdoc cref="IStockDebitService" />
/// <remarks>
/// Idempotency design: every processed request is recorded as a
/// <see cref="StockDebitOperation"/> keyed by the caller-supplied
/// <c>OperationId</c>, unique at the database level. Before debiting
/// anything, the service checks whether an operation with that id already
/// exists and, if so, replays the stored result instead of touching product
/// balances again. Atomicity is guaranteed because all product balance
/// updates and the operation record are validated in memory first (missing
/// products and insufficient balances are detected before any entity is
/// mutated) and then persisted together in a single
/// <see cref="DbContext.SaveChangesAsync"/> call wrapped in an explicit
/// database transaction: either every change is committed or none is.
/// </remarks>
public class StockDebitService : IStockDebitService
{
    private const string UniqueViolationSqlState = "23505";

    private readonly InventoryDbContext _dbContext;

    public StockDebitService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StockDebitResponse> DebitAsync(StockDebitRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var existing = await FindExistingOperationAsync(request.OperationId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var productIds = request.Items!.Select(i => i.ProductId).Distinct().ToList();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Tracked (not AsNoTracking) because balances are mutated below.
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var missingId = productIds.FirstOrDefault(id => !products.ContainsKey(id));
        if (missingId != 0)
        {
            // Nothing has been mutated yet, so no explicit rollback is
            // needed: disposing the transaction below without committing
            // rolls it back.
            throw new ProductNotFoundException(missingId);
        }

        var operation = StockDebitOperation.Create(request.OperationId);
        var itemResponses = new List<StockDebitItemResponse>(request.Items!.Count);

        foreach (var item in request.Items!)
        {
            var product = products[item.ProductId];

            // Throws InsufficientProductBalanceException without mutating
            // the balance when there isn't enough stock. Because this loop
            // runs entirely in memory before SaveChangesAsync is called, a
            // failure here leaves every product (including ones already
            // processed earlier in the loop) untouched in the database.
            product.Debit(item.Quantity);

            operation.AddItem(product.Id, product.Code, item.Quantity, product.Balance);
            itemResponses.Add(new StockDebitItemResponse(product.Id, product.Code, item.Quantity, product.Balance));
        }

        _dbContext.StockDebitOperations.Add(operation);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent request for the same OperationId committed first.
            // Roll back this attempt (no balances are actually persisted)
            // and return the result of the operation that won the race.
            await transaction.RollbackAsync(cancellationToken);

            var winner = await FindExistingOperationAsync(request.OperationId, cancellationToken);
            if (winner is not null)
            {
                return winner;
            }

            throw;
        }

        await transaction.CommitAsync(cancellationToken);

        return new StockDebitResponse(request.OperationId, itemResponses);
    }

    private async Task<StockDebitResponse?> FindExistingOperationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.StockDebitOperations
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OperationId == operationId, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        var items = existing.Items
            .Select(i => new StockDebitItemResponse(i.ProductId, i.ProductCode, i.Quantity, i.BalanceAfter))
            .ToList();

        return new StockDebitResponse(existing.OperationId, items);
    }

    private static void ValidateRequest(StockDebitRequest request)
    {
        var errors = new List<string>();

        if (request.OperationId == Guid.Empty)
        {
            errors.Add("OperationId is required.");
        }

        if (request.Items is not { Count: > 0 })
        {
            errors.Add("At least one item is required.");
        }
        else
        {
            var seenProductIds = new HashSet<int>();

            foreach (var item in request.Items)
            {
                if (item.ProductId <= 0)
                {
                    errors.Add("ProductId must be greater than zero.");
                }
                else if (!seenProductIds.Add(item.ProductId))
                {
                    errors.Add($"Duplicate product '{item.ProductId}' in the same debit request.");
                }

                if (item.Quantity <= 0)
                {
                    errors.Add($"Quantity for product '{item.ProductId}' must be greater than zero.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new StockDebitValidationException(errors);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };
}
