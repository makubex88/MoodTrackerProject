using Microsoft.EntityFrameworkCore;

namespace MoodTrackerProject.Data;

/// <summary>
/// Applies EF Core migrations at startup. The compose file ships an empty database and no init SQL,
/// so the app must be able to create its own schema on a cold volume. MySQL can still be finishing
/// its own start-up when we get here (the compose healthcheck narrows that window but does not
/// close it), hence the retry loop.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task MigrateWithRetryAsync(
        MoodDbContext db,
        ILogger logger,
        int attempts = 10,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        var wait = delay ?? TimeSpan.FromSeconds(3);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations applied (attempt {Attempt}).", attempt);
                return;
            }
            catch (Exception ex) when (attempt < attempts && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex,
                    "Database not ready (attempt {Attempt}/{Attempts}); retrying in {Delay}s.",
                    attempt, attempts, wait.TotalSeconds);
                await Task.Delay(wait, cancellationToken);
            }
        }
    }
}
