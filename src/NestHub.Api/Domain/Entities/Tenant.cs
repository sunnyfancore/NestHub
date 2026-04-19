using FreeSql.DataAnnotations;

namespace NestHub.Api.Domain.Entities;

[Table(Name = "tenants")]
public sealed class Tenant : AuditableEntity
{
    public static readonly Guid PublicTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Column(Name = "name", StringLength = 100, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    [Column(Name = "email", StringLength = 180, IsNullable = true)]
    public string? Email { get; set; }

    [Column(Name = "display_name", StringLength = 80, IsNullable = true)]
    public string? DisplayName { get; set; }

    [Column(Name = "password_hash", StringLength = 512, IsNullable = true)]
    public string? PasswordHash { get; set; }

    [Column(Name = "last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [Column(Name = "is_active")]
    public bool IsActive { get; set; } = true;

    [Column(Name = "is_super_admin")]
    public bool IsSuperAdmin { get; set; } = false;
}
