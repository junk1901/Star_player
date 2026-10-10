using Microsoft.Data.Sqlite;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Raw provider behavior on a real temporary database with a test schema (not a save format).
/// These deliberately do not use SqliteStorageDiagnostics so the probe is checked against the provider itself.
/// </summary>
public sealed class SqlitePackagingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "starplayer-tests-" + Guid.NewGuid().ToString("N"));

    public SqlitePackagingTests()
    {
        Directory.CreateDirectory(_directory);
        using var connection = Open(DbPath);
        Exec(connection, "CREATE TABLE parent (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        Exec(connection, "CREATE TABLE child (id INTEGER PRIMARY KEY, parent_id INTEGER NOT NULL REFERENCES parent(id))");
    }

    private string DbPath => Path.Combine(_directory, "test.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Temporary_database_file_is_created_and_removed_by_cleanup()
    {
        Assert.True(File.Exists(DbPath));
    }

    [Fact]
    public void Committed_transaction_is_visible_to_a_new_connection()
    {
        using (var writer = Open(DbPath))
        using (var transaction = writer.BeginTransaction())
        {
            Exec(writer, "INSERT INTO parent VALUES (1, 'alpha')");
            transaction.Commit();
        }

        using var reader = Open(DbPath);
        Assert.Equal(1L, Scalar(reader, "SELECT COUNT(*) FROM parent"));
    }

    [Fact]
    public void Rolled_back_transaction_leaves_no_rows()
    {
        using (var writer = Open(DbPath))
        using (var transaction = writer.BeginTransaction())
        {
            Exec(writer, "INSERT INTO parent VALUES (1, 'alpha')");
            transaction.Rollback();
        }

        using var reader = Open(DbPath);
        Assert.Equal(0L, Scalar(reader, "SELECT COUNT(*) FROM parent"));
    }

    [Fact]
    public void Foreign_keys_are_enforced_on_two_independently_opened_connections()
    {
        using var first = Open(DbPath);
        using var second = Open(DbPath);

        foreach (var connection in new[] { first, second })
        {
            Assert.Equal(1L, Scalar(connection, "PRAGMA foreign_keys"));
            var error = Assert.Throws<SqliteException>(() => Exec(connection, "INSERT INTO child VALUES (1, 999)"));
            Assert.Equal(19, error.SqliteErrorCode); // SQLITE_CONSTRAINT
        }

        Exec(first, "INSERT INTO parent VALUES (5, 'p')");
        Exec(second, "INSERT INTO child VALUES (1, 5)"); // the other connection sees the committed parent
    }

    [Fact]
    public void Backup_can_be_reopened_and_contains_the_data()
    {
        var backupPath = Path.Combine(_directory, "backup.db");
        using (var source = Open(DbPath))
        {
            Exec(source, "INSERT INTO parent VALUES (1, 'alpha')");
            using var destination = Open(backupPath);
            source.BackupDatabase(destination);
        }

        // Source is gone; only the backup file is reopened.
        using var reopened = Open(backupPath);
        Assert.Equal(1L, Scalar(reopened, "SELECT COUNT(*) FROM parent WHERE name = 'alpha'"));
        Assert.Equal("ok", (string)new SqliteCommand("PRAGMA integrity_check", reopened).ExecuteScalar()!);
    }

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
            ForeignKeys = true,
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void Exec(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
