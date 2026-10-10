using StarPlayer.Domain.Baseball.Game;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// One out or one run of a play in the order it happened. <c>IsOut</c> terminals carry the rule classification of the out;
/// <c>ReconOnly</c> marks an out that exists only in the earned-run reconstruction (an error saved the runner, or the batter reached on an error).
/// </summary>
internal sealed record Terminal(int Key, bool IsScore, bool IsOut, int Sequence, OutClassification Class, bool ReconOnly);

/// <summary>
/// The single place that decides which runs count when a play ends the half-inning (OBR-style principle, KBO certification pending, see
/// docs/game-engine.md section 5): no run counts if the third out is made on the batter-runner or is a force out, whatever the order; if the
/// third out is any other out, only runs that crossed the plate BEFORE it count; a run after the third out never counts. Shared by the live
/// scorer and the earned-run reconstruction so both read the same verified order.
/// </summary>
internal static class ThirdOutRule
{
    public sealed record Result(HashSet<int> CountedRuns, bool ThirdOutMade, int OutsAfter);

    /// <param name="ordered">Terminals sorted by sequence.</param>
    /// <param name="includeReconOuts">False for the real play; true when replaying with error-saved outs counted as outs.</param>
    public static Result Evaluate(int outsBefore, int limit, IReadOnlyList<Terminal> ordered, bool includeReconOuts)
    {
        var outs = outsBefore;
        Terminal? third = null;
        foreach (var t in ordered)
        {
            if (!t.IsOut || (t.ReconOnly && !includeReconOuts))
                continue;
            outs++;
            if (outs == limit && third is null)
                third = t;
        }

        var counted = new HashSet<int>();
        foreach (var t in ordered.Where(t => t.IsScore))
        {
            var counts = third is null || (third.Class == OutClassification.NonForce && t.Sequence < third.Sequence);
            if (counts)
                counted.Add(t.Key);
        }

        return new Result(counted, third is not null, outs);
    }
}
