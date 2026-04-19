using System.ComponentModel.DataAnnotations;

namespace NestHub.Api.Contracts.Portal;

public sealed class PortalResponse
{
    public PortalSiteDto Site { get; set; } = new();

    public PortalTenantDto Tenant { get; set; } = new();

    public bool IsAuthenticated { get; set; }

    public bool CanEdit { get; set; }

    public IReadOnlyCollection<PortalLinkDto> FeaturedLinks { get; set; } = [];

    public IReadOnlyCollection<PortalCategoryDto> Categories { get; set; } = [];

    public PortalEditorDto? Editor { get; set; }
}

public sealed class PortalSiteDto
{
    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string LogoText { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public string SearchPlaceholder { get; set; } = string.Empty;

    public string FooterText { get; set; } = string.Empty;

    public string ThemeName { get; set; } = "default2";

    public string MobileThemeName { get; set; } = "default2";

    public string? DefaultSearchEngine { get; set; }

    public string LogoMode { get; set; } = "compact";

    public bool ShowBottomDock { get; set; } = true;
}

public sealed class PortalTenantDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class PortalCategoryDto
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    public IReadOnlyCollection<PortalLinkDto> Links { get; set; } = [];

    public IReadOnlyCollection<PortalCategoryDto> Children { get; set; } = [];
}

public sealed class PortalLinkDto
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? StandbyUrl { get; set; }

    public string? Description { get; set; }

    public string? Tags { get; set; }

    public string? IconUrl { get; set; }

    public string? FontIcon { get; set; }

    public bool IsPinned { get; set; }

    public int SortOrder { get; set; }

    public int ClickCount { get; set; }

    public int CheckStatus { get; set; }

    public DateTime? LastCheckedAt { get; set; }

    public DateTime? LastVisitedAt { get; set; }
}

public sealed class PortalEditorDto
{
    public IReadOnlyCollection<PortalCategoryOptionDto> CategoryOptions { get; set; } = [];
}

public sealed class PortalCategoryOptionDto
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Level { get; set; }
}

public sealed class PortalSearchResultDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? StandbyUrl { get; set; }

    public string? Description { get; set; }

    public string? IconUrl { get; set; }

    public string CategoryName { get; set; } = string.Empty;
}

public sealed class SavePortalCategoryRequest
{
    [Required]
    [StringLength(80, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }

    [StringLength(64)]
    public string? Icon { get; set; }

    public Guid? ParentId { get; set; }

    public int SortOrder { get; set; }
}

public sealed class SavePortalLinkRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Url]
    [StringLength(1024)]
    public string Url { get; set; } = string.Empty;

    [StringLength(1024)]
    public string? StandbyUrl { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(255)]
    public string? Tags { get; set; }

    [StringLength(1024)]
    public string? IconUrl { get; set; }

    [StringLength(64)]
    public string? FontIcon { get; set; }

    public bool IsPinned { get; set; }

    public int SortOrder { get; set; }
}

public sealed class SaveSiteSettingsRequest
{
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Subtitle { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(32)]
    public string? LogoText { get; set; }

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    [StringLength(120)]
    public string? SearchPlaceholder { get; set; }

    [StringLength(255)]
    public string? FooterText { get; set; }

    [StringLength(40)]
    public string? ThemeName { get; set; }

    [StringLength(40)]
    public string? MobileThemeName { get; set; }

    [StringLength(20)]
    public string? LogoMode { get; set; }

    public bool? ShowBottomDock { get; set; }
}

public sealed class UpdateSearchEngineRequest
{
    [Required]
    [StringLength(20)]
    public string SearchEngine { get; set; } = string.Empty;
}

public sealed class SortRequest
{
    [Required]
    public IReadOnlyCollection<Guid> OrderedIds { get; set; } = [];
}
