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
/// balances again. A second, identical check is repeated immediately after
/// the <c>SELECT ... FOR UPDATE</c> row locks are acquired (see
/// <see cref="DebitAsync"/>): a request blocked on that lock while a
/// concurrent request with the *same* <c>OperationId</c> commits would
/// otherwise wake up, observe the already-updated balance, and could fail
/// the normal insufficient-balance validation instead of replaying the
/// winner's result - the first check alone does not cover that window.
/// Atomicity is guaranteed because all product balance
/// updates and the operation record are validated in memory first (missing
/// products and insufficient balances are detected before any entity is
/// mutated) and then persisted together in a single
/// <see cref="DbContext.SaveChangesAsync"/> call wrapped in an explicit
/// database transaction: either every change is committed or none is.
/// Concurrency design: idempotency (above) only covers the *same*
/// <c>OperationId</c> being repeated. Two requests with *different*
/// <c>OperationId</c>s racing to debit the same product's balance are
/// protected by locking the referenced product rows with
/// <c>SELECT ... FOR UPDATE</c> (see <see cref="DebitAsync"/>), in
/// deterministic <c>ProductId</c> order, before any balance is validated or
/// mutated - this is a database-level lock, so it holds even across
/// multiple Inventory.Api instances sharing the same PostgreSQL database.
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

        var productIds = request.Items!.Select(i => i.ProductId).Distinct().OrderBy(id => id).ToArray();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Tracked (not AsNoTracking) because balances are mutated below.
        //
        // SELECT ... FOR UPDATE locks the selected product rows at the
        // PostgreSQL level (not in memory), for the remainder of this
        // transaction. This is what protects against two concurrent
        // requests with *different* OperationIds racing to debit the same
        // product's balance: whichever transaction acquires the row lock
        // first proceeds; the other blocks on the SELECT itself until the
        // first transaction commits or rolls back, then observes the
        // already-updated balance and is validated against it (see
        // Product.Debit below). The lock lives entirely in the database, so
        // it works correctly across multiple Inventory.Api instances/
        // processes sharing the same PostgreSQL database - there is no
        // in-memory/application-level lock involved.
        //
        // Rows are locked in a deterministic order (ORDER BY "Id") to avoid
        // deadlocks: two concurrent debits referencing the same set of
        // products in different list orders would otherwise be able to
        // each hold one row's lock while waiting for the other's, causing
        // a deadlock. Sorting productIds before the query guarantees every
        // transaction acquires locks in the same ascending order.
        //
        // Ids are passed as a parameterized array (FromSqlInterpolated),
        // never concatenated into the SQL string.
        var products = await _dbContext.Products
            .FromSqlInterpolated(
                $"SELECT * FROM products WHERE \"Id\" = ANY({productIds}) ORDER BY \"Id\" FOR UPDATE")
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        // Second idempotency check, right after acquiring the row locks and
        // before any validation/mutation. The first check (above, before
        // the transaction) does not cover the window where this request was
        // blocked *inside* the SELECT ... FOR UPDATE waiting for a
        // concurrent request with the same OperationId to commit: once
        // unblocked, this transaction would otherwise re-read the
        // already-updated balance and run the normal validation against it
        // (Product.Debit), which can throw InsufficientProductBalanceException
        // for what is, in fact, the very same logical request - a false
        // 409 for what should be an idempotent 200 replay of the winner's
        // result. Detecting the now-committed operation here, before any
        // validation, avoids that entirely.
        var winnerAfterLock = await FindExistingOperationAsync(request.OperationId, cancellationToken);
        if (winnerAfterLock is not null)
        {
            // Nothing has been mutated yet in this transaction, so no
            // explicit rollback is strictly required, but it is done here
            // for clarity: this attempt must not persist or mutate anything.
            await transaction.RollbackAsync(cancellationToken);
            return winnerAfterLock;
        }

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
            errors.Add("O OperationId é obrigatório.");
        }

        if (request.Items is not { Count: > 0 })
        {
            errors.Add("É necessário informar ao menos um item.");
        }
        else
        {
            var seenProductIds = new HashSet<int>();

            foreach (var item in request.Items)
            {
                if (item.ProductId <= 0)
                {
                    errors.Add("O ProductId deve ser maior que zero.");
                }
                else if (!seenProductIds.Add(item.ProductId))
                {
                    errors.Add($"Produto '{item.ProductId}' duplicado na mesma requisição de baixa.");
                }

                if (item.Quantity <= 0)
                {
                    errors.Add($"A quantidade do produto '{item.ProductId}' deve ser um número inteiro maior que zero.");
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
