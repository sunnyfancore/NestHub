using NestHub.Api.Contracts.Profile;

namespace NestHub.Api.Contracts.Auth;

public sealed class AuthResponse
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public bool IsSuperAdmin { get; set; }

    public ProfileUserDto User { get; set; } = new();

    public ProfileTenantDto? Tenant { get; set; }
}
