namespace NestHub.Api.Infrastructure.MultiTenancy;

public static class TenantClaims
{
    public const string TenantId = "tenant_id";
    public const string TenantName = "tenant_name";
    public const string IsSuperAdmin = "is_super_admin";
}
