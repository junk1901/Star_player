using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.Simulation.Tests;

/// <summary>Named rule profiles for tests. These are explicit fictional test values, not claims about official KBO rules.</summary>
internal static class TestRules
{
    public static GameRuleSnapshot Make(string id, bool dh = true, bool twoWay = true, int? limit = 11, int regulation = 9, int starterMin = 15,
        bool dhForfeit = false, bool walkOffLob = true, bool dhOnDefenseOnly = false)
    {
        var tie = limit is not null;
        return GameRuleSnapshot.Create(id, "test-v1", GameRulesStatus.FictionalTest, "테스트용 가상 규칙", ["단위 테스트 전용"], 3, 4, 3, regulation, limit, tie, dh, twoWay && dh, starterMin, 3, 3, 9, (dhForfeit || dhOnDefenseOnly) && dh, walkOffLob, dhForfeit && dh, "fictional-test-v1").Value;
    }

    public static GameRuleSnapshot Dh { get; } = Make("test-dh-cap11");
    public static GameRuleSnapshot NoDh { get; } = Make("test-no-dh-cap11", dh: false);
    public static GameRuleSnapshot Unlimited { get; } = Make("test-dh-unlimited", limit: null);
    public static GameRuleSnapshot Cap10 { get; } = Make("test-dh-cap10", limit: 10);
    public static GameRuleSnapshot DhNoTwoWay { get; } = Make("test-dh-no-two-way", twoWay: false);
    public static GameRuleSnapshot DhForfeit { get; } = Make("test-dh-forfeit", dhForfeit: true);
    public static GameRuleSnapshot DhOnDefenseOnly { get; } = Make("test-dh-obr-defense", dhOnDefenseOnly: true);
    public static GameRuleSnapshot DhForfeitNoTwoWay { get; } = Make("test-dh-forfeit-no-two-way", twoWay: false, dhForfeit: true);
    public static GameRuleSnapshot WalkOffNoLob { get; } = Make("test-walk-off-lob-off", walkOffLob: false);
}

/// <summary>
/// Concise driver for oracle tests: builds a standard fictional game and applies events with generated ids, asserting success
/// so that each test states only the play it is about. Away batters are a1..a9 (a5 is the DH, or ap bats fifth without a DH),
/// away pitcher ap, bench ab1..ab5; the home team uses h/hp/hb.
/// </summary>
internal sealed class Script
{
    private int _n;

    public GameState State { get; private set; }

    /// <summary>Every accepted event in order, so a test can replay or re-check a whole game.</summary>
    public List<GameEvent> Log { get; } = [];

    public GameRuleSnapshot Rules { get; }

    public Script(GameRuleSnapshot? rules = null)
    {
        Rules = rules ?? TestRules.Dh;
        State = GameReducer.Start(new GameStart("g1", Rules, Team("a", Rules.DesignatedHitter), Team("h", Rules.DesignatedHitter))).Value;
    }

    /// <summary>A standard fictional game started under a Phase 3-D game execution contract (park snapshot and pinned versions).</summary>
    public Script(GameExecution execution)
    {
        Rules = TestRules.Dh;
        State = GameReducer.Start(new GameStart("g1", Rules, Team("a", Rules.DesignatedHitter), Team("h", Rules.DesignatedHitter), execution)).Value;
    }

    public Script(GameRuleSnapshot rules, TeamSetup away, TeamSetup home)
    {
        Rules = rules;
        State = GameReducer.Start(new GameStart("g1", rules, away, home)).Value;
    }

    public static PlayerId P(string id) => PlayerId.Create(id).Value;

    public static TeamSetup Team(string prefix, bool dh, PlayerId? starter = null)
    {
        var order = new[] { FieldPosition.CenterField, FieldPosition.Shortstop, FieldPosition.RightField, FieldPosition.FirstBase,
            dh ? FieldPosition.DesignatedHitter : FieldPosition.Pitcher, FieldPosition.LeftField, FieldPosition.ThirdBase, FieldPosition.SecondBase, FieldPosition.Catcher };
        var slots = new List<LineupSlot>();
        for (var i = 0; i < 9; i++)
        {
            var player = !dh && order[i] == FieldPosition.Pitcher ? P(prefix + "p") : P($"{prefix}{i + 1}");
            slots.Add(new LineupSlot(i + 1, player, order[i]));
        }

        return new TeamSetup(slots, starter ?? P(prefix + "p"), Enumerable.Range(1, 5).Select(i => P($"{prefix}b{i}")).ToList());
    }

    public Result<GameStep> Try(GameEvent e)
    {
        var r = GameReducer.Apply(State, e);
        if (r.IsSuccess)
        {
            State = r.Value.State;
            Log.Add(e);
        }

        return r;
    }

    public Script Ok(GameEvent e)
    {
        var r = Try(e);
        Assert.True(r.IsSuccess, $"{e.GetType().Name} {e.Id}: {r.Error?.Code} {r.Error?.Message}");
        return this;
    }

    public string Id() => $"e{++_n}";

