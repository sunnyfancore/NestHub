using FreeSql;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;
using NestHub.Api.Domain.Entities;

namespace NestHub.Api.Services;

public sealed class EmailService
{
    private readonly IFreeSql _orm;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IFreeSql orm, ILogger<EmailService> logger)
    {
        _orm = orm;
        _logger = logger;
    }

    public async Task<bool> IsConfiguredAsync()
    {
        var setting = await _orm.Select<SmtpSetting>().FirstAsync();
        return setting is not null && !string.IsNullOrWhiteSpace(setting.Host);
    }

    private async Task<SmtpSetting?> GetConfigAsync()
    {
        return await _orm.Select<SmtpSetting>().FirstAsync();
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken, string resetUrl)
    {
        var config = await GetConfigAsync();
        if (config is null || string.IsNullOrWhiteSpace(config.Host))
        {
            throw new InvalidOperationException("邮件服务未配置。");
        }

        var link = $"{resetUrl}?token={Uri.EscapeDataString(resetToken)}";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config.FromName ?? "NestHub", config.FromEmail ?? config.Username));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "NestHub - 重置密码";

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = $"""
                <div style="max-width:560px;margin:0 auto;font-family:sans-serif;padding:32px;background:#f9fafb;border-radius:12px;">
                  <h2 style="color:#1a1a2e;margin:0 0 16px;">重置您的密码</h2>
                  <p style="color:#555;font-size:15px;line-height:1.6;">您收到此邮件是因为有人请求重置您的 NestHub 账户密码。</p>
                  <p style="color:#555;font-size:15px;line-height:1.6;">请点击下方按钮设置新密码，此链接 30 分钟内有效：</p>
                  <div style="margin:24px 0;text-align:center;">
                    <a href="{link}" style="background:#5b9aff;color:#fff;padding:12px 32px;border-radius:8px;text-decoration:none;font-weight:600;display:inline-block;">重置密码</a>
                  </div>
                  <p style="color:#999;font-size:13px;">如果您没有请求重置密码，请忽略此邮件。</p>
                </div>
                """
        };
        message.Body = bodyBuilder.ToMessageBody();

        await SendAsync(config, message);
        _logger.LogInformation("Password reset email sent to {Email}", toEmail);
    }

    public async Task SendTestEmailAsync(string toEmail)
    {
        var config = await GetConfigAsync();
        if (config is null || string.IsNullOrWhiteSpace(config.Host))
        {
            throw new InvalidOperationException("邮件服务未配置，请先在安全设置中配置 SMTP。");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config.FromName ?? "NestHub", config.FromEmail ?? config.Username));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "NestHub - 邮件测试";
        message.Body = new TextPart("plain") { Text = "这是一封来自 NestHub 的测试邮件，如果您看到此邮件，说明邮件服务配置正确。" };

        await SendAsync(config, message);
        _logger.LogInformation("Test email sent to {Email}", toEmail);
    }

    private async Task SendAsync(SmtpSetting config, MimeMessage message)
    {
        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(config.Host, config.Port, config.UseSsl);
            if (!string.IsNullOrWhiteSpace(config.Username))
            {
                await client.AuthenticateAsync(config.Username, config.Password ?? "");
            }
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via {Host}:{Port}", config.Host, config.Port);
            throw new InvalidOperationException($"邮件发送失败：{ex.InnerException?.Message ?? ex.Message}");
        }
    }
}
