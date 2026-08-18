using Inventory.Api.Features.Products;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Features.Stock;

/// <summary>
/// Stock debit operations. Thin HTTP layer: all business rules live in
/// <see cref="IStockDebitService"/>.
/// </summary>
[ApiController]
[Route("api/stock")]
public class StockController : ControllerBase
{
    private readonly IStockDebitService _stockDebitService;

    public StockController(IStockDebitService stockDebitService)
    {
        _stockDebitService = stockDebitService;
    }

    /// <summary>
    /// Atomically debits the balance of one or more products. Idempotent by
    /// <c>OperationId</c>: repeating a request with the same
    /// <c>OperationId</c> returns the result of the original debit without
    /// applying it a second time.
    /// </summary>
    [HttpPost("debits")]
    [ProducesResponseType(typeof(StockDebitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockDebitResponse>> Debit(
        [FromBody] StockDebitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _stockDebitService.DebitAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (StockDebitValidationException ex)
        {
            return BadRequest(BuildValidationProblem(ex));
        }
        catch (ProductNotFoundException ex)
        {
            return NotFound(BuildProblem(
                "Product not found.",
                StatusCodes.Status404NotFound,
                ex.Message));
        }
        catch (InsufficientProductBalanceException ex)
        {
            return Conflict(BuildProblem(
                "Insufficient stock balance.",
                StatusCodes.Status409Conflict,
                ex.Message));
        }
    }

    private ValidationProblemDetails BuildValidationProblem(StockDebitValidationException ex)
    {
        var problem = new ValidationProblemDetails
        {
            Title = "Invalid stock debit request.",
            Status = StatusCodes.Status400BadRequest,
            Detail = ex.Message,
            Instance = HttpContext.Request.Path,
        };
        problem.Errors["debit"] = ex.Errors.ToArray();
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
