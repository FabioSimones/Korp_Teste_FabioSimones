using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
/// Task 11: verifies that retry exhaustion and per-attempt timeout in the
/// resilience pipeline both converge on the same public contract as any
/// other Inventory failure &#8212; HTTP 503 with a <c>ProblemDetails</c> body
/// including <c>traceId</c>, and the invoice left <c>Open</c> &#8212; through the
/// full <c>POST /api/invoices/{id}/print</c> HTTP endpoint (not just the
/// client in isolation, unlike <see cref="InventoryResilienceClientTests"/>).
/// The <see cref="IInventoryStockClient"/> singleton injected into the host
/// is still the production <see cref="InventoryStockClient"/> wired to the
/// real <see cref="InventoryResiliencePipeline"/>, only backed by a
/// <see cref="ScriptedHttpMessageHandler"/> instead of a socket.
/// </summary>
public class InvoicesPrintResilienceApiTests : IAsyncLifetime
{
    private static readonly IReadOnlyDictionary<int, InventoryProductLookupResult> KnownProducts =
        new Dictionary<int, InventoryProductLookupResult>
        {
            [1] = new InventoryProductLookupResult(1, "SKU-1", "Widget A"),
        };

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("billing_db")
        .WithUsername("billing_user")
        .WithPassword("billing_test_pwd")
        .Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private async Task<(WebApplicationFactory<Program> Factory, HttpClient Client, ScriptedHttpMessageHandler Handler, ServiceProvider ClientProvider)> CreateFactoryAsync(
        InventoryResilienceOptions resilienceOptions,
        Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        var handler = new ScriptedHttpMessageHandler(responder);
        var (stockClient, clientProvider) = ResilientInventoryClientFactory.CreateStockClient(resilienceOptions, handler);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:BillingDb", _container.GetConnectionString());
            builder.UseSetting("InventoryApi:BaseUrl", "http://unused.invalid");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInventoryProductClient>();
                services.AddSingleton<IInventoryProductClient>(new FakeInventoryProductClient(KnownProducts));

                services.RemoveAll<IInventoryStockClient>();
                services.AddSingleton(stockClient);
            });
        });

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        return (factory, factory.CreateClient(), handler, clientProvider);
    }

    private static async Task<InvoiceResponse> CreateInvoiceAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(1, 1)]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
    }

    [Fact]
    public async Task Print_When_Retries_Are_Exhausted_Returns_ServiceUnavailable_With_TraceId_And_Keeps_Invoice_Open()
    {
        // Arrange: every attempt against Inventory fails transiently.
        var options = ResilientInventoryClientFactory.FastTestOptions(retryMaxAttempts: 2);
        var (factory, client, handler, clientProvider) = await CreateFactoryAsync(
            options,
            (_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var _ = factory;
        await using var ___ = clientProvider;
        using var __ = client;

        var invoice = await CreateInvoiceAsync(client);

        // Act
        using var printResponse = await client.PostAsync($"/api/invoices/{invoice.Id}/print", null);

        // Assert: public contract is 503 + ProblemDetails + traceId + errorCode.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, printResponse.StatusCode);
        var problem = await printResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
        Assert.True(problem.TryGetProperty("errorCode", out var errorCode));
        Assert.Equal("INVENTORY_UNAVAILABLE", errorCode.GetString());
        Assert.True(problem.TryGetProperty("detail", out var detail));
        Assert.False(detail.GetString()!.Contains("Exception", StringComparison.OrdinalIgnoreCase));

        // Assert: 1 initial attempt + 2 retries = 3 HTTP attempts reached the "network".
        Assert.Equal(3, handler.CallCount);

        // Assert: the invoice stays Open; InvoiceService only closes it after
        // a successful debit.
        using var getResponse = await client.GetAsync($"/api/invoices/{invoice.Id}");
        var reloaded = await getResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal("Open", reloaded!.Status);
        Assert.Null(reloaded.ClosedAtUtc);
    }

    [Fact]
    public async Task Print_When_Every_Attempt_Times_Out_Returns_ServiceUnavailable_And_Keeps_Invoice_Open()
    {
        // Arrange: every attempt against Inventory hangs past the per-attempt timeout.
        var options = ResilientInventoryClientFactory.FastTestOptions(
            retryMaxAttempts: 2,
            attemptTimeoutSeconds: 0.3,
            totalTimeoutSeconds: 8);

        var (factory, client, handler, clientProvider) = await CreateFactoryAsync(options, async (_, _, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await using var _ = factory;
        await using var ___ = clientProvider;
        using var __ = client;

        var invoice = await CreateInvoiceAsync(client);

        // Act
        using var printResponse = await client.PostAsync($"/api/invoices/{invoice.Id}/print", null);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, printResponse.StatusCode);
        var problem = await printResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
        Assert.True(problem.TryGetProperty("errorCode", out var errorCode));
        Assert.Equal("INVENTORY_TIMEOUT", errorCode.GetString());

        Assert.Equal(3, handler.CallCount);

        using var getResponse = await client.GetAsync($"/api/invoices/{invoice.Id}");
        var reloaded = await getResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal("Open", reloaded!.Status);
        Assert.Null(reloaded.ClosedAtUtc);
    }
}
