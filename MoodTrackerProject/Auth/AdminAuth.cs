using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace MoodTrackerProject.Auth;

/// <summary>
/// Admin access (D7): ASP.NET Core's cookie authentication scheme with one configured credential and
/// role-based authorisation — the framework's own pipeline, without the Identity user store. Anyone
/// who later wants real accounts changes where the <see cref="ClaimsPrincipal"/> comes from; the
/// endpoints, the role check and the cookie scheme stay as they are.
/// </summary>
public static class AdminAuth
{
    public const string Scheme = "AdminCookie";
    public const string CookieName = "mt_admin";
    public const string Role = "Admin";
    public const string Policy = "AdminOnly";

    public static IServiceCollection AddAdminAuth(this IServiceCollection services)
    {
        services.AddOptions<AdminOptions>()
            .BindConfiguration(AdminOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(o => !string.IsNullOrWhiteSpace(o.Password), "Admin:Password must be configured.")
            .ValidateOnStart();

        services.AddAuthentication(Scheme)
            .AddCookie(Scheme, options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.Path = "/";
                options.SlidingExpiration = true;

                // This is an API: never redirect to a login page, answer with the status code.
                options.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        // ExpireTimeSpan depends on configuration, so it is applied once options are available.
        services.AddSingleton<IConfigureOptions<CookieAuthenticationOptions>, ConfigureAdminCookieLifetime>();

        services.AddAuthorizationBuilder()
            .AddPolicy(Policy, p => p.RequireAuthenticatedUser().RequireRole(Role));

        return services;
    }

    /// <summary>
    /// Fixed-time comparison. Both sides are hashed first so their lengths are equal and the compare
    /// cannot short-circuit on a length mismatch either.
    /// </summary>
    public static bool PasswordMatches(string? supplied, string? configured)
    {
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(supplied ?? string.Empty));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(configured ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    public static ClaimsPrincipal CreateAdminPrincipal()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, Role),
        ], Scheme);

        return new ClaimsPrincipal(identity);
    }

    public static AuthenticationProperties SessionProperties(TimeProvider time, AdminOptions options) => new()
    {
        IsPersistent = true,
        IssuedUtc = time.GetUtcNow(),
        ExpiresUtc = time.GetUtcNow().AddHours(options.SessionHours),
    };

    private sealed class ConfigureAdminCookieLifetime(IOptions<AdminOptions> admin)
        : IConfigureNamedOptions<CookieAuthenticationOptions>
    {
        public void Configure(string? name, CookieAuthenticationOptions options)
        {
            if (name == Scheme)
            {
                options.ExpireTimeSpan = TimeSpan.FromHours(admin.Value.SessionHours);
            }
        }

        public void Configure(CookieAuthenticationOptions options) => Configure(Options.DefaultName, options);
    }
}
