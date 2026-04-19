using Microsoft.AspNetCore.Http;

namespace NestHub.Api.Contracts.Admin;

public sealed class AdminCategoryItemDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }

    public string? ParentName { get; set; }

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    public int LinkCount { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public sealed class AdminLinkItemDto
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

    public bool IsPinned { get; set; }

    public int SortOrder { get; set; }

    public int ClickCount { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public sealed class AdminThemeOptionDto
{
    public string Name { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ScreenshotUrl { get; set; } = string.Empty;

    public bool Installed { get; set; } = true;
}

public sealed class BookmarkImportRequest
{
    public Guid? CategoryId { get; set; }

    public IFormFile? File { get; set; }
}

public sealed class BookmarkImportResultDto
{
    public int Total { get; set; }

    public int Imported { get; set; }

    public int Failed { get; set; }
}

public sealed class BackupExportDto
{
    public object Site { get; set; } = new();

    public IReadOnlyCollection<AdminCategoryItemDto> Categories { get; set; } = [];

    public IReadOnlyCollection<AdminLinkItemDto> Links { get; set; } = [];
}

// ── Super Admin DTOs ──

public sealed class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class UpdateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

public sealed class AdminTenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public bool IsSuperAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LinkCount { get; set; }
}

public sealed class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Password { get; set; } = string.Empty;
}
