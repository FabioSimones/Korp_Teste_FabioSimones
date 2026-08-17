using Billing.Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Billing.Tests;

/// <summary>
/// Integration tests that validate the Billing service can connect to its own
/// PostgreSQL database (container isolated from any other service database).
/// </summary>
public class BillingDbContextConnectivityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("billing_db")
        .WithUsername("billing_user")
        .WithPassword("billing_test_pwd")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task CanConnect_To_Own_PostgreSql_Database()
    {
        // Arrange
        await using var context = CreateContext(_container.GetConnectionString());

        // Act
        var canConnect = await context.Database.CanConnectAsync();

        // Assert
        Assert.True(canConnect);
    }

    [Fact]
    public async Task DbContext_Maps_Invoice_Domain_Entities()
    {
        // Arrange
        await using var context = CreateContext(_container.GetConnectionString());

        // Act
        var entityTypeNames = context.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();

        // Assert: since Task 06, the context maps the invoice domain (no product/stock entities).
        Assert.Contains("Invoice", entityTypeNames);
        Assert.Contains("InvoiceItem", entityTypeNames);
    }

    [Fact]
    public async Task CanConnect_Returns_False_When_Database_Is_Unreachable()
    {
        // Arrange: point to a port with no listening PostgreSQL instance.
        const string unreachableConnectionString =
            "Host=127.0.0.1;Port=1;Database=billing_db;Username=billing_user;Password=invalid;Timeout=2";
        await using var context = CreateContext(unreachableConnectionString);

        // Act
        var canConnect = await context.Database.CanConnectAsync();

        // Assert
        Assert.False(canConnect);
    }

    private static BillingDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BillingDbContext(options);
    }
}
