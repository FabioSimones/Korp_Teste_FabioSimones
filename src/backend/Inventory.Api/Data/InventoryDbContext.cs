using Inventory.Api.Features.Products;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Data;

/// <summary>
/// EF Core database context owned by the Inventory service.
/// Not shared with any other microservice.
/// </summary>
public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Code).IsRequired().HasMaxLength(64);
            entity.Property(p => p.Description).IsRequired().HasMaxLength(256);
            entity.Property(p => p.Balance).IsRequired();

            // Enforces code uniqueness at the database level as well as in
            // the domain/service layer.
            entity.HasIndex(p => p.Code).IsUnique();
        });
    }
}
