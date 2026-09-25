using MoodTrackerProject.Domain;

namespace MoodTrackerProject.Data.Entities;

/// <summary>
/// One row per participant per team-local day. The uniqueness of (ParticipantId, EntryDate) is
/// enforced by the database index, not by application code — see MoodService for why.
/// </summary>
public sealed class MoodEntry
{
    public long Id { get; set; }

    /// <summary>
    /// Opaque identity from the mt_pid cookie. Identifies a browser, never a person.
    /// Stored as char(36); MySqlConnector reads char(36) as a Guid natively, so the CLR type matches the driver.
    /// </summary>
    public Guid ParticipantId { get; set; }

    public MoodRating Rating { get; set; }

    /// <summary>Optional, trimmed, max 500 characters. Empty input is stored as NULL.</summary>
    public string? Comment { get; set; }

    /// <summary>The calendar day in the team's time zone. This is the column the unique index uses.</summary>
    public DateOnly EntryDate { get; set; }

    /// <summary>The exact instant, for "most recent first" ordering.</summary>
    public DateTime CreatedAtUtc { get; set; }
}
