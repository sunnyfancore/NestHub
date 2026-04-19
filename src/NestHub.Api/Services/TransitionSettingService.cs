using FreeSql;
using NestHub.Api.Contracts.Admin;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class TransitionSettingService
{
    private readonly IFreeSql _orm;

    public TransitionSettingService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<TransitionSettingDto> GetAsync(TenantContext tenantContext)
    {
        var setting = await _orm.Select<TransitionSetting>().Where(item => item.TenantId == tenantContext.TenantId).ToOneAsync();
        return Map(setting);
    }

    public async Task<TransitionSettingDto> UpdateAsync(SaveTransitionSettingRequest request, TenantContext tenantContext)
    {
        var setting = await _orm.Select<TransitionSetting>().Where(item => item.TenantId == tenantContext.TenantId).ToOneAsync();
        if (setting is null)
        {
            setting = new TransitionSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _orm.Insert(setting).ExecuteAffrowsAsync();
        }

        setting.IsEnabled = request.IsEnabled;
        setting.VisitorStaySeconds = request.VisitorStaySeconds;
        setting.AdminStaySeconds = request.AdminStaySeconds;
        setting.AdScript1 = string.IsNullOrWhiteSpace(request.AdScript1) ? null : request.AdScript1.Trim();
        setting.AdScript2 = string.IsNullOrWhiteSpace(request.AdScript2) ? null : request.AdScript2.Trim();
        setting.UpdatedAt = DateTime.UtcNow;

        await _orm.Update<TransitionSetting>().SetSource(setting).ExecuteAffrowsAsync();
        return Map(setting);
    }

    private static TransitionSettingDto Map(TransitionSetting? setting)
    {
        return new TransitionSettingDto
        {
            IsEnabled = setting?.IsEnabled ?? false,
            VisitorStaySeconds = setting?.VisitorStaySeconds ?? 5,
            AdminStaySeconds = setting?.AdminStaySeconds ?? 1,
            AdScript1 = setting?.AdScript1,
            AdScript2 = setting?.AdScript2
        };
    }
}
