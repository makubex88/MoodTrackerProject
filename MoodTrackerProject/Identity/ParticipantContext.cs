namespace MoodTrackerProject.Identity;

/// <summary>
/// Scoped holder for the participant id established by <see cref="ParticipantCookieMiddleware"/>.
/// Controllers read it from here rather than from the cookie, so the cookie is the only place the
/// mechanism is known.
/// </summary>
public sealed class ParticipantContext
{
    public Guid ParticipantId { get; internal set; }

    /// <summary>True when the id was minted during this request (first visit, or cookie cleared).</summary>
    public bool IsNew { get; internal set; }
}
