namespace NestHub.Api.Contracts.Dashboard;

public sealed class DashboardSummaryDto
{
    public string TenantName { get; set; } = string.Empty;

    public int BookmarkCount { get; set; }

    public int FolderCount { get; set; }

    public int PinnedCount { get; set; }

    public IReadOnlyCollection<RecentBookmarkDto> RecentBookmarks { get; set; } = [];
}

public sealed class RecentBookmarkDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? FolderName { get; set; }

    public DateTime UpdatedAt { get; set; }
}
