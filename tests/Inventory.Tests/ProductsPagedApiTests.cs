using System.Net;
using System.Net.Http.Json;
using Inventory.Api.Common;
using Inventory.Api.Data;
using Inventory.Api.Features.Products;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Inventory.Tests;

/// <summary>
/// End-to-end tests for the server-side paginated products listing
/// (<c>GET /api/products/paged</c>), exercised against a real, containerized
/// PostgreSQL database, per the project's persistence-testing convention.
/// The unpaginated <c>GET /api/products</c> endpoint is covered separately in
/// <see cref="ProductsApiTests"/> and re-verified here to guarantee it is
/// unaffected by the new endpoint.
/// A fresh container is started for each test instance (xUnit creates one
/// class instance per test), so tests do not interfere with each other.
/// </summary>
public class ProductsPagedApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("inventory_db")
        .WithUsername("inventory_user")
        .WithPassword("inventory_test_pwd")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:InventoryDb", _container.GetConnectionString());
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    private async Task SeedProductsAsync(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            var code = $"PG-{i:D4}";
            using var response = await _client.PostAsJsonAsync(
                "/api/products", new CreateProductRequest(code, $"Item {i}", i));
            response.EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task GetPaged_With_Default_Parameters_Returns_First_Page_With_Five_Items()
    {
        // Arrange
        await SeedProductsAsync(15);

        // Act
        using var response = await _client.GetAsync("/api/products/paged");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(page);
        Assert.Equal(1, page!.PageNumber);
        Assert.Equal(5, page.PageSize);
        Assert.Equal(15, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.False(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
        Assert.Equal(5, page.Items.Count);
    }

    [Fact]
    public async Task GetPaged_With_Specific_PageSize_Returns_Requested_Item_Count()
    {
        // Arrange
        await SeedProductsAsync(12);

        // Act
        using var response = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(page);
        Assert.Equal(5, page!.PageSize);
        Assert.Equal(5, page.Items.Count);
        Assert.Equal(12, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public async Task GetPaged_First_Page_Returns_First_Items_In_Code_Order()
    {
        // Arrange
        await SeedProductsAsync(5);

        // Act
        using var response = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=2");

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(page);
        Assert.Equal(2, page!.Items.Count);
        Assert.Equal("PG-0001", page.Items[0].Code);
        Assert.Equal("PG-0002", page.Items[1].Code);
        Assert.False(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task GetPaged_Intermediate_Page_Returns_Correct_Slice_And_Flags()
    {
        // Arrange
        await SeedProductsAsync(5);

        // Act
        using var response = await _client.GetAsync("/api/products/paged?pageNumber=2&pageSize=2");

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(page);
        Assert.Equal(2, page!.Items.Count);
        Assert.Equal("PG-0003", page.Items[0].Code);
        Assert.Equal("PG-0004", page.Items[1].Code);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task GetPaged_Last_Page_Returns_Remaining_Items_And_No_Next_Page()
    {
        // Arrange
        await SeedProductsAsync(5);

        // Act
        using var response = await _client.GetAsync("/api/products/paged?pageNumber=3&pageSize=2");

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(page);
        Assert.Single(page!.Items);
        Assert.Equal("PG-0005", page.Items[0].Code);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task GetPaged_Page_Beyond_Total_Returns_Ok_With_Empty_Items()
    {
        // Arrange
        await SeedProductsAsync(3);

        // Act
        using var response = await _client.GetAsync("/api/products/paged?pageNumber=99&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(page);
        Assert.Empty(page!.Items);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task GetPaged_With_Empty_Collection_Returns_Zero_Total_And_Zero_TotalPages()
    {
        // Act: no products seeded in this fresh database.
        using var response = await _client.GetAsync("/api/products/paged");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(page);
        Assert.Empty(page!.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task GetPaged_Across_All_Pages_Does_Not_Duplicate_Or_Skip_Items()
    {
        // Arrange
        const int totalCount = 23;
        const int pageSize = 7;
        await SeedProductsAsync(totalCount);

        // Act: walk every page and collect all codes returned.
        var allCodes = new List<string>();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        for (var pageNumber = 1; pageNumber <= totalPages; pageNumber++)
        {
            using var response = await _client.GetAsync($"/api/products/paged?pageNumber={pageNumber}&pageSize={pageSize}");
            var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
            allCodes.AddRange(page!.Items.Select(p => p.Code));
        }

        // Assert: every seeded code appears exactly once across all pages,
        // in ascending order, proving the ordering is deterministic and the
        // pages neither overlap nor leave gaps.
        Assert.Equal(totalCount, allCodes.Count);
        Assert.Equal(allCodes.Distinct().Count(), allCodes.Count);
        var expectedCodes = Enumerable.Range(1, totalCount).Select(i => $"PG-{i:D4}").ToList();
        Assert.Equal(expectedCodes, allCodes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPaged_With_Invalid_PageNumber_Returns_BadRequest(int pageNumber)
    {
        // Act
        using var response = await _client.GetAsync($"/api/products/paged?pageNumber={pageNumber}&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPaged_With_Invalid_PageSize_Returns_BadRequest(int pageSize)
    {
        // Act
        using var response = await _client.GetAsync($"/api/products/paged?pageNumber=1&pageSize={pageSize}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPaged_With_PageSize_Above_Maximum_Returns_BadRequest()
    {
        // Act
        using var response = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=101");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPaged_With_PageSize_At_Maximum_Is_Accepted()
    {
        // Arrange
        await SeedProductsAsync(1);

        // Act
        using var response = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=100");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Unpaginated_Endpoint_Still_Returns_Plain_List_Unaffected_By_Paged_Endpoint()
    {
        // Arrange
        await SeedProductsAsync(3);

        // Act
        using var response = await _client.GetAsync("/api/products");

        // Assert: same contract as before Task's paged endpoint was added --
        // a plain JSON array, not an envelope with pagination metadata.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<ProductResponse>>();
        Assert.NotNull(products);
        Assert.Equal(3, products!.Count);
    }

    [Fact]
    public async Task GetPaged_Without_Sort_Params_Defaults_To_Code_Ascending()
    {
        // Arrange
        await SeedProductsAsync(3);

        // Act
        using var response = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(page);
        Assert.Equal(["PG-0001", "PG-0002", "PG-0003"], page!.Items.Select(p => p.Code));
    }

    [Fact]
    public async Task GetPaged_SortBy_Code_Desc_Returns_Reverse_Order()
    {
        // Arrange
        await SeedProductsAsync(3);

        // Act
        using var response = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=10&sortBy=code&sortDirection=desc");

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(page);
        Assert.Equal(["PG-0003", "PG-0002", "PG-0001"], page!.Items.Select(p => p.Code));
    }

    [Fact]
    public async Task GetPaged_SortBy_Description_Asc_And_Desc_Orders_Correctly()
    {
        // Arrange: descriptions match seeded codes ("Item 1".."Item 3"), whose
        // lexicographic order coincides with code order for this small set.
        await SeedProductsAsync(3);

        // Act
        using var ascResponse = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=10&sortBy=description&sortDirection=asc");
        using var descResponse = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=10&sortBy=description&sortDirection=desc");

        // Assert
        var ascPage = await ascResponse.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        var descPage = await descResponse.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(ascPage);
        Assert.NotNull(descPage);
        Assert.Equal(["Item 1", "Item 2", "Item 3"], ascPage!.Items.Select(p => p.Description));
        Assert.Equal(["Item 3", "Item 2", "Item 1"], descPage!.Items.Select(p => p.Description));
    }

    [Fact]
    public async Task GetPaged_SortBy_Balance_Asc_And_Desc_Orders_Correctly()
    {
        // Arrange: seeded balances equal the 1-based index (Item i has balance i).
        await SeedProductsAsync(3);

        // Act
        using var ascResponse = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=10&sortBy=balance&sortDirection=asc");
        using var descResponse = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=10&sortBy=balance&sortDirection=desc");

        // Assert
        var ascPage = await ascResponse.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        var descPage = await descResponse.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(ascPage);
        Assert.NotNull(descPage);
        Assert.Equal([1, 2, 3], ascPage!.Items.Select(p => p.Balance));
        Assert.Equal([3, 2, 1], descPage!.Items.Select(p => p.Balance));
    }

    [Fact]
    public async Task GetPaged_SortBy_Is_Case_Insensitive()
    {
        // Arrange
        await SeedProductsAsync(3);

        // Act
        using var response = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=10&sortBy=CODE&sortDirection=DESC");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(page);
        Assert.Equal(["PG-0003", "PG-0002", "PG-0001"], page!.Items.Select(p => p.Code));
    }

    [Fact]
    public async Task GetPaged_Sort_With_Duplicate_Balances_Breaks_Ties_Deterministically_By_Id()
    {
        // Arrange: all products share the same balance, so sorting by balance
        // must fall back to a stable tie-break (Code, then Id) instead of an
        // arbitrary/unstable order.
        for (var i = 1; i <= 4; i++)
        {
            using var response = await _client.PostAsJsonAsync(
                "/api/products", new CreateProductRequest($"TIE-{i:D4}", $"Tied {i}", 10));
            response.EnsureSuccessStatusCode();
        }

        // Act: call twice to confirm the ordering is stable across calls.
        using var first = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=10&sortBy=balance&sortDirection=asc");
        using var second = await _client.GetAsync("/api/products/paged?pageNumber=1&pageSize=10&sortBy=balance&sortDirection=asc");

        // Assert
        var firstPage = await first.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        var secondPage = await second.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        var expectedOrder = new[] { "TIE-0001", "TIE-0002", "TIE-0003", "TIE-0004" };
        Assert.Equal(expectedOrder, firstPage!.Items.Select(p => p.Code));
        Assert.Equal(expectedOrder, secondPage!.Items.Select(p => p.Code));
    }

    [Fact]
    public async Task GetPaged_Sort_Is_Applied_Before_Pagination_Second_Page_Matches_Sorted_Order()
    {
        // Arrange
        await SeedProductsAsync(5);

        // Act: page 2 of a descending-by-code listing should hold items 3 and 2.
        using var response = await _client.GetAsync("/api/products/paged?pageNumber=2&pageSize=2&sortBy=code&sortDirection=desc");

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.NotNull(page);
        Assert.Equal(["PG-0003", "PG-0002"], page!.Items.Select(p => p.Code));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("id")]
    [InlineData("name")]
    public async Task GetPaged_With_Invalid_SortBy_Returns_BadRequest_With_InvalidSort_ErrorCode(string sortBy)
    {
        // Act
        using var response = await _client.GetAsync($"/api/products/paged?pageNumber=1&pageSize=10&sortBy={sortBy}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("INVALID_SORT", body);
    }

    [Theory]
    [InlineData("sideways")]
    [InlineData("ascending")]
    public async Task GetPaged_With_Invalid_SortDirection_Returns_BadRequest_With_InvalidSort_ErrorCode(string sortDirection)
    {
        // Act
        using var response = await _client.GetAsync($"/api/products/paged?pageNumber=1&pageSize=10&sortDirection={sortDirection}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("INVALID_SORT", body);
    }
}
