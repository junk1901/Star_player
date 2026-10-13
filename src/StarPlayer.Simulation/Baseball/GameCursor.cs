using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// The one holder of a game's current <see cref="AuthoritativeGame"/> reference (C3-A, game-engine 27.7 and 27.16 O-3). <see cref="Publish"/> swaps in a staged
/// aggregate with one compare-and-swap against the reference the stage was based on; of several commits staged from the same base exactly one succeeds. There is no
/// static or global store: one cursor per game, created by whoever owns the game.
/// <para>
/// Cancellation is honoured only before the swap. After a successful swap nothing is checked, no hook runs, no exception can propagate and the result is the
/// staged record: a committed change is never reported as failed.
/// </para>
/// </summary>
public sealed class GameCursor
{
    /// <summary>Stage names seen by the test hook; the cancellation token is checked right after the hook.</summary>
    internal static class Stage
    {
        public const string BeforeSwap = "before_swap";
    }

    private AuthoritativeGame _current;

    internal GameCursor(AuthoritativeGame initial)
    {
        _current = initial ?? throw new ArgumentNullException(nameof(initial));
    }

    public AuthoritativeGame Current => Volatile.Read(ref _current);

    public Result<CommitRecord> Publish(StagedCommit? staged, CancellationToken cancellation = default) => Publish(staged, null, cancellation);

    /// <summary>The same, with a hook that sees <see cref="Stage.BeforeSwap"/> (after the freshness check, before the swap) so a test can hold two publishers there. It can only fail the publish before the swap.</summary>
    internal Result<CommitRecord> Publish(StagedCommit? staged, Action<string>? checkpoint, CancellationToken cancellation)
    {
        if (staged is null)
            return Fail("Commit.staged_missing", "확정할 준비 결과가 없습니다.");
        if (!ReferenceEquals(Volatile.Read(ref _current), staged.Expected))
            return Fail("Commit.revision_stale", "준비한 시점 이후 경기 상태가 바뀌었거나 이 경기의 커서가 아닙니다.");
        checkpoint?.Invoke(Stage.BeforeSwap);
        cancellation.ThrowIfCancellationRequested();

        // Nothing below this line may throw or consult the token: the swap is the commit.
        var seen = Interlocked.CompareExchange(ref _current, staged.Next, staged.Expected);
        return ReferenceEquals(seen, staged.Expected)
            ? Result<CommitRecord>.Success(staged.Record)
            : Fail("Commit.cas_lost", "다른 확정이 먼저 반영되어 이 확정은 적용되지 않았습니다.");
    }

    private static Result<CommitRecord> Fail(string code, string message) => Result<CommitRecord>.Failure(new Error(code, message));
}
