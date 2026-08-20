using System.Net;
using System.Net.Http.Json;
using Inventory.Api.Data;
using Inventory.Api.Features.Products;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Inventory.Tests;

/// <summary>
/// End-to-end tests for the products endpoints, exercised against a real,
/// containerized PostgreSQL database (migrations applied at startup) instead
/// of mocks, per the project's persistence-testing convention.
/// A fresh container is started for each test instance (xUnit creates one
/// class instance per test), so tests do not interfere with each other.
/// </summary>
public class ProductsApiTests : IAsyncLifetime
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

    [Fact]
    public async Task Create_With_Valid_Data_Returns_Created_With_Location()
    {
        // Arrange
        var request = new CreateProductRequest("SKU-001", "Widget", 10);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/products", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);
        Assert.Equal("SKU-001", created!.Code);
        Assert.Equal("Widget", created.Description);
        Assert.Equal(10, created.Balance);
        Assert.True(created.Id > 0);
    }

    [Theory]
    [InlineData(null, "Widget", 1)]
    [InlineData("", "Widget", 1)]
    [InlineData("   ", "Widget", 1)]
    [InlineData("SKU-002", null, 1)]
    [InlineData("SKU-003", "", 1)]
    [InlineData("SKU-004", "Widget", -1)]
    [InlineData("SKU-005", "Widget", 0)]
    public async Task Create_With_Invalid_Data_Returns_BadRequest(string? code, string? description, int balance)
    {
        // Arrange
        var request = new CreateProductRequest(code, description, balance);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/products", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_With_Non_Positive_Initial_Balance_Returns_BadRequest_With_Portuguese_Message_ErrorCode_And_TraceId_And_Does_Not_Persist(int balance)
    {
        // Arrange
        var request = new CreateProductRequest("SKU-ZERO", "Widget", balance);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/products", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(problem.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
        Assert.True(problem.TryGetProperty("errorCode", out var errorCode));
        Assert.Equal("INVALID_PRODUCT", errorCode.GetString());
        Assert.True(problem.TryGetProperty("errors", out var errors));
        Assert.Contains(
            "O saldo inicial deve ser um número inteiro maior que zero.",
            errors.GetProperty("product").EnumerateArray().Select(e => e.GetString()));
        Assert.False(problem.ToString().Contains("StackTrace", StringComparison.OrdinalIgnoreCase));

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.False(await dbContext.Products.AnyAsync(p => p.Code == "SKU-ZERO"));
    }

    [Fact]
    public async Task Create_With_Initial_Balance_One_Returns_Created_And_Persists()
    {
        // Arrange
        var request = new CreateProductRequest("SKU-ONE", "Widget", 1);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/products", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);
        Assert.Equal(1, created!.Balance);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var persisted = await dbContext.Products.AsNoTracking().SingleAsync(p => p.Code == "SKU-ONE");
        Assert.Equal(1, persisted.Balance);
    }

    [Fact]
    public async Task Create_With_Duplicate_Code_Returns_Conflict()
    {
        // Arrange
        var request = new CreateProductRequest("SKU-DUP", "Widget", 1);
        using var first = await _client.PostAsJsonAsync("/api/products", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Act
        using var second = await _client.PostAsJsonAsync("/api/products", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetAll_Returns_Registered_Products()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/products", new CreateProductRequest("SKU-LIST-1", "Item 1", 1));
        await _client.PostAsJsonAsync("/api/products", new CreateProductRequest("SKU-LIST-2", "Item 2", 2));

        // Act
        using var response = await _client.GetAsync("/api/products");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<ProductResponse>>();
        Assert.NotNull(products);
        Assert.Equal(2, products!.Count);
        Assert.Contains(products, p => p.Code == "SKU-LIST-1");
        Assert.Contains(products, p => p.Code == "SKU-LIST-2");
    }

    [Fact]
    public async Task GetById_With_Existing_Product_Returns_Ok()
    {
        // Arrange
        using var createResponse = await _client.PostAsJsonAsync(
            "/api/products", new CreateProductRequest("SKU-GET", "Item", 5));
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        using var response = await _client.GetAsync($"/api/products/{created!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.Equal(created.Id, product!.Id);
        Assert.Equal("SKU-GET", product.Code);
        Assert.Equal(5, product.Balance);
    }

    [Fact]
    public async Task GetById_With_Unknown_Id_Returns_NotFound()
    {
        // Act
        using var response = await _client.GetAsync("/api/products/999999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Created_Product_Is_Physically_Persisted_In_PostgreSql()
    {
        // Arrange / Act
        using var createResponse = await _client.PostAsJsonAsync(
            "/api/products", new CreateProductRequest("SKU-PERSIST", "Persisted item", 7));
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();

        // Assert: read directly from the container through an independent
        // DbContext instance, bypassing the application's request scope,
        // proving the data was physically written to PostgreSQL.
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        await using var independentContext = new InventoryDbContext(options);

        var persisted = await independentContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == created!.Id);

        Assert.NotNull(persisted);
        Assert.Equal("SKU-PERSIST", persisted!.Code);
        Assert.Equal("Persisted item", persisted.Description);
        Assert.Equal(7, persisted.Balance);
    }
}
