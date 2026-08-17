using Inventory.Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Inventory.Tests;

/// <summary>
/// Integration tests that validate the Inventory service can connect to its own
/// PostgreSQL database (container isolated from any other service database).
/// </summary>
public class InventoryDbContextConnectivityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("inventory_db")
        .WithUsername("inventory_user")
        .WithPassword("inventory_test_pwd")
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
    public async Task DbContext_Has_Product_Entity_Mapped()
    {
        // Arrange
        await using var context = CreateContext(_container.GetConnectionString());

        // Act
        var entityTypes = context.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();

        // Assert
        Assert.Contains("Product", entityTypes);
    }

    [Fact]
    public async Task CanConnect_Returns_False_When_Database_Is_Unreachable()
    {
        // Arrange: point to a port with no listening PostgreSQL instance.
        const string unreachableConnectionString =
            "Host=127.0.0.1;Port=1;Database=inventory_db;Username=inventory_user;Password=invalid;Timeout=2";
        await using var context = CreateContext(unreachableConnectionString);

        // Act
        var canConnect = await context.Database.CanConnectAsync();

        // Assert
        Assert.False(canConnect);
    }

    private static InventoryDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new InventoryDbContext(options);
    }
}
