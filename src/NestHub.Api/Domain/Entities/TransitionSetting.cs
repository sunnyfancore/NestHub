using FreeSql.DataAnnotations;
using NestHub.Api.Domain.Interfaces;

namespace NestHub.Api.Domain.Entities;

[Table(Name = "transition_settings")]
public sealed class TransitionSetting : AuditableEntity, ITenantEntity
{
    [Column(Name = "tenant_id", DbType = "char(36)", IsNullable = false)]
    public Guid TenantId { get; set; }

    [Column(Name = "is_enabled")]
    public bool IsEnabled { get; set; }

    [Column(Name = "visitor_stay_seconds")]
    public int VisitorStaySeconds { get; set; } = 5;

    [Column(Name = "admin_stay_seconds")]
    public int AdminStaySeconds { get; set; } = 1;

    [Column(Name = "ad_script_1", StringLength = 2000)]
    public string? AdScript1 { get; set; }

    [Column(Name = "ad_script_2", StringLength = 2000)]
    public string? AdScript2 { get; set; }
}
