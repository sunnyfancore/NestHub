using FreeSql.DataAnnotations;

namespace NestHub.Api.Domain.Entities;

[Table(Name = "password_reset_tokens")]
public sealed class PasswordResetToken
{
    [Column(Name = "id", DbType = "char(36)", IsPrimary = true)]
    public Guid Id { get; set; }

    [Column(Name = "user_id", DbType = "char(36)", IsNullable = false)]
    public Guid UserId { get; set; }

    [Column(Name = "token", StringLength = 128, IsNullable = false)]
    public string Token { get; set; } = string.Empty;

    [Column(Name = "expires_at", IsNullable = false)]
    public DateTime ExpiresAt { get; set; }

    [Column(Name = "is_used")]
    public bool IsUsed { get; set; }

    [Column(Name = "created_at", IsNullable = false)]
    public DateTime CreatedAt { get; set; }
}
