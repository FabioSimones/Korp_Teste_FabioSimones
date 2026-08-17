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
}
