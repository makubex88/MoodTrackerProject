using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MoodTrackerProject.Contracts;
using MoodTrackerProject.Controllers;
using MoodTrackerProject.Data.Entities;
using MoodTrackerProject.Domain;
using MoodTrackerProject.Identity;
using MoodTrackerProject.Services;
using NSubstitute;

namespace MoodTrackerProject.Tests.Controllers;

/// <summary>The controller is HTTP glue: these pin the status codes and the problem-details shape the client branches on.</summary>
public sealed class MoodsControllerTests
{
    private static readonly Guid Alice = Guid.Parse("8f3c1a2e-5d7b-4e9a-b2c1-0f6d3a8e7c21");

    private static (MoodsController Controller, IMoodService Service) Create()
    {
        var service = Substitute.For<IMoodService>();
        var clock = Substitute.For<ITeamClock>();
        clock.Today.Returns(new DateOnly(2026, 9, 23));
        clock.Zone.Returns(TimeZoneInfo.Utc);

        var controller = new MoodsController(service, clock, new ParticipantContext { ParticipantId = Alice })
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        controller.HttpContext.Request.Path = "/api/moods";
        return (controller, service);
    }

    private static MoodEntry Entry(long id = 4) => new()
    {
        Id = id,
        ParticipantId = Alice,
        Rating = MoodRating.PrettyGood,
        Comment = "Release went out clean.",
        EntryDate = new DateOnly(2026, 9, 23),
        CreatedAtUtc = new DateTime(2026, 9, 22, 23, 14, 5, DateTimeKind.Utc),
    };

    [Fact(DisplayName = "POST → 201 Created with the entry and a Location header when the service created it")]
    public async Task Create_Created_Returns201()
    {
        var (controller, service) = Create();
        service.CreateAsync(Alice, MoodRating.PrettyGood, "Release went out clean.", Arg.Any<CancellationToken>())
            .Returns(CreateMoodResult.Created(Entry()));

        var result = await controller.Create(new CreateMoodRequest { Rating = 3, Comment = "Release went out clean." }, CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Which;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.ActionName.Should().Be(nameof(MoodsController.GetToday));
        var body = created.Value.Should().BeOfType<MoodResponse>().Which;
        body.Id.Should().Be(4);
        body.Rating.Should().Be(3);
        body.EntryDate.Should().Be(new DateOnly(2026, 9, 23));
        body.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact(DisplayName = "POST → 409 Conflict with problem details when today is already logged")]
    public async Task Create_AlreadyLogged_Returns409ProblemDetails()
    {
        var (controller, service) = Create();
        service.CreateAsync(Alice, Arg.Any<MoodRating>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(CreateMoodResult.AlreadyLogged(Entry()));

        var result = await controller.Create(new CreateMoodRequest { Rating = 4 }, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<ConflictObjectResult>().Which;
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        conflict.ContentTypes.Should().Contain("application/problem+json");
        var problem = conflict.Value.Should().BeOfType<ProblemDetails>().Which;
        problem.Status.Should().Be(409);
        problem.Title.Should().Be("Mood already logged");
        problem.Detail.Should().Be("You've already logged your mood for today. Come back tomorrow.");
        problem.Extensions.Should().ContainKey("entryDate").WhoseValue.Should().Be("2026-09-23");
    }

    [Fact(DisplayName = "POST passes the participant id from the cookie context, never from the body")]
    public async Task Create_UsesParticipantContext()
    {
        var (controller, service) = Create();
        service.CreateAsync(Arg.Any<Guid>(), Arg.Any<MoodRating>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(CreateMoodResult.Created(Entry()));

        await controller.Create(new CreateMoodRequest { Rating = 1 }, CancellationToken.None);

        await service.Received(1).CreateAsync(Alice, MoodRating.NotGoodAtAll, null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "GET today → logged:false with the team date and zone when nothing is logged")]
    public async Task GetToday_NotLogged()
    {
        var (controller, service) = Create();
        service.GetTodayAsync(Alice, Arg.Any<CancellationToken>()).Returns((MoodEntry?)null);

        var result = await controller.GetToday(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Which;
        var body = ok.Value.Should().BeOfType<TodayResponse>().Which;
        body.Logged.Should().BeFalse();
        body.Entry.Should().BeNull();
        body.Today.Should().Be(new DateOnly(2026, 9, 23));
        body.TimeZone.Should().Be("UTC");
    }

    [Fact(DisplayName = "GET today → logged:true with the entry once logged")]
    public async Task GetToday_Logged()
    {
        var (controller, service) = Create();
        service.GetTodayAsync(Alice, Arg.Any<CancellationToken>()).Returns(Entry(7));

        var result = await controller.GetToday(CancellationToken.None);

        var body = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<TodayResponse>().Which;
        body.Logged.Should().BeTrue();
        body.Entry!.Id.Should().Be(7);
    }
}
