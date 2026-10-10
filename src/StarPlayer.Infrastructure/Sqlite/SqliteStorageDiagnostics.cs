using System.Globalization;
using Microsoft.Data.Sqlite;
using StarPlayer.Application.Diagnostics;
using StarPlayer.Application.Ports;

namespace StarPlayer.Infrastructure.Sqlite;

/// <summary>
/// Phase 0 provider probe. Uses a throwaway two-table test schema in a temporary directory that is
/// removed afterwards; it is not a save format and consumes no career migration version.
/// </summary>
/// <param name="workRoot">Directory that will hold the temporary probe folder; defaults to the OS temp path.</param>
public sealed class SqliteStorageDiagnostics(string? workRoot = null) : IStorageDiagnostics
{
    public IReadOnlyList<DiagnosticStep> RunChecks()
    {
        var steps = new List<DiagnosticStep>();
        var root = Path.Combine(workRoot ?? Path.GetTempPath(), "starplayer-diag-" + Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(root, "diag.db");
        var backupPath = Path.Combine(root, "diag-backup.db");

        try
        {
            Step(steps, "SQLite 공급자 로드", () =>
            {
                using var connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();
                return $"SQLite {Scalar<string>(connection, "SELECT sqlite_version()")}, Microsoft.Data.Sqlite {typeof(SqliteConnection).Assembly.GetName().Version}";
            });

            Step(steps, "임시 데이터베이스 생성", () =>
            {
                Directory.CreateDirectory(root);
                using var connection = Open(dbPath);
                Exec(connection, "CREATE TABLE parent (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
                Exec(connection, "CREATE TABLE child (id INTEGER PRIMARY KEY, parent_id INTEGER NOT NULL REFERENCES parent(id), note TEXT NOT NULL)");
                Require(File.Exists(dbPath), "데이터베이스 파일이 생성되지 않았습니다.");
                Require(Scalar<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table'") == 2, "테스트 테이블 수가 2개가 아닙니다.");
                return "테스트 스키마(parent, child) 생성";
            });

            Step(steps, "트랜잭션 Commit", () =>
            {
                using (var writer = Open(dbPath))
                {
                    using var transaction = writer.BeginTransaction();
                    Exec(writer, "INSERT INTO parent (id, name) VALUES (1, 'alpha')");
                    Exec(writer, "INSERT INTO child (id, parent_id, note) VALUES (1, 1, 'first')");
                    transaction.Commit();
                }

                using var reader = Open(dbPath);
                Require(Scalar<long>(reader, "SELECT COUNT(*) FROM parent") == 1, "Commit한 parent 행이 다른 연결에서 보이지 않습니다.");
                Require(Scalar<long>(reader, "SELECT COUNT(*) FROM child") == 1, "Commit한 child 행이 다른 연결에서 보이지 않습니다.");
                return "Commit 후 새 연결에서 행 확인";
            });

            Step(steps, "트랜잭션 Rollback", () =>
            {
                using (var writer = Open(dbPath))
                {
                    using var transaction = writer.BeginTransaction();
                    Exec(writer, "INSERT INTO parent (id, name) VALUES (2, 'beta')");
                    Require(Scalar<long>(writer, "SELECT COUNT(*) FROM parent") == 2, "트랜잭션 안에서 삽입한 행이 보이지 않습니다.");
                    transaction.Rollback();
                }

                using var reader = Open(dbPath);
                Require(Scalar<long>(reader, "SELECT COUNT(*) FROM parent") == 1, "Rollback한 행이 남아 있습니다.");
                Require(Scalar<long>(reader, "SELECT COUNT(*) FROM parent WHERE id = 2") == 0, "Rollback한 행이 조회됩니다.");
                return "Rollback 후 행이 사라짐";
            });

            Step(steps, "서로 다른 연결의 외래 키 제약", () =>
            {
                using var first = Open(dbPath);
                using var second = Open(dbPath);
                var codes = new[] { RejectOrphanChild(first, 100), RejectOrphanChild(second, 101) };

                Exec(second, "INSERT INTO child (id, parent_id, note) VALUES (2, 1, 'valid')");
                Require(Scalar<long>(first, "SELECT COUNT(*) FROM child") == 2, "다른 연결에서 넣은 정상 행이 보이지 않습니다.");

                // Informational: what the provider does when Foreign Keys is not requested explicitly.
                using var implicitConnection = Open(dbPath, foreignKeys: null);
                var providerDefault = Scalar<long>(implicitConnection, "PRAGMA foreign_keys");
                return $"두 연결 모두 고아 행 거부(SQLite 오류 {string.Join("/", codes)}); 공급자 기본 foreign_keys={providerDefault}";
            });

            Step(steps, "데이터베이스 Backup", () =>
            {
                using (var source = Open(dbPath))
                using (var destination = new SqliteConnection(ConnectionString(backupPath, foreignKeys: true)))
                {
                    source.BackupDatabase(destination);
                }

                Require(File.Exists(backupPath) && new FileInfo(backupPath).Length > 0, "백업 파일이 비어 있거나 없습니다.");
                return $"백업 파일 {new FileInfo(backupPath).Length}바이트";
            });

            Step(steps, "백업 파일 재열기와 데이터 확인", () =>
            {
                using var connection = new SqliteConnection(ConnectionString(backupPath, foreignKeys: true, SqliteOpenMode.ReadOnly));
                connection.Open();
                Require(Scalar<long>(connection, "SELECT COUNT(*) FROM parent") == 1, "백업의 parent 행 수가 다릅니다.");
                Require(Scalar<string>(connection, "SELECT name FROM parent WHERE id = 1") == "alpha", "백업의 parent 값이 다릅니다.");
                Require(Scalar<long>(connection, "SELECT COUNT(*) FROM child") == 2, "백업의 child 행 수가 다릅니다.");
                Require(Scalar<string>(connection, "PRAGMA integrity_check") == "ok", "백업 무결성 검사에 실패했습니다.");
                return "parent 1행, child 2행, integrity_check ok";
            });
        }
        finally
        {
            Step(steps, "임시 파일 정리", () =>
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
                Require(!Directory.Exists(root), "임시 폴더가 남아 있습니다.");
                return "임시 폴더 삭제";
            });
        }

        return steps;
    }

    private static int RejectOrphanChild(SqliteConnection connection, int childId)
    {
        try
        {
            Exec(connection, $"INSERT INTO child (id, parent_id, note) VALUES ({childId}, 999, 'orphan')");
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            return exception.SqliteErrorCode;
        }

        throw new InvalidOperationException("외래 키 위반 삽입이 거부되지 않았습니다.");
    }

    private static string ConnectionString(string path, bool? foreignKeys, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate) =>
        new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false, ForeignKeys = foreignKeys }.ToString();

    private static SqliteConnection Open(string path, bool? foreignKeys = true)
    {
        var connection = new SqliteConnection(ConnectionString(path, foreignKeys));
        connection.Open();
        return connection;
    }

    private static void Exec(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static T Scalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T), CultureInfo.InvariantCulture);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Step(List<DiagnosticStep> steps, string name, Func<string> check)
    {
        try
        {
            steps.Add(new DiagnosticStep(name, true, check()));
        }
        catch (Exception exception)
        {
            steps.Add(new DiagnosticStep(name, false, $"{exception.GetType().Name}: {exception.Message}"));
        }
    }
}
