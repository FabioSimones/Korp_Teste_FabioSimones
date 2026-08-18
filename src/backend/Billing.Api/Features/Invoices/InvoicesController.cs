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
                "Product not found.",
                StatusCodes.Status404NotFound,
                ex.Message));
        }
        catch (DuplicateInvoiceNumberException ex)
        {
            return Conflict(BuildProblem(
                "Invoice number conflict.",
                StatusCodes.Status409Conflict,
                ex.Message));
        }
        catch (InventoryServiceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, BuildProblem(
                "Inventory service unavailable.",
                StatusCodes.Status503ServiceUnavailable,
                ex.Message));
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
                "Invoice not found.",
                StatusCodes.Status404NotFound,
                ex.Message));
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
                "Invoice not found.",
                StatusCodes.Status404NotFound,
                ex.Message));
        }
        catch (InvoiceAlreadyClosedException ex)
        {
            return Conflict(BuildProblem(
                "Invoice already closed.",
                StatusCodes.Status409Conflict,
                ex.Message));
        }
        catch (InvoiceProductNotFoundException ex)
        {
            return NotFound(BuildProblem(
                "Product not found.",
                StatusCodes.Status404NotFound,
                ex.Message));
        }
        catch (InsufficientStockBalanceException ex)
        {
            return Conflict(BuildProblem(
                "Insufficient stock balance.",
                StatusCodes.Status409Conflict,
                ex.Message));
        }
        catch (InventoryServiceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, BuildProblem(
                "Inventory service unavailable.",
                StatusCodes.Status503ServiceUnavailable,
                ex.Message));
        }
    }

    private ValidationProblemDetails BuildValidationProblem(InvoiceValidationException ex)
    {
        var problem = new ValidationProblemDetails
        {
            Title = "Invalid invoice data.",
            Status = StatusCodes.Status400BadRequest,
            Detail = ex.Message,
            Instance = HttpContext.Request.Path,
        };
        problem.Errors["invoice"] = ex.Errors.ToArray();
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return problem;
    }

    private ProblemDetails BuildProblem(string title, int status, string detail)
    {
        var problem = new ProblemDetails
        {
            Title = title,
            Status = status,
            Detail = detail,
            Instance = HttpContext.Request.Path,
        };
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return problem;
    }
}
