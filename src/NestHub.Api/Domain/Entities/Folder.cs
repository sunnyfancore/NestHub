using FreeSql.DataAnnotations;
using NestHub.Api.Domain.Interfaces;

namespace NestHub.Api.Domain.Entities;

[Table(Name = "folders")]
public sealed class Folder : AuditableEntity, ITenantEntity
{
    [Column(Name = "tenant_id", DbType = "char(36)", IsNullable = false)]
    public Guid TenantId { get; set; }

    [Column(Name = "parent_id", DbType = "char(36)")]
    public Guid? ParentId { get; set; }

    [Column(Name = "name", StringLength = 80, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    [Column(Name = "description", StringLength = 255)]
    public string? Description { get; set; }

    [Column(Name = "icon", StringLength = 64)]
    public string? Icon { get; set; }

    [Column(Name = "sort_order")]
    public int SortOrder { get; set; }
}
