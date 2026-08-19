using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.Application.Features.Auth.Commands.ChangePassword;
using SocietyManagement.Application.Features.Auth.Commands.ForgotPassword;
using SocietyManagement.Application.Features.Auth.Commands.Login;
using SocietyManagement.Application.Features.Auth.Commands.Logout;
using SocietyManagement.Application.Features.Auth.Commands.RefreshToken;
using SocietyManagement.Application.Features.Auth.Commands.ResetPassword;
using SocietyManagement.Application.Features.Auth.Commands.VerifyOtp;
using SocietyManagement.Application.Features.Users.Queries.GetProfile;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Controllers;

/// <summary>Login (email or mobile), refresh, logout, forgot/reset password with
/// OTP, self-service change password, and "who am I" profile retrieval.</summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginCommand command)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await Mediator.Send(command with { IpAddress = ip });
        return Ok(ApiResponse<object>.SuccessResponse(result, "Login successful."));
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await Mediator.Send(command with { IpAddress = ip });
        return Ok(ApiResponse<object>.SuccessResponse(result, "Token refreshed."));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(LogoutCommand command)
    {
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Logged out."));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command)
    {
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("If an account exists, an OTP has been sent."));
    }

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp(VerifyOtpCommand command)
    {
        var isValid = await Mediator.Send(command);
        return Ok(ApiResponse<bool>.SuccessResponse(isValid, isValid ? "OTP verified." : "Invalid or expired OTP."));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command)
    {
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Password has been reset. Please log in with your new password."));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordCommand command)
    {
        await Mediator.Send(command);
        return Ok(ApiResponse.SuccessResponse("Password changed successfully."));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var result = await Mediator.Send(new GetProfileQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }
}
