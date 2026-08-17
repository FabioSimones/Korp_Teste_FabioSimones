extern alias InventoryApiAssembly;

using System.Net;
using System.Net.Http.Json;
using Billing.Api.Data;
using Billing.Api.Features.Invoices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using InventoryCreateProductRequest = InventoryApiAssembly::Inventory.Api.Features.Products.CreateProductRequest;
using InventoryDbContext = InventoryApiAssembly::Inventory.Api.Data.InventoryDbContext;
using InventoryProductResponse = InventoryApiAssembly::Inventory.Api.Features.Products.ProductResponse;
using InventoryProductsController = InventoryApiAssembly::Inventory.Api.Features.Products.ProductsController;

namespace Billing.Tests;

/// <summary>
/// Genuine end-to-end test that hosts a real, in-process Inventory.Api next
/// to a real, in-process Billing.Api, each backed by its own ephemeral
/// PostgreSQL container (matching the two-databases-per-service
/// architecture). Billing.Api's <see cref="IInventoryProductClient"/> is
/// wired to Inventory.Api's real ASP.NET Core pipeline via
/// <see cref="TestServer"/>'s in-memory transport (no real TCP sockets/ports
/// involved, but the full HTTP + JSON serialization stack of both services is
/// exercised), instead of the test double used by the other test classes in
/// this project.
/// </summary>
public class InvoicesRealInventoryIntegrationTests : IAsyncLifetime
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
                // Re-registers the typed client, routing its primary handler
                // through Inventory.Api's own in-process TestServer instead
                // of a real socket connection.
                services.AddHttpClient<IInventoryProductClient, InventoryProductClient>(client =>
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
    }

    public async Task DisposeAsync()
    {
        _billingClient.Dispose();
        await _billingFactory.DisposeAsync();
        await _inventoryFactory.DisposeAsync();
        await _inventoryDbContainer.DisposeAsync();
        await _billingDbContainer.DisposeAsync();
    }

    [Fact]
    public async Task Create_Invoice_Validates_Product_And_Captures_Snapshot_From_Real_Inventory_Api()
    {
        // Arrange: register a real product in the real, in-process Inventory.Api.
        using var inventoryClient = _inventoryFactory.CreateClient();
        using var productResponse = await inventoryClient.PostAsJsonAsync(
            "/api/products", new InventoryCreateProductRequest("SKU-REAL", "Real Inventory Widget", 50));
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = await productResponse.Content.ReadFromJsonAsync<InventoryProductResponse>();

        var request = new CreateInvoiceRequest([new CreateInvoiceItemRequest(product!.Id, 4)]);

        // Act: create the invoice through Billing.Api, which calls Inventory.Api over real HTTP.
        using var invoiceResponse = await _billingClient.PostAsJsonAsync("/api/invoices", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, invoiceResponse.StatusCode);
        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(invoice);
        Assert.Equal("Open", invoice!.Status);
        Assert.Single(invoice.Items);
        Assert.Equal(product.Id, invoice.Items[0].ProductId);
        Assert.Equal("SKU-REAL", invoice.Items[0].ProductCode);
        Assert.Equal("Real Inventory Widget", invoice.Items[0].ProductDescription);
        Assert.Equal(4, invoice.Items[0].Quantity);
    }

    [Fact]
    public async Task Create_Invoice_With_Product_Unknown_To_Real_Inventory_Api_Returns_NotFound()
    {
        // Arrange
        var request = new CreateInvoiceRequest([new CreateInvoiceItemRequest(123456, 1)]);

        // Act
        using var response = await _billingClient.PostAsJsonAsync("/api/invoices", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
