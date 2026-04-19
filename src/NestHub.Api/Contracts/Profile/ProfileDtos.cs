namespace NestHub.Api.Contracts.Profile;

public sealed class ProfileResponse
{
    public bool IsSuperAdmin { get; set; }

    public ProfileUserDto User { get; set; } = new();

    public ProfileTenantDto? Tenant { get; set; }
}

public sealed class ProfileUserDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

public sealed class ProfileTenantDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
