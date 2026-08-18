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
/// Tests for <c>POST /api/invoices/{id}/print</c> that isolate the call to
/// Inventory's stock debit endpoint with <see cref="FakeInventoryStockClient"/>
/// (mirroring how <see cref="InvoicesApiTests"/> isolates the product lookup
/// client), so this class focuses on Billing's own orchestration:
/// open/closed/not-found guards, closing on success, and OperationId reuse
/// across print attempts. Scenarios that depend on genuine Inventory balance
/// math (success reduces balance, insufficient balance keeps the invoice
/// open) are covered end-to-end against a real, in-process Inventory.Api by
/// <see cref="InvoicesPrintRealInventoryIntegrationTests"/>, per the
/// project's persistence-testing convention.
/// </summary>
public class InvoicesPrintApiTests : IAsyncLifetime
{
    private static readonly IReadOnlyDictionary<int, InventoryProductLookupResult> KnownProducts =
        new Dictionary<int, InventoryProductLookupResult>
        {
            [1] = new InventoryProductLookupResult(1, "SKU-1", "Widget A"),
            [2] = new InventoryProductLookupResult(2, "SKU-2", "Widget B"),
        };

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("billing_db")
        .WithUsername("billing_user")
        .WithPassword("billing_test_pwd")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private FakeInventoryStockClient _stockClient = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _stockClient = new FakeInventoryStockClient();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:BillingDb", _container.GetConnectionString());
            builder.UseSetting("InventoryApi:BaseUrl", "http://unused.invalid");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInventoryProductClient>();
                services.AddSingleton<IInventoryProductClient>(new FakeInventoryProductClient(KnownProducts));

                services.RemoveAll<IInventoryStockClient>();
                services.AddSingleton<IInventoryStockClient>(_stockClient);
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

