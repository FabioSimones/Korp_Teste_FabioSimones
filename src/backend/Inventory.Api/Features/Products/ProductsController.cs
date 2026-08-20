using Inventory.Api.Common;
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
                "Código de produto já cadastrado.",
                StatusCodes.Status409Conflict,
                ex.Message,
                "DUPLICATE_PRODUCT_CODE"));
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

    /// <summary>
    /// Lists registered products with server-side pagination, ordered
    /// deterministically by <c>Code</c> then <c>Id</c>. Separate from
    /// <see cref="GetAll"/> so the unpaginated endpoint (used by the invoice
    /// creation form) keeps its existing contract unchanged.
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResponse<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string sortBy = "code",
        [FromQuery] string sortDirection = "asc",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await _productService.GetPagedAsync(
                new ProductsPageQuery(pageNumber, pageSize, sortBy, sortDirection),
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
                "Produto não encontrado.",
                StatusCodes.Status404NotFound,
                ex.Message,
                "PRODUCT_NOT_FOUND"));
        }
    }

    private ValidationProblemDetails BuildValidationProblem(ProductValidationException ex)
    {
        var problem = new ValidationProblemDetails
        {
            Title = "Dados do produto inválidos.",
            Status = StatusCodes.Status400BadRequest,
            Detail = ex.Message,
            Instance = HttpContext.Request.Path,
        };
        problem.Errors["product"] = ex.Errors.ToArray();
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        problem.Extensions["errorCode"] = "INVALID_PRODUCT";
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
}
