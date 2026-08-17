using Microsoft.EntityFrameworkCore;

namespace Billing.Api.Data;

/// <summary>
/// EF Core database context owned by the Billing service.
/// Domain entities (invoices, invoice items) will be added in later tasks;
/// this context is currently an empty skeleton ready for migrations infrastructure.
/// </summary>
public class BillingDbContext : DbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options)
        : base(options)
    {
    }
}
