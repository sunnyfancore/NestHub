using FreeSql;
using Microsoft.AspNetCore.Identity;
using NestHub.Api.Contracts.Admin;
using NestHub.Api.Domain.Entities;

namespace NestHub.Api.Services;

public sealed class SuperAdminService
{
    private readonly IFreeSql _orm;
    private readonly IPasswordHasher<Tenant> _passwordHasher;

    public SuperAdminService(IFreeSql orm, IPasswordHasher<Tenant> passwordHasher)
    {
        _orm = orm;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyCollection<AdminTenantDto>> GetTenantsAsync()
    {
        var tenants = await _orm.Select<Tenant>()
            .DisableGlobalFilter("TenantFilter")
            .Where(t => t.Id != Tenant.PublicTenantId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        var allBookmarks = await _orm.Select<Bookmark>()
            .DisableGlobalFilter("TenantFilter")
            .ToListAsync(b => new { b.TenantId });

        var countMap = allBookmarks
            .GroupBy(b => b.TenantId)
            .ToDictionary(g => g.Key, g => g.Count());

        return tenants.Select(t => new AdminTenantDto
        {
            Id = t.Id,
            Name = t.Name,
            DisplayName = t.DisplayName,
            Email = t.Email,
            IsActive = t.IsActive,
            IsSuperAdmin = t.IsSuperAdmin,
            CreatedAt = t.CreatedAt,
            LinkCount = countMap.GetValueOrDefault(t.Id, 0)
        }).ToList();
    }

    public async Task<AdminTenantDto> CreateTenantAsync(CreateTenantRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var exists = await _orm.Select<Tenant>().DisableGlobalFilter("TenantFilter").Where(t => t.Email == normalizedEmail).AnyAsync();
        if (exists)
        {
            throw new InvalidOperationException("该邮箱已被使用。");
        }

        var now = DateTime.UtcNow;
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            DisplayName = request.DisplayName?.Trim() ?? request.Name.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        tenant.PasswordHash = _passwordHasher.HashPassword(tenant, request.Password);

        await _orm.Insert(tenant).ExecuteAffrowsAsync();

        await _orm.Insert(new SiteSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Title = request.Name.Trim(),
            Subtitle = "为团队与设备准备的一站式导航门户。",
            LogoText = request.Name.Trim()[..Math.Min(1, request.Name.Trim().Length)].ToUpperInvariant(),
            ThemeName = "default2",
            CreatedAt = now,
            UpdatedAt = now
        }).ExecuteAffrowsAsync();

        return (await GetTenantsAsync()).First(t => t.Id == tenant.Id);
    }

    public async Task ToggleTenantActiveAsync(Guid tenantId)
    {
        var tenant = await _orm.Select<Tenant>().DisableGlobalFilter("TenantFilter").Where(t => t.Id == tenantId).ToOneAsync();
        if (tenant is null) throw new InvalidOperationException("租户不存在。");
        if (tenant.IsSuperAdmin) throw new InvalidOperationException("不能禁用超级管理员。");

        tenant.IsActive = !tenant.IsActive;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _orm.Update<Tenant>().SetSource(tenant).ExecuteAffrowsAsync();
    }

    public async Task ResetPasswordAsync(Guid tenantId, string newPassword)
    {
        var tenant = await _orm.Select<Tenant>().DisableGlobalFilter("TenantFilter").Where(t => t.Id == tenantId).ToOneAsync();
        if (tenant is null) throw new InvalidOperationException("租户不存在。");

        tenant.PasswordHash = _passwordHasher.HashPassword(tenant, newPassword);
        tenant.UpdatedAt = DateTime.UtcNow;
        await _orm.Update<Tenant>().SetSource(tenant).ExecuteAffrowsAsync();
    }

    public async Task UpdateTenantAsync(Guid tenantId, string name, string? displayName)
    {
        var tenant = await _orm.Select<Tenant>().DisableGlobalFilter("TenantFilter").Where(t => t.Id == tenantId).ToOneAsync();
        if (tenant is null) throw new InvalidOperationException("租户不存在。");

        tenant.Name = name.Trim();
        tenant.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        tenant.UpdatedAt = DateTime.UtcNow;
        await _orm.Update<Tenant>().SetSource(tenant).ExecuteAffrowsAsync();

        var setting = await _orm.Select<SiteSetting>().DisableGlobalFilter("TenantFilter").Where(s => s.TenantId == tenantId).FirstAsync();
        if (setting is not null)
        {
            setting.Title = name.Trim();
            setting.UpdatedAt = DateTime.UtcNow;
            await _orm.Update<SiteSetting>().SetSource(setting).ExecuteAffrowsAsync();
        }
    }
}
