namespace NestHub.Api.Common;

public class PagedResult<T>
{
    public IReadOnlyCollection<T> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public long Total { get; init; }
}
