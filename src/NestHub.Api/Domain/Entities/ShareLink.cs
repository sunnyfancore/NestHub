using FreeSql.DataAnnotations;
using NestHub.Api.Domain.Interfaces;

namespace NestHub.Api.Domain.Entities;

[Table(Name = "share_links")]
[Index("idx_share_access_token", "AccessToken", true)]
public sealed class ShareLink : AuditableEntity, ITenantEntity
{
    [Column(Name = "tenant_id", DbType = "char(36)", IsNullable = false)]
    public Guid TenantId { get; set; }

    [Column(Name = "category_id", DbType = "char(36)", IsNullable = false)]
    public Guid CategoryId { get; set; }

    [Column(Name = "share_code", StringLength = 16, IsNullable = false)]
    public string ShareCode { get; set; } = string.Empty;

    [Column(Name = "access_token", StringLength = 64)]
    public string? AccessToken { get; set; }

    [Column(Name = "password_hash", StringLength = 256)]
    public string? PasswordHash { get; set; }

    [Column(Name = "password", StringLength = 32)]
    public string? Password { get; set; }

    [Column(Name = "note", StringLength = 255)]
    public string? Note { get; set; }

    [Column(Name = "expire_at")]
    public DateTime? ExpireAt { get; set; }
}
