extern alias InventoryApiAssembly;

using System.Net;
using System.Net.Http.Json;
using Billing.Api.Data;
using Billing.Api.Features.Invoices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using InventoryCreateProductRequest = InventoryApiAssembly::Inventory.Api.Features.Products.CreateProductRequest;
using InventoryDbContext = InventoryApiAssembly::Inventory.Api.Data.InventoryDbContext;
using InventoryProductResponse = InventoryApiAssembly::Inventory.Api.Features.Products.ProductResponse;
using InventoryProductsController = InventoryApiAssembly::Inventory.Api.Features.Products.ProductsController;

namespace Billing.Tests;

/// <summary>
/// Genuine end-to-end tests for the print/close flow, hosting a real,
/// in-process Inventory.Api next to a real, in-process Billing.Api, each
/// backed by its own ephemeral PostgreSQL container, mirroring
/// <see cref="InvoicesRealInventoryIntegrationTests"/>. These are the tests
/// that actually exercise Inventory's atomic stock debit and its balance
/// math, which a fake stock client cannot meaningfully verify.
/// </summary>
public class InvoicesPrintRealInventoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _inventoryDbContainer = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("inventory_db")
        .WithUsername("inventory_user")
        .WithPassword("inventory_test_pwd")
        .Build();

    private readonly PostgreSqlContainer _billingDbContainer = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("billing_db")
        .WithUsername("billing_user")
        .WithPassword("billing_test_pwd")
        .Build();

    private WebApplicationFactory<InventoryProductsController> _inventoryFactory = null!;
    private WebApplicationFactory<Program> _billingFactory = null!;
    private HttpClient _billingClient = null!;
    private HttpClient _inventoryClient = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_inventoryDbContainer.StartAsync(), _billingDbContainer.StartAsync());

        _inventoryFactory = new WebApplicationFactory<InventoryProductsController>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:InventoryDb", _inventoryDbContainer.GetConnectionString());
        });

        using (var scope = _inventoryFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await db.Database.MigrateAsync();
        }

        _billingFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:BillingDb", _billingDbContainer.GetConnectionString());
            builder.UseSetting("InventoryApi:BaseUrl", "http://inventory.local");

            builder.ConfigureTestServices(services =>
            {
                // Route both Billing->Inventory typed clients through
                // Inventory.Api's own in-process TestServer instead of a
                // real socket connection, exactly like
                // InvoicesRealInventoryIntegrationTests does for the product
                // lookup client.
                services.AddHttpClient<IInventoryProductClient, InventoryProductClient>(client =>
                {
                    client.BaseAddress = new Uri("http://inventory.local");
                    client.Timeout = TimeSpan.FromSeconds(5);
                }).ConfigurePrimaryHttpMessageHandler(() => _inventoryFactory.Server.CreateHandler());

                services.AddHttpClient<IInventoryStockClient, InventoryStockClient>(client =>
                {
                    client.BaseAddress = new Uri("http://inventory.local");
                    client.Timeout = TimeSpan.FromSeconds(5);
                }).ConfigurePrimaryHttpMessageHandler(() => _inventoryFactory.Server.CreateHandler());
            });
        });

        using (var scope = _billingFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            await db.Database.MigrateAsync();
        }

        _billingClient = _billingFactory.CreateClient();
        _inventoryClient = _inventoryFactory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _billingClient.Dispose();
        _inventoryClient.Dispose();
        await _billingFactory.DisposeAsync();
        await _inventoryFactory.DisposeAsync();
        await _inventoryDbContainer.DisposeAsync();
        await _billingDbContainer.DisposeAsync();
    }

    private async Task<InventoryProductResponse> CreateProductAsync(string code, int balance)
    {
        using var response = await _inventoryClient.PostAsJsonAsync(
            "/api/products", new InventoryCreateProductRequest(code, $"Product {code}", balance));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InventoryProductResponse>())!;
    }

    private async Task<int> GetBalanceAsync(int productId)
    {
        using var response = await _inventoryClient.GetAsync($"/api/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<InventoryProductResponse>();
        return product!.Balance;
    }

    [Fact]
    public async Task Print_Open_Invoice_Closes_It_And_Debits_The_Real_Inventory_Balance()
    {
        // Arrange
        var product = await CreateProductAsync("SKU-PRINT-1", 10);
        using var invoiceResponse = await _billingClient.PostAsJsonAsync(
            "/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(product.Id, 4)]));
        var invoice = (await invoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        // Act
        using var printResponse = await _billingClient.PostAsync($"/api/invoices/{invoice.Id}/print", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, printResponse.StatusCode);
        var printed = await printResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal("Closed", printed!.Status);
        Assert.NotNull(printed.ClosedAtUtc);

        Assert.Equal(6, await GetBalanceAsync(product.Id));
    }

    [Fact]
    public async Task Print_With_Multiple_Items_Debits_All_Balances_Atomically()
    {
        // Arrange
        var productA = await CreateProductAsync("SKU-PRINT-2A", 5);
        var productB = await CreateProductAsync("SKU-PRINT-2B", 8);

        using var invoiceResponse = await _billingClient.PostAsJsonAsync(
            "/api/invoices",
            new CreateInvoiceRequest(
            [
                new CreateInvoiceItemRequest(productA.Id, 2),
                new CreateInvoiceItemRequest(productB.Id, 3),
            ]));
        var invoice = (await invoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        // Act
        using var printResponse = await _billingClient.PostAsync($"/api/invoices/{invoice.Id}/print", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, printResponse.StatusCode);
        Assert.Equal(3, await GetBalanceAsync(productA.Id));
        Assert.Equal(5, await GetBalanceAsync(productB.Id));
    }

    [Fact]
    public async Task Print_With_Insufficient_Balance_Returns_Conflict_And_Keeps_Invoice_Open_And_Balance_Unchanged()
    {
        // Arrange: order more than what is in stock.
        var product = await CreateProductAsync("SKU-PRINT-3", 2);
        using var invoiceResponse = await _billingClient.PostAsJsonAsync(
            "/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(product.Id, 5)]));
        var invoice = (await invoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        // Act
        using var printResponse = await _billingClient.PostAsync($"/api/invoices/{invoice.Id}/print", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, printResponse.StatusCode);

        using var getResponse = await _billingClient.GetAsync($"/api/invoices/{invoice.Id}");
        var reloaded = await getResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal("Open", reloaded!.Status);
        Assert.Null(reloaded.ClosedAtUtc);

        Assert.Equal(2, await GetBalanceAsync(product.Id));
    }

    [Fact]
    public async Task Print_Retried_After_Success_Does_Not_Debit_The_Balance_Twice()
    {
        // Arrange
        var product = await CreateProductAsync("SKU-PRINT-4", 10);
        using var invoiceResponse = await _billingClient.PostAsJsonAsync(
            "/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(product.Id, 3)]));
        var invoice = (await invoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        using var firstPrint = await _billingClient.PostAsync($"/api/invoices/{invoice.Id}/print", null);
        Assert.Equal(HttpStatusCode.OK, firstPrint.StatusCode);
        Assert.Equal(7, await GetBalanceAsync(product.Id));

        // Act: retry printing the already-closed invoice.
        using var secondPrint = await _billingClient.PostAsync($"/api/invoices/{invoice.Id}/print", null);

        // Assert: rejected (already closed) and the balance was debited only once.
        Assert.Equal(HttpStatusCode.Conflict, secondPrint.StatusCode);
        Assert.Equal(7, await GetBalanceAsync(product.Id));
    }

    [Fact]
    public async Task Print_Reused_OperationId_After_Simulated_Crash_Is_Idempotent_On_The_Real_Inventory_Api()
    {
        // Arrange: create the invoice and pre-reserve its OperationId, then
        // call Inventory's debit endpoint directly with that same
        // OperationId, simulating a Billing crash that happened right after
        // the debit succeeded but before the invoice could be closed (the
        // invoice therefore stays Open with the OperationId already
        // persisted, exactly as InvoiceService.PrintAsync leaves it).
        var product = await CreateProductAsync("SKU-PRINT-5", 10);
        using var invoiceResponse = await _billingClient.PostAsJsonAsync(
            "/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(product.Id, 4)]));
        var invoice = (await invoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        var preReservedOperationId = Guid.NewGuid();

        using (var scope = _billingFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE invoices SET \"OperationId\" = {preReservedOperationId} WHERE \"Id\" = {invoice.Id}");
        }

        // Simulate the "debit already succeeded" half of the crash by
        // calling Inventory directly with the same OperationId Billing would
        // reuse.
        using var directDebitResponse = await _inventoryClient.PostAsJsonAsync(
            "/api/stock/debits",
            new
            {
                OperationId = preReservedOperationId,
                Items = new[] { new { ProductId = product.Id, Quantity = 4 } },
            });
        Assert.Equal(HttpStatusCode.OK, directDebitResponse.StatusCode);
        Assert.Equal(6, await GetBalanceAsync(product.Id));

        // Act: Billing retries the print for the still-Open invoice, reusing
        // the persisted OperationId.
        using var printResponse = await _billingClient.PostAsync($"/api/invoices/{invoice.Id}/print", null);

        // Assert: Inventory recognizes the repeated OperationId and replays
        // the original result instead of debiting again; Billing then closes
        // the invoice.
        Assert.Equal(HttpStatusCode.OK, printResponse.StatusCode);
        var printed = await printResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal("Closed", printed!.Status);

        Assert.Equal(6, await GetBalanceAsync(product.Id));
    }

    [Fact]
    public async Task Print_Unknown_Invoice_Returns_NotFound()
    {
        // Act
        using var response = await _billingClient.PostAsync("/api/invoices/999999/print", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Print_When_First_Attempt_Response_Is_Lost_At_Transport_Level_Automatic_Retry_Reuses_OperationId_And_Debits_Once()
    {
        // Arrange: a real product with real balance in the real Inventory.Api/Postgres.
        var product = await CreateProductAsync("SKU-PRINT-7", 10);

        // Unlike Print_When_Stock_Was_Debited_But_Response_Was_Lost_Retry_Closes_Without_Second_Debit
        // below (which simulates the crash/retry sequence by making the test
        // itself call print twice), this test drives the retry entirely
        // through the real Billing->Inventory resilience pipeline (Task 11):
        // a single POST /print from the caller. The transport-level wrapper
        // below lets the first physical HTTP attempt genuinely reach the
        // real, in-process Inventory.Api and apply the debit, then discards
        // that successful response and throws a transient
        // HttpRequestException instead, simulating a dropped
        // connection/lost response *underneath* the pipeline. Polly's own
        // retry stage catches that transient failure and re-sends the exact
        // same request (same OperationId, computed and persisted once by
        // InvoiceService before DebitAsync is ever called) to Inventory,
        // which recognizes the repeat via its own OperationId idempotency
        // and replays the original result instead of debiting again.
        var responseLostOnce = new ResponseLostOnceHandler(_inventoryFactory.Server.CreateHandler());
        var resilienceOptions = ResilientInventoryClientFactory.FastTestOptions(retryMaxAttempts: 2);
        var (stockClient, stockClientProvider) = ResilientInventoryClientFactory.CreateStockClient(resilienceOptions, responseLostOnce);
        await using var stockClientProviderDisposable = stockClientProvider;

        await using var billingFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:BillingDb", _billingDbContainer.GetConnectionString());
            builder.UseSetting("InventoryApi:BaseUrl", "http://inventory.local");

            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<IInventoryProductClient, InventoryProductClient>(client =>
                {
                    client.BaseAddress = new Uri("http://inventory.local");
                    client.Timeout = TimeSpan.FromSeconds(5);
                }).ConfigurePrimaryHttpMessageHandler(() => _inventoryFactory.Server.CreateHandler());

                services.RemoveAll<IInventoryStockClient>();
                services.AddSingleton(stockClient);
            });
        });

        using (var scope = billingFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            await db.Database.MigrateAsync();
        }

        using var client = billingFactory.CreateClient();

        using var invoiceResponse = await client.PostAsJsonAsync(
            "/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(product.Id, 4)]));
        var invoice = (await invoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        // Act: a single print request; the retry happens transparently
        // inside the resilience pipeline, invisible to the caller.
        using var printResponse = await client.PostAsync($"/api/invoices/{invoice.Id}/print", null);

        // Assert: the caller sees a single successful response; the invoice is closed.
        Assert.Equal(HttpStatusCode.OK, printResponse.StatusCode);
        var printed = await printResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal("Closed", printed!.Status);
        Assert.NotNull(printed.ClosedAtUtc);

        // Assert: the balance was debited exactly once (not twice).
        Assert.Equal(6, await GetBalanceAsync(product.Id));

        // Assert: the OperationId persisted for the invoice was reused by
        // the automatic retry, and Inventory recorded exactly one operation
        // for it.
        var operationId = await GetPersistedOperationIdAsync(billingFactory, invoice.Id);
        Assert.NotNull(operationId);
        Assert.Equal(1, await CountStockDebitOperationsAsync(operationId!.Value));

        // Assert: the transport-level wrapper genuinely saw two physical
        // HTTP attempts underneath the pipeline (the lost one and the retry).
        Assert.Equal(2, responseLostOnce.AttemptCount);
    }

    /// <summary>
    /// Wraps the real, in-process Inventory.Api handler to simulate a
    /// transport-level failure (dropped connection) immediately after a
    /// successful response, so that the resilience pipeline sitting above it
    /// (not the test) is the one driving the retry.
    /// </summary>
    private sealed class ResponseLostOnceHandler : DelegatingHandler
    {
        private bool _hasSimulatedLoss;

        public ResponseLostOnceHandler(HttpMessageHandler inner)
            : base(inner)
        {
        }

        public int AttemptCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AttemptCount++;
            var response = await base.SendAsync(request, cancellationToken);

            if (!_hasSimulatedLoss && response.IsSuccessStatusCode)
            {
                _hasSimulatedLoss = true;
                response.Dispose();
                throw new HttpRequestException(
                    "Simulated: the debit succeeded on Inventory but the response was lost at the transport layer.");
            }

            return response;
        }
    }

    [Fact]
    public async Task Print_When_Stock_Was_Debited_But_Response_Was_Lost_Retry_Closes_Without_Second_Debit()
    {
        // Arrange: a dedicated Billing factory whose IInventoryStockClient is
        // the real InventoryStockClient (hitting the real, in-process
        // Inventory.Api/Postgres via the same TestServer handler used
        // elsewhere in this class), wrapped so the first call's debit is
        // genuinely applied but its response is discarded, simulating a
        // crash/dropped connection between Inventory committing the debit
        // and Billing reading the HTTP response.
        var product = await CreateProductAsync("SKU-PRINT-6", 10);

        var realStockClient = new InventoryStockClient(_inventoryFactory.CreateClient());
        var lossyStockClient = new ResponseLostAfterSuccessfulDebitStockClient(realStockClient);

        await using var billingFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:BillingDb", _billingDbContainer.GetConnectionString());
            builder.UseSetting("InventoryApi:BaseUrl", "http://inventory.local");

            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<IInventoryProductClient, InventoryProductClient>(client =>
                {
                    client.BaseAddress = new Uri("http://inventory.local");
                    client.Timeout = TimeSpan.FromSeconds(5);
                }).ConfigurePrimaryHttpMessageHandler(() => _inventoryFactory.Server.CreateHandler());

                services.RemoveAll<IInventoryStockClient>();
                services.AddSingleton<IInventoryStockClient>(lossyStockClient);
            });
        });

        using var client = billingFactory.CreateClient();

        using var invoiceResponse = await client.PostAsJsonAsync(
            "/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(product.Id, 4)]));
        var invoice = (await invoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        // Act: first print attempt. The real debit happens (balance drops),
        // but the simulated response loss makes Billing observe a failure
        // before it can close the invoice.
        using var firstPrintResponse = await client.PostAsync($"/api/invoices/{invoice.Id}/print", null);

        // Assert: the failure surfaces as 503 and the invoice stays Open,
        // but the OperationId reserved before the call was persisted and the
        // balance was already reduced exactly once.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, firstPrintResponse.StatusCode);

        using var afterFirstAttemptResponse = await client.GetAsync($"/api/invoices/{invoice.Id}");
        var afterFirstAttempt = await afterFirstAttemptResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal("Open", afterFirstAttempt!.Status);
        Assert.Null(afterFirstAttempt.ClosedAtUtc);

        Assert.Equal(6, await GetBalanceAsync(product.Id));

        var persistedOperationId = await GetPersistedOperationIdAsync(billingFactory, invoice.Id);
        Assert.NotNull(persistedOperationId);

        // Act: retry the print. Billing reuses the persisted OperationId;
        // Inventory recognizes the repeat and replays the original result
        // instead of debiting a second time.
        using var secondPrintResponse = await client.PostAsync($"/api/invoices/{invoice.Id}/print", null);

        // Assert: the retry succeeds and closes the invoice, the balance
        // reflects only the single original debit, and Inventory recorded
        // exactly one operation for the reused OperationId.
        Assert.Equal(HttpStatusCode.OK, secondPrintResponse.StatusCode);
        var printed = await secondPrintResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal("Closed", printed!.Status);
        Assert.NotNull(printed.ClosedAtUtc);

        Assert.Equal(6, await GetBalanceAsync(product.Id));
        Assert.Equal(1, await CountStockDebitOperationsAsync(persistedOperationId!.Value));
    }

    private static async Task<Guid?> GetPersistedOperationIdAsync(WebApplicationFactory<Program> factory, int invoiceId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        return await dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => i.OperationId)
            .FirstOrDefaultAsync();
    }

    private async Task<int> CountStockDebitOperationsAsync(Guid operationId)
    {
        using var scope = _inventoryFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        return await dbContext.StockDebitOperations.CountAsync(o => o.OperationId == operationId);
    }

    /// <summary>
    /// Wraps a real <see cref="IInventoryStockClient"/> to simulate the
    /// distributed-failure window between Inventory committing a debit and
    /// Billing observing that success. The first call is forwarded to the
    /// inner (real) client, so the debit genuinely happens against
    /// Inventory's database, but the result is then discarded and an
    /// <see cref="InventoryServiceUnavailableException"/> is thrown instead
    /// &#8212; exactly how <see cref="InventoryStockClient"/> itself surfaces a
    /// dropped connection or timed-out response. Subsequent calls are
    /// forwarded normally, so a retry observes Inventory's idempotent replay.
    /// </summary>
    private sealed class ResponseLostAfterSuccessfulDebitStockClient : IInventoryStockClient
    {
        private readonly IInventoryStockClient _inner;
        private bool _hasSimulatedTheLoss;

        public ResponseLostAfterSuccessfulDebitStockClient(IInventoryStockClient inner)
        {
            _inner = inner;
        }

        public async Task<StockDebitResultDto> DebitAsync(StockDebitRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _inner.DebitAsync(request, cancellationToken);

            if (!_hasSimulatedTheLoss)
            {
                _hasSimulatedTheLoss = true;
                throw new InventoryServiceUnavailableException(
                    "Simulated: the debit succeeded on Inventory but the HTTP response was lost before Billing could read it.");
            }

            return result;
        }
    }
}
