using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MoodTrackerProject.Contracts;

namespace MoodTrackerProject.Tests.Integration;

/// <summary>IT-01 … IT-04 and the validation edge cases, end to end against real MySQL.</summary>
[Collection(MySqlApiCollection.Name)]
public sealed class ParticipantApiTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "IT-00 first contact issues the identity cookie and reports logged:false")]
    public async Task FirstContact_IssuesCookie()
    {
        using var browser = fixture.NewBrowser();

        var response = await browser.GetAsync("/api/moods/today");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Set-Cookie").Should().ContainSingle(c => c.StartsWith("mt_pid=") && c.Contains("httponly"));
        var body = await response.Content.ReadFromJsonAsync<TodayResponse>(Json);
        body!.Logged.Should().BeFalse();
        body.TimeZone.Should().Be("Australia/Melbourne");
    }

    [Fact(DisplayName = "IT-01 the same browser gets 201 then 409, and the 409 is problem details with entryDate")]
    public async Task SameBrowser_201Then409()
    {
        using var browser = fixture.NewBrowser();
        await browser.GetAsync("/api/moods/today");

        var first = await browser.PostAsJsonAsync("/api/moods", new { rating = 3, comment = "Release went out clean." });
        var second = await browser.PostAsJsonAsync("/api/moods", new { rating = 4, comment = (string?)null });

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        first.Headers.Location.Should().NotBeNull();
        var created = await first.Content.ReadFromJsonAsync<MoodResponse>(Json);
        created!.Rating.Should().Be(3);
        created.Comment.Should().Be("Release went out clean.");

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        second.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var problem = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(409);
        problem.RootElement.GetProperty("detail").GetString().Should().Be("You've already logged your mood for today. Come back tomorrow.");
        problem.RootElement.GetProperty("entryDate").GetString().Should().Be(created.EntryDate.ToString("yyyy-MM-dd"));

        var today = await browser.GetFromJsonAsync<TodayResponse>("/api/moods/today", Json);
        today!.Logged.Should().BeTrue();
        today.Entry!.Rating.Should().Be(3, "the second attempt never overwrote the first");
    }

    [Fact(DisplayName = "IT-03 two different browsers both succeed on the same day")]
    public async Task TwoBrowsers_BothSucceed()
    {
        using var a = fixture.NewBrowser();
        using var b = fixture.NewBrowser();

        var ra = await a.PostAsJsonAsync("/api/moods", new { rating = 1 });
        var rb = await b.PostAsJsonAsync("/api/moods", new { rating = 4 });

        ra.StatusCode.Should().Be(HttpStatusCode.Created);
        rb.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "IT-04 two simultaneous posts from one browser: exactly one 201 and one 409")]
    public async Task SimultaneousPosts_ExactlyOneWins()
    {
        using var browser = fixture.NewBrowser();
        await browser.GetAsync("/api/moods/today"); // establish the cookie first so both posts share it

        var posts = await Task.WhenAll(
            browser.PostAsJsonAsync("/api/moods", new { rating = 2, comment = "tab A" }),
            browser.PostAsJsonAsync("/api/moods", new { rating = 3, comment = "tab B" }));

        posts.Select(p => p.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });

        var today = await browser.GetFromJsonAsync<TodayResponse>("/api/moods/today", Json);
        today!.Logged.Should().BeTrue();
    }

    [Theory(DisplayName = "Validation: a rating outside 1–4, a missing rating, or an over-long comment is a 400 with field errors")]
    [InlineData("{\"rating\":0}", "Rating")]
    [InlineData("{\"rating\":5}", "Rating")]
    [InlineData("{}", "Rating")]
    [InlineData("{\"rating\":\"great\"}", "rating")]
    public async Task Validation_Returns400(string body, string expectedField)
    {
        using var browser = fixture.NewBrowser();

        var response = await browser.PostAsync("/api/moods", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").EnumerateObject()
            .Select(p => p.Name).Should().Contain(name => name.Contains(expectedField, StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "Validation: a 501-character comment is refused and nothing is written")]
    public async Task LongComment_Returns400()
    {
        using var browser = fixture.NewBrowser();

        var response = await browser.PostAsJsonAsync("/api/moods", new { rating = 3, comment = new string('x', 501) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var today = await browser.GetFromJsonAsync<TodayResponse>("/api/moods/today", Json);
        today!.Logged.Should().BeFalse();
    }

    [Fact(DisplayName = "A comment with newlines and unicode survives the round trip intact")]
    public async Task Comment_RoundTrips()
    {
        using var browser = fixture.NewBrowser();
        const string comment = "Line one.\nLine two — with “quotes” and an emoji 🙂";

        await browser.PostAsJsonAsync("/api/moods", new { rating = 4, comment });
        var today = await browser.GetFromJsonAsync<TodayResponse>("/api/moods/today", Json);

        today!.Entry!.Comment.Should().Be(comment);
    }

    [Fact(DisplayName = "A forged or garbage cookie does not grant another participant's entry; it becomes a new identity")]
    public async Task GarbageCookie_IsReplaced()
    {
        using var raw = fixture.RawClient();
        raw.DefaultRequestHeaders.Add("Cookie", "mt_pid=not-a-guid");

        var response = await raw.GetAsync("/api/moods/today");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Set-Cookie").Should().ContainSingle(c => c.StartsWith("mt_pid="));
    }
}
