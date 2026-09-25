using MoodTrackerProject.Data.Entities;

namespace MoodTrackerProject.Contracts;

/// <summary>A participant's own entry. Never includes the participant id — the caller already is that participant.</summary>
public sealed record MoodResponse(long Id, int Rating, string? Comment, DateOnly EntryDate, DateTime CreatedAtUtc)
{
    public static MoodResponse From(MoodEntry e) =>
        new(e.Id, (int)e.Rating, e.Comment, e.EntryDate, DateTime.SpecifyKind(e.CreatedAtUtc, DateTimeKind.Utc));
}

/// <summary>Answer to GET /api/moods/today. <see cref="Today"/> is the team-local date the server is using.</summary>
public sealed record TodayResponse(bool Logged, MoodResponse? Entry, DateOnly Today, string TimeZone);
