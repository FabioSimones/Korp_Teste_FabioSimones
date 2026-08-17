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

namespace Billing.Tests;

/// <summary>
/// End-to-end tests for the invoices endpoints, exercised against a real,
/// containerized PostgreSQL database (migrations applied at startup), per the
/// project's persistence-testing convention. The call to Inventory.Api is
/// isolated with a test double (<see cref="FakeInventoryProductClient"/>);
/// a separate real, in-process integration with Inventory.Api is covered by
/// <see cref="InvoicesRealInventoryIntegrationTests"/>.
/// A fresh container/factory is started for each test (xUnit creates one
/// class instance per test), so tests do not interfere with each other.
/// </summary>
public class InvoicesApiTests : IAsyncLifetime
{
    private static readonly IReadOnlyDictionary<int, InventoryProductLookupResult> KnownProducts =
        new Dictionary<int, InventoryProductLookupResult>
        {
            [1] = new InventoryProductLookupResult(1, "SKU-1", "Widget A"),
            [2] = new InventoryProductLookupResult(2, "SKU-2", "Widget B"),
            [3] = new InventoryProductLookupResult(3, "SKU-3", "Widget C"),
        };

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("billing_db")
        .WithUsername("billing_user")
        .WithPassword("billing_test_pwd")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:BillingDb", _container.GetConnectionString());
            builder.UseSetting("InventoryApi:BaseUrl", "http://unused.invalid");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInventoryProductClient>();
                services.AddSingleton<IInventoryProductClient>(new FakeInventoryProductClient(KnownProducts));
            });
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
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
    public async Task Create_With_Single_Product_Returns_Created_With_Open_Status_And_Snapshot()
    {
        // Arrange
        var request = new CreateInvoiceRequest([new CreateInvoiceItemRequest(1, 2)]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/invoices", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(created);
        Assert.Equal("Open", created!.Status);
        Assert.True(created.Number > 0);
        Assert.Single(created.Items);
        Assert.Equal("SKU-1", created.Items[0].ProductCode);
        Assert.Equal("Widget A", created.Items[0].ProductDescription);
        Assert.Equal(2, created.Items[0].Quantity);
    }

    [Fact]
    public async Task Create_With_Multiple_Products_Returns_Created_With_All_Items()
    {
        // Arrange
        var request = new CreateInvoiceRequest(
        [
            new CreateInvoiceItemRequest(1, 1),
            new CreateInvoiceItemRequest(2, 5),
            new CreateInvoiceItemRequest(3, 10),
        ]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/invoices", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(created);
        Assert.Equal(3, created!.Items.Count);
        Assert.Contains(created.Items, i => i.ProductCode == "SKU-1" && i.Quantity == 1);
        Assert.Contains(created.Items, i => i.ProductCode == "SKU-2" && i.Quantity == 5);
        Assert.Contains(created.Items, i => i.ProductCode == "SKU-3" && i.Quantity == 10);
    }

    [Fact]
    public async Task Create_Assigns_Sequential_Unique_Numbers_Across_Invoices()
    {
        // Arrange
        var request = new CreateInvoiceRequest([new CreateInvoiceItemRequest(1, 1)]);

        // Act
        using var firstResponse = await _client.PostAsJsonAsync("/api/invoices", request);
        using var secondResponse = await _client.PostAsJsonAsync("/api/invoices", request);

        // Assert
        var first = await firstResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        var second = await secondResponse.Content.ReadFromJsonAsync<InvoiceResponse>();

        Assert.NotEqual(first!.Number, second!.Number);
        Assert.True(second.Number > first.Number);
    }

    [Fact]
    public async Task Create_With_Repeated_Product_In_Same_Order_Is_Allowed()
    {
        // Arrange: same product referenced twice with different quantities.
        // Decision (documented): repeated products in the same order are
        // allowed, since neither requirements.md nor architecture.md forbid
        // it; each line is kept as a separate item, matching what the caller
        // submitted.
        var request = new CreateInvoiceRequest(
        [
            new CreateInvoiceItemRequest(1, 2),
            new CreateInvoiceItemRequest(1, 3),
        ]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/invoices", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(created);
        Assert.Equal(2, created!.Items.Count);
        Assert.All(created.Items, i => Assert.Equal("SKU-1", i.ProductCode));
        Assert.Contains(created.Items, i => i.Quantity == 2);
        Assert.Contains(created.Items, i => i.Quantity == 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_With_Invalid_Quantity_Returns_BadRequest(int quantity)
    {
        // Arrange
        var request = new CreateInvoiceRequest([new CreateInvoiceItemRequest(1, quantity)]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/invoices", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_With_No_Items_Returns_BadRequest()
    {
        // Arrange
        var request = new CreateInvoiceRequest([]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/invoices", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_With_Null_Items_Returns_BadRequest()
    {
        // Arrange
        var request = new CreateInvoiceRequest(null);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/invoices", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_With_NonInteger_Quantity_Returns_BadRequest()
    {
        // Arrange: raw JSON with a fractional quantity, which cannot bind to `int`.
        const string payload = """{"items":[{"productId":1,"quantity":1.5}]}""";
        using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        // Act
        using var response = await _client.PostAsync("/api/invoices", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_With_Unknown_Product_Returns_NotFound()
    {
        // Arrange
        var request = new CreateInvoiceRequest([new CreateInvoiceItemRequest(999, 1)]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/invoices", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_With_Existing_Invoice_Returns_Ok_With_Snapshot()
    {
        // Arrange
        var request = new CreateInvoiceRequest([new CreateInvoiceItemRequest(2, 4)]);
        using var createResponse = await _client.PostAsJsonAsync("/api/invoices", request);
        var created = await createResponse.Content.ReadFromJsonAsync<InvoiceResponse>();

        // Act
        using var response = await _client.GetAsync($"/api/invoices/{created!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal(created.Id, invoice!.Id);
        Assert.Equal(created.Number, invoice.Number);
        Assert.Equal("Open", invoice.Status);
        Assert.Single(invoice.Items);
        Assert.Equal("SKU-2", invoice.Items[0].ProductCode);
        Assert.Equal("Widget B", invoice.Items[0].ProductDescription);
    }

    [Fact]
    public async Task GetById_With_Unknown_Id_Returns_NotFound()
    {
        // Act
        using var response = await _client.GetAsync("/api/invoices/999999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Returns_Created_Invoices()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(1, 1)]));
        await _client.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(2, 2)]));

        // Act
        using var response = await _client.GetAsync("/api/invoices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invoices = await response.Content.ReadFromJsonAsync<List<InvoiceResponse>>();
        Assert.NotNull(invoices);
        Assert.Equal(2, invoices!.Count);
    }

    [Fact]
    public async Task Created_Invoice_Is_Physically_Persisted_In_PostgreSql()
    {
        // Arrange / Act
        using var createResponse = await _client.PostAsJsonAsync(
            "/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(1, 6)]));
        var created = await createResponse.Content.ReadFromJsonAsync<InvoiceResponse>();

        // Assert: read directly from the container through an independent
        // DbContext instance, bypassing the application's request scope.
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        await using var independentContext = new BillingDbContext(options);

        var persisted = await independentContext.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == created!.Id);

        Assert.NotNull(persisted);
        Assert.Equal(InvoiceStatus.Open, persisted!.Status);
        Assert.Single(persisted.Items);
        Assert.Equal("SKU-1", persisted.Items.First().ProductCode);
        Assert.Equal(6, persisted.Items.First().Quantity);
    }
}
