using MoodTrackerProject.Data.Entities;

namespace MoodTrackerProject.Services;

public enum CreateMoodOutcome
{
    /// <summary>A new row was written.</summary>
    Created,

    /// <summary>An entry already existed for this participant today; nothing was written.</summary>
    AlreadyLogged,
}

/// <summary>
/// Outcome of a create attempt. <see cref="Entry"/> is the new row on <see cref="CreateMoodOutcome.Created"/>
/// and the pre-existing row on <see cref="CreateMoodOutcome.AlreadyLogged"/>, so the client can render the
/// locked view from either without a second request.
/// </summary>
public sealed record CreateMoodResult(CreateMoodOutcome Outcome, MoodEntry Entry)
{
    public static CreateMoodResult Created(MoodEntry entry) => new(CreateMoodOutcome.Created, entry);
    public static CreateMoodResult AlreadyLogged(MoodEntry existing) => new(CreateMoodOutcome.AlreadyLogged, existing);
}

public sealed record MoodPage(
    IReadOnlyList<MoodEntry> Items,
    int Total,
    int Page,
    int PageSize,
    /// <summary>Keyed by the rating value as a string ("1".."4"), which is also how it travels in JSON.</summary>
    IReadOnlyDictionary<string, int> CountsByRating);
