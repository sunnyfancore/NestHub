namespace NestHub.Api.Infrastructure.MultiTenancy;

public sealed record TenantContext(
    Guid UserId,
    Guid TenantId,
    string DisplayName,
    string Email,
    string TenantName,
    bool IsSuperAdmin = false);
