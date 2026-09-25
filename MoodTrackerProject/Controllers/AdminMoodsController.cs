using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodTrackerProject.Auth;
using MoodTrackerProject.Contracts;
using MoodTrackerProject.Services;

namespace MoodTrackerProject.Controllers;

/// <summary>Every mood entry in the system, newest first. The attribute is the access control; the client guard is convenience.</summary>
[ApiController]
[Route("api/admin/moods")]
[Authorize(Policy = AdminAuth.Policy)]
public sealed class AdminMoodsController(IMoodService moods, ITeamClock clock) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AdminMoodPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AdminMoodPageResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await moods.ListAllAsync(page, pageSize, cancellationToken);

        return Ok(new AdminMoodPageResponse(
            Items: result.Items.Select(AdminMoodResponse.From).ToList(),
            Total: result.Total,
            Page: result.Page,
            PageSize: result.PageSize,
            CountsByRating: result.CountsByRating,
            TimeZone: clock.Zone.Id));
    }
}
