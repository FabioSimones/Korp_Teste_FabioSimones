using Inventory.Api.Features.Products;
using Inventory.Api.Features.Stock;
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

    public DbSet<StockDebitOperation> StockDebitOperations => Set<StockDebitOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products", t => t.HasCheckConstraint(
                "CK_products_balance_non_negative",
                "\"Balance\" >= 0"));
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Code).IsRequired().HasMaxLength(64);
            entity.Property(p => p.Description).IsRequired().HasMaxLength(256);
            entity.Property(p => p.Balance).IsRequired();

            // Enforces code uniqueness at the database level as well as in
            // the domain/service layer.
            entity.HasIndex(p => p.Code).IsUnique();
        });

        modelBuilder.Entity<StockDebitOperation>(entity =>
        {
            entity.ToTable("stock_debit_operations");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.OperationId).IsRequired();
            entity.Property(o => o.CreatedAtUtc).IsRequired();

            // Enforces OperationId uniqueness at the database level, the
            // backbone of the idempotency guarantee.
            entity.HasIndex(o => o.OperationId).IsUnique();

            entity.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey(i => i.StockDebitOperationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StockDebitOperationItem>(entity =>
        {
            entity.ToTable("stock_debit_operation_items", t => t.HasCheckConstraint(
                "CK_stock_debit_operation_items_quantity_positive",
                "\"Quantity\" > 0"));
            entity.HasKey(i => i.Id);
            entity.Property(i => i.ProductId).IsRequired();
            entity.Property(i => i.ProductCode).IsRequired().HasMaxLength(64);
            entity.Property(i => i.Quantity).IsRequired();
            entity.Property(i => i.BalanceAfter).IsRequired();
        });
    }
}
