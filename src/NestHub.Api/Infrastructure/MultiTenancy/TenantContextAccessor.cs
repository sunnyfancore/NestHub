using System.Threading;

namespace NestHub.Api.Infrastructure.MultiTenancy;

public sealed class TenantContextAccessor
{
    private readonly AsyncLocal<TenantContext?> _current = new();

    public TenantContext? Current => _current.Value;

    public void Set(TenantContext context)
    {
        _current.Value = context;
    }

    public void Clear()
    {
        _current.Value = null;
    }
}
