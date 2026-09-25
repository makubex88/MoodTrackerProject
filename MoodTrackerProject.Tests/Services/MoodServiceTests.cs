using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoodTrackerProject.Domain;
using MoodTrackerProject.Services;
using MoodTrackerProject.Tests.TestSupport;

namespace MoodTrackerProject.Tests.Services;

/// <summary>The once-per-day rule, its boundaries, and the input normalisation around it (MS-01 … MS-08).</summary>
public sealed class MoodServiceTests : IDisposable
{
    private readonly SqliteTestDb _db = new();
    private static readonly Guid Alice = Guid.Parse("8f3c1a2e-5d7b-4e9a-b2c1-0f6d3a8e7c21");
    private static readonly Guid Bob = Guid.Parse("2d91f0a3-6c4e-4b18-9a5d-7e2b1c9fe4af");

    private MoodService CreateService(TeamClock clock) =>
        new(_db.CreateContext(), clock, new SqliteDuplicateKeyDetector(), NullLogger<MoodService>.Instance);

    [Fact(DisplayName = "MS-01 first entry of the day is created with the team-local date")]
    public async Task FirstEntry_IsCreated_WithTeamLocalDate()
    {
        // 23:14 UTC on the 22nd is 09:14 AEST on the 23rd.
        var (clock, _) = Clocks.Melbourne_At("2026-09-22T23:14:05Z");
        var service = CreateService(clock);

        var result = await service.CreateAsync(Alice, MoodRating.PrettyGood, "Release went out clean.", CancellationToken.None);

        result.Outcome.Should().Be(CreateMoodOutcome.Created);
        result.Entry.Id.Should().BePositive();
        result.Entry.ParticipantId.Should().Be(Alice);
        result.Entry.Rating.Should().Be(MoodRating.PrettyGood);
        result.Entry.EntryDate.Should().Be(new DateOnly(2026, 9, 23), "the day is the team's day, not the UTC day");
        result.Entry.CreatedAtUtc.Should().Be(new DateTime(2026, 9, 22, 23, 14, 5, DateTimeKind.Utc));
    }

    [Fact(DisplayName = "MS-02 a second entry on the same day is refused and nothing is written")]
    public async Task SecondEntrySameDay_IsAlreadyLogged_AndRowCountUnchanged()
    {
        var (clock, time) = Clocks.Melbourne_At("2026-09-22T23:14:05Z");
        var service = CreateService(clock);
        var first = await service.CreateAsync(Alice, MoodRating.PrettyGood, null, CancellationToken.None);

        time.Advance(TimeSpan.FromHours(3)); // still the 23rd in Melbourne
        var second = await service.CreateAsync(Alice, MoodRating.FeelingGreat, "trying again", CancellationToken.None);

        second.Outcome.Should().Be(CreateMoodOutcome.AlreadyLogged);
        second.Entry.Id.Should().Be(first.Entry.Id, "the existing row is returned so the client can render it");
        second.Entry.Rating.Should().Be(MoodRating.PrettyGood, "the original is never overwritten");

        using var check = _db.CreateContext();
        (await check.MoodEntries.CountAsync()).Should().Be(1);
    }

    [Fact(DisplayName = "MS-02b different participants on the same day are both accepted")]
    public async Task DifferentParticipants_SameDay_BothCreated()
    {
        var (clock, _) = Clocks.Melbourne_At("2026-09-22T23:14:05Z");
        var service = CreateService(clock);

        var a = await service.CreateAsync(Alice, MoodRating.ABitMeh, null, CancellationToken.None);
        var b = await service.CreateAsync(Bob, MoodRating.NotGoodAtAll, null, CancellationToken.None);

        a.Outcome.Should().Be(CreateMoodOutcome.Created);
        b.Outcome.Should().Be(CreateMoodOutcome.Created);
    }

