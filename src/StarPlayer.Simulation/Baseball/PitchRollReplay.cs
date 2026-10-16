using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// Game Calling v4 (GC-S2): a VERIFICATION replay of a pitch's draws. It is built from a position snapshot only (H0, the aggregate's immutable <see cref="GameRandomSnapshot"/>) and works on a PRIVATE
/// <see cref="GameRandom"/> restored from it - a copy that shares nothing mutable with the aggregate, the cursor, the candidate or any other replay, and that is never published, stored or returned. It performs the
/// existing decision step exactly as the resolver does (one <c>NextBounded(1,000,000)</c> on the stream the step names, with the existing rejection sampling) and reports the roll and the raw draws, so the commit
/// evidence can compare them with what the draft recorded. Running it is a check, not a game draw: no stream, stage or counter of the game is added or moved. Only <see cref="CommitEvidence"/> uses it, and
/// nothing here can reach a cursor, a reducer or a staged commit (a test pins this file's vocabulary).
/// </summary>
internal sealed class PitchRollReplay
{
    private readonly GameRandom _copy;

    private PitchRollReplay(GameRandom copy)
    {
        _copy = copy;
    }

    /// <summary>A replay positioned at <paramref name="h0"/>. A structurally broken snapshot keeps its existing <c>Rng.*</c> error; nothing is created then.</summary>
    internal static Result<PitchRollReplay> From(GameRandomSnapshot h0)
    {
        var restored = GameRandom.Restore(h0);
        return restored.IsSuccess ? Result<PitchRollReplay>.Success(new PitchRollReplay(restored.Value)) : Result<PitchRollReplay>.Failure(restored.Error!.Value);
    }

    /// <summary>The next decision on <paramref name="stream"/>: the roll the generator produces there and the raw 32-bit outputs it consumed (rejected ones included).</summary>
    internal (uint Roll, long RawDraws) Next(RngStreamId stream)
    {
        var s = _copy.Stream(stream);
        var before = s.Draws;
        var roll = s.NextBounded(ProbabilityScale.RollBound);
        return (roll, s.Draws - before);
    }

    /// <summary>The state hash of the replay's position (compared with the position the draft says it ended at, H1).</summary>
    internal string StateHash => _copy.Snapshot().StateHash;
}
