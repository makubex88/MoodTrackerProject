using Microsoft.Extensions.Logging.Abstractions;
using MoodTrackerProject.Data.Entities;
using MoodTrackerProject.Domain;
using MoodTrackerProject.Services;
using MoodTrackerProject.Tests.TestSupport;

namespace MoodTrackerProject.Tests.Services;

/// <summary>MS-09 and the paging edge cases: order, counts, clamping, and an empty page past the end.</summary>
public sealed class MoodServiceListTests : IDisposable
{
    private readonly SqliteTestDb _db = new();

    private MoodService CreateService()
    {
        var (clock, _) = Clocks.Melbourne_At("2026-09-23T05:00:00Z");
        return new MoodService(_db.CreateContext(), clock, new SqliteDuplicateKeyDetector(), NullLogger<MoodService>.Instance);
    }

    private async Task SeedAsync(params (Guid Participant, MoodRating Rating, string CreatedUtc, string Day)[] rows)
    {
        using var context = _db.CreateContext();
        foreach (var r in rows)
        {
            context.MoodEntries.Add(new MoodEntry
            {
                ParticipantId = r.Participant,
                Rating = r.Rating,
                EntryDate = DateOnly.Parse(r.Day),
                CreatedAtUtc = DateTime.Parse(r.CreatedUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal),
            });
        }
        await context.SaveChangesAsync();
    }

    [Fact(DisplayName = "MS-09 entries come back strictly newest-first by instant, regardless of insert order")]
    public async Task ListAll_IsNewestFirst()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        await SeedAsync(
            (a, MoodRating.NotGoodAtAll, "2026-09-22T07:40:00Z", "2026-09-22"),   // oldest, inserted first
            (b, MoodRating.FeelingGreat, "2026-09-22T23:14:05Z", "2026-09-23"),   // newest, inserted second
            (c, MoodRating.ABitMeh, "2026-09-22T22:51:00Z", "2026-09-23"));       // middle, inserted last

        var page = await CreateService().ListAllAsync(1, 50, CancellationToken.None);

        page.Items.Select(e => e.ParticipantId).Should().ContainInOrder(b, c, a);
        page.Total.Should().Be(3);
    }

    [Fact(DisplayName = "Counts are over every row, not just the page")]
    public async Task Counts_CoverAllRows()
    {
        var rows = Enumerable.Range(0, 7).Select(i => (Guid.NewGuid(), (MoodRating)(i % 4 + 1), $"2026-09-2{i % 3}T0{i}:00:00Z", "2026-09-23")).ToArray();
        await SeedAsync(rows);

        var page = await CreateService().ListAllAsync(1, 2, CancellationToken.None);

        page.Items.Should().HaveCount(2);
        page.Total.Should().Be(7);
        page.CountsByRating.Should().HaveCount(4);
        page.CountsByRating["1"].Should().Be(2);
        page.CountsByRating["2"].Should().Be(2);
        page.CountsByRating["3"].Should().Be(2);
        page.CountsByRating["4"].Should().Be(1);
        page.CountsByRating.Values.Sum().Should().Be(7);
    }

    [Fact(DisplayName = "An empty database lists nothing but still reports four zero counts")]
    public async Task Empty_IsWellFormed()
    {
        var page = await CreateService().ListAllAsync(1, 50, CancellationToken.None);

        page.Items.Should().BeEmpty();
        page.Total.Should().Be(0);
        page.CountsByRating.Should().Equal(new Dictionary<string, int> { ["1"] = 0, ["2"] = 0, ["3"] = 0, ["4"] = 0 });
    }

    [Fact(DisplayName = "Paging: page 2 continues where page 1 stopped; a page past the end is empty with the same total")]
    public async Task Paging_Works()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        await SeedAsync(ids.Select((id, i) => (id, MoodRating.PrettyGood, $"2026-09-23T0{i}:00:00Z", "2026-09-23")).ToArray());
        var service = CreateService();

        var p1 = await service.ListAllAsync(1, 2, CancellationToken.None);
        var p2 = await service.ListAllAsync(2, 2, CancellationToken.None);
        var p3 = await service.ListAllAsync(3, 2, CancellationToken.None);
        var p9 = await service.ListAllAsync(9, 2, CancellationToken.None);

        p1.Items.Select(e => e.ParticipantId).Should().Equal(ids[4], ids[3]);
        p2.Items.Select(e => e.ParticipantId).Should().Equal(ids[2], ids[1]);
        p3.Items.Select(e => e.ParticipantId).Should().Equal(ids[0]);
        p9.Items.Should().BeEmpty();
        p9.Total.Should().Be(5);
    }

    [Theory(DisplayName = "Paging: page < 1 becomes 1 and pageSize is clamped to 1..200")]
    [InlineData(0, 50, 1, 50)]
    [InlineData(-3, 0, 1, 1)]
    [InlineData(2, 5000, 2, 200)]
    public async Task Paging_IsClamped(int page, int pageSize, int expectedPage, int expectedSize)
    {
        var result = await CreateService().ListAllAsync(page, pageSize, CancellationToken.None);

        result.Page.Should().Be(expectedPage);
        result.PageSize.Should().Be(expectedSize);
    }

    [Fact(DisplayName = "Two entries with the same instant are ordered by id descending so the order is stable")]
    public async Task SameInstant_TieBreaksById()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        await SeedAsync(
            (a, MoodRating.PrettyGood, "2026-09-23T01:00:00Z", "2026-09-23"),
            (b, MoodRating.PrettyGood, "2026-09-23T01:00:00Z", "2026-09-23"));

        var page = await CreateService().ListAllAsync(1, 50, CancellationToken.None);

        page.Items.Select(e => e.ParticipantId).Should().Equal(b, a);
    }

    public void Dispose() => _db.Dispose();
}
