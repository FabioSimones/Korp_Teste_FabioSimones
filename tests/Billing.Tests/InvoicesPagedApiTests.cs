using System.Net;
using System.Net.Http.Json;
using Billing.Api.Common;
using Billing.Api.Data;
using Billing.Api.Features.Invoices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Billing.Tests;

/// <summary>
/// End-to-end tests for the server-side paginated invoices listing
/// (<c>GET /api/invoices/paged</c>), exercised against a real, containerized
/// PostgreSQL database. The call to Inventory.Api is isolated with
/// <see cref="CountingInventoryProductClient"/> so tests can additionally
/// assert the paginated listing never calls out to Inventory. The
/// unpaginated <c>GET /api/invoices</c> endpoint is covered separately in
/// <see cref="InvoicesApiTests"/> and re-verified here to guarantee it is
/// unaffected by the new endpoint.
/// A fresh container/factory is started for each test (xUnit creates one
/// class instance per test), so tests do not interfere with each other.
/// </summary>
public class InvoicesPagedApiTests : IAsyncLifetime
{
    private static readonly IReadOnlyDictionary<int, InventoryProductLookupResult> KnownProducts =
        new Dictionary<int, InventoryProductLookupResult>
        {
            [1] = new InventoryProductLookupResult(1, "SKU-1", "Widget A"),
        };

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("billing_db")
        .WithUsername("billing_user")
        .WithPassword("billing_test_pwd")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private CountingInventoryProductClient _productClient = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _productClient = new CountingInventoryProductClient(KnownProducts);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:BillingDb", _container.GetConnectionString());
            builder.UseSetting("InventoryApi:BaseUrl", "http://unused.invalid");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInventoryProductClient>();
                services.AddSingleton<IInventoryProductClient>(_productClient);
            });
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
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

    private async Task<InvoiceResponse> CreateInvoiceAsync(int quantity = 1)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/invoices", new CreateInvoiceRequest([new CreateInvoiceItemRequest(1, quantity)]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
    }

    private async Task SeedInvoicesAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await CreateInvoiceAsync();
        }
    }

    private async Task<InvoiceResponse> CreateInvoiceWithItemCountAsync(int itemCount)
    {
        var items = Enumerable.Range(0, itemCount).Select(_ => new CreateInvoiceItemRequest(1, 1)).ToList();
        using var response = await _client.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(items));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
    }

    [Fact]
    public async Task GetPaged_With_Default_Parameters_Returns_First_Page_With_Five_Items()
    {
        // Arrange
        await SeedInvoicesAsync(15);
        _productClient.CallCount = default; // reset counter after seeding, which legitimately calls Inventory

        // Act
        using var response = await _client.GetAsync("/api/invoices/paged");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(page);
        Assert.Equal(1, page!.PageNumber);
        Assert.Equal(5, page.PageSize);
        Assert.Equal(15, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(5, page.Items.Count);
    }

    [Fact]
    public async Task GetPaged_With_Specific_PageSize_Returns_Requested_Item_Count()
    {
        // Arrange
        await SeedInvoicesAsync(9);

        // Act
        using var response = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=4");

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(page);
        Assert.Equal(4, page!.PageSize);
        Assert.Equal(4, page.Items.Count);
        Assert.Equal(9, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public async Task GetPaged_Returns_Correct_Metadata_Fields()
    {
        // Arrange
        await SeedInvoicesAsync(3);

        // Act
        using var response = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=2");

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(page);
        Assert.Equal(1, page!.PageNumber);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.False(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
        Assert.All(page.Items, i => Assert.Equal(1, i.ItemsCount));
        Assert.All(page.Items, i => Assert.Equal("Open", i.Status));
    }

    [Fact]
    public async Task GetPaged_Orders_By_Number_Descending()
    {
        // Arrange
        var first = await CreateInvoiceAsync();
        var second = await CreateInvoiceAsync();
        var third = await CreateInvoiceAsync();

        // Act
        using var response = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=10");

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(page);
        Assert.Equal(3, page!.Items.Count);
        Assert.Equal(third.Number, page.Items[0].Number);
        Assert.Equal(second.Number, page.Items[1].Number);
        Assert.Equal(first.Number, page.Items[2].Number);
        Assert.True(page.Items[0].Number > page.Items[1].Number);
        Assert.True(page.Items[1].Number > page.Items[2].Number);
    }

    [Fact]
    public async Task GetPaged_Page_Beyond_Total_Returns_Ok_With_Empty_Items()
    {
        // Arrange
        await SeedInvoicesAsync(2);

        // Act
        using var response = await _client.GetAsync("/api/invoices/paged?pageNumber=99&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(page);
        Assert.Empty(page!.Items);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public async Task GetPaged_With_Empty_Collection_Returns_Zero_Total_And_Zero_TotalPages()
    {
        // Act: no invoices created in this fresh database.
        using var response = await _client.GetAsync("/api/invoices/paged");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(page);
        Assert.Empty(page!.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public async Task GetPaged_With_Invalid_Parameters_Returns_BadRequest(int pageNumber, int pageSize)
    {
        // Act
        using var response = await _client.GetAsync($"/api/invoices/paged?pageNumber={pageNumber}&pageSize={pageSize}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Unpaginated_Endpoint_Still_Returns_Plain_List_Unaffected_By_Paged_Endpoint()
    {
        // Arrange
        await SeedInvoicesAsync(2);

        // Act
        using var response = await _client.GetAsync("/api/invoices");

        // Assert: same contract as before -- a plain JSON array with full
        // item snapshots, not the paginated envelope.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invoices = await response.Content.ReadFromJsonAsync<List<InvoiceResponse>>();
        Assert.NotNull(invoices);
        Assert.Equal(2, invoices!.Count);
        Assert.All(invoices, i => Assert.Single(i.Items));
    }

    [Fact]
    public async Task GetPaged_Does_Not_Change_Invoice_Status_Or_Data()
    {
        // Arrange
        var created = await CreateInvoiceAsync();

        // Act: call the paginated listing multiple times.
        using var response1 = await _client.GetAsync("/api/invoices/paged");
        using var response2 = await _client.GetAsync("/api/invoices/paged");

        // Assert: the invoice, re-fetched by id, is unchanged (still Open,
        // same number, no ClosedAtUtc) -- the read-only listing never
        // mutates state.
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        using var getResponse = await _client.GetAsync($"/api/invoices/{created.Id}");
        var invoice = await getResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(invoice);
        Assert.Equal("Open", invoice!.Status);
        Assert.Null(invoice.ClosedAtUtc);
        Assert.Equal(created.Number, invoice.Number);
    }

    [Fact]
    public async Task GetPaged_Does_Not_Call_Inventory_Service()
    {
        // Arrange
        await SeedInvoicesAsync(5);
        _productClient.CallCount = 0;

        // Act
        using var response = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=3");
        using var secondPageResponse = await _client.GetAsync("/api/invoices/paged?pageNumber=2&pageSize=3");

        // Assert: the paginated listing is a read-only query against
        // Billing's own database and must never call out to Inventory.Api.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
        Assert.Equal(0, _productClient.CallCount);
    }

    [Fact]
    public async Task GetPaged_Without_Sort_Params_Defaults_To_Number_Descending()
    {
        // Arrange
        var first = await CreateInvoiceAsync();
        var second = await CreateInvoiceAsync();
        var third = await CreateInvoiceAsync();

        // Act
        using var response = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=10");

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(page);
        Assert.Equal([third.Number, second.Number, first.Number], page!.Items.Select(i => i.Number));
    }

    [Fact]
    public async Task GetPaged_SortBy_Number_Asc_Returns_Ascending_Order()
    {
        // Arrange
        var first = await CreateInvoiceAsync();
        var second = await CreateInvoiceAsync();
        var third = await CreateInvoiceAsync();

        // Act
        using var response = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=10&sortBy=number&sortDirection=asc");

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(page);
        Assert.Equal([first.Number, second.Number, third.Number], page!.Items.Select(i => i.Number));
    }

    [Fact]
    public async Task GetPaged_SortBy_CreatedAtUtc_Asc_And_Desc_Orders_Correctly()
    {
        // Arrange: invoices are created strictly in sequence, so CreatedAtUtc
        // order coincides with creation (and Number) order.
        var first = await CreateInvoiceAsync();
        var second = await CreateInvoiceAsync();
        var third = await CreateInvoiceAsync();

        // Act
        using var ascResponse = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=10&sortBy=createdAtUtc&sortDirection=asc");
        using var descResponse = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=10&sortBy=createdAtUtc&sortDirection=desc");

        // Assert
        var ascPage = await ascResponse.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        var descPage = await descResponse.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(ascPage);
        Assert.NotNull(descPage);
        Assert.Equal([first.Number, second.Number, third.Number], ascPage!.Items.Select(i => i.Number));
        Assert.Equal([third.Number, second.Number, first.Number], descPage!.Items.Select(i => i.Number));
    }

    [Fact]
    public async Task GetPaged_SortBy_ItemsCount_Asc_And_Desc_Orders_Correctly_Without_Loading_Full_Item_Rows()
    {
        // Arrange: invoices with 1, 2 and 3 items respectively (product 1
        // repeated across item lines, allowed by design).
        var oneItem = await CreateInvoiceWithItemCountAsync(1);
        var twoItems = await CreateInvoiceWithItemCountAsync(2);
        var threeItems = await CreateInvoiceWithItemCountAsync(3);

        // Act
        using var ascResponse = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=10&sortBy=itemsCount&sortDirection=asc");
        using var descResponse = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=10&sortBy=itemsCount&sortDirection=desc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, ascResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, descResponse.StatusCode);
        var ascPage = await ascResponse.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        var descPage = await descResponse.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(ascPage);
        Assert.NotNull(descPage);
        Assert.Equal([oneItem.Number, twoItems.Number, threeItems.Number], ascPage!.Items.Select(i => i.Number));
        Assert.Equal(1, ascPage.Items.First(i => i.Number == oneItem.Number).ItemsCount);
        Assert.Equal(3, ascPage.Items.First(i => i.Number == threeItems.Number).ItemsCount);
        Assert.Equal([threeItems.Number, twoItems.Number, oneItem.Number], descPage!.Items.Select(i => i.Number));
    }

    [Fact]
    public async Task GetPaged_SortBy_Status_Is_Accepted_And_Deterministic()
    {
        // Arrange: all seeded invoices remain Open, so ordering by Status
        // must fall back to the Id tie-break for a stable, reproducible order.
        var first = await CreateInvoiceAsync();
        var second = await CreateInvoiceAsync();
        var third = await CreateInvoiceAsync();

        // Act: call twice to confirm stability across calls.
        using var firstCall = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=10&sortBy=status&sortDirection=asc");
        using var secondCall = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=10&sortBy=status&sortDirection=asc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, firstCall.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondCall.StatusCode);
        var firstPage = await firstCall.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        var secondPage = await secondCall.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.All(firstPage!.Items, i => Assert.Equal("Open", i.Status));
        var expectedOrder = new[] { first.Number, second.Number, third.Number };
        Assert.Equal(expectedOrder, firstPage.Items.Select(i => i.Number));
        Assert.Equal(expectedOrder, secondPage!.Items.Select(i => i.Number));
    }

    [Fact]
    public async Task GetPaged_SortBy_Is_Case_Insensitive()
    {
        // Arrange
        var first = await CreateInvoiceAsync();
        var second = await CreateInvoiceAsync();

        // Act
        using var response = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=10&sortBy=NUMBER&sortDirection=ASC");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(page);
        Assert.Equal([first.Number, second.Number], page!.Items.Select(i => i.Number));
    }

    [Fact]
    public async Task GetPaged_Sort_Is_Applied_Before_Pagination_Second_Page_Matches_Sorted_Order()
    {
        // Arrange
        var invoices = new List<InvoiceResponse>();
        for (var i = 0; i < 5; i++)
        {
            invoices.Add(await CreateInvoiceAsync());
        }

        // Act: page 2 of a number-ascending listing (2 per page) should hold
        // the 3rd and 4th invoices created.
        using var response = await _client.GetAsync("/api/invoices/paged?pageNumber=2&pageSize=2&sortBy=number&sortDirection=asc");

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<InvoiceSummaryResponse>>();
        Assert.NotNull(page);
        Assert.Equal([invoices[2].Number, invoices[3].Number], page!.Items.Select(i => i.Number));
    }

    [Fact]
    public async Task GetPaged_Sorted_Listing_Still_Never_Calls_Inventory_Service()
    {
        // Arrange
        await SeedInvoicesAsync(3);
        _productClient.CallCount = 0;

        // Act
        using var response = await _client.GetAsync("/api/invoices/paged?pageNumber=1&pageSize=10&sortBy=itemsCount&sortDirection=desc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, _productClient.CallCount);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("id")]
    [InlineData("total")]
    public async Task GetPaged_With_Invalid_SortBy_Returns_BadRequest_With_InvalidSort_ErrorCode(string sortBy)
    {
        // Act
        using var response = await _client.GetAsync($"/api/invoices/paged?pageNumber=1&pageSize=10&sortBy={sortBy}");

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
        using var response = await _client.GetAsync($"/api/invoices/paged?pageNumber=1&pageSize=10&sortDirection={sortDirection}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("INVALID_SORT", body);
    }
}
