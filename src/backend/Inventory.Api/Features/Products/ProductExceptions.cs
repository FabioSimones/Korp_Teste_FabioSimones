namespace Inventory.Api.Features.Products;

/// <summary>
/// Thrown when the submitted product data violates a domain invariant
/// (missing code/description or negative balance). Maps to HTTP 400.
/// </summary>
public class ProductValidationException : Exception
{
    public IReadOnlyCollection<string> Errors { get; }

    public ProductValidationException(IEnumerable<string> errors)
        : base("Invalid product data.")
    {
        Errors = errors.ToList();
    }
}

/// <summary>
/// Thrown when a product code is already registered. Maps to HTTP 409.
/// </summary>
public class DuplicateProductCodeException : Exception
{
    public string Code { get; }

    public DuplicateProductCodeException(string code)
        : base($"Product code '{code}' is already registered.")
    {
        Code = code;
    }
}

/// <summary>
/// Thrown when a product cannot be found by its identifier. Maps to HTTP 404.
/// </summary>
public class ProductNotFoundException : Exception
{
    public int Id { get; }

    public ProductNotFoundException(int id)
        : base($"Product '{id}' was not found.")
    {
        Id = id;
    }
}
