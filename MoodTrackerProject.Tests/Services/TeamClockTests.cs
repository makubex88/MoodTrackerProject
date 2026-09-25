using MoodTrackerProject.Services;
using MoodTrackerProject.Tests.TestSupport;

namespace MoodTrackerProject.Tests.Services;

/// <summary>"Today" is the team's day. These pin the conversion, including daylight saving, and the startup guard.</summary>
public sealed class TeamClockTests
{
    [Theory(DisplayName = "Today is computed in the team zone, not UTC")]
    [InlineData("2026-09-22T13:59:59Z", "2026-09-22")] // 23:59:59 AEST (+10)
    [InlineData("2026-09-22T14:00:00Z", "2026-09-23")] // 00:00:00 AEST — the day flips at UTC 14:00 in winter
    [InlineData("2026-09-22T23:14:05Z", "2026-09-23")] // 09:14 AEST: UTC still says the 22nd
    [InlineData("2026-09-23T00:30:00Z", "2026-09-23")] // 10:30 AEST: same team day as the previous case
    public void Today_IsTeamLocal(string utc, string expectedDay)
    {
        var (clock, _) = Clocks.Melbourne_At(utc);

        clock.Today.Should().Be(DateOnly.Parse(expectedDay));
    }

    [Theory(DisplayName = "Daylight saving is honoured: after the October switch the day flips at UTC 13:00, not 14:00")]
    [InlineData("2026-10-10T12:59:59Z", "2026-10-10")] // 23:59:59 AEDT (+11)
    [InlineData("2026-10-10T13:00:00Z", "2026-10-11")] // 00:00:00 AEDT — a fixed +10 offset would still say the 10th
    public void Today_HonoursDaylightSaving(string utc, string expectedDay)
    {
        var (clock, _) = Clocks.Melbourne_At(utc);

        clock.Today.Should().Be(DateOnly.Parse(expectedDay));
    }

    [Fact(DisplayName = "UtcNow is passed through from the time provider unchanged")]
    public void UtcNow_IsUnchanged()
    {
        var (clock, _) = Clocks.Melbourne_At("2026-09-22T23:14:05Z");

        clock.UtcNow.Should().Be(new DateTime(2026, 9, 22, 23, 14, 5, DateTimeKind.Utc));
        clock.UtcNow.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory(DisplayName = "The startup guard resolves real IANA ids and rejects nonsense")]
    [InlineData("Australia/Melbourne", true)]
    [InlineData("UTC", true)]
    [InlineData("Mars/Olympus_Mons", false)]
    [InlineData("", false)]
    public void CanResolve_Guard(string id, bool expected)
    {
        TeamClockOptions.CanResolve(id).Should().Be(expected);
    }
}
