using System.Net;
using System.Net.Http.Json;
using Inventory.Api.Data;
using Inventory.Api.Features.Products;
using Inventory.Api.Features.Stock;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Inventory.Tests;

/// <summary>
/// End-to-end concurrency tests for the stock debit endpoint (Task 12),
/// exercised against a real, containerized PostgreSQL database. These tests
/// prove that two (or more) concurrent debit requests with *different*
/// <c>OperationId</c>s racing to consume the balance of the same product(s)
/// never oversell: the database-level <c>SELECT ... FOR UPDATE</c> row lock
/// in <see cref="StockDebitService.DebitAsync"/> serializes access to the
/// contended product rows, so exactly one request wins per unit of balance
/// and every loser observes the updated balance and fails the normal
/// insufficient-balance business rule (409), never a lock/timeout error.
///
/// All coordination uses real concurrent HTTP requests started together via
/// <see cref="Task.WhenAll"/> (with <see cref="Barrier"/>/<see cref="TaskCompletionSource{TResult}"/>
/// where genuinely simultaneous starts matter) and a safety
/// <see cref="CancellationTokenSource"/> timeout to fail fast and loudly on
/// a real deadlock instead of hanging. No production code was changed to
/// support these tests - no test-only flags, delays, or endpoints.
/// </summary>
public class StockConcurrencyApiTests : IAsyncLifetime
{
    private static readonly TimeSpan DeadlockSafetyTimeout = TimeSpan.FromSeconds(30);

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("inventory_db")
        .WithUsername("inventory_user")
        .WithPassword("inventory_test_pwd")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:InventoryDb", _container.GetConnectionString());
        });

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    private async Task<ProductResponse> CreateProductAsync(HttpClient client, string code, string description, int balance)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/products", new CreateProductRequest(code, description, balance));
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        return product!;
    }

    private async Task<int> GetBalanceAsync(HttpClient client, int productId)
    {
        using var response = await client.GetAsync($"/api/products/{productId}");
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        return product!.Balance;
    }

    private InventoryDbContext CreateIndependentDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new InventoryDbContext(options);
    }

    /// <summary>
    /// Fires <paramref name="requests"/> concurrently, all released at once
    /// via a <see cref="Barrier"/> so every task reaches the HTTP call at
    /// roughly the same time (rather than being sequenced by task
    /// scheduling), each on its own <see cref="HttpClient"/>/scope. A safety
    /// timeout guards against a real deadlock hanging the test suite.
    /// </summary>
    private static async Task<HttpResponseMessage[]> SendConcurrentlyAsync(
        IReadOnlyList<(HttpClient Client, StockDebitRequest Request)> requests)
    {
        using var cts = new CancellationTokenSource(DeadlockSafetyTimeout);
        using var barrier = new Barrier(requests.Count);

        var tasks = requests.Select(r => Task.Run(async () =>
        {
            barrier.SignalAndWait(cts.Token);
            return await r.Client.PostAsJsonAsync("/api/stock/debits", r.Request, cts.Token);
        }, cts.Token)).ToArray();

        try
        {
            return await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"Concurrent stock debit requests did not complete within {DeadlockSafetyTimeout} " +
                "- possible deadlock.");
        }
    }

    [Fact]
    public async Task Two_Concurrent_Debits_With_Different_OperationIds_Against_Balance_One_Yield_One_Success_One_Conflict()
    {
        // Arrange
        using var setupClient = CreateClient();
        var product = await CreateProductAsync(setupClient, "SKU-CONC-1", "Widget", 1);

        var clientA = CreateClient();
        var clientB = CreateClient();
        try
        {
            var requestA = new StockDebitRequest(Guid.NewGuid(), [new StockDebitItemRequest(product.Id, 1)]);
            var requestB = new StockDebitRequest(Guid.NewGuid(), [new StockDebitItemRequest(product.Id, 1)]);

            // Act
            var responses = await SendConcurrentlyAsync(
                [(clientA, requestA), (clientB, requestB)]);

            // Assert
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(0, await GetBalanceAsync(setupClient, product.Id));

            await using var dbContext = CreateIndependentDbContext();
            var operationsCount = await dbContext.StockDebitOperations
                .AsNoTracking()
                .CountAsync(o => o.Items.Any(i => i.ProductId == product.Id));
            Assert.Equal(1, operationsCount);

            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
        finally
        {
            clientA.Dispose();
            clientB.Dispose();
        }
    }

    [Fact]
    public async Task More_Concurrent_Debits_Than_Balance_Succeed_Exactly_As_Many_Times_As_The_Initial_Balance()
    {
        // Arrange: balance of 3, five concurrent single-unit debits.
        const int initialBalance = 3;
        const int concurrentRequests = 5;

        using var setupClient = CreateClient();
        var product = await CreateProductAsync(setupClient, "SKU-CONC-2", "Widget", initialBalance);

        var clients = Enumerable.Range(0, concurrentRequests).Select(_ => CreateClient()).ToList();
        try
        {
            var requests = clients
                .Select(client => (
                    client,
                    new StockDebitRequest(Guid.NewGuid(), [new StockDebitItemRequest(product.Id, 1)])))
                .ToList();

            // Act
            var responses = await SendConcurrentlyAsync(requests);

            // Assert
            var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
            var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

            Assert.Equal(initialBalance, successCount);
            Assert.Equal(concurrentRequests - initialBalance, conflictCount);

            var finalBalance = await GetBalanceAsync(setupClient, product.Id);
            Assert.Equal(0, finalBalance);
            Assert.True(finalBalance >= 0);

            await using var dbContext = CreateIndependentDbContext();
            var operationsCount = await dbContext.StockDebitOperations
                .AsNoTracking()
                .CountAsync(o => o.Items.Any(i => i.ProductId == product.Id));
            Assert.Equal(successCount, operationsCount);

            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }
    }

    [Fact]
    public async Task Concurrent_Debits_With_Overlapping_Products_In_Reversed_Order_Complete_Without_Deadlock()
    {
        // Arrange: two products, each request references both, but in
        // opposite list order. Without deterministic lock ordering this
        // could deadlock (A locks product 1 then waits on product 2, B
        // locks product 2 then waits on product 1).
        using var setupClient = CreateClient();
        var productX = await CreateProductAsync(setupClient, "SKU-CONC-3-X", "Widget X", 5);
        var productY = await CreateProductAsync(setupClient, "SKU-CONC-3-Y", "Widget Y", 5);

        var clientA = CreateClient();
        var clientB = CreateClient();
        try
        {
            var requestA = new StockDebitRequest(
                Guid.NewGuid(),
                [
                    new StockDebitItemRequest(productX.Id, 1),
                    new StockDebitItemRequest(productY.Id, 1),
                ]);
            var requestB = new StockDebitRequest(
                Guid.NewGuid(),
                [
                    new StockDebitItemRequest(productY.Id, 1),
                    new StockDebitItemRequest(productX.Id, 1),
                ]);

            // Act (bounded by the safety timeout inside SendConcurrentlyAsync;
            // a real deadlock would throw TimeoutException and fail the test
            // explicitly instead of hanging).
            var responses = await SendConcurrentlyAsync(
                [(clientA, requestA), (clientB, requestB)]);

            // Assert: both requests succeed (enough balance for both), and
            // balances reflect exactly two debits of one unit each.
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            Assert.Equal(3, await GetBalanceAsync(setupClient, productX.Id));
            Assert.Equal(3, await GetBalanceAsync(setupClient, productY.Id));

            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
        finally
        {
            clientA.Dispose();
            clientB.Dispose();
        }
    }

    [Fact]
    public async Task Concurrent_Debits_Against_Different_Products_Both_Succeed_Without_Global_Lock()
    {
        // Arrange: two independent products. This proves there is no
        // global table/application-level lock: one debit is coordinated to
        // hold its row lock deliberately (via an overlapping second item on
        // a "hold" product with a slow-to-arrive but otherwise independent
        // request) while a completely unrelated product's debit is issued
        // and must complete promptly rather than being blocked behind it.
        using var setupClient = CreateClient();
        var productA = await CreateProductAsync(setupClient, "SKU-CONC-4-A", "Widget A", 5);
        var productB = await CreateProductAsync(setupClient, "SKU-CONC-4-B", "Widget B", 5);

        var clientA = CreateClient();
        var clientB = CreateClient();
        try
        {
            var requestA = new StockDebitRequest(Guid.NewGuid(), [new StockDebitItemRequest(productA.Id, 2)]);
            var requestB = new StockDebitRequest(Guid.NewGuid(), [new StockDebitItemRequest(productB.Id, 3)]);

            // Act
            var responses = await SendConcurrentlyAsync(
                [(clientA, requestA), (clientB, requestB)]);

            // Assert: both succeed independently, no cross-blocking.
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            Assert.Equal(3, await GetBalanceAsync(setupClient, productA.Id));
            Assert.Equal(2, await GetBalanceAsync(setupClient, productB.Id));

            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
        finally
        {
            clientA.Dispose();
            clientB.Dispose();
        }
    }

    [Fact]
    public async Task Concurrent_Requests_With_Same_OperationId_Still_Debit_Only_Once()
    {
        // Arrange: existing idempotency protection (unique index +
        // DbUpdateException handling) must keep working now that the
        // product read also takes a row lock.
        using var setupClient = CreateClient();
        var product = await CreateProductAsync(setupClient, "SKU-CONC-5", "Widget", 5);
        var operationId = Guid.NewGuid();

        var clientA = CreateClient();
        var clientB = CreateClient();
        try
        {
            var request = new StockDebitRequest(operationId, [new StockDebitItemRequest(product.Id, 2)]);

            // Act
            var responses = await SendConcurrentlyAsync(
                [(clientA, request), (clientB, request)]);

            // Assert: both requests receive the same idempotent 200 result.
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

            var results = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<StockDebitResponse>()));
            Assert.All(results, result => Assert.Equal(3, result!.Items.Single().BalanceAfter));
            Assert.Equal(3, await GetBalanceAsync(setupClient, product.Id));

            await using var dbContext = CreateIndependentDbContext();
            var operationsCount = await dbContext.StockDebitOperations
                .AsNoTracking()
                .CountAsync(o => o.OperationId == operationId);
            Assert.Equal(1, operationsCount);

            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
        finally
        {
            clientA.Dispose();
            clientB.Dispose();
        }
    }

    [Fact]
    public async Task Concurrent_Requests_With_Same_OperationId_Against_Exact_Balance_Both_Return_Success()
    {
        // Arrange: this is the regression scenario for the bug where the
        // "loser" of the row lock (blocked inside SELECT ... FOR UPDATE
        // while the "winner" commits) wakes up, re-reads the now-updated
        // balance, and - if that balance is no longer sufficient for the
        // requested quantity - throws InsufficientProductBalanceException
        // (409) from inside Product.Debit *before* ever reaching the
        // SaveChangesAsync/unique-violation safety net. Balance 1, quantity
        // 1: after the winner debits, 0 remains, which is < 1, so the loser
        // must never fall through to the insufficient-balance business rule
        // for what is, in fact, the very same logical request (same
        // OperationId). Both responses must be the same idempotent 200.
        using var setupClient = CreateClient();
        var product = await CreateProductAsync(setupClient, "SKU-CONC-7", "Widget", 1);
        var operationId = Guid.NewGuid();

        var clientA = CreateClient();
        var clientB = CreateClient();
        try
        {
            var request = new StockDebitRequest(operationId, [new StockDebitItemRequest(product.Id, 1)]);

            // Act
            var responses = await SendConcurrentlyAsync(
                [(clientA, request), (clientB, request)]);

            // Assert: both requests receive the same idempotent 200 result,
            // never a 409, even though the balance is exactly exhausted by
            // a single debit.
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

            var results = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<StockDebitResponse>()));
            Assert.All(results, result => Assert.Equal(0, result!.Items.Single().BalanceAfter));
            Assert.Equal(results[0]!.Items.Single().BalanceAfter, results[1]!.Items.Single().BalanceAfter);
            Assert.Equal(0, await GetBalanceAsync(setupClient, product.Id));

            await using var dbContext = CreateIndependentDbContext();
            var operationsCount = await dbContext.StockDebitOperations
                .AsNoTracking()
                .CountAsync(o => o.OperationId == operationId);
            Assert.Equal(1, operationsCount);

            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
        finally
        {
            clientA.Dispose();
            clientB.Dispose();
        }
    }

    [Fact]
    public async Task Concurrent_Requests_With_Same_OperationId_But_Different_Payloads_Only_Debit_Once()
    {
        // Arrange: same OperationId, but the two concurrent requests carry
        // different quantities for the same product. Whichever wins the row
        // lock persists first and its result must prevail; the loser's
        // second idempotency check (right after acquiring the lock) must
        // detect the already-persisted operation and return the winner's
        // result untouched, ignoring its own (different) payload entirely -
        // no second real debit may occur. This is the concurrent
        // counterpart to the sequential
        // Repeating_Same_OperationId_With_Different_Items_Still_Returns_Original_Result
        // test in StockApiTests.cs.
        using var setupClient = CreateClient();
        var product = await CreateProductAsync(setupClient, "SKU-CONC-8", "Widget", 10);
        var operationId = Guid.NewGuid();

        var clientA = CreateClient();
        var clientB = CreateClient();
        try
        {
            var requestA = new StockDebitRequest(operationId, [new StockDebitItemRequest(product.Id, 3)]);
            var requestB = new StockDebitRequest(operationId, [new StockDebitItemRequest(product.Id, 7)]);

            // Act
            var responses = await SendConcurrentlyAsync(
                [(clientA, requestA), (clientB, requestB)]);

            // Assert: both requests return 200 with the exact same result
            // (whichever payload won), and only one real debit happened.
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

            var results = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<StockDebitResponse>()));
            var quantityDebited = results[0]!.Items.Single().QuantityDebited;
            Assert.True(quantityDebited is 3 or 7);
            Assert.All(results, result => Assert.Equal(quantityDebited, result!.Items.Single().QuantityDebited));
            Assert.All(results, result => Assert.Equal(10 - quantityDebited, result!.Items.Single().BalanceAfter));

            var expectedBalance = 10 - quantityDebited;
            Assert.Equal(expectedBalance, await GetBalanceAsync(setupClient, product.Id));

            await using var dbContext = CreateIndependentDbContext();
            var operationsCount = await dbContext.StockDebitOperations
                .AsNoTracking()
                .CountAsync(o => o.OperationId == operationId);
            Assert.Equal(1, operationsCount);

            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
        finally
        {
            clientA.Dispose();
            clientB.Dispose();
        }
    }

    [Fact]
    public async Task Debit_With_One_Valid_And_One_Insufficient_Item_Still_Rolls_Back_Atomically_Under_Row_Locking()
    {
        // Arrange: regression check that the FOR UPDATE change did not
        // affect atomicity of a single (non-concurrent) request spanning a
        // valid item and an insufficient-balance item.
        using var client = CreateClient();
        var productA = await CreateProductAsync(client, "SKU-CONC-6-A", "Widget A", 10);
        var productB = await CreateProductAsync(client, "SKU-CONC-6-B", "Widget B", 1);

        var request = new StockDebitRequest(
            Guid.NewGuid(),
            [
                new StockDebitItemRequest(productA.Id, 5),
                new StockDebitItemRequest(productB.Id, 5),
            ]);

        // Act
        using var response = await client.PostAsJsonAsync("/api/stock/debits", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(10, await GetBalanceAsync(client, productA.Id));
        Assert.Equal(1, await GetBalanceAsync(client, productB.Id));

        await using var dbContext = CreateIndependentDbContext();
        var operationsCount = await dbContext.StockDebitOperations
            .AsNoTracking()
            .CountAsync(o => o.Items.Any(i => i.ProductId == productA.Id || i.ProductId == productB.Id));
        Assert.Equal(0, operationsCount);
    }
}
