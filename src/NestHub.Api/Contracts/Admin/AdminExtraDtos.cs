using System.ComponentModel.DataAnnotations;

namespace NestHub.Api.Contracts.Admin;

public sealed class ShareLinkItemDto
{
    public Guid Id { get; set; }

    public string ShareCode { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public string? Password { get; set; }

    public string? Note { get; set; }

    public DateTime? ExpireAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

public sealed class SaveShareLinkRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [StringLength(32)]
    public string? Password { get; set; }

    [StringLength(255)]
    public string? Note { get; set; }

    public DateTime? ExpireAt { get; set; }
}

public sealed class TransitionSettingDto
{
    public bool IsEnabled { get; set; }

    public int VisitorStaySeconds { get; set; }

    public int AdminStaySeconds { get; set; }

    public string? AdScript1 { get; set; }

    public string? AdScript2 { get; set; }
}

public sealed class SaveTransitionSettingRequest
{
    public bool IsEnabled { get; set; }

    [Range(0, 86400)]
    public int VisitorStaySeconds { get; set; }

    [Range(0, 86400)]
    public int AdminStaySeconds { get; set; }

    [StringLength(2000)]
    public string? AdScript1 { get; set; }

    [StringLength(2000)]
    public string? AdScript2 { get; set; }
}
