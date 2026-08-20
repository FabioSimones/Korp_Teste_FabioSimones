namespace Inventory.Api.Features.Products;

/// <summary>
/// Product owned by the Inventory service. Enforces its own invariants
/// (required code/description, positive initial balance on creation,
/// non-negative current balance thereafter) independently of the HTTP
/// layer or the database constraints.
/// </summary>
public class Product
{
    public int Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int Balance { get; private set; }

    // Required by EF Core.
    private Product()
    {
    }

    private Product(string code, string description, int balance)
    {
        Code = code;
        Description = description;
        Balance = balance;
    }

    /// <summary>
    /// Creates a new product, validating domain invariants. Throws
    /// <see cref="ProductValidationException"/> when the input is invalid.
    /// </summary>
    public static Product Create(string? code, string? description, int balance)
    {
        var normalizedCode = code?.Trim();
        var normalizedDescription = description?.Trim();

        var errors = new List<string>();

        if (string.IsNullOrEmpty(normalizedCode))
        {
            errors.Add("O código é obrigatório.");
        }

        if (string.IsNullOrEmpty(normalizedDescription))
        {
            errors.Add("A descrição é obrigatória.");
        }

        if (balance < 1)
        {
            errors.Add("O saldo inicial deve ser um número inteiro maior que zero.");
        }

        if (errors.Count > 0)
        {
            throw new ProductValidationException(errors);
        }

        return new Product(normalizedCode!, normalizedDescription!, balance);
    }

    /// <summary>
    /// Debits the given quantity from the product's balance, enforcing the
    /// domain invariant that balance can never go negative. Throws
    /// <see cref="InsufficientProductBalanceException"/> when the current
    /// balance is not enough to cover the requested quantity.
    /// </summary>
    public void Debit(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (Balance < quantity)
        {
            throw new InsufficientProductBalanceException(Id, Code, Balance, quantity);
        }

        Balance -= quantity;
    }
}
