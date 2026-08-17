using System.Net;
using System.Net.Http;
using Inventory.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Inventory.Tests;

/// <summary>
/// Verifies the CORS policy configured for the Angular dev server
/// (http://localhost:4200): allowed origin gets the preflight/response
/// headers it needs, while an unauthorized origin is never reflected.
/// Exercised against a real, containerized PostgreSQL database, following
/// the same pattern as <see cref="ProductsApiTests"/>.
/// </summary>
public class CorsApiTests : IAsyncLifetime
{
    private const string AllowedOrigin = "http://localhost:4200";
    private const string DisallowedOrigin = "http://origem-nao-permitida.example";

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
    public async Task Preflight_From_Allowed_Origin_Is_Permitted()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/products");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        // Act
        using var response = await _client.SendAsync(request);

        // Assert
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Expected 200/204 for preflight, got {(int)response.StatusCode}.");

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var originValues));
        Assert.Equal(AllowedOrigin, Assert.Single(originValues!));

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Methods", out var methodValues));
        Assert.Contains(methodValues!, v => v.Split(',').Select(m => m.Trim()).Contains("POST"));

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Headers", out var headerValues));
        Assert.Contains(
            headerValues!,
            v => v.Split(',').Select(h => h.Trim()).Contains("Content-Type", StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preflight_From_Disallowed_Origin_Is_Not_Reflected()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/products");
        request.Headers.Add("Origin", DisallowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        // Act
        using var response = await _client.SendAsync(request);

        // Assert
        Assert.False(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "An unauthorized origin must never receive an Access-Control-Allow-Origin header.");
    }

    [Fact]
    public async Task Get_From_Allowed_Origin_Returns_Cors_Header_With_Exact_Origin()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/products");
        request.Headers.Add("Origin", AllowedOrigin);

        // Act
        using var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var originValues));
        Assert.Equal(AllowedOrigin, Assert.Single(originValues!));
    }
}
