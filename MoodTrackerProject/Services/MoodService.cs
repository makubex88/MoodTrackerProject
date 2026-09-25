using Microsoft.EntityFrameworkCore;
using MoodTrackerProject.Data;
using MoodTrackerProject.Data.Entities;
using MoodTrackerProject.Domain;

namespace MoodTrackerProject.Services;

/// <summary>
/// The one real rule in the application: a participant logs at most one mood per team-local day.
///
/// The read-check in <see cref="CreateAsync"/> is a fast path for the common case, not the guarantee.
/// Two requests can both pass it before either insert is visible. The unique index
/// (<see cref="MoodDbContext.ParticipantDayIndex"/>) is what holds under concurrency, so the
/// duplicate-key exception from the database is translated into the same outcome the read-check
/// would have produced (D2, D6).
/// </summary>
public sealed class MoodService(
    MoodDbContext db,
    ITeamClock clock,
    IDuplicateKeyDetector duplicateKeys,
    ILogger<MoodService> logger) : IMoodService
{
    public const int MaxCommentLength = 500;
    public const int MaxPageSize = 200;

    public Task<MoodEntry?> GetTodayAsync(Guid participantId, CancellationToken cancellationToken)
    {
        var today = clock.Today;

        return db.MoodEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.ParticipantId == participantId && e.EntryDate == today, cancellationToken);
    }

    public async Task<CreateMoodResult> CreateAsync(
        Guid participantId,
        MoodRating rating,
        string? comment,
        CancellationToken cancellationToken)
    {
        if (!rating.IsDefined())
        {
            throw new ArgumentOutOfRangeException(nameof(rating), rating, "Rating must be between 1 and 4.");
        }

        var normalisedComment = NormaliseComment(comment);
        if (normalisedComment is { Length: > MaxCommentLength })
        {
            throw new ArgumentException($"Comment must be at most {MaxCommentLength} characters.", nameof(comment));
        }

        var today = clock.Today;

        // Fast path: the normal "you already logged today" case, answered without attempting a write.
        var existing = await db.MoodEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.ParticipantId == participantId && e.EntryDate == today, cancellationToken);

        if (existing is not null)
        {
            return CreateMoodResult.AlreadyLogged(existing);
        }

        var entry = new MoodEntry
        {
            ParticipantId = participantId,
            Rating = rating,
            Comment = normalisedComment,
            EntryDate = today,
            CreatedAtUtc = clock.UtcNow,
        };

        db.MoodEntries.Add(entry);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return CreateMoodResult.Created(entry);
        }
        catch (DbUpdateException ex) when (duplicateKeys.IsDuplicateKey(ex))
        {
            // The race: another request won between our read-check and our insert. The index did its job;
            // report the same outcome the read-check would have, using the row that actually won.
            logger.LogInformation("Duplicate mood entry rejected by {Index} for participant {Participant} on {Date}.",
                MoodDbContext.ParticipantDayIndex, participantId, today);

            db.Entry(entry).State = EntityState.Detached;

            var winner = await db.MoodEntries
                .AsNoTracking()
                .SingleAsync(e => e.ParticipantId == participantId && e.EntryDate == today, cancellationToken);

            return CreateMoodResult.AlreadyLogged(winner);
        }
    }

    public async Task<MoodPage> ListAllAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.MoodEntries.AsNoTracking();

        var total = await query.CountAsync(cancellationToken);

        // One count per mood, over every row. Four trivially indexed COUNTs rather than a GROUP BY on
        // the converted enum column — simpler SQL, and it cannot fail to translate.
        var countsByRating = new Dictionary<string, int>(4);
        foreach (var rating in new[] { MoodRating.NotGoodAtAll, MoodRating.ABitMeh, MoodRating.PrettyGood, MoodRating.FeelingGreat })
        {
            countsByRating[((byte)rating).ToString()] = await query.CountAsync(e => e.Rating == rating, cancellationToken);
        }

        var items = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new MoodPage(items, total, page, pageSize, countsByRating);
    }

    internal static string? NormaliseComment(string? comment)
    {
        if (comment is null)
        {
            return null;
        }

        var trimmed = comment.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
