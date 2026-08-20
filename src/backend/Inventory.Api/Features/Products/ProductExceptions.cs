namespace Inventory.Api.Features.Products;

/// <summary>
/// Thrown when the submitted product data violates a domain invariant
/// (missing code/description or negative balance). Maps to HTTP 400.
/// </summary>
public class ProductValidationException : Exception
{
    public IReadOnlyCollection<string> Errors { get; }

    public ProductValidationException(IEnumerable<string> errors)
        : base("Dados do produto inválidos.")
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
        : base("Já existe um produto cadastrado com este código.")
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
        : base("Produto não encontrado.")
    {
        Id = id;
    }
}

/// <summary>
/// Thrown when the pagination parameters for a paged listing are invalid
/// (page number less than 1, or page size outside the allowed range).
/// Maps to HTTP 400.
/// </summary>
public class InvalidPaginationException : Exception
{
    public IReadOnlyCollection<string> Errors { get; }

    public InvalidPaginationException(IEnumerable<string> errors)
        : base("Parâmetros de paginação inválidos.")
    {
        Errors = errors.ToList();
    }
}

/// <summary>
/// Thrown when the sort parameters for a paged listing are invalid: an
/// unsupported <c>sortBy</c> field, or a <c>sortDirection</c> other than
/// <c>asc</c>/<c>desc</c>. Maps to HTTP 400.
/// </summary>
public class InvalidSortException : Exception
{
    public IReadOnlyCollection<string> Errors { get; }

    public InvalidSortException(IEnumerable<string> errors)
        : base("Parâmetros de ordenação inválidos.")
    {
        Errors = errors.ToList();
    }
}

/// <summary>
/// Thrown when a stock debit would take a product's balance below zero.
/// Enforces the "balance can never be negative" invariant at the domain
/// level (in addition to the database check constraint). Maps to HTTP 409.
/// </summary>
public class InsufficientProductBalanceException : Exception
{
    public int ProductId { get; }

    public string Code { get; }

    public int AvailableBalance { get; }

    public int RequestedQuantity { get; }

    public InsufficientProductBalanceException(int productId, string code, int availableBalance, int requestedQuantity)
        : base($"O produto \"{code}\" não possui saldo suficiente. Disponível: {availableBalance}; solicitado: {requestedQuantity}.")
    {
        ProductId = productId;
        Code = code;
        AvailableBalance = availableBalance;
        RequestedQuantity = requestedQuantity;
    }
}
