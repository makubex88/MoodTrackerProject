using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace MoodTrackerProject.Data;

/// <summary>
/// Decides whether a <see cref="DbUpdateException"/> is the unique index rejecting a duplicate.
/// This is the one provider-specific piece of the write path (D6), so it is isolated behind an
/// interface: MySQL reports error 1062, SQLite reports 2067, and tests can substitute either.
/// </summary>
public interface IDuplicateKeyDetector
{
    bool IsDuplicateKey(DbUpdateException exception);
}

public sealed class MySqlDuplicateKeyDetector : IDuplicateKeyDetector
{
    public bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry };
}
