using StarPlayer.Domain.Baseball.Game;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// Earned-run reconstruction for one half-inning (documented in docs/game-engine.md). The half is replayed as if every error
/// and passed ball had not happened: a batter who reached on an error, or on a dropped third strike charged to the catcher,
/// is out; a runner who advanced on an error or passed ball stays put (one base if a reaching batter forces him); a runner saved from an out by an error is out. Other
/// runners advance by the same number of bases they really did (never passing or sharing a base with a runner ahead), and
/// the replay stops at the third reconstructed out. A run is earned only if its runner scores inside the replay. Which runs of the
/// play that makes the third reconstructed out still count is decided by <see cref="ThirdOutRule"/> from the verified order and
/// classification stored on each move record (real outs keep their real classification, an error-saved out gets the classification it
/// would have had, an error-saved batter is a batter-runner out), the same rule the live scorer applies.
/// </summary>
internal static class EarnedRuns
{
    /// <summary>
    /// The runs of the half-inning that would have scored without the errors and passed balls, reconstructed from the play at <paramref name="startIndex"/> on. From the
    /// first play (0) this is the whole-inning figure. From a later play it is the view of a relief pitcher (OBR 9.16(i)): the real outs and the real runners on base
    /// when he entered are the starting position, so the reconstructed outs the earlier errors would have produced are not given to him.
    /// </summary>
    public static HashSet<int> Reconstruct(IReadOnlyList<ScoredPlay> half, int outsLimit, int startIndex = 0)
    {
        var recon = new Dictionary<int, int>(); // runner key -> reconstructed base (1-3)
        var earned = new HashSet<int>();
        var outs = 0;
        if (startIndex > 0 && startIndex < half.Count)
        {
            foreach (var before in half.Take(startIndex))
                foreach (var move in before.Moves)
                    if (move.To is >= 1 and <= 3) recon[move.RunnerKey] = move.To; else recon.Remove(move.RunnerKey);
            outs = half[startIndex].OutsBefore;
        }

        foreach (var play in half.Skip(startIndex))
        {
            if (outs >= outsLimit)
                break;

            var moves = play.Moves.ToDictionary(m => m.RunnerKey);
            var batterMove = play.Moves.FirstOrDefault(m => m.From == 0);
            var next = new Dictionary<int, int>();
            var terminals = new List<Terminal>();
            var blocker = int.MaxValue;

            // a batter put out after a safe hit (classified NonForce) did reach base; only a batter out before reaching, or one saved by an error, did not
            var batterReaches = batterMove is not null && (batterMove.To != 0 || batterMove.Classification == OutClassification.NonForce) && batterMove.Cause is not (MoveCause.ErrorSavedOut or MoveCause.PassedBall);
            foreach (var (key, reconBase) in recon.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, kv.Value)))
            {
                moves.TryGetValue(key, out var move);
                int dest;
                if (move is null)
                    dest = reconBase;
                else if (move.Cause is MoveCause.PassedBall or MoveCause.ErrorExtraBase)
                {
                    // the error/passed ball is undone, but a runner forced along by a batter who still reaches moves one base
                    var forced = batterReaches && Enumerable.Range(1, reconBase - 1).All(recon.ContainsValue);
                    dest = forced ? reconBase + 1 : reconBase;
                }
                else if (move.To == 0 || move.Cause == MoveCause.ErrorSavedOut)
                {
                    terminals.Add(new Terminal(key, false, true, move.Sequence, move.Classification ?? OutClassification.NonForce, false));
                    continue;
                }
                else
                    dest = Math.Min(4, reconBase + (move.To - move.From));

                Place(dest, reconBase, ref blocker, key, next, terminals, moves);
            }

            if (batterMove is not null)
            {
                if (batterMove.To == 0 || batterMove.Cause is MoveCause.ErrorSavedOut or MoveCause.PassedBall)
                    terminals.Add(new Terminal(batterMove.RunnerKey, false, true, batterMove.Sequence, batterMove.To == 0 ? batterMove.Classification ?? OutClassification.BatterRunner : OutClassification.BatterRunner, false));
                else
                    Place(Math.Min(4, batterMove.To), 1, ref blocker, batterMove.RunnerKey, next, terminals, moves);
            }

            var ordered = terminals.OrderBy(t => t.Sequence).ToList();
            var rule = ThirdOutRule.Evaluate(outs, outsLimit, ordered, includeReconOuts: true);
            earned.UnionWith(rule.CountedRuns);
            if (rule.ThirdOutMade)
                break;

            recon = next;
            outs = rule.OutsAfter;
        }

        return earned;
    }

    private static int Place(int dest, int floor, ref int blocker, int key, Dictionary<int, int> next, List<Terminal> terminals, Dictionary<int, RunnerMoveRecord> moves)
    {
        if (dest >= 4)
        {
            terminals.Add(new Terminal(key, true, false, moves.TryGetValue(key, out var m) ? m.Sequence : 0, OutClassification.NonForce, false));
            return 4;
        }

        dest = Math.Max(floor, Math.Min(dest, blocker - 1));
        next[key] = dest;
        blocker = Math.Min(blocker, dest);
        return dest;
    }
}
