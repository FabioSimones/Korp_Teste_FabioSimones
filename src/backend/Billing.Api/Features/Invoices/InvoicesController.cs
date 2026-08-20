using Billing.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Billing.Api.Features.Invoices;

/// <summary>
/// Invoice registration and queries. Thin HTTP layer: all business rules
/// (including the call to Inventory.Api) live in <see cref="IInvoiceService"/>.
/// </summary>
[ApiController]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    /// <summary>Registers a new invoice with status Open.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<InvoiceResponse>> Create(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _invoiceService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvoiceValidationException ex)
        {
            return BadRequest(BuildValidationProblem(ex));
        }
        catch (InvoiceProductNotFoundException ex)
        {
            return NotFound(BuildProblem(
                "Produto não encontrado.",
                StatusCodes.Status404NotFound,
                ex.Message,
                "PRODUCT_NOT_FOUND"));
        }
        catch (DuplicateInvoiceNumberException ex)
        {
            return Conflict(BuildProblem(
                "Conflito na numeração da nota fiscal.",
                StatusCodes.Status409Conflict,
                ex.Message,
                "DUPLICATE_INVOICE_NUMBER"));
        }
        catch (InventoryServiceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, BuildProblem(
                "Serviço de estoque indisponível.",
                StatusCodes.Status503ServiceUnavailable,
                ex.Message,
                ErrorCodeFor(ex)));
        }
    }

    /// <summary>Lists all registered invoices.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvoiceResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var invoices = await _invoiceService.GetAllAsync(cancellationToken);
        return Ok(invoices);
    }

    /// <summary>
    /// Lists invoices with server-side pagination, ordered deterministically
    /// by <c>Number</c> descending then <c>Id</c>. Separate from
    /// <see cref="GetAll"/> so the unpaginated endpoint keeps its existing
    /// contract unchanged. Read-only against Billing's own database; never
    /// calls the Inventory service.
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResponse<InvoiceSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<InvoiceSummaryResponse>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string sortBy = "number",
        [FromQuery] string sortDirection = "desc",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await _invoiceService.GetPagedAsync(
                new InvoicesPageQuery(pageNumber, pageSize, sortBy, sortDirection),
                cancellationToken);
            return Ok(page);
        }
        catch (InvalidPaginationException ex)
        {
            return BadRequest(BuildValidationProblem(ex));
        }
        catch (InvalidSortException ex)
        {
            return BadRequest(BuildValidationProblem(ex));
        }
    }

    /// <summary>Retrieves a single invoice by id, including its item snapshot.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _invoiceService.GetByIdAsync(id, cancellationToken);
            return Ok(invoice);
        }
        catch (InvoiceNotFoundException ex)
        {
            return NotFound(BuildProblem(
                "Nota fiscal não encontrada.",
                StatusCodes.Status404NotFound,
                ex.Message,
                "INVOICE_NOT_FOUND"));
        }
    }

    /// <summary>
    /// Prints an open invoice: orchestrates the atomic stock debit with the
    /// Inventory service and, only after it is confirmed, closes the
    /// invoice. If the invoice is already closed, unknown, or the stock
    /// debit fails/is rejected, the invoice is left unchanged and an error
    /// is returned; retrying reuses the same debit operation and therefore
    /// never duplicates the write-off.
    /// </summary>
    [HttpPost("{id:int}/print")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<InvoiceResponse>> Print(int id, CancellationToken cancellationToken)
    {
        try
        {
            var printed = await _invoiceService.PrintAsync(id, cancellationToken);
            return Ok(printed);
        }
        catch (InvoiceNotFoundException ex)
        {
            return NotFound(BuildProblem(
                "Nota fiscal não encontrada.",
                StatusCodes.Status404NotFound,
                ex.Message,
                "INVOICE_NOT_FOUND"));
        }
        catch (InvoiceAlreadyClosedException ex)
        {
            return Conflict(BuildProblem(
                "Nota fiscal já fechada.",
                StatusCodes.Status409Conflict,
                ex.Message,
                "INVOICE_ALREADY_CLOSED"));
        }
        catch (InvoiceProductNotFoundException ex)
        {
            return NotFound(BuildProblem(
                "Produto não encontrado.",
                StatusCodes.Status404NotFound,
                ex.Message,
                "PRODUCT_NOT_FOUND"));
        }
        catch (InsufficientStockBalanceException ex)
        {
            return Conflict(BuildProblem(
                "Saldo de estoque insuficiente.",
                StatusCodes.Status409Conflict,
                ex.Message,
                "INSUFFICIENT_STOCK"));
        }
        catch (InventoryServiceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, BuildProblem(
                "Serviço de estoque indisponível.",
                StatusCodes.Status503ServiceUnavailable,
                ex.Message,
                ErrorCodeFor(ex)));
        }
    }

    private ValidationProblemDetails BuildValidationProblem(InvoiceValidationException ex)
    {
        var problem = new ValidationProblemDetails
        {
            Title = "Dados da nota fiscal inválidos.",
            Status = StatusCodes.Status400BadRequest,
            Detail = ex.Message,
            Instance = HttpContext.Request.Path,
        };
        problem.Errors["invoice"] = ex.Errors.ToArray();
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        problem.Extensions["errorCode"] = "INVALID_INVOICE";
        return problem;
    }

    private ValidationProblemDetails BuildValidationProblem(InvalidPaginationException ex)
    {
        var problem = new ValidationProblemDetails
        {
            Title = "Parâmetros de paginação inválidos.",
            Status = StatusCodes.Status400BadRequest,
            Detail = ex.Message,
            Instance = HttpContext.Request.Path,
        };
        problem.Errors["pagination"] = ex.Errors.ToArray();
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        problem.Extensions["errorCode"] = "INVALID_PAGINATION";
        return problem;
    }

    private ValidationProblemDetails BuildValidationProblem(InvalidSortException ex)
    {
        var problem = new ValidationProblemDetails
        {
            Title = "Parâmetros de ordenação inválidos.",
            Status = StatusCodes.Status400BadRequest,
            Detail = ex.Message,
            Instance = HttpContext.Request.Path,
        };
        problem.Errors["sort"] = ex.Errors.ToArray();
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        problem.Extensions["errorCode"] = "INVALID_SORT";
        return problem;
    }

    private ProblemDetails BuildProblem(string title, int status, string detail, string errorCode)
    {
        var problem = new ProblemDetails
        {
            Title = title,
            Status = status,
            Detail = detail,
            Instance = HttpContext.Request.Path,
        };
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        problem.Extensions["errorCode"] = errorCode;
        return problem;
    }

    /// <summary>
    /// Maps <see cref="InventoryServiceUnavailableException.Reason"/> to the
    /// public <c>errorCode</c>: <c>INVENTORY_TIMEOUT</c> when the resilience
    /// pipeline's timeout tripped, <c>INVENTORY_UNAVAILABLE</c> for every
    /// other unreachable/unexpected-response case. Both still map to HTTP 503.
    /// </summary>
    private static string ErrorCodeFor(InventoryServiceUnavailableException ex) =>
        ex.Reason == InventoryUnavailableReason.Timeout ? "INVENTORY_TIMEOUT" : "INVENTORY_UNAVAILABLE";
}
