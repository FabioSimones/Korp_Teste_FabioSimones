using Billing.Api.Features.Invoices;

namespace Billing.Tests;

/// <summary>
/// Domain-only tests for <see cref="Invoice"/>/<see cref="InvoiceItem"/>
/// invariants, without a database dependency.
/// </summary>
public class InvoiceDomainTests
{
    [Fact]
    public void Create_With_No_Items_Throws_InvoiceValidationException()
    {
        // Act & Assert
        var ex = Assert.Throws<InvoiceValidationException>(() => Invoice.Create([]));
        Assert.Contains(ex.Errors, e => e.Contains("at least one item", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Create_With_Valid_Items_Sets_Open_Status_And_Timestamp()
    {
        // Arrange
        var items = new[] { InvoiceItem.Create(1, "SKU-1", "Widget", 3) };

        // Act
        var invoice = Invoice.Create(items);

        // Assert
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.Single(invoice.Items);
        Assert.True(invoice.CreatedAtUtc <= DateTime.UtcNow);
        Assert.True(invoice.CreatedAtUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvoiceItem_Create_With_NonPositive_Quantity_Throws(int quantity)
    {
        // Act & Assert
        Assert.Throws<InvoiceValidationException>(() => InvoiceItem.Create(1, "SKU-1", "Widget", quantity));
    }

    [Fact]
    public void InvoiceItem_Create_With_Positive_Quantity_Succeeds()
    {
        // Act
        var item = InvoiceItem.Create(7, "SKU-7", "Gadget", 5);

        // Assert
        Assert.Equal(7, item.ProductId);
        Assert.Equal("SKU-7", item.ProductCode);
        Assert.Equal("Gadget", item.ProductDescription);
        Assert.Equal(5, item.Quantity);
    }
}
