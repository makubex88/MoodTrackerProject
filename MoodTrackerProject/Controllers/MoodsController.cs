using Microsoft.AspNetCore.Mvc;
using MoodTrackerProject.Contracts;
using MoodTrackerProject.Domain;
using MoodTrackerProject.Identity;
using MoodTrackerProject.Services;

namespace MoodTrackerProject.Controllers;

/// <summary>
/// Participant endpoints. HTTP only: bind, delegate to the service, map the result.
/// Deliberately no class-level [Produces]: that attribute replaces a result's content types, which
/// would turn the 409's application/problem+json back into application/json.
/// </summary>
[ApiController]
[Route("api/moods")]
public sealed class MoodsController(IMoodService moods, ITeamClock clock, ParticipantContext participant) : ControllerBase
{
    /// <summary>Has this browser already logged a mood for the team's current day?</summary>
    [HttpGet("today")]
    [ProducesResponseType<TodayResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TodayResponse>> GetToday(CancellationToken cancellationToken)
    {
        var entry = await moods.GetTodayAsync(participant.ParticipantId, cancellationToken);

        return Ok(new TodayResponse(
            Logged: entry is not null,
            Entry: entry is null ? null : MoodResponse.From(entry),
            Today: clock.Today,
            TimeZone: clock.Zone.Id));
    }

    /// <summary>Log today's mood. 201 the first time; 409 with problem details if today is already logged.</summary>
    [HttpPost]
    [ProducesResponseType<MoodResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MoodResponse>> Create([FromBody] CreateMoodRequest request, CancellationToken cancellationToken)
    {
        // [ApiController] has already rejected a missing/out-of-range rating and an over-long comment with a 400.
        var rating = (MoodRating)request.Rating!.Value;

        var result = await moods.CreateAsync(participant.ParticipantId, rating, request.Comment, cancellationToken);

        if (result.Outcome == CreateMoodOutcome.AlreadyLogged)
        {
            // Conflict(value) alone serialises as application/json; RFC 7807 clients key off the media type.
            var conflict = Conflict(AlreadyLoggedProblem(result.Entry.EntryDate));
            conflict.ContentTypes.Add("application/problem+json");
            return conflict;
        }

        return CreatedAtAction(nameof(GetToday), routeValues: null, value: MoodResponse.From(result.Entry));
    }

    private ProblemDetails AlreadyLoggedProblem(DateOnly entryDate)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            Title = "Mood already logged",
            Status = StatusCodes.Status409Conflict,
            Detail = "You've already logged your mood for today. Come back tomorrow.",
            Instance = HttpContext.Request.Path,
        };
        problem.Extensions["entryDate"] = entryDate.ToString("yyyy-MM-dd");
        return problem;
    }
}
