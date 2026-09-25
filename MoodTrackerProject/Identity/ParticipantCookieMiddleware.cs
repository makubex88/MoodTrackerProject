namespace MoodTrackerProject.Identity;

/// <summary>
/// Identification without authentication (D1). Every API request carries an opaque GUID in an
/// HttpOnly cookie; the server mints it on first contact and never learns anything else about the
/// caller. This identifies a browser, not a person — a limitation the brief creates by forbidding
/// authentication, and one the README states plainly.
///
/// Cookie flags: HttpOnly (script can't read or forge it), SameSite=Lax, Path=/, no Domain
/// (host-only), Secure only when the request itself was HTTPS so it still works on http://localhost.
/// </summary>
public sealed class ParticipantCookieMiddleware(RequestDelegate next)
{
    public const string CookieName = "mt_pid";
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    public Task InvokeAsync(HttpContext context, ParticipantContext participant)
    {
        if (context.Request.Cookies.TryGetValue(CookieName, out var raw) && Guid.TryParseExact(raw, "D", out var existing))
        {
            participant.ParticipantId = existing;
            participant.IsNew = false;
        }
        else
        {
            var minted = Guid.NewGuid();
            participant.ParticipantId = minted;
            participant.IsNew = true;

            context.Response.Cookies.Append(CookieName, minted.ToString("D"), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                Path = "/",
                MaxAge = Lifetime,
                IsEssential = true,
            });
        }

        return next(context);
    }
}
