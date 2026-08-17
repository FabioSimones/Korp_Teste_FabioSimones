using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Features.Products;

/// <summary>
/// Product registration and queries. Thin HTTP layer: all business rules
/// live in <see cref="IProductService"/>.
/// </summary>
[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>Registers a new product.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _productService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ProductValidationException ex)
        {
            return BadRequest(BuildValidationProblem(ex));
        }
        catch (DuplicateProductCodeException ex)
        {
            return Conflict(BuildProblem(
                "Product code already registered.",
                StatusCodes.Status409Conflict,
                ex.Message));
        }
    }

    /// <summary>Lists all registered products.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var products = await _productService.GetAllAsync(cancellationToken);
        return Ok(products);
    }

    /// <summary>Retrieves a single product by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productService.GetByIdAsync(id, cancellationToken);
            return Ok(product);
        }
        catch (ProductNotFoundException ex)
        {
            return NotFound(BuildProblem(
                "Product not found.",
                StatusCodes.Status404NotFound,
                ex.Message));
        }
    }

    private ValidationProblemDetails BuildValidationProblem(ProductValidationException ex)
    {
        var problem = new ValidationProblemDetails
        {
            Title = "Invalid product data.",
            Status = StatusCodes.Status400BadRequest,
            Detail = ex.Message,
            Instance = HttpContext.Request.Path,
        };
        problem.Errors["product"] = ex.Errors.ToArray();
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
