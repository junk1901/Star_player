using StarPlayer.Infrastructure.Sqlite;

namespace StarPlayer.Infrastructure.Tests;

public sealed class SqliteStorageDiagnosticsTests : IDisposable
{
    private readonly string _workRoot = Path.Combine(Path.GetTempPath(), "starplayer-diagtests-" + Guid.NewGuid().ToString("N"));

    public SqliteStorageDiagnosticsTests() => Directory.CreateDirectory(_workRoot);

    public void Dispose() => Directory.Delete(_workRoot, recursive: true);

    [Fact]
    public void All_probe_steps_pass_and_the_temporary_folder_is_removed()
    {
        var steps = new SqliteStorageDiagnostics(_workRoot).RunChecks();

        Assert.All(steps, step => Assert.True(step.Passed, $"{step.Name}: {step.Detail}"));
        Assert.Equal(
            ["SQLite 공급자 로드", "임시 데이터베이스 생성", "트랜잭션 Commit", "트랜잭션 Rollback",
             "서로 다른 연결의 외래 키 제약", "데이터베이스 Backup", "백업 파일 재열기와 데이터 확인", "임시 파일 정리"],
            steps.Select(step => step.Name));
        Assert.Empty(Directory.GetFileSystemEntries(_workRoot));
    }

    [Fact]
    public void An_unusable_work_root_is_reported_as_failed_steps_not_an_exception()
    {
        var blocker = Path.Combine(_workRoot, "not-a-directory");
        File.WriteAllText(blocker, "x");

        var steps = new SqliteStorageDiagnostics(blocker).RunChecks();

        Assert.False(steps.Single(step => step.Name == "임시 데이터베이스 생성").Passed);
        Assert.False(steps.Single(step => step.Name == "트랜잭션 Commit").Passed);
    }
}
