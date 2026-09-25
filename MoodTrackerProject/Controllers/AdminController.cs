using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MoodTrackerProject.Auth;
using MoodTrackerProject.Contracts;

namespace MoodTrackerProject.Controllers;

/// <summary>Admin sign-in. The only anonymous route on the admin side is the login itself.</summary>
[ApiController]
[Route("api/admin")]
public sealed class AdminController(IOptions<AdminOptions> options, TimeProvider time, ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>Exchange the configured password for an admin ticket cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        if (!AdminAuth.PasswordMatches(request.Password, options.Value.Password))
        {
            logger.LogWarning("Admin sign-in failed from {RemoteIp}.", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized();
        }

        await HttpContext.SignInAsync(
            AdminAuth.Scheme,
            AdminAuth.CreateAdminPrincipal(),
            AdminAuth.SessionProperties(time, options.Value));

        logger.LogInformation("Admin signed in.");
        return NoContent();
    }

    /// <summary>Is this browser signed in as admin? Lets the client route guard decide before rendering.</summary>
    [HttpGet("me")]
    [Authorize(Policy = AdminAuth.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me() => NoContent();

    [HttpPost("logout")]
    [Authorize(Policy = AdminAuth.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AdminAuth.Scheme);
        return NoContent();
    }
}