    // ---- events
    public Script Approach(ApproachKind kind = ApproachKind.Normal) => Ok(new ApproachEvent(Id(), kind));

    public Script Pitch(PitchOutcome outcome, PitchMishap mishap = PitchMishap.None, RunnerMove[]? moves = null, PitchMishap dropped = PitchMishap.None, PositionCredit[]? ks = null) =>
        Ok(new PitchEvent(Id(), new PitchSpec(outcome, mishap, moves?.ToImmutableList(), dropped, ks?.ToImmutableList())));

    public Script Take(PitchOutcome outcome, PitchMishap mishap = PitchMishap.None, RunnerMove[]? moves = null) =>
        Ok(new TakeEvent(Id(), new PitchSpec(outcome, mishap, moves?.ToImmutableList())));

    public Script Ball() => Pitch(PitchOutcome.Ball);
    public Script Strike() => Pitch(PitchOutcome.CalledStrike);
    public Script Swing() => Pitch(PitchOutcome.SwingingStrike);
    public Script Foul() => Pitch(PitchOutcome.Foul);

    public Script Walk() { Approach(); for (var i = 0; i < 4; i++) Ball(); return this; }
    public Script Strikeout() { Approach(); for (var i = 0; i < 3; i++) Swing(); return this; }
    public Script HitByPitch() { Approach(); return Pitch(PitchOutcome.HitByPitch); }

    public Script InPlay(BattedBallType batted, InPlayResult result, RunnerMove[]? moves = null, PositionCredit[]? batterCredits = null, FieldPosition[]? errors = null, bool sacrifice = false, int? batterSequence = null) =>
        Ok(new InPlayEvent(Id(), batted, result, sacrifice, moves?.ToImmutableList(), batterCredits?.ToImmutableList(), errors?.ToImmutableList(), batterSequence));

    public Script Single(params RunnerMove[] moves) { Approach(); return InPlay(BattedBallType.Line, InPlayResult.Single, moves); }
    public Script Double(params RunnerMove[] moves) { Approach(); return InPlay(BattedBallType.Line, InPlayResult.Double, moves); }
    public Script Triple(params RunnerMove[] moves) { Approach(); return InPlay(BattedBallType.Line, InPlayResult.Triple, moves); }
    public Script HomeRun(params RunnerMove[] moves) { Approach(); return InPlay(BattedBallType.Fly, InPlayResult.HomeRun, moves); }

    public Script FlyOut(params RunnerMove[] moves) { Approach(); return InPlay(BattedBallType.Fly, InPlayResult.Out, moves, [Po(FieldPosition.LeftField)]); }
    public Script GroundOut(params RunnerMove[] moves) { Approach(); return InPlay(BattedBallType.Ground, InPlayResult.Out, moves, [A(FieldPosition.Shortstop), Po(FieldPosition.FirstBase)]); }

    /// <summary>A plate appearance that is a simple out with nobody on base needing a move.</summary>
    public Script Out() => FlyOut();

    public Script Runner(RunnerPlayKind kind, params RunnerMove[] moves) => Ok(new RunnerPlayEvent(Id(), kind, moves.ToImmutableList()));

    public Script Sub(SubstitutionKind kind, TeamSide team, string player = "", int? @base = null, int? slot = null, int? slot2 = null) =>
        Ok(new SubstitutionEvent(Id(), kind, team, player.Length == 0 ? default : P(player), @base, slot, slot2));

    public Script Outs(int count) { for (var i = 0; i < count; i++) Out(); return this; }

    /// <summary>Plays <paramref name="halfInnings"/> half-innings of three straight outs each.</summary>
    public Script ThreeUp(int halfInnings = 1) { for (var i = 0; i < halfInnings; i++) Outs(3); return this; }

    // ---- helpers
    public static RunnerMove M(int from, int to, MoveCause cause = MoveCause.Play, OutKind? kind = null, params PositionCredit[] credits) =>
        new(from, to, cause, kind, credits.Length == 0 ? null : credits.ToImmutableList());

    /// <summary>A move with its position in the play's order of outs and runs, and optionally where an out was made.</summary>
    public static RunnerMove Seq(RunnerMove move, int sequence, int? outAt = null, OutMethod? method = null) => move with { Sequence = sequence, OutAt = outAt, Method = method };

    public static PositionCredit Po(FieldPosition p) => new(p, CreditKind.Putout);
    public static PositionCredit A(FieldPosition p) => new(p, CreditKind.Assist);

    public int Stat(string player, StatKey key) => State.Stats.Where(e => e.Player == P(player) && e.Key == key).Sum(e => e.Amount);

    public int Team(TeamSide team, StatKey key) => State.Stats.Where(e => e.Team == team && e.Key == key && e.Player == default).Sum(e => e.Amount);

    public Script Finish()
    {
        var r = GameReducer.Finalize(State);
        Assert.True(r.IsSuccess, r.Error?.Message);
        State = r.Value.State;
        return this;
    }

    public BoxScore Box => State.Final!.BoxScore;

    public int Line(string player, StatKey key) => Box.Players.Where(p => p.Player == P(player)).Sum(p => p.Get(key));
}
