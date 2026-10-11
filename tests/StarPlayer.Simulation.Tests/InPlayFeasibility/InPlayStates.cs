using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.Simulation.Tests.InPlayFeasibility;

/// <summary>
/// Builds REAL legal game states through the public <see cref="GameReducer.Apply"/> only (walks, steals, fly outs, home runs); no state is forged and no
/// integrity check is bypassed. Every returned state is a plate appearance in progress (an approach is chosen, count 0-0) with the wanted outs and runners.
/// </summary>
internal static class InPlayStates
{
    private sealed class Driver
    {
        private int _n;
        private readonly string _prefix;
        public GameState State { get; private set; }

        public Driver(GameState start, string prefix, int startAt = 0) { State = start; _prefix = prefix; _n = startAt; }

        public string Id() => $"{_prefix}{++_n}";

        public Driver Do(GameEvent e)
        {
            var r = GameReducer.Apply(State, e);
            Assert.True(r.IsSuccess, $"{e.GetType().Name} {e.Id}: {r.Error?.Code} {r.Error?.Message}");
            State = r.Value.State;
            return this;
        }

        public Driver Approach() => Do(new ApproachEvent(Id(), ApproachKind.Normal));

        public Driver Walk()
        {
            Approach();
            for (var i = 0; i < 4; i++)
                Do(new PitchEvent(Id(), new PitchSpec(PitchOutcome.Ball)));
            return this;
        }

        public Driver FlyOut() =>
            Approach().Do(new InPlayEvent(Id(), BattedBallType.Fly, InPlayResult.Out, false, null, ImmutableList.Create(new PositionCredit(FieldPosition.LeftField, CreditKind.Putout))));

        public Driver HomeRun() => Approach().Do(new InPlayEvent(Id(), BattedBallType.Fly, InPlayResult.HomeRun));

        public Driver Steal(int from) =>
            Do(new RunnerPlayEvent(Id(), RunnerPlayKind.Steal, ImmutableList.Create(new RunnerMove(from, from + 1, MoveCause.Steal))));

        public Driver Outs(int count) { for (var i = 0; i < count; i++) FlyOut(); return this; }

        public Driver Halves(int halfInnings) { for (var i = 0; i < halfInnings; i++) Outs(3); return this; }
    }

    /// <summary>Where the reusable root of a context stands: the plate appearance about to start with no runner and no out.</summary>
    public enum Root
    {
        /// <summary>Top of the 3rd inning, 0-0, away team batting (no walk-off is possible).</summary>
        GeneralTop,

        /// <summary>Bottom of the 3rd inning, 0-0, home team batting (a home half, but not the final or an extra inning).</summary>
        GeneralBottom,

        /// <summary>Top of the 3rd inning, 0-0, away team batting, in a game WITHOUT a designated hitter (the pitcher bats).</summary>
        GeneralTopNoDh,

        /// <summary>Bottom of the 9th, the home team trails by <c>diff</c> (0 = tied).</summary>
        WalkOff,

        /// <summary>Bottom of the 10th (the first extra inning), tied.</summary>
        ExtraInning,
    }

    private static readonly Dictionary<(Root, int, int, int), GameState> Cache = [];
    private static readonly object Gate = new();

    private static GameState Start(GameRuleSnapshot rules, bool dh) =>
        GameReducer.Start(new GameStart("g1", rules, Script.Team("a", dh), Script.Team("h", dh))).Value;

    /// <summary>The game state with the given outs and runners (a bit mask: 1 = first, 2 = second, 4 = third) and an approach already chosen.</summary>
    public static GameState Make(Root root, int diff, int occupancy, int outs)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue((root, diff, occupancy, outs), out var hit))
                return hit;
            var built = Build(root, diff, occupancy, outs);
            Cache[(root, diff, occupancy, outs)] = built;
            return built;
        }
    }

    private static GameState Build(Root root, int diff, int occupancy, int outs)
    {
        Driver d = root switch
        {
            Root.GeneralTop => new Driver(Start(TestRules.Dh, true), "x").Halves(4),
            Root.GeneralBottom => new Driver(Start(TestRules.Dh, true), "x").Halves(5),
            Root.GeneralTopNoDh => new Driver(Start(TestRules.NoDh, false), "x").Halves(4),
            Root.WalkOff => AtBottomNinth(diff),
            _ => AtBottomNinth(0).Halves(2),   // the bottom of the 9th ends level (3 outs), the top of the 10th is retired, the bottom of the 10th begins
        };
        d.Outs(outs);
        if (occupancy == 1) d.Walk();                                                                                // first
        else if (occupancy == 2) d.Walk().Steal(1);                                                                                          // second
        else if (occupancy == 4) d.Walk().Steal(1).Steal(2);                                                                                 // third
        else if (occupancy == 3) d.Walk().Steal(1).Walk();                                                                                   // first and second
        else if (occupancy == 5) d.Walk().Steal(1).Steal(2).Walk();                                                                          // first and third
        else if (occupancy == 6) d.Walk().Steal(1).Steal(2).Walk().Steal(1);                                                                 // second and third
        else if (occupancy == 7) d.Walk().Walk().Walk();                                                                                     // loaded
        d.Approach();
        var s = d.State;
        var mask = (s.Bases.First is null ? 0 : 1) | (s.Bases.Second is null ? 0 : 2) | (s.Bases.Third is null ? 0 : 4);
        Assert.Equal(occupancy, mask);
        Assert.Equal(outs, s.Outs);
        Assert.Equal(PaStage.InSequence, s.Stage);
        Assert.Equal(GameStatus.InProgress, s.Status);
        return s;
    }

    /// <summary>Away leads by <paramref name="diff"/> runs (scored as solo home runs in the top of the 1st), eight further innings are played with no runs, and the bottom of the 9th is about to start.</summary>
    private static Driver AtBottomNinth(int diff)
    {
        var d = new Driver(Start(TestRules.Dh, true), "x");
        for (var i = 0; i < diff; i++)
            d.HomeRun();
        d.Outs(3);          // end of the top of the 1st
        d.Halves(16);       // bottom 1st ... top 9th
        Assert.Equal(9, d.State.Inning);
        Assert.Equal(InningHalf.Bottom, d.State.Half);
        Assert.Equal(diff, d.State.AwayRuns - d.State.HomeRuns);
        return d;
    }

    /// <summary>A state like <see cref="Make"/> but with a non-trivial count (3 balls and 2 strikes), to show the plan does not depend on the count.</summary>
    public static GameState WithFullCount(Root root, int diff, int occupancy, int outs)
    {
        var s = Make(root, diff, occupancy, outs);
        var d = new Driver(s, "c");
        for (var i = 0; i < 3; i++)
            d.Do(new PitchEvent(d.Id(), new PitchSpec(PitchOutcome.Ball)));
        for (var i = 0; i < 2; i++)
            d.Do(new PitchEvent(d.Id(), new PitchSpec(PitchOutcome.CalledStrike)));
        Assert.Equal(new PitchCountState(3, 2), d.State.Count);
        return d.State;
    }
}
