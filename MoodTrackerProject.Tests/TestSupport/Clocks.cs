using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MoodTrackerProject.Services;

namespace MoodTrackerProject.Tests.TestSupport;

/// <summary>
/// The real <see cref="TeamClock"/> over a fake time source, so tests exercise the actual
/// UTC-to-team-zone conversion (including daylight saving) rather than a stub that agrees with itself.
/// </summary>
public static class Clocks
{
    public const string Melbourne = "Australia/Melbourne";

    public static (TeamClock Clock, FakeTimeProvider Time) Melbourne_At(string utcIso)
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse(utcIso, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal));
        var clock = new TeamClock(time, Options.Create(new TeamClockOptions { TimeZone = Melbourne }));
        return (clock, time);
    }
}
