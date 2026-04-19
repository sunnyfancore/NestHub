using FreeSql.DataAnnotations;

namespace NestHub.Api.Domain.Entities;

[Table(Name = "smtp_settings")]
public sealed class SmtpSetting
{
    [Column(Name = "id", DbType = "char(36)", IsPrimary = true)]
    public Guid Id { get; set; }

    [Column(Name = "host", StringLength = 255, IsNullable = false)]
    public string Host { get; set; } = string.Empty;

    [Column(Name = "port")]
    public int Port { get; set; } = 465;

    [Column(Name = "use_ssl")]
    public bool UseSsl { get; set; } = true;

    [Column(Name = "username", StringLength = 255, IsNullable = false)]
    public string Username { get; set; } = string.Empty;

    [Column(Name = "password", StringLength = 512)]
    public string? Password { get; set; }

    [Column(Name = "from_email", StringLength = 255)]
    public string? FromEmail { get; set; }

    [Column(Name = "from_name", StringLength = 100)]
    public string? FromName { get; set; }

    [Column(Name = "created_at", IsNullable = false)]
    public DateTime CreatedAt { get; set; }

    [Column(Name = "updated_at", IsNullable = false)]
    public DateTime UpdatedAt { get; set; }
}
