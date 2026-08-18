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
/// End-to-end tests for the stock debit endpoint, exercised against a real,
/// containerized PostgreSQL database (migrations applied at startup) instead
/// of mocks, per the project's persistence-testing convention. These tests
/// prove the debit is atomic (all-or-nothing across products in the same
/// request) and idempotent (repeating the same OperationId never debits
/// twice) against real PostgreSQL transactions and constraints.
/// A fresh container is started for each test instance (xUnit creates one
/// class instance per test), so tests do not interfere with each other.
/// </summary>
public class StockApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("inventory_db")
        .WithUsername("inventory_user")
        .WithPassword("inventory_test_pwd")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:InventoryDb", _container.GetConnectionString());
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    private async Task<ProductResponse> CreateProductAsync(string code, string description, int balance)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/products", new CreateProductRequest(code, description, balance));
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        return product!;
    }

    private async Task<int> GetBalanceAsync(int productId)
    {
        using var response = await _client.GetAsync($"/api/products/{productId}");
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        return product!.Balance;
    }

    [Fact]
    public async Task Debit_With_Exact_Balance_Reaches_Zero()
    {
        // Arrange
        var product = await CreateProductAsync("SKU-EXACT", "Widget", 5);
        var request = new StockDebitRequest(
            Guid.NewGuid(),
            [new StockDebitItemRequest(product.Id, 5)]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/stock/debits", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<StockDebitResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result!.Items.Single().BalanceAfter);
        Assert.Equal(0, await GetBalanceAsync(product.Id));
    }

    [Fact]
    public async Task Debit_With_Multiple_Products_In_Same_Request_Debits_All()
    {
        // Arrange
        var productA = await CreateProductAsync("SKU-MULTI-A", "Widget A", 10);
        var productB = await CreateProductAsync("SKU-MULTI-B", "Widget B", 20);
        var request = new StockDebitRequest(
            Guid.NewGuid(),
            [
                new StockDebitItemRequest(productA.Id, 4),
                new StockDebitItemRequest(productB.Id, 15),
            ]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/stock/debits", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(6, await GetBalanceAsync(productA.Id));
        Assert.Equal(5, await GetBalanceAsync(productB.Id));
    }

    [Fact]
    public async Task Debit_With_Unknown_Product_Returns_NotFound_And_No_Partial_Effects()
    {
        // Arrange
        var product = await CreateProductAsync("SKU-NF", "Widget", 10);
        var request = new StockDebitRequest(
            Guid.NewGuid(),
            [
                new StockDebitItemRequest(product.Id, 5),
                new StockDebitItemRequest(999999, 1),
            ]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/stock/debits", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(10, await GetBalanceAsync(product.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Debit_With_NonPositive_Quantity_Returns_BadRequest(int quantity)
    {
        // Arrange
        var product = await CreateProductAsync("SKU-QTY", "Widget", 10);
        var request = new StockDebitRequest(
            Guid.NewGuid(),
            [new StockDebitItemRequest(product.Id, quantity)]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/stock/debits", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(10, await GetBalanceAsync(product.Id));
    }

    [Fact]
    public async Task Debit_With_Empty_OperationId_Returns_BadRequest()
    {
        // Arrange
        var product = await CreateProductAsync("SKU-NOOP", "Widget", 10);
        var request = new StockDebitRequest(
            Guid.Empty,
            [new StockDebitItemRequest(product.Id, 1)]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/stock/debits", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Debit_With_Empty_Items_Returns_BadRequest()
    {
        // Arrange
        var request = new StockDebitRequest(Guid.NewGuid(), []);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/stock/debits", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Debit_With_Insufficient_Balance_In_One_Item_Rolls_Back_All_Products()
    {
        // Arrange: product A has enough balance alone, product B does not.
        var productA = await CreateProductAsync("SKU-ROLLBACK-A", "Widget A", 10);
        var productB = await CreateProductAsync("SKU-ROLLBACK-B", "Widget B", 3);
        var request = new StockDebitRequest(
            Guid.NewGuid(),
            [
                new StockDebitItemRequest(productA.Id, 5),
                new StockDebitItemRequest(productB.Id, 5),
            ]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/stock/debits", request);

        // Assert: conflict, and neither product was affected (real rollback,
        // proven against the PostgreSQL container).
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(10, await GetBalanceAsync(productA.Id));
        Assert.Equal(3, await GetBalanceAsync(productB.Id));
    }

    [Fact]
    public async Task Repeating_Same_OperationId_Does_Not_Debit_Twice_And_Returns_Same_Result()
    {
        // Arrange
        var product = await CreateProductAsync("SKU-IDEMP", "Widget", 10);
        var operationId = Guid.NewGuid();
        var request = new StockDebitRequest(
            operationId,
            [new StockDebitItemRequest(product.Id, 2)]);

        // Act
        using var first = await _client.PostAsJsonAsync("/api/stock/debits", request);
        var firstResult = await first.Content.ReadFromJsonAsync<StockDebitResponse>();

        using var second = await _client.PostAsJsonAsync("/api/stock/debits", request);
        var secondResult = await second.Content.ReadFromJsonAsync<StockDebitResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(8, firstResult!.Items.Single().BalanceAfter);
        Assert.Equal(8, secondResult!.Items.Single().BalanceAfter);
        Assert.Equal(8, await GetBalanceAsync(product.Id));
        Assert.Equal(firstResult.OperationId, secondResult.OperationId);
    }

    [Fact]
    public async Task Repeating_Same_OperationId_With_Different_Items_Still_Returns_Original_Result()
    {
        // Arrange: the second call carries different items but the same
        // OperationId; idempotency must win, so the original result is
        // returned and no additional debit is applied.
        var product = await CreateProductAsync("SKU-IDEMP-2", "Widget", 10);
        var operationId = Guid.NewGuid();
        var firstRequest = new StockDebitRequest(
            operationId,
            [new StockDebitItemRequest(product.Id, 3)]);

        using var first = await _client.PostAsJsonAsync("/api/stock/debits", firstRequest);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var secondRequest = new StockDebitRequest(
            operationId,
            [new StockDebitItemRequest(product.Id, 4)]);

        // Act
        using var second = await _client.PostAsJsonAsync("/api/stock/debits", secondRequest);
        var secondResult = await second.Content.ReadFromJsonAsync<StockDebitResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(3, secondResult!.Items.Single().QuantityDebited);
        Assert.Equal(7, await GetBalanceAsync(product.Id));
    }

    [Fact]
    public async Task Debit_Operation_Is_Physically_Persisted_In_PostgreSql()
    {
        // Arrange / Act
        var product = await CreateProductAsync("SKU-PERSIST-STOCK", "Widget", 10);
        var operationId = Guid.NewGuid();
        var request = new StockDebitRequest(
            operationId,
            [new StockDebitItemRequest(product.Id, 4)]);

        using var response = await _client.PostAsJsonAsync("/api/stock/debits", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert: read directly from the container through an independent
        // DbContext instance, bypassing the application's request scope,
        // proving the operation was physically written to PostgreSQL.
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        await using var independentContext = new InventoryDbContext(options);

        var persisted = await independentContext.StockDebitOperations
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OperationId == operationId);

        Assert.NotNull(persisted);
        Assert.Single(persisted!.Items);
        Assert.Equal(product.Id, persisted.Items.Single().ProductId);
        Assert.Equal(4, persisted.Items.Single().Quantity);
        Assert.Equal(6, persisted.Items.Single().BalanceAfter);
    }
}
