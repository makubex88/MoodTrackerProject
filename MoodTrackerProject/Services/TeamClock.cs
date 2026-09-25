using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace MoodTrackerProject.Services;

public sealed class TeamClockOptions
{
    public const string SectionName = "TeamClock";

    /// <summary>IANA id (e.g. Australia/Melbourne). Resolved once at startup; the app refuses to start if it can't be.</summary>
    [Required]
    public string TimeZone { get; set; } = "Australia/Melbourne";

    public static bool CanResolve(string id)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return false;
        }
    }
}

public sealed class TeamClock : ITeamClock
{
    private readonly TimeProvider _time;

    public TeamClock(TimeProvider time, IOptions<TeamClockOptions> options)
    {
        _time = time;
        Zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
    }

    public TimeZoneInfo Zone { get; }

    public DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, Zone));
}
