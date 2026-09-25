using Microsoft.AspNetCore.Http;
using MoodTrackerProject.Identity;

namespace MoodTrackerProject.Tests.Identity;

/// <summary>ID-01 … ID-03: the identity cookie is minted once, reused thereafter, and replaced if it is garbage.</summary>
public sealed class ParticipantCookieMiddlewareTests
{
    private static async Task<(ParticipantContext Participant, DefaultHttpContext Http)> RunAsync(string? cookieHeader, bool https = false)
    {
        var http = new DefaultHttpContext();
        if (cookieHeader is not null)
        {
            http.Request.Headers.Cookie = cookieHeader;
        }
        if (https)
        {
            http.Request.Scheme = "https";
        }

        var participant = new ParticipantContext();
        var nextCalled = false;
        var middleware = new ParticipantCookieMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(http, participant);

        nextCalled.Should().BeTrue();
        return (participant, http);
    }

    [Fact(DisplayName = "ID-01 no cookie: a new id is minted and set with the right flags")]
    public async Task NoCookie_MintsAndSets()
    {
        var (participant, http) = await RunAsync(cookieHeader: null);

        participant.IsNew.Should().BeTrue();
        participant.ParticipantId.Should().NotBe(Guid.Empty);

        var setCookie = http.Response.Headers.SetCookie.ToString();
        setCookie.Should().StartWith($"mt_pid={participant.ParticipantId:D};");
        setCookie.Should().Contain("httponly", Exactly.Once(), because: "script must not be able to read or forge it");
        setCookie.Should().ContainEquivalentOf("samesite=lax");
        setCookie.Should().Contain("path=/");
        setCookie.Should().Contain("max-age=31536000");
        setCookie.Should().NotContainEquivalentOf("secure", "over plain http://localhost a Secure cookie would be silently dropped");
        setCookie.Should().NotContainEquivalentOf("domain=", "host-only cookies are what we want");
    }

    [Fact(DisplayName = "ID-01b over HTTPS the cookie is also marked Secure")]
    public async Task Https_SetsSecure()
    {
        var (_, http) = await RunAsync(cookieHeader: null, https: true);

        http.Response.Headers.SetCookie.ToString().Should().ContainEquivalentOf("; secure");
    }

    [Fact(DisplayName = "ID-02 an existing cookie is reused and no new cookie is issued")]
    public async Task ExistingCookie_IsReused()
    {
        var existing = Guid.Parse("8f3c1a2e-5d7b-4e9a-b2c1-0f6d3a8e7c21");

        var (participant, http) = await RunAsync($"mt_pid={existing:D}; other=1");

        participant.IsNew.Should().BeFalse();
        participant.ParticipantId.Should().Be(existing);
        http.Response.Headers.SetCookie.ToString().Should().BeEmpty();
    }

    [Theory(DisplayName = "ID-03 a malformed cookie is replaced rather than trusted")]
    [InlineData("mt_pid=not-a-guid")]
    [InlineData("mt_pid=")]
    [InlineData("mt_pid=8f3c1a2e5d7b4e9ab2c10f6d3a8e7c21")] // 32-digit form is not the 'D' format we issue
    [InlineData("mt_pid={8f3c1a2e-5d7b-4e9a-b2c1-0f6d3a8e7c21}")]
    public async Task MalformedCookie_IsReplaced(string header)
    {
        var (participant, http) = await RunAsync(header);

        participant.IsNew.Should().BeTrue();
        http.Response.Headers.SetCookie.ToString().Should().StartWith("mt_pid=");
    }

    [Fact(DisplayName = "Cookie GUIDs are accepted case-insensitively")]
    public async Task UpperCaseGuid_IsAccepted()
    {
        var (participant, _) = await RunAsync("mt_pid=8F3C1A2E-5D7B-4E9A-B2C1-0F6D3A8E7C21");

        participant.IsNew.Should().BeFalse();
        participant.ParticipantId.Should().Be(Guid.Parse("8f3c1a2e-5d7b-4e9a-b2c1-0f6d3a8e7c21"));
    }
}
