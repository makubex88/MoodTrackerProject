using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MoodTrackerProject.Data.Entities;
using MoodTrackerProject.Domain;
using MoodTrackerProject.Services;
using MoodTrackerProject.Tests.TestSupport;

namespace MoodTrackerProject.Tests.Services;

/// <summary>
/// MS-03 / DB-01: the read-check passes, and then another writer wins before our INSERT runs.
/// A SaveChanges interceptor inserts the competing row at exactly that moment, so the unique index
/// — not the service's read-check — is what refuses the second write. The service must translate the
/// constraint violation into the same AlreadyLogged outcome and hand back the row that actually won.
/// </summary>
public sealed class MoodServiceRaceTests : IDisposable
{
    private readonly SqliteTestDb _db = new();
    private static readonly Guid Alice = Guid.Parse("8f3c1a2e-5d7b-4e9a-b2c1-0f6d3a8e7c21");

    [Fact(DisplayName = "MS-03 a competing insert between read-check and write is refused by the index and mapped to AlreadyLogged")]
    public async Task CompetingWriter_IsCaughtByIndex_AndMappedToAlreadyLogged()
    {
        var (clock, _) = Clocks.Melbourne_At("2026-09-22T23:14:05Z");
        var day = clock.Today;

        var competitor = new CompetingWriterInterceptor(_db, Alice, day, clock.UtcNow.AddMilliseconds(-4));
        var service = new MoodService(
            _db.CreateContext(competitor),
            clock,
            new SqliteDuplicateKeyDetector(),
            NullLogger<MoodService>.Instance);

        var result = await service.CreateAsync(Alice, MoodRating.FeelingGreat, "tab B", CancellationToken.None);

        competitor.Fired.Should().BeTrue("the race must actually have happened for this test to mean anything");
        result.Outcome.Should().Be(CreateMoodOutcome.AlreadyLogged);
        result.Entry.Rating.Should().Be(MoodRating.PrettyGood, "the winner's row is returned, not the loser's");
        result.Entry.Comment.Should().Be("tab A");

        using var check = _db.CreateContext();
        (await check.MoodEntries.CountAsync()).Should().Be(1, "exactly one row survives, whichever request won");
    }

    [Fact(DisplayName = "DB-01 the unique index itself rejects a second row for the same participant and day")]
    public async Task UniqueIndex_RejectsDuplicate()
    {
        using var context = _db.CreateContext();
        context.MoodEntries.Add(Row(Alice, new DateOnly(2026, 9, 23)));
        await context.SaveChangesAsync();

        context.MoodEntries.Add(Row(Alice, new DateOnly(2026, 9, 23)));
        var act = () => context.SaveChangesAsync();

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        new SqliteDuplicateKeyDetector().IsDuplicateKey(ex.Which).Should().BeTrue();
    }

    [Fact(DisplayName = "DB-02 same participant on different days, and different participants on the same day, are allowed")]
    public async Task UniqueIndex_AllowsOtherCombinations()
    {
        using var context = _db.CreateContext();
        context.MoodEntries.Add(Row(Alice, new DateOnly(2026, 9, 23)));
        context.MoodEntries.Add(Row(Alice, new DateOnly(2026, 9, 24)));
        context.MoodEntries.Add(Row(Guid.NewGuid(), new DateOnly(2026, 9, 23)));

        await context.SaveChangesAsync();

        (await context.MoodEntries.CountAsync()).Should().Be(3);
    }

    private static MoodEntry Row(Guid participant, DateOnly day) => new()
    {
        ParticipantId = participant,
        Rating = MoodRating.PrettyGood,
        EntryDate = day,
        CreatedAtUtc = DateTime.UtcNow,
    };

    /// <summary>Inserts the "other tab's" row the moment the service is about to save, once.</summary>
    private sealed class CompetingWriterInterceptor(SqliteTestDb db, Guid participant, DateOnly day, DateTime createdAtUtc) : SaveChangesInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!Fired)
            {
                Fired = true;
                using var other = db.CreateContext(); // no interceptor: the competitor writes plainly
                other.MoodEntries.Add(new MoodEntry
                {
                    ParticipantId = participant,
                    Rating = MoodRating.PrettyGood,
                    Comment = "tab A",
                    EntryDate = day,
                    CreatedAtUtc = createdAtUtc,
                });
                await other.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }

    public void Dispose() => _db.Dispose();
}
