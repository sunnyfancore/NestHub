using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.Configuration;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Infrastructure.Security;

public sealed class JwtTokenService
{
    private readonly JwtOptions _jwtOptions;

    public JwtTokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public (string Token, DateTime ExpiresAtUtc) CreateToken(Tenant tenant)
    {
        var expiresAt = DateTime.UtcNow.AddHours(_jwtOptions.ExpireHours);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, tenant.Id.ToString()),
            new(ClaimTypes.NameIdentifier, tenant.Id.ToString()),
            new(ClaimTypes.Name, tenant.DisplayName ?? tenant.Name),
            new(ClaimTypes.Email, tenant.Email ?? string.Empty),
            new(TenantClaims.IsSuperAdmin, tenant.IsSuperAdmin.ToString().ToLowerInvariant()),
            new(TenantClaims.TenantId, tenant.Id.ToString()),
            new(TenantClaims.TenantName, tenant.Name)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
