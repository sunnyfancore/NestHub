using FreeSql.DataAnnotations;
using NestHub.Api.Domain.Interfaces;

namespace NestHub.Api.Domain.Entities;

[Table(Name = "site_settings")]
public sealed class SiteSetting : AuditableEntity, ITenantEntity
{
    [Column(Name = "tenant_id", DbType = "char(36)", IsNullable = false)]
    public Guid TenantId { get; set; }

    [Column(Name = "title", StringLength = 120, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    [Column(Name = "subtitle", StringLength = 255)]
    public string? Subtitle { get; set; }

    [Column(Name = "description", StringLength = 500)]
    public string? Description { get; set; }

    [Column(Name = "logo_text", StringLength = 32)]
    public string? LogoText { get; set; }

    [Column(Name = "logo_url", StringLength = 500)]
    public string? LogoUrl { get; set; }

    [Column(Name = "search_placeholder", StringLength = 120)]
    public string? SearchPlaceholder { get; set; }

    [Column(Name = "footer_text", StringLength = 255)]
    public string? FooterText { get; set; }

    [Column(Name = "theme_name", StringLength = 40)]
    public string? ThemeName { get; set; }

    [Column(Name = "mobile_theme_name", StringLength = 40)]
    public string? MobileThemeName { get; set; }

    [Column(Name = "default_search_engine", StringLength = 20)]
    public string? DefaultSearchEngine { get; set; }

    [Column(Name = "logo_mode", StringLength = 20)]
    public string? LogoMode { get; set; }

    [Column(Name = "show_bottom_dock")]
    public bool? ShowBottomDock { get; set; }
}
