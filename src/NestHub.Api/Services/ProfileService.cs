using FreeSql;
using NestHub.Api.Contracts.Profile;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class ProfileService
{
    private readonly IFreeSql _orm;

    public ProfileService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<ProfileResponse> GetAsync(TenantContext tenantContext)
    {
        var tenant = await _orm.Select<Tenant>().Where(t => t.Id == tenantContext.UserId).ToOneAsync();
        if (tenant is null)
        {
            throw new KeyNotFoundException("账号不存在。");
        }

        return new ProfileResponse
        {
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
}
