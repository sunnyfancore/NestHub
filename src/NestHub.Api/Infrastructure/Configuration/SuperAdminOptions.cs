namespace NestHub.Api.Infrastructure.Configuration;

public sealed class SuperAdminOptions
{
    public const string SectionName = "SuperAdmin";

    public string Email { get; set; } = "admin@nesthub.local";

    public string Password { get; set; } = "Admin123!";

    public string DisplayName { get; set; } = "Super Admin";
}
