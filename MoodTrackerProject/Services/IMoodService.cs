using MoodTrackerProject.Data.Entities;
using MoodTrackerProject.Domain;

namespace MoodTrackerProject.Services;

public interface IMoodService
{
    /// <summary>The participant's entry for the team's current day, or null.</summary>
    Task<MoodEntry?> GetTodayAsync(Guid participantId, CancellationToken cancellationToken);

    /// <summary>Records today's mood, or reports that one already exists. Never overwrites.</summary>
    Task<CreateMoodResult> CreateAsync(Guid participantId, MoodRating rating, string? comment, CancellationToken cancellationToken);

    /// <summary>Every entry in the system, most recent first, one page at a time.</summary>
    Task<MoodPage> ListAllAsync(int page, int pageSize, CancellationToken cancellationToken);
}
