using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Contracts.Auth;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public AuthController(AuthService authService, TenantContextAccessor tenantContextAccessor)
    {
        _authService = authService;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            return Ok(await _authService.LoginAsync(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("renew")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> RenewToken()
    {
        var ctx = _tenantContextAccessor.Current;
        if (ctx is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _authService.RenewTokenAsync(ctx));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var ctx = _tenantContextAccessor.Current;
        if (ctx is null)
        {
            return Unauthorized();
        }

        try
        {
            await _authService.ChangePasswordAsync(ctx.UserId, request.CurrentPassword, request.NewPassword);
            return Ok(new { message = "密码已修改。" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("request-reset")]
    public async Task<IActionResult> RequestReset([FromBody] ResetPasswordRequest request)
    {
        try
        {
            var resetUrl = $"{Request.Scheme}://{Request.Host}/reset-password/confirm";
            await _authService.RequestPasswordResetAsync(request.Email, resetUrl);
            return Ok(new { message = "如果该邮箱已注册，您将收到一封重置密码的邮件。" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("confirm-reset")]
    public async Task<IActionResult> ConfirmReset([FromBody] ConfirmResetPasswordRequest request)
    {
        try
        {
            await _authService.ConfirmPasswordResetAsync(request.Token, request.NewPassword);
            return Ok(new { message = "密码已重置，请使用新密码登录。" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
