using System.ComponentModel.DataAnnotations;
using MoodTrackerProject.Data.Entities;

namespace MoodTrackerProject.Contracts;

public sealed class AdminLoginRequest
{
    [Required]
    public string? Password { get; init; }
}

/// <summary>An entry as the admin sees it: includes the (opaque) participant id so patterns across days are visible.</summary>
public sealed record AdminMoodResponse(
    long Id,
    string ParticipantId,
    int Rating,
    string? Comment,
    DateOnly EntryDate,
    DateTime CreatedAtUtc)
{
    public static AdminMoodResponse From(MoodEntry e) =>
        new(e.Id, e.ParticipantId.ToString("D"), (int)e.Rating, e.Comment, e.EntryDate, DateTime.SpecifyKind(e.CreatedAtUtc, DateTimeKind.Utc));
}

public sealed record AdminMoodPageResponse(
    IReadOnlyList<AdminMoodResponse> Items,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyDictionary<string, int> CountsByRating,
    string TimeZone);
