using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Data;

/// <summary>
/// EF Core database context owned by the Inventory service.
/// Domain entities (products, balances) will be added in later tasks;
/// this context is currently an empty skeleton ready for migrations infrastructure.
/// </summary>
public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }
}
