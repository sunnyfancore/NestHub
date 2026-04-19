using FreeSql.DataAnnotations;
using NestHub.Api.Domain.Interfaces;

namespace NestHub.Api.Domain.Entities;

[Table(Name = "bookmarks")]
public sealed class Bookmark : AuditableEntity, ITenantEntity
{
    [Column(Name = "tenant_id", DbType = "char(36)", IsNullable = false)]
    public Guid TenantId { get; set; }

    [Column(Name = "folder_id", DbType = "char(36)")]
    public Guid? FolderId { get; set; }

    [Column(Name = "title", StringLength = 120, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    [Column(Name = "url", StringLength = 1024, IsNullable = false)]
    public string Url { get; set; } = string.Empty;

    [Column(Name = "standby_url", StringLength = 1024)]
    public string? StandbyUrl { get; set; }

    [Column(Name = "description", StringLength = 500)]
    public string? Description { get; set; }

    [Column(Name = "tags", StringLength = 255)]
    public string? Tags { get; set; }

    [Column(Name = "icon_url", StringLength = 1024)]
    public string? IconUrl { get; set; }

    [Column(Name = "font_icon", StringLength = 64)]
    public string? FontIcon { get; set; }

    [Column(Name = "is_pinned")]
    public bool IsPinned { get; set; }

    [Column(Name = "sort_order")]
    public int SortOrder { get; set; }

    [Column(Name = "click_count")]
    public int ClickCount { get; set; }

    [Column(Name = "last_visited_at")]
    public DateTime? LastVisitedAt { get; set; }

    [Column(Name = "check_status")]
    public int CheckStatus { get; set; }

    [Column(Name = "last_checked_at")]
    public DateTime? LastCheckedAt { get; set; }
}
