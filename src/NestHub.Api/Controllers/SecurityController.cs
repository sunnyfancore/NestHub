using FreeSql;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/security")]
public sealed class SecurityController : ControllerBase
{
    private readonly IFreeSql _orm;
    private readonly EmailService _emailService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public SecurityController(IFreeSql orm, EmailService emailService, TenantContextAccessor tenantContextAccessor)
    {
        _orm = orm;
        _emailService = emailService;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet("smtp")]
    public async Task<IActionResult> GetSmtpConfig()
    {
        var ctx = _tenantContextAccessor.Current;
        if (ctx is null || !ctx.IsSuperAdmin) return Forbid();

        var setting = await _orm.Select<SmtpSetting>().FirstAsync();
        if (setting is null)
        {
            return Ok(new { host = "", port = 465, useSsl = true, username = "", hasPassword = false, fromEmail = "", fromName = "NestHub" });
        }

        return Ok(new
        {
            setting.Host,
            setting.Port,
            setting.UseSsl,
            setting.Username,
            hasPassword = !string.IsNullOrWhiteSpace(setting.Password),
            setting.FromEmail,
            setting.FromName
        });
    }

    [HttpPut("smtp")]
    public async Task<IActionResult> UpdateSmtpConfig([FromBody] UpdateSmtpRequest request)
    {
        var ctx = _tenantContextAccessor.Current;
        if (ctx is null || !ctx.IsSuperAdmin) return Forbid();

        var setting = await _orm.Select<SmtpSetting>().FirstAsync();
        var now = DateTime.UtcNow;

        if (setting is null)
        {
            setting = new SmtpSetting
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                UpdatedAt = now
            };
            setting.Host = request.Host?.Trim() ?? "";
            setting.Port = request.Port;
            setting.UseSsl = request.UseSsl;
            setting.Username = request.Username?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                setting.Password = request.Password;
            }
            setting.FromEmail = string.IsNullOrWhiteSpace(request.FromEmail) ? null : request.FromEmail.Trim();
            setting.FromName = string.IsNullOrWhiteSpace(request.FromName) ? null : request.FromName.Trim();
            await _orm.Insert(setting).ExecuteAffrowsAsync();
        }
        else
        {
            setting.Host = request.Host?.Trim() ?? "";
            setting.Port = request.Port;
            setting.UseSsl = request.UseSsl;
            setting.Username = request.Username?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                setting.Password = request.Password;
            }
            setting.FromEmail = string.IsNullOrWhiteSpace(request.FromEmail) ? null : request.FromEmail.Trim();
            setting.FromName = string.IsNullOrWhiteSpace(request.FromName) ? null : request.FromName.Trim();
            setting.UpdatedAt = now;
            await _orm.Update<SmtpSetting>().SetSource(setting).ExecuteAffrowsAsync();
        }

        return Ok(new { message = "SMTP 配置已保存。" });
    }

    [HttpPost("smtp/test")]
    public async Task<IActionResult> TestSmtp([FromBody] TestEmailRequest request)
    {
        var ctx = _tenantContextAccessor.Current;
        if (ctx is null || !ctx.IsSuperAdmin) return Forbid();

        try
        {
            await _emailService.SendTestEmailAsync(request.Email);
            return Ok(new { message = "测试邮件已发送。" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public sealed class UpdateSmtpRequest
{
    public string? Host { get; set; }
    public int Port { get; set; } = 465;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
}

public sealed class TestEmailRequest
{
    public string Email { get; set; } = string.Empty;
}
