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
/// Verifies the mapping to HTTP 503 when the Inventory service is
/// unreachable/failing while creating an invoice (dependency indisponível,
/// per docs/architecture.md's error table), and that the invoice is not
/// persisted in that case.
/// </summary>
public class InvoicesInventoryUnavailableApiTests : IAsyncLifetime
{
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
                services.AddSingleton<IInventoryProductClient>(
                    new FakeInventoryProductClient(new Dictionary<int, InventoryProductLookupResult>(), throwUnavailable: true));
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
    public async Task Create_When_Inventory_Is_Unavailable_Returns_ServiceUnavailable()
    {
        // Arrange
        var request = new CreateInvoiceRequest([new CreateInvoiceItemRequest(1, 1)]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/invoices", request);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(problem.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
        Assert.True(problem.TryGetProperty("errorCode", out var errorCode));
        Assert.Equal("INVENTORY_UNAVAILABLE", errorCode.GetString());
        Assert.True(problem.TryGetProperty("detail", out var detail));
        Assert.False(detail.GetString()!.Contains("Exception", StringComparison.OrdinalIgnoreCase));
        Assert.False(problem.ToString().Contains("StackTrace", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_When_Inventory_Is_Unavailable_Does_Not_Persist_Invoice()
    {
        // Arrange
        var request = new CreateInvoiceRequest([new CreateInvoiceItemRequest(1, 1)]);

        // Act
        using var response = await _client.PostAsJsonAsync("/api/invoices", request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        // Assert: nothing was written to the database.
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        await using var independentContext = new BillingDbContext(options);

        var count = await independentContext.Invoices.CountAsync();
        Assert.Equal(0, count);
    }
}
