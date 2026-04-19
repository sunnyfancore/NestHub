using FreeSql;
using Microsoft.AspNetCore.Identity;
using NestHub.Api.Contracts.Auth;
using NestHub.Api.Contracts.Profile;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Infrastructure.Security;

namespace NestHub.Api.Services;

public sealed class AuthService
{
    private readonly IFreeSql _orm;
    private readonly IPasswordHasher<Tenant> _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly EmailService _emailService;

    public AuthService(
        IFreeSql orm,
        IPasswordHasher<Tenant> passwordHasher,
        JwtTokenService jwtTokenService,
        EmailService emailService)
    {
        _orm = orm;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _emailService = emailService;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var tenant = await _orm.Select<Tenant>().Where(t => t.Email == normalizedEmail).ToOneAsync();
        if (tenant is null || !tenant.IsActive)
        {
            throw new InvalidOperationException("账号不存在或已被禁用。");
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(tenant, tenant.PasswordHash!, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            throw new InvalidOperationException("邮箱或密码不正确。");
        }

        tenant.LastLoginAt = DateTime.UtcNow;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _orm.Update<Tenant>().SetSource(tenant).ExecuteAffrowsAsync();

        return BuildAuthResponse(tenant);
    }

    private AuthResponse BuildAuthResponse(Tenant tenant)
    {
        var token = _jwtTokenService.CreateToken(tenant);

        return new AuthResponse
        {
            Token = token.Token,
            ExpiresAtUtc = token.ExpiresAtUtc,
            IsSuperAdmin = tenant.IsSuperAdmin,
            User = new ProfileUserDto
            {
                Id = tenant.Id,
                Email = tenant.Email ?? string.Empty,
                DisplayName = tenant.DisplayName ?? tenant.Name
            },
            Tenant = new ProfileTenantDto
            {
                Id = tenant.Id,
                Name = tenant.Name
            }
        };
    }

    public async Task<AuthResponse> RenewTokenAsync(TenantContext ctx)
    {
        var tenant = await _orm.Select<Tenant>().Where(t => t.Id == ctx.UserId).ToOneAsync();
        if (tenant is null || !tenant.IsActive)
        {
            throw new InvalidOperationException("账号不存在或已被禁用。");
        }

        return BuildAuthResponse(tenant);
    }

    public async Task ChangePasswordAsync(Guid tenantId, string currentPassword, string newPassword)
    {
        var tenant = await _orm.Select<Tenant>().Where(t => t.Id == tenantId).ToOneAsync();
        if (tenant is null)
        {
            throw new InvalidOperationException("账号不存在。");
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(tenant, tenant.PasswordHash!, currentPassword);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            throw new InvalidOperationException("当前密码不正确。");
        }

        tenant.PasswordHash = _passwordHasher.HashPassword(tenant, newPassword);
        tenant.UpdatedAt = DateTime.UtcNow;
        await _orm.Update<Tenant>().SetSource(tenant).ExecuteAffrowsAsync();
    }

    public async Task RequestPasswordResetAsync(string email, string resetUrl)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var tenant = await _orm.Select<Tenant>().Where(t => t.Email == normalizedEmail).ToOneAsync();
        if (tenant is null)
        {
            return;
        }

        var existingTokens = await _orm.Select<PasswordResetToken>()
            .Where(t => t.UserId == tenant.Id && !t.IsUsed)
            .ToListAsync();
        foreach (var t in existingTokens)
        {
            t.IsUsed = true;
        }
        if (existingTokens.Count > 0)
        {
            await _orm.Update<PasswordResetToken>().SetSource(existingTokens).ExecuteAffrowsAsync();
        }

        var token = Guid.NewGuid().ToString("N");
        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = tenant.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        await _orm.Insert(resetToken).ExecuteAffrowsAsync();
        await _emailService.SendPasswordResetEmailAsync(tenant.Email!, token, resetUrl);
    }

    public async Task ConfirmPasswordResetAsync(string token, string newPassword)
    {
        var resetToken = await _orm.Select<PasswordResetToken>()
            .Where(t => t.Token == token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
            .ToOneAsync();

        if (resetToken is null)
        {
            throw new InvalidOperationException("重置链接无效或已过期。");
        }

        var tenant = await _orm.Select<Tenant>().Where(t => t.Id == resetToken.UserId).ToOneAsync();
        if (tenant is null)
        {
            throw new InvalidOperationException("账号不存在。");
        }

        tenant.PasswordHash = _passwordHasher.HashPassword(tenant, newPassword);
        tenant.UpdatedAt = DateTime.UtcNow;
        await _orm.Update<Tenant>().SetSource(tenant).ExecuteAffrowsAsync();

        resetToken.IsUsed = true;
        await _orm.Update<PasswordResetToken>().SetSource(resetToken).ExecuteAffrowsAsync();
    }
}
