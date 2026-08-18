using Billing.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Billing.Api.Features.Invoices;

/// <inheritdoc cref="IInvoiceService" />
public class InvoiceService : IInvoiceService
{
    private const string UniqueViolationSqlState = "23505";

    private readonly BillingDbContext _dbContext;
    private readonly IInventoryProductClient _inventoryClient;
    private readonly IInventoryStockClient _stockClient;

    public InvoiceService(BillingDbContext dbContext, IInventoryProductClient inventoryClient, IInventoryStockClient stockClient)
    {
        _dbContext = dbContext;
        _inventoryClient = inventoryClient;
        _stockClient = stockClient;
    }

    public async Task<InvoiceResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var itemRequests = request.Items ?? [];

        if (itemRequests.Count == 0)
        {
            throw new InvoiceValidationException(["At least one item is required."]);
        }

        var quantityErrors = itemRequests
            .Where(i => i.Quantity <= 0)
            .Select(i => $"Quantity for product '{i.ProductId}' must be a positive integer.")
            .ToList();

        if (quantityErrors.Count > 0)
        {
            throw new InvoiceValidationException(quantityErrors);
        }

        // Validate products against Inventory and capture the snapshot. Products
        // repeated across multiple item lines (allowed by design, see
        // docs/technical-details.md) are only looked up once each.
        var distinctProductIds = itemRequests.Select(i => i.ProductId).Distinct();
        var products = new Dictionary<int, InventoryProductLookupResult>();

        foreach (var productId in distinctProductIds)
        {
            var product = await _inventoryClient.GetProductAsync(productId, cancellationToken);
            if (product is null)
            {
                throw new InvoiceProductNotFoundException(productId);
            }

            products[productId] = product;
        }

        var items = itemRequests
            .Select(i =>
            {
                var product = products[i.ProductId];
                return InvoiceItem.Create(product.ProductId, product.Code, product.Description, i.Quantity);
            })
            .ToList();

        var invoice = Invoice.Create(items);

        _dbContext.Invoices.Add(invoice);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Database-level invariant, protects against a race on the
            // generated invoice number (belt and suspenders).
            throw new DuplicateInvoiceNumberException();
        }

        return ToResponse(invoice);
    }

    public async Task<IReadOnlyList<InvoiceResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Invoices
            .AsNoTracking()
            .OrderBy(i => i.Number)
            .Select(i => new InvoiceResponse(
                i.Id,
                i.Number,
                i.Status.ToString(),
                i.CreatedAtUtc,
                i.ClosedAtUtc,
                i.Items
                    .Select(it => new InvoiceItemResponse(it.Id, it.ProductId, it.ProductCode, it.ProductDescription, it.Quantity))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<InvoiceResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new InvoiceResponse(
                i.Id,
                i.Number,
                i.Status.ToString(),
                i.CreatedAtUtc,
                i.ClosedAtUtc,
                i.Items
                    .Select(it => new InvoiceItemResponse(it.Id, it.ProductId, it.ProductCode, it.ProductDescription, it.Quantity))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return invoice ?? throw new InvoiceNotFoundException(id);
    }

    public async Task<InvoiceResponse> PrintAsync(int id, CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice is null)
        {
            throw new InvoiceNotFoundException(id);
        }

        // Validates the invoice is still Open and reserves/reuses the
        // OperationId used for the stock debit call.
        var operationId = invoice.PrepareForPrint();

        // Persisted before calling Inventory so a crash or failure between a
        // successful debit and closing the invoice does not lose the
        // reserved OperationId: a retry reuses it, and Inventory's own
        // idempotency (keyed by OperationId) guarantees the balances are not
        // debited a second time.
        await _dbContext.SaveChangesAsync(cancellationToken);

        var debitRequest = new StockDebitRequestDto(
            operationId,
            invoice.Items.Select(it => new StockDebitItemRequestDto(it.ProductId, it.Quantity)).ToList());

        // Any failure here (validation, missing product, insufficient
        // balance, or Inventory being unavailable) propagates to the caller
        // without closing the invoice, which stays Open for a retry.
        await _stockClient.DebitAsync(debitRequest, cancellationToken);

        invoice.Close(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(invoice);
    }

    private static InvoiceResponse ToResponse(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.Number,
            invoice.Status.ToString(),
            invoice.CreatedAtUtc,
            invoice.ClosedAtUtc,
            invoice.Items
                .Select(it => new InvoiceItemResponse(it.Id, it.ProductId, it.ProductCode, it.ProductDescription, it.Quantity))
                .ToList());

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };
}
