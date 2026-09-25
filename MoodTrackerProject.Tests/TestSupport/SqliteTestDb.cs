using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MoodTrackerProject.Data;

namespace MoodTrackerProject.Tests.TestSupport;

/// <summary>
/// An in-memory SQLite database for model-level tests. Unlike the EF InMemory provider, SQLite is
/// relational and enforces the unique index, so a test that expects the index to reject a row is a
/// real test. The schema comes from <c>EnsureCreated()</c> (the model), not from the MySQL migrations —
/// those are proven separately against a real MySQL in the integration tests.
///
/// The connection is opened once and held open: closing it destroys the database.
/// </summary>
public sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>A fresh context on the shared connection. Optional interceptors let a test simulate a competing writer.</summary>
    public MoodDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<MoodDbContext>().UseSqlite(_connection);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }
        return new MoodDbContext(builder.Options);
    }

    public void Dispose() => _connection.Dispose();
}

/// <summary>SQLite's spelling of "the unique index said no": constraint error 19, extended code 2067.</summary>
public sealed class SqliteDuplicateKeyDetector : IDuplicateKeyDetector
{
    public bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19, SqliteExtendedErrorCode: 2067 };
}
