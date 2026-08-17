using Billing.Api.Features.Invoices;
using Microsoft.EntityFrameworkCore;

namespace Billing.Api.Data;

/// <summary>
/// EF Core database context owned by the Billing service.
/// Not shared with any other microservice.
/// </summary>
public class BillingDbContext : DbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Backs Invoice.Number: guarantees unique, sequential numbers even
        // under concurrent inserts, without relying on application-level
        // max()+1 logic (which would race). Combined with the unique index
        // below as a database-level invariant.
        modelBuilder.HasSequence<int>("invoice_number_seq").StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("invoices");
            entity.HasKey(i => i.Id);

            entity.Property(i => i.Number)
                .IsRequired()
                .HasDefaultValueSql("nextval('invoice_number_seq')");
            entity.HasIndex(i => i.Number).IsUnique();

            entity.Property(i => i.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(16);

            entity.Property(i => i.CreatedAtUtc).IsRequired();

            // Items is exposed as a read-only collection backed by the
            // private field "_items"; EF Core's default backing-field
            // convention (property "Items" -> field "_items") resolves it
            // automatically, so no public setter/ICollection-typed property
            // is required to satisfy the ORM.
            entity.HasMany(i => i.Items)
                .WithOne()
                .HasForeignKey(it => it.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.ToTable("invoice_items");
            entity.HasKey(it => it.Id);

            entity.Property(it => it.ProductId).IsRequired();
            entity.Property(it => it.ProductCode).IsRequired().HasMaxLength(64);
            entity.Property(it => it.ProductDescription).IsRequired().HasMaxLength(256);
            entity.Property(it => it.Quantity).IsRequired();
        });
    }
}
