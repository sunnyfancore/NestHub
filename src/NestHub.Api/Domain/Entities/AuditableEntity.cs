using FreeSql.DataAnnotations;

namespace NestHub.Api.Domain.Entities;

public abstract class AuditableEntity
{
    [Column(Name = "id", DbType = "char(36)", IsPrimary = true)]
    public Guid Id { get; set; }

    [Column(Name = "created_at")]
    public DateTime CreatedAt { get; set; }

    [Column(Name = "updated_at")]
    public DateTime UpdatedAt { get; set; }
}
