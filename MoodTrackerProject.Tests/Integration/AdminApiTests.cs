using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MoodTrackerProject.Contracts;

namespace MoodTrackerProject.Tests.Integration;

/// <summary>AD-01 … AD-04: the admin gate is enforced by the endpoint, and the ticket cannot be forged or tampered with.</summary>
[Collection(MySqlApiCollection.Name)]
public sealed class AdminApiTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "AD-01 the list is 401 with no ticket, and 401 with only a participant cookie")]
    public async Task List_WithoutTicket_Is401()
    {
        using var browser = fixture.NewBrowser();
        await browser.GetAsync("/api/moods/today"); // now holds a participant cookie, and nothing else

        var me = await browser.GetAsync("/api/admin/me");
        var list = await browser.GetAsync("/api/admin/moods");

        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        list.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await list.Content.ReadAsStringAsync()).Should().NotContain("\"items\"", "no rows leave the database");
    }

    [Fact(DisplayName = "AD-02 the wrong password is 401 and issues no admin ticket")]
    public async Task Login_WrongPassword_Is401()
    {
        using var browser = fixture.NewBrowser();

        var response = await browser.PostAsJsonAsync("/api/admin/login", new { password = "wrong-guess" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // A participant cookie (mt_pid) may well be issued here — any first contact mints one. What must
        // not appear is the admin ticket.
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];
        cookies.Should().NotContain(c => c.StartsWith("mt_admin="));
    }

    [Fact(DisplayName = "AD-02b a missing password is a 400, not a 401")]
    public async Task Login_MissingPassword_Is400()
    {
        using var browser = fixture.NewBrowser();

        var response = await browser.PostAsJsonAsync("/api/admin/login", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "AD-03 the right password issues an HttpOnly ticket; the list then returns entries newest-first with counts")]
    public async Task Login_ThenList_Works()
    {
        // Seed two entries from two browsers so the list has something ordered to show.
        using var alice = fixture.NewBrowser();
        using var bob = fixture.NewBrowser();
        await alice.PostAsJsonAsync("/api/moods", new { rating = 1, comment = "alice" });
        await bob.PostAsJsonAsync("/api/moods", new { rating = 4, comment = "bob" });

        using var admin = fixture.NewBrowser();
        var login = await admin.PostAsJsonAsync("/api/admin/login", new { password = MySqlApiFixture.AdminPassword });

        login.StatusCode.Should().Be(HttpStatusCode.NoContent);
        login.Headers.GetValues("Set-Cookie").Should().ContainSingle(c =>
            c.StartsWith("mt_admin=") && c.Contains("httponly") && c.Contains("samesite=lax"));

        (await admin.GetAsync("/api/admin/me")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var page = await admin.GetFromJsonAsync<AdminMoodPageResponse>("/api/admin/moods?page=1&pageSize=50", Json);
        page!.Total.Should().BeGreaterThanOrEqualTo(2);
        page.TimeZone.Should().Be("Australia/Melbourne");
        page.CountsByRating.Keys.Should().BeEquivalentTo(new[] { "1", "2", "3", "4" });
        page.CountsByRating.Values.Sum().Should().Be(page.Total);
        page.Items.Should().BeInDescendingOrder(e => e.CreatedAtUtc);
        page.Items.Select(e => e.Comment).Should().ContainInOrder("bob", "alice");
        page.Items.Select(e => e.ParticipantId).Should().AllSatisfy(id =>
            Guid.TryParse(id, out _).Should().BeTrue("participant ids are opaque GUIDs"));
    }

    [Fact(DisplayName = "AD-04 a tampered ticket is rejected as anonymous")]
    public async Task TamperedTicket_Is401()
    {
        using var admin = fixture.NewBrowser();
        var login = await admin.PostAsJsonAsync("/api/admin/login", new { password = MySqlApiFixture.AdminPassword });
        var setCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("mt_admin="));
        var ticket = setCookie.Split(';')[0]["mt_admin=".Length..];

        // Flip one character in the middle of the ciphertext.
        var mid = ticket.Length / 2;
        var tampered = ticket[..mid] + (ticket[mid] == 'A' ? 'B' : 'A') + ticket[(mid + 1)..];

        using var raw = fixture.RawClient();
        raw.DefaultRequestHeaders.Add("Cookie", $"mt_admin={tampered}");

        var response = await raw.GetAsync("/api/admin/moods");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Logout clears the ticket; the next list call is 401 again")]
    public async Task Logout_RevokesAccess()
    {
        using var admin = fixture.NewBrowser();
        await admin.PostAsJsonAsync("/api/admin/login", new { password = MySqlApiFixture.AdminPassword });
        (await admin.GetAsync("/api/admin/me")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var logout = await admin.PostAsync("/api/admin/logout", null);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync("/api/admin/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "The admin ticket grants nothing on the participant side: the admin browser is just another participant")]
    public async Task AdminTicket_DoesNotAffectParticipantFlow()
    {
        using var admin = fixture.NewBrowser();
        await admin.PostAsJsonAsync("/api/admin/login", new { password = MySqlApiFixture.AdminPassword });

        var today = await admin.GetFromJsonAsync<TodayResponse>("/api/moods/today", Json);

        today!.Logged.Should().BeFalse("signing in as admin does not log a mood or borrow anyone's identity");
    }
}
