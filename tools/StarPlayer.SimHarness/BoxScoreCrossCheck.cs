using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.SimHarness;

public sealed record CrossCheckItem(string Name, long FromEvents, long FromBoxScore)
{
    public bool Matches => FromEvents == FromBoxScore;
}

public sealed record CrossCheckResult(bool Passed, IReadOnlyList<CrossCheckItem> Items)
{
    public IReadOnlyList<CrossCheckItem> Mismatches => Items.Where(i => !i.Matches).ToList();
}

/// <summary>
/// Compares two derivations of the same official numbers of a FINISHED game: path A is <see cref="MetricCounters"/> (the confirmed commits' facts and events, counted by <see cref="MetricsRecorder"/>);
/// path B is the production <see cref="BoxScoreBuilder"/> run on the final game state (it sums the reducer's append-only stat entries). The recorder never reads a stat entry and the builder
/// never sees a commit, so a disagreement means one of them is wrong. The in-play events are also counted a second way (home runs, hits) against the plate-appearance facts.
/// </summary>
public static class BoxScoreCrossCheck
{
    public static CrossCheckResult Compare(MetricCounters events, GameState finalState)
    {
        var box = BoxScoreBuilder.Build(finalState);
        long Sum(StatKey key) => box.Players.Sum(p => (long)p.Get(key));
        var items = new List<CrossCheckItem>
        {
            new("PA", events.PlateAppearances, Sum(StatKey.BatPA)),
            new("AB", events.AtBats, Sum(StatKey.BatAB)),
            new("H", events.Hits, Sum(StatKey.BatH)),
            new("1B", events.Singles, Sum(StatKey.Bat1B)),
            new("2B", events.Doubles, Sum(StatKey.Bat2B)),
            new("3B", events.Triples, Sum(StatKey.Bat3B)),
            new("HR", events.HomeRuns, Sum(StatKey.BatHR)),
            new("BB", events.Walks, Sum(StatKey.BatBB)),
            new("HBP", events.HitByPitch, Sum(StatKey.BatHBP)),
            new("SO", events.Strikeouts, Sum(StatKey.BatSO)),
            new("SF", events.SacrificeFlies, Sum(StatKey.BatSF)),
            new("Pitches", events.Pitches, Sum(StatKey.PitchPitches)),
            // the same facts from the other side of the engine and from the in-play events themselves
            new("HR(in-play events)", events.EventHomeRuns, events.HomeRuns),
            new("H(in-play events)", events.EventHits, events.Hits),
            new("HBP(pitch events)", events.HitByPitchPitches, events.HitByPitch),
        };
        return new CrossCheckResult(items.All(i => i.Matches), items);
    }
}
