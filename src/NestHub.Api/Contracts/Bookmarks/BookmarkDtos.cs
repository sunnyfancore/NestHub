using System.ComponentModel.DataAnnotations;
using NestHub.Api.Common;

namespace NestHub.Api.Contracts.Bookmarks;

public sealed class BookmarkDto
{
    public Guid Id { get; set; }

    public Guid? FolderId { get; set; }

    public string? FolderName { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Tags { get; set; }

    public string? IconUrl { get; set; }

    public bool IsPinned { get; set; }

    public int SortOrder { get; set; }

    public int ClickCount { get; set; }

    public DateTime? LastVisitedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public sealed class BookmarkQueryRequest
{
    public string? Keyword { get; set; }

    public Guid? FolderId { get; set; }

    [Range(1, 200)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 12;
}

public sealed class SaveBookmarkRequest
{
    public Guid? FolderId { get; set; }

    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Url]
    [StringLength(1024)]
    public string Url { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(255)]
    public string? Tags { get; set; }

    [StringLength(1024)]
    public string? IconUrl { get; set; }

    public bool IsPinned { get; set; }

    public int SortOrder { get; set; }
}

public sealed class BookmarkListResponse : PagedResult<BookmarkDto>
{
    public IReadOnlyCollection<BookmarkFolderOptionDto> Folders { get; init; } = [];
}

public sealed class BookmarkFolderOptionDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
