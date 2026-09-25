using System.Security.Claims;
using Microsoft.Extensions.Time.Testing;
using MoodTrackerProject.Auth;

namespace MoodTrackerProject.Tests.Auth;

public sealed class AdminAuthTests
{
    [Theory(DisplayName = "PasswordMatches compares exactly, including near-misses and length differences")]
    [InlineData("change-me-locally", "change-me-locally", true)]
    [InlineData("change-me-locally", "change-me-locallY", false)]
    [InlineData("change-me-locally", "change-me-local", false)]
    [InlineData("change-me-locally", "change-me-locally ", false)]
    [InlineData("change-me-locally", "", false)]
    [InlineData("", "", true)]
    public void PasswordMatches_IsExact(string configured, string supplied, bool expected)
    {
        AdminAuth.PasswordMatches(supplied, configured).Should().Be(expected);
    }

    [Fact(DisplayName = "A null supplied password never matches a configured one")]
    public void NullSupplied_DoesNotMatch()
    {
        AdminAuth.PasswordMatches(null, "change-me-locally").Should().BeFalse();
    }

    [Fact(DisplayName = "The admin principal carries the Admin role and the scheme's authentication type")]
    public void Principal_HasRole()
    {
        var principal = AdminAuth.CreateAdminPrincipal();

        principal.Identity!.IsAuthenticated.Should().BeTrue();
        principal.Identity.AuthenticationType.Should().Be(AdminAuth.Scheme);
        principal.IsInRole(AdminAuth.Role).Should().BeTrue();
        principal.FindFirst(ClaimTypes.Name)!.Value.Should().Be("admin");
    }

    [Fact(DisplayName = "Session properties expire SessionHours after issue")]
    public void SessionProperties_UseConfiguredLifetime()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-23T00:00:00Z"));

        var props = AdminAuth.SessionProperties(time, new AdminOptions { Password = "x", SessionHours = 8 });

        props.IsPersistent.Should().BeTrue();
        props.IssuedUtc.Should().Be(DateTimeOffset.Parse("2026-09-23T00:00:00Z"));
        props.ExpiresUtc.Should().Be(DateTimeOffset.Parse("2026-09-23T08:00:00Z"));
    }
}