    private async Task<InvoiceResponse> CreateInvoiceAsync(params (int ProductId, int Quantity)[] items)
    {
        var request = new CreateInvoiceRequest(
            items.Select(i => new CreateInvoiceItemRequest(i.ProductId, i.Quantity)).ToList());
        using var response = await _client.PostAsJsonAsync("/api/invoices", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
    }

    [Fact]
    public async Task Print_Unknown_Invoice_Returns_NotFound()
    {
        // Act
        using var response = await _client.PostAsync("/api/invoices/999999/print", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Print_Open_Invoice_Closes_It_And_Requests_A_Single_Stock_Debit()
    {
        // Arrange
        var created = await CreateInvoiceAsync((1, 2), (2, 3));

        // Act
        using var response = await _client.PostAsync($"/api/invoices/{created.Id}/print", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var printed = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(printed);
        Assert.Equal("Closed", printed!.Status);
        Assert.NotNull(printed.ClosedAtUtc);

        Assert.Single(_stockClient.Calls);
        var debitRequest = _stockClient.Calls[0];
        Assert.NotEqual(Guid.Empty, debitRequest.OperationId);
        Assert.Equal(2, debitRequest.Items.Count);
        Assert.Contains(debitRequest.Items, i => i.ProductId == 1 && i.Quantity == 2);
        Assert.Contains(debitRequest.Items, i => i.ProductId == 2 && i.Quantity == 3);
    }

    [Fact]
    public async Task Print_Persists_Closed_Status_Queryable_Via_GetById()
    {
        // Arrange
        var created = await CreateInvoiceAsync((1, 1));

        // Act
        await _client.PostAsync($"/api/invoices/{created.Id}/print", null);
        using var getResponse = await _client.GetAsync($"/api/invoices/{created.Id}");

        // Assert
        var invoice = await getResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal("Closed", invoice!.Status);
        Assert.NotNull(invoice.ClosedAtUtc);
    }

    [Fact]
    public async Task Print_Already_Closed_Invoice_Returns_Conflict_And_Does_Not_Debit_Stock_Again()
    {
        // Arrange: first print succeeds and closes the invoice.
        var created = await CreateInvoiceAsync((1, 1));
        using var firstResponse = await _client.PostAsync($"/api/invoices/{created.Id}/print", null);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Single(_stockClient.Calls);

        // Act: retry printing the now-closed invoice.
        using var secondResponse = await _client.PostAsync($"/api/invoices/{created.Id}/print", null);

        // Assert: rejected, and the stock client was not invoked a second
        // time (repetition does not duplicate the debit).
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Single(_stockClient.Calls);
    }

    [Fact]
    public async Task Print_Reuses_Persisted_OperationId_From_A_Previous_Interrupted_Attempt()
    {
        // Arrange: create an invoice and simulate a previous print attempt
        // that reserved an OperationId (and, hypothetically, the debit
        // already succeeded with Inventory) but crashed before the invoice
        // could be closed. This is done by writing OperationId directly to
        // the database, bypassing the domain's private setter, exactly as
        // InvoiceService.PrintAsync itself would have left things.
        var created = await CreateInvoiceAsync((1, 1));
        var preReservedOperationId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE invoices SET \"OperationId\" = {preReservedOperationId} WHERE \"Id\" = {created.Id}");
        }

        // Act: retry the print.
        using var response = await _client.PostAsync($"/api/invoices/{created.Id}/print", null);

        // Assert: the retried print reuses the same OperationId instead of
        // minting a new one, so Inventory's own idempotency guard (keyed by
        // OperationId) would recognize it as the same operation.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(_stockClient.Calls);
        Assert.Equal(preReservedOperationId, _stockClient.Calls[0].OperationId);
    }

    [Fact]
    public async Task Print_When_Inventory_Rejects_Insufficient_Balance_Returns_Conflict_And_Keeps_Invoice_Open()
    {
        // Arrange: rebuild the factory with a stock client that simulates
        // Inventory refusing the debit for insufficient balance.
        var throwingStockClient = new FakeInventoryStockClient(
            new InsufficientStockBalanceException("Product 'SKU-1' has insufficient balance."));

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:BillingDb", _container.GetConnectionString());
            builder.UseSetting("InventoryApi:BaseUrl", "http://unused.invalid");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInventoryProductClient>();
                services.AddSingleton<IInventoryProductClient>(new FakeInventoryProductClient(KnownProducts));

                services.RemoveAll<IInventoryStockClient>();
                services.AddSingleton<IInventoryStockClient>(throwingStockClient);
            });
        });

        using var client = factory.CreateClient();
        using var createResponse = await client.PostAsJsonAsync(
            "/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(1, 1000)]));
        var created = (await createResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        // Act
        using var printResponse = await client.PostAsync($"/api/invoices/{created.Id}/print", null);

        // Assert: rejected, and the invoice remains Open.
        Assert.Equal(HttpStatusCode.Conflict, printResponse.StatusCode);

        using var getResponse = await client.GetAsync($"/api/invoices/{created.Id}");
        var invoice = await getResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal("Open", invoice!.Status);
        Assert.Null(invoice.ClosedAtUtc);
    }

    [Fact]
    public async Task Print_When_Inventory_Is_Unavailable_Returns_ServiceUnavailable_And_Keeps_Invoice_Open()
    {
        // Arrange
        var throwingStockClient = new FakeInventoryStockClient(
            new InventoryServiceUnavailableException("Simulated Inventory outage."));

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:BillingDb", _container.GetConnectionString());
            builder.UseSetting("InventoryApi:BaseUrl", "http://unused.invalid");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInventoryProductClient>();
                services.AddSingleton<IInventoryProductClient>(new FakeInventoryProductClient(KnownProducts));

                services.RemoveAll<IInventoryStockClient>();
                services.AddSingleton<IInventoryStockClient>(throwingStockClient);
            });
        });

        using var client = factory.CreateClient();
        using var createResponse = await client.PostAsJsonAsync(
            "/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(1, 1)]));
        var created = (await createResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        // Act
        using var printResponse = await client.PostAsync($"/api/invoices/{created.Id}/print", null);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, printResponse.StatusCode);

        using var getResponse = await client.GetAsync($"/api/invoices/{created.Id}");
        var invoice = await getResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal("Open", invoice!.Status);
    }
}
