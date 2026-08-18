using Inventory.Api.Features.Products;

namespace Inventory.Tests;

/// <summary>
/// Fast, container-free unit tests for the <see cref="Product"/> domain
/// invariants (kept independent from the HTTP layer and the database).
/// </summary>
public class ProductDomainTests
{
    [Fact]
    public void Create_With_Valid_Data_Succeeds()
    {
        // Act
        var product = Product.Create(" SKU-001 ", " Widget ", 10);

        // Assert
        Assert.Equal("SKU-001", product.Code);
        Assert.Equal("Widget", product.Description);
        Assert.Equal(10, product.Balance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_With_Missing_Code_Throws_ProductValidationException(string? code)
    {
        // Act & Assert
        var ex = Assert.Throws<ProductValidationException>(() => Product.Create(code, "Widget", 1));
        Assert.Contains("Code is required.", ex.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_With_Missing_Description_Throws_ProductValidationException(string? description)
    {
        // Act & Assert
        var ex = Assert.Throws<ProductValidationException>(() => Product.Create("SKU-001", description, 1));
        Assert.Contains("Description is required.", ex.Errors);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_With_Negative_Balance_Throws_ProductValidationException(int balance)
    {
        // Act & Assert
        var ex = Assert.Throws<ProductValidationException>(() => Product.Create("SKU-001", "Widget", balance));
        Assert.Contains("Balance must be greater than or equal to zero.", ex.Errors);
    }

    [Fact]
    public void Create_With_Zero_Balance_Succeeds()
    {
        // Act
        var product = Product.Create("SKU-001", "Widget", 0);

        // Assert
        Assert.Equal(0, product.Balance);
    }

    [Fact]
    public void Debit_With_Sufficient_Balance_Decreases_Balance()
    {
        // Arrange
        var product = Product.Create("SKU-001", "Widget", 10);

        // Act
        product.Debit(4);

        // Assert
        Assert.Equal(6, product.Balance);
    }

    [Fact]
    public void Debit_With_Balance_Exactly_Equal_To_Quantity_Reaches_Zero()
    {
        // Arrange
        var product = Product.Create("SKU-001", "Widget", 5);

        // Act
        product.Debit(5);

        // Assert
        Assert.Equal(0, product.Balance);
    }

    [Fact]
    public void Debit_With_Insufficient_Balance_Throws_InsufficientProductBalanceException_And_Does_Not_Mutate_Balance()
    {
        // Arrange
        var product = Product.Create("SKU-001", "Widget", 3);

        // Act & Assert
        var ex = Assert.Throws<InsufficientProductBalanceException>(() => product.Debit(4));
        Assert.Equal(3, ex.AvailableBalance);
        Assert.Equal(4, ex.RequestedQuantity);
        Assert.Equal(3, product.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Debit_With_NonPositive_Quantity_Throws_ArgumentOutOfRangeException(int quantity)
    {
        // Arrange
        var product = Product.Create("SKU-001", "Widget", 10);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => product.Debit(quantity));
    }
}
