namespace Billing.Api.Common;

/// <summary>
/// Generic envelope for a server-side paginated listing. Kept local to
/// Billing.Api (no shared library between microservices) since it is a
/// small, self-contained DTO with no dependency on domain types.
/// </summary>
/// <typeparam name="T">Type of the items returned in the current page.</typeparam>
public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage)
{
    /// <summary>
    /// Builds a <see cref="PagedResponse{T}"/> computing <see cref="TotalPages"/>,
    /// <see cref="HasPreviousPage"/> and <see cref="HasNextPage"/> from the given
    /// page number, page size and total count.
    /// </summary>
    public static PagedResponse<T> Create(IReadOnlyList<T> items, int pageNumber, int pageSize, int totalCount)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResponse<T>(
            items,
            pageNumber,
            pageSize,
            totalCount,
            totalPages,
            HasPreviousPage: pageNumber > 1,
            HasNextPage: pageNumber < totalPages);
    }
}
