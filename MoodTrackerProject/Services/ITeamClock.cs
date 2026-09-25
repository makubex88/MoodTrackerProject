namespace MoodTrackerProject.Services;

/// <summary>
/// "Today" as the team experiences it. Injected everywhere a date is needed so tests can sit the
/// clock at 23:59 and 00:01 without touching the machine's time zone (D3).
/// </summary>
public interface ITeamClock
{
    TimeZoneInfo Zone { get; }
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}
