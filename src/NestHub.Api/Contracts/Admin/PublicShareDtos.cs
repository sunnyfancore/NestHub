using System.ComponentModel.DataAnnotations;

namespace NestHub.Api.Contracts.Admin;

public sealed record PublicShareInfo(string CategoryName, bool RequiresPassword, DateTime? ExpireAt);
public sealed class ReadShareRequest
{
    [StringLength(32)]
    public string? Password { get; set; }
}
public sealed record SharedBookmark(string Title, string Url, string? Description);
public sealed record SharedCategory(string Name, IReadOnlyCollection<SharedBookmark> Links);
public sealed record PublicShareContent(string CategoryName, DateTime? ExpireAt, IReadOnlyCollection<SharedCategory> Categories);