    [Theory(DisplayName = "MS-04 a rating outside 1–4 is rejected before touching the database")]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(255)]
    public async Task RatingOutOfRange_Throws(byte raw)
    {
        var (clock, _) = Clocks.Melbourne_At("2026-09-22T23:14:05Z");
        var service = CreateService(clock);

        var act = () => service.CreateAsync(Alice, (MoodRating)raw, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        using var check = _db.CreateContext();
        (await check.MoodEntries.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "MS-05 a 500-character comment is accepted; 501 is rejected")]
    public async Task CommentLength_Boundary()
    {
        var (clock, _) = Clocks.Melbourne_At("2026-09-22T23:14:05Z");
        var service = CreateService(clock);

        var ok = await service.CreateAsync(Alice, MoodRating.PrettyGood, new string('x', 500), CancellationToken.None);
        ok.Outcome.Should().Be(CreateMoodOutcome.Created);
        ok.Entry.Comment.Should().HaveLength(500);

        var act = () => service.CreateAsync(Bob, MoodRating.PrettyGood, new string('x', 501), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory(DisplayName = "MS-06 whitespace-only comments are stored as NULL; real ones are trimmed")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("\t\n", null)]
    [InlineData("  fine  ", "fine")]
    [InlineData("line one\nline two", "line one\nline two")]
    public async Task Comment_IsNormalised(string? input, string? expected)
    {
        var (clock, _) = Clocks.Melbourne_At("2026-09-22T23:14:05Z");
        var service = CreateService(clock);

        var result = await service.CreateAsync(Guid.NewGuid(), MoodRating.PrettyGood, input, CancellationToken.None);

        result.Entry.Comment.Should().Be(expected);
    }

    [Fact(DisplayName = "MS-07 23:59 and 00:01 team-local are different days — both accepted")]
    public async Task EitherSideOfTeamMidnight_BothAccepted()
    {
        // 13:59 UTC = 23:59 AEST (+10) on the 22nd; 14:01 UTC = 00:01 on the 23rd.
        var (clock, time) = Clocks.Melbourne_At("2026-09-22T13:59:00Z");
        var service = CreateService(clock);

        var late = await service.CreateAsync(Alice, MoodRating.ABitMeh, null, CancellationToken.None);
        time.SetUtcNow(DateTimeOffset.Parse("2026-09-22T14:01:00Z"));
        var early = await service.CreateAsync(Alice, MoodRating.FeelingGreat, null, CancellationToken.None);

        late.Outcome.Should().Be(CreateMoodOutcome.Created);
        early.Outcome.Should().Be(CreateMoodOutcome.Created);
        late.Entry.EntryDate.Should().Be(new DateOnly(2026, 9, 22));
        early.Entry.EntryDate.Should().Be(new DateOnly(2026, 9, 23));
    }

    [Fact(DisplayName = "MS-08 crossing UTC midnight without crossing team midnight is still the same day — refused")]
    public async Task CrossingUtcMidnightOnly_IsRefused()
    {
        // 23:30 UTC on the 22nd = 09:30 AEST on the 23rd; 00:30 UTC on the 23rd = 10:30 AEST on the 23rd.
        // A UTC-based day boundary would accept both. The team-local one must not.
        var (clock, time) = Clocks.Melbourne_At("2026-09-22T23:30:00Z");
        var service = CreateService(clock);

        var first = await service.CreateAsync(Alice, MoodRating.PrettyGood, null, CancellationToken.None);
        time.SetUtcNow(DateTimeOffset.Parse("2026-09-23T00:30:00Z"));
        var second = await service.CreateAsync(Alice, MoodRating.NotGoodAtAll, null, CancellationToken.None);

        first.Outcome.Should().Be(CreateMoodOutcome.Created);
        second.Outcome.Should().Be(CreateMoodOutcome.AlreadyLogged);
        second.Entry.Id.Should().Be(first.Entry.Id);
    }

    [Fact(DisplayName = "GetToday returns null before logging and the entry afterwards")]
    public async Task GetToday_ReflectsState()
    {
        var (clock, _) = Clocks.Melbourne_At("2026-09-22T23:14:05Z");
        var service = CreateService(clock);

        (await service.GetTodayAsync(Alice, CancellationToken.None)).Should().BeNull();
        await service.CreateAsync(Alice, MoodRating.PrettyGood, null, CancellationToken.None);
        var today = await service.GetTodayAsync(Alice, CancellationToken.None);

        today.Should().NotBeNull();
        today!.Rating.Should().Be(MoodRating.PrettyGood);
        (await service.GetTodayAsync(Bob, CancellationToken.None)).Should().BeNull("another participant's entry is not mine");
    }

    [Fact(DisplayName = "GetToday does not return yesterday's entry")]
    public async Task GetToday_IgnoresYesterday()
    {
        var (clock, time) = Clocks.Melbourne_At("2026-09-22T23:14:05Z");
        var service = CreateService(clock);
        await service.CreateAsync(Alice, MoodRating.PrettyGood, null, CancellationToken.None);

        time.Advance(TimeSpan.FromDays(1));

        (await service.GetTodayAsync(Alice, CancellationToken.None)).Should().BeNull();
    }

    public void Dispose() => _db.Dispose();
}
