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

    [Fact]
    public void PrepareForPrint_On_Open_Invoice_Assigns_OperationId()
    {
        // Arrange
        var invoice = Invoice.Create([InvoiceItem.Create(1, "SKU-1", "Widget", 2)]);
        Assert.Null(invoice.OperationId);

        // Act
        var operationId = invoice.PrepareForPrint();

        // Assert
        Assert.NotEqual(Guid.Empty, operationId);
        Assert.Equal(operationId, invoice.OperationId);
    }

    [Fact]
    public void PrepareForPrint_Called_Twice_Reuses_Same_OperationId()
    {
        // Arrange: simulates a retried print request for the same invoice.
        var invoice = Invoice.Create([InvoiceItem.Create(1, "SKU-1", "Widget", 2)]);

        // Act
        var first = invoice.PrepareForPrint();
        var second = invoice.PrepareForPrint();

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void PrepareForPrint_On_Closed_Invoice_Throws_InvoiceAlreadyClosedException()
    {
        // Arrange
        var invoice = Invoice.Create([InvoiceItem.Create(1, "SKU-1", "Widget", 2)]);
        invoice.PrepareForPrint();
        invoice.Close(DateTime.UtcNow);

        // Act & Assert
        Assert.Throws<InvoiceAlreadyClosedException>(() => invoice.PrepareForPrint());
    }

    [Fact]
    public void Close_Sets_Status_And_ClosedAtUtc()
    {
        // Arrange
        var invoice = Invoice.Create([InvoiceItem.Create(1, "SKU-1", "Widget", 2)]);
        var closedAt = DateTime.UtcNow;

        // Act
        invoice.Close(closedAt);

        // Assert
        Assert.Equal(InvoiceStatus.Closed, invoice.Status);
        Assert.Equal(closedAt, invoice.ClosedAtUtc);
    }

    [Fact]
    public void Close_On_Already_Closed_Invoice_Throws_InvoiceAlreadyClosedException()
    {
        // Arrange
        var invoice = Invoice.Create([InvoiceItem.Create(1, "SKU-1", "Widget", 2)]);
        invoice.Close(DateTime.UtcNow);

        // Act & Assert
        Assert.Throws<InvoiceAlreadyClosedException>(() => invoice.Close(DateTime.UtcNow));
    }
}
