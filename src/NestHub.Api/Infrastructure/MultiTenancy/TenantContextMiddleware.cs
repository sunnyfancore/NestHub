using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace NestHub.Api.Infrastructure.MultiTenancy;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public TenantContextMiddleware(
        RequestDelegate next,
        TenantContextAccessor tenantContextAccessor)
    {
        _next = next;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await _next(context);
            return;
        }

        var displayName = context.User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";
        var email = context.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var tenantName = context.User.FindFirstValue(TenantClaims.TenantName) ?? string.Empty;
        var isSuperAdmin = string.Equals(context.User.FindFirstValue(TenantClaims.IsSuperAdmin), "true", StringComparison.OrdinalIgnoreCase);

        Guid tenantId = Guid.Empty;
        var tenantIdValue = context.User.FindFirstValue(TenantClaims.TenantId);
        if (Guid.TryParse(tenantIdValue, out var parsedTenantId))
        {
            tenantId = parsedTenantId;
        }

        _tenantContextAccessor.Set(new TenantContext(userId, tenantId, displayName, email, tenantName, isSuperAdmin));

        try
        {
            await _next(context);
        }
        finally
        {
            _tenantContextAccessor.Clear();
        }
    }
}
