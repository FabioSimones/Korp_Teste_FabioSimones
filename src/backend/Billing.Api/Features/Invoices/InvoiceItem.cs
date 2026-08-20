namespace Billing.Api.Features.Invoices;

/// <summary>
/// Line item of an invoice. Stores a snapshot of the product code and
/// description captured from the Inventory service at creation time (per
/// docs/architecture.md: "Faturamento guarda snapshot de código e descrição
/// nos itens da nota"), so Billing never depends on Inventory to render an
/// already-created invoice. <see cref="ProductId"/> is kept only as a
/// reference to the product in the Inventory service; Billing does not own
/// nor join against Inventory's tables.
/// </summary>
public class InvoiceItem
{
    public int Id { get; private set; }

    public int InvoiceId { get; private set; }

    public int ProductId { get; private set; }

    public string ProductCode { get; private set; } = string.Empty;

    public string ProductDescription { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    // Required by EF Core.
    private InvoiceItem()
    {
    }

    private InvoiceItem(int productId, string productCode, string productDescription, int quantity)
    {
        ProductId = productId;
        ProductCode = productCode;
        ProductDescription = productDescription;
        Quantity = quantity;
    }

    /// <summary>
    /// Creates a new invoice item, validating domain invariants. Throws
    /// <see cref="InvoiceValidationException"/> when the quantity is not a
    /// positive integer or the product snapshot is incomplete.
    /// </summary>
    public static InvoiceItem Create(int productId, string productCode, string productDescription, int quantity)
    {
        var errors = new List<string>();

        if (quantity <= 0)
        {
            errors.Add($"A quantidade do produto '{productCode}' deve ser um número inteiro maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(productCode))
        {
            errors.Add($"O código do produto '{productId}' é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(productDescription))
        {
            errors.Add($"A descrição do produto '{productId}' é obrigatória.");
        }

        if (errors.Count > 0)
        {
            throw new InvoiceValidationException(errors);
        }

        return new InvoiceItem(productId, productCode, productDescription, quantity);
    }
}
