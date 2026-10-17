using System.Collections.Immutable;
using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.SimHarness;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// F-B independent oracle. Every expected number below is hand-tallied from the script written in the test (the tally is shown beside it); nothing is read back from the recorder or the
/// evaluator under test. Fixture 1 is counted straight from plain events and facts; fixture 2 is a real game driven through the PUBLIC reducer by hand-written events (no resolver, no RNG),
/// so the production <see cref="BoxScoreBuilder"/> can be compared with the recorder on a game whose every number is known.
/// </summary>
public class MetricsOracleTests
{
    private static PitchLocation Loc(string zone) =>
        H.Ok(PitchLocation.FromZone(ZoneModelVersion.Pzl1, H.Ok(PitchZoneId.Create(zone)), AppliedBatterSide.Right));

    private static PlateAppearanceCompleted Pa(BatterResult r) => new("fx", "p", default, default, r, 0);

    // ---------------------------------------------------------------- fixture 1: plain events and facts

    private static MetricsRecorder Fixture1()
    {
        var recorder = new MetricsRecorder();
        var seq = 0;
        void Pitch(PitchOutcome o, string zone, BatterResult? ends = null, bool take = false)
        {
            GameEvent e = take ? new TakeEvent($"e{++seq}", new PitchSpec(o, Location: Loc(zone))) : new PitchEvent($"e{++seq}", new PitchSpec(o, Location: Loc(zone)));
            Assert.Equal(ObserveOutcome.Counted, recorder.Observe(e, seq, ends is { } r ? [Pa(r)] : []));
        }

        void InPlay(InPlayResult result, string zone, BatterResult ends) =>
            Assert.Equal(ObserveOutcome.Counted, recorder.Observe(new InPlayEvent($"e{++seq}", BattedBallType.Fly, result, Location: Loc(zone)), seq, [Pa(ends)]));

        // PA1 single
        Pitch(PitchOutcome.Ball, "out.low");
        Pitch(PitchOutcome.CalledStrike, "in.r2c2");
        InPlay(InPlayResult.Single, "in.r2c3", BatterResult.Single);
        // PA2 strikeout swinging after two fouls
        Pitch(PitchOutcome.Foul, "in.r1c1");
        Pitch(PitchOutcome.SwingingStrike, "out.high");
        Pitch(PitchOutcome.Foul, "in.r3c3");
        Pitch(PitchOutcome.SwingingStrike, "in.r2c1", BatterResult.Strikeout);
        // PA3 walk (the fourth ball is called on an inner-zone location)
        Pitch(PitchOutcome.Ball, "out.in");
        Pitch(PitchOutcome.Ball, "out.high_in");
        Pitch(PitchOutcome.Ball, "out.low_out");
        Pitch(PitchOutcome.Ball, "in.r1c3", BatterResult.Walk);
        // PA4 hit by pitch
        Pitch(PitchOutcome.HitByPitch, "out.in", BatterResult.HitByPitch);
        // PA5 home run, PA6 double (on an outside pitch), PA7 triple, PA8 sacrifice fly, PA9 ordinary out
        InPlay(InPlayResult.HomeRun, "in.r2c2", BatterResult.HomeRun);
        InPlay(InPlayResult.Double, "out.low", BatterResult.Double);
        InPlay(InPlayResult.Triple, "in.r3c2", BatterResult.Triple);
        InPlay(InPlayResult.Out, "in.r2c2", BatterResult.SacrificeFly);
        InPlay(InPlayResult.Out, "in.r2c3", BatterResult.Out);
        // an unfinished plate appearance: two explicit Takes
        Pitch(PitchOutcome.Ball, "out.high_out", take: true);
        Pitch(PitchOutcome.CalledStrike, "in.r1c2", take: true);
        return recorder;
    }

    [Fact]
    public void Fixture_1_counters_equal_the_hand_tally()
    {
        var c = Fixture1().Counters;

        // plate appearances: single, strikeout, walk, HBP, HR, double, triple, SF, out = 9; at-bats = 9 - walk - HBP - SF = 6; hits = single + HR + double + triple = 4
        Assert.Equal((9L, 6L, 4L), (c.PlateAppearances, c.AtBats, c.Hits));
        Assert.Equal((1L, 1L, 1L, 1L), (c.Singles, c.Doubles, c.Triples, c.HomeRuns));
        Assert.Equal((1L, 1L, 1L, 1L, 0L), (c.Walks, c.HitByPitch, c.Strikeouts, c.SacrificeFlies, c.SacrificeBunts));
        // pitches: 3 + 4 + 4 + 1 + 5 in play + 2 takes = 19, every one located
        Assert.Equal((19L, 19L, 0L), (c.Pitches, c.LocatedPitches, c.UnlocatedPitches));
        // inner-zone pitches: in.r2c2 (strike), in.r2c3, in.r1c1, in.r3c3, in.r2c1, in.r1c3 (ball), in.r2c2 (HR), in.r3c2, in.r2c2 (SF), in.r2c3, in.r1c2 (take) = 11
        Assert.Equal(11L, c.ZonePitches);
        // swings: 6 in play + 2 fouls + 2 swinging strikes = 10; one hit-by-pitch pitch
        Assert.Equal((10L, 2L, 2L, 6L, 1L), (c.Swings, c.SwingingStrikes, c.Fouls, c.InPlayPitches, c.HitByPitchPitches));
        Assert.Equal((6L, 2L, 2L), (c.CalledBalls, c.CalledStrikes, c.Takes));
        Assert.Equal(c.Pitches, c.CalledBalls + c.CalledStrikes + c.Swings + c.HitByPitchPitches);   // each pitch has exactly one outcome
        Assert.Equal((1L, 4L), (c.EventHomeRuns, c.EventHits));
        Assert.Equal((19L, 0L), (c.Events, c.DuplicatesIgnored));
    }

    [Fact]
    public void Fixture_1_metrics_equal_the_hand_arithmetic()
    {
        var report = H.Ok(MetricsEvaluator.Evaluate(Fixture1().Counters, NeutralCohort.Create(), H.Tuning, EvaluationPolicy.Unapproved));
        MetricResult M(string f) => report.Metrics.Single(m => m.FId == f);

        Assert.Equal((1L, 9L, 111_111L), (M("F1").NumeratorCount, M("F1").DenominatorCount, M("F1").MeasuredPpm!.Value));     // 1/9 = 111,111.1
        Assert.Equal(111_111L, M("F2").MeasuredPpm);
        Assert.Equal(111_111L, M("F3").MeasuredPpm);
        Assert.Equal(111_111L, M("F4").MeasuredPpm);
        // BABIP = (H - HR) / (AB - SO - HR + SF) = (4 - 1) / (6 - 1 - 1 + 1) = 3/5
        Assert.Equal((3L, 5L, 600_000L), (M("F5").NumeratorCount, M("F5").DenominatorCount, M("F5").MeasuredPpm!.Value));
        Assert.Equal((11L, 19L, 578_947L), (M("F6").NumeratorCount, M("F6").DenominatorCount, M("F6").MeasuredPpm!.Value));   // 11/19 = 578,947.4
        // swing rate excludes the hit-by-pitch pitch from the denominator: 10 / (19 - 1) = 5/9 = 555,555.6 -> 555,556
        Assert.Equal((10L, 18L, 555_556L), (M("F7").NumeratorCount, M("F7").DenominatorCount, M("F7").MeasuredPpm!.Value));
        Assert.Equal((2L, 10L, 200_000L), (M("F8").NumeratorCount, M("F8").DenominatorCount, M("F8").MeasuredPpm!.Value));
        // deviation from the declared interval in the tuning file: below the low bound is negative, above the high bound positive, inside is 0
        Assert.Equal(111_111L - 160_000L, M("F1").DeviationPpm);
        Assert.Equal(111_111L - 110_000L, M("F2").DeviationPpm);
        Assert.Equal(600_000L - 320_000L, M("F5").DeviationPpm);
        Assert.Equal(0L, M("F6").DeviationPpm);
        Assert.Equal(0L, M("F7").DeviationPpm);
        Assert.Equal(0L, M("F8").DeviationPpm);
    }

    [Fact]
    public void A_pitch_is_never_counted_twice_whichever_records_describe_it()
    {
        var recorder = new MetricsRecorder();
        var inPlay = new InPlayEvent("e1", BattedBallType.Fly, InPlayResult.Single, Location: Loc("in.r2c2"));
        Assert.Equal(ObserveOutcome.Counted, recorder.Observe(inPlay, 1, [Pa(BatterResult.Single)]));
        Assert.Equal(ObserveOutcome.DuplicateIgnored, recorder.Observe(inPlay, 1, [Pa(BatterResult.Single)]));
        Assert.Equal(ObserveOutcome.DuplicateIgnored, recorder.Observe(new PitchEvent("e0", new PitchSpec(PitchOutcome.Ball)), 1, []));    // an older or equal sequence

        Assert.Equal((1L, 1L, 1L, 1L), (recorder.Counters.Pitches, recorder.Counters.InPlayPitches, recorder.Counters.PlateAppearances, recorder.Counters.Events));
        Assert.Equal(2L, recorder.Counters.DuplicatesIgnored);
        // an in-play pitch is ONE pitch, one swing, and not a swinging strike
        Assert.Equal((1L, 0L), (recorder.Counters.Swings, recorder.Counters.SwingingStrikes));
    }

    [Fact]
    public void The_zone_is_the_actual_location_and_a_pitch_without_one_is_reported_not_guessed()
    {
        var recorder = new MetricsRecorder();
        recorder.Observe(new PitchEvent("e1", new PitchSpec(PitchOutcome.Ball, Location: Loc("out.high"))), 1, []);       // a ball just outside
        recorder.Observe(new PitchEvent("e2", new PitchSpec(PitchOutcome.CalledStrike, Location: Loc("in.r1c1"))), 2, []); // a corner of the zone
        recorder.Observe(new PitchEvent("e3", new PitchSpec(PitchOutcome.Ball)), 3, []);                                    // legacy event: no location

        Assert.Equal((3L, 2L, 1L, 1L), (recorder.Counters.Pitches, recorder.Counters.LocatedPitches, recorder.Counters.UnlocatedPitches, recorder.Counters.ZonePitches));
        // F6's denominator is the located pitches only: the unlocated one is neither inside nor outside
        var report = H.Ok(MetricsEvaluator.Evaluate(recorder.Counters, NeutralCohort.Create(), H.Tuning, EvaluationPolicy.Unapproved));
        Assert.Equal((1L, 2L, 500_000L), (report.Metrics[5].NumeratorCount, report.Metrics[5].DenominatorCount, report.Metrics[5].MeasuredPpm!.Value));
    }

    [Fact]
    public void Foul_and_foul_bunt_are_swings_a_hit_by_pitch_and_a_take_are_not()
    {
        var recorder = new MetricsRecorder();
        recorder.Observe(new PitchEvent("e1", new PitchSpec(PitchOutcome.Foul)), 1, []);
        recorder.Observe(new PitchEvent("e2", new PitchSpec(PitchOutcome.FoulBunt)), 2, []);
        recorder.Observe(new PitchEvent("e3", new PitchSpec(PitchOutcome.HitByPitch)), 3, [Pa(BatterResult.HitByPitch)]);
        recorder.Observe(new TakeEvent("e4", new PitchSpec(PitchOutcome.CalledStrike)), 4, []);

        var c = recorder.Counters;
        Assert.Equal((4L, 2L, 2L, 0L, 1L, 1L), (c.Pitches, c.Swings, c.Fouls, c.SwingingStrikes, c.HitByPitchPitches, c.Takes));
        var f7 = H.Ok(MetricsEvaluator.Evaluate(c, NeutralCohort.Create(), H.Tuning, EvaluationPolicy.Unapproved)).Metrics[6];
        Assert.Equal((2L, 3L, 666_667L), (f7.NumeratorCount, f7.DenominatorCount, f7.MeasuredPpm!.Value));       // 2 / (4 - 1)
    }

    [Fact]
    public void A_strikeout_that_reaches_base_counts_as_an_at_bat_and_a_strikeout_and_a_sacrifice_hit_as_neither_at_bat_nor_hit()
    {
        var recorder = new MetricsRecorder();
        recorder.Observe(new PitchEvent("e1", new PitchSpec(PitchOutcome.SwingingStrike)), 1, [Pa(BatterResult.StrikeoutReachedBase)]);
        recorder.Observe(new InPlayEvent("e2", BattedBallType.Bunt, InPlayResult.Out, true), 2, [Pa(BatterResult.SacrificeBunt)]);
        recorder.Observe(new InPlayEvent("e3", BattedBallType.Ground, InPlayResult.ReachedOnError), 3, [Pa(BatterResult.ReachedOnError)]);
        recorder.Observe(new InPlayEvent("e4", BattedBallType.Ground, InPlayResult.FieldersChoice), 4, [Pa(BatterResult.FieldersChoice)]);

        var c = recorder.Counters;
        Assert.Equal((4L, 3L, 0L, 1L, 0L, 1L), (c.PlateAppearances, c.AtBats, c.Hits, c.Strikeouts, c.SacrificeFlies, c.SacrificeBunts));
    }

    // ---------------------------------------------------------------- fixture 2: a real game driven by hand through the public reducer

    private sealed class HandGame
    {
        private readonly RunConfig _config = H.Config();
        private int _id;

        public HandGame() => State = H.Ok(GameReducer.Start(new GameStart("fx-box", _config.Rules, _config.Cohort.Away, _config.Cohort.Home, null)));

        public GameState State { get; private set; }

        public MetricsRecorder Recorder { get; } = new();

        public int Applied { get; private set; }

        private void Do(GameEvent e)
        {
            var step = H.Ok(GameReducer.Apply(State, e));
            State = step.State;
            Applied++;
            Assert.Equal(ObserveOutcome.Counted, Recorder.Observe(e, Applied, step.Facts));
        }

        private string Id() => $"fx{++_id:D3}";

        private PitchSpec P(PitchOutcome o, string zone) => new(o, Location: Loc(zone));

        public void Approach() => Do(new ApproachEvent(Id(), ApproachKind.Normal));

        public void Pitch(PitchOutcome o, string zone) => Do(new PitchEvent(Id(), P(o, zone)));

        public void Steal(int from) => Do(new RunnerPlayEvent(Id(), RunnerPlayKind.Steal, ImmutableList.Create(new RunnerMove(from, from + 1, MoveCause.Steal))));

        public void FlyOut(string zone, ImmutableList<RunnerMove>? moves = null) =>
            Do(new InPlayEvent(Id(), BattedBallType.Fly, InPlayResult.Out, false, moves, ImmutableList.Create(new PositionCredit(FieldPosition.LeftField, CreditKind.Putout)), Location: Loc(zone)));

        public void Hit(InPlayResult result, string zone, BattedBallType type = BattedBallType.Line) => Do(new InPlayEvent(Id(), type, result, Location: Loc(zone)));

        public void Looking(string z1, string z2, string z3)
        {
            Pitch(PitchOutcome.CalledStrike, z1);
            Pitch(PitchOutcome.CalledStrike, z2);
            Pitch(PitchOutcome.CalledStrike, z3);
        }
    }

    private static ImmutableList<RunnerMove> Hold(params int[] bases) => bases.Select(b => new RunnerMove(b, b)).ToImmutableList();

    private static HandGame PlayFixture2()
    {
        var g = new HandGame();
        // ---- top 1: walk (4 pitches), two steals, sacrifice fly (1), looking strikeout (3), fly out (1)
        g.Approach();
        g.Pitch(PitchOutcome.Ball, "out.in");
        g.Pitch(PitchOutcome.Ball, "out.high");
        g.Pitch(PitchOutcome.Ball, "in.r1c1");
        g.Pitch(PitchOutcome.Ball, "out.low_out");
        g.Steal(1);
        g.Steal(2);
        g.Approach();
        g.FlyOut("in.r2c2", moves: ImmutableList.Create(new RunnerMove(3, 4)));      // the reducer itself recognises a sacrifice fly: fly out, run scores, fewer than two outs
        g.Approach();
        g.Looking("in.r1c2", "in.r2c2", "in.r3c2");
        g.Approach();
        g.FlyOut("out.low");
        // ---- bottom 1: single, hit by pitch, swinging strikeout (foul, SS, SS), mixed strikeout (CS, foul, SS), fly out
        g.Approach();
        g.Hit(InPlayResult.Single, "in.r2c3", BattedBallType.Ground);
        g.Approach();
        g.Pitch(PitchOutcome.HitByPitch, "out.in");
        g.Approach();
        g.Pitch(PitchOutcome.Foul, "in.r1c1");
        g.Pitch(PitchOutcome.SwingingStrike, "out.high");
        g.Pitch(PitchOutcome.SwingingStrike, "in.r3c3");
        g.Approach();
        g.Pitch(PitchOutcome.CalledStrike, "in.r2c1");
        g.Pitch(PitchOutcome.Foul, "in.r2c2");
        g.Pitch(PitchOutcome.SwingingStrike, "out.low_in");
        g.Approach();
        g.FlyOut("in.r3c2", Hold(1, 2));                                                  // runners on first and second stay (a batted-ball play describes every runner)
        // ---- top 2: home run, double, fly out, K (ball, CS, foul, SS), looking K
        g.Approach();
        g.Hit(InPlayResult.HomeRun, "in.r2c2", BattedBallType.Fly);
        g.Approach();
        g.Hit(InPlayResult.Double, "out.out");
        g.Approach();
        g.FlyOut("in.r1c3", Hold(2));
        g.Approach();
        g.Pitch(PitchOutcome.Ball, "out.low");
        g.Pitch(PitchOutcome.CalledStrike, "in.r2c2");
        g.Pitch(PitchOutcome.Foul, "in.r2c1");
        g.Pitch(PitchOutcome.SwingingStrike, "in.r2c2");
        g.Approach();
        g.Looking("in.r3c1", "in.r3c2", "in.r3c3");
        // ---- bottom 2: an unfinished plate appearance (ball, called strike)
        g.Approach();
        g.Pitch(PitchOutcome.Ball, "out.high_in");
        g.Pitch(PitchOutcome.CalledStrike, "in.r2c2");
        return g;
    }

    [Fact]
    public void Fixture_2_counters_equal_the_hand_tally_including_the_unfinished_plate_appearance()
    {
        var c = PlayFixture2().Recorder.Counters;

        // 14 completed plate appearances: walk, SF, K, out | single, HBP, K, K, out | HR, double, out, K, K; the 15th is unfinished
        Assert.Equal((14L, 11L, 3L), (c.PlateAppearances, c.AtBats, c.Hits));     // at-bats = 14 - walk - HBP - SF = 11; hits = single + HR + double
        Assert.Equal((1L, 1L, 1L, 0L), (c.Singles, c.Doubles, c.HomeRuns, c.Triples));
        Assert.Equal((1L, 1L, 5L, 1L), (c.Walks, c.HitByPitch, c.Strikeouts, c.SacrificeFlies));
        // pitches: top 1 = 4+1+3+1 = 9, bottom 1 = 1+1+3+3+1 = 9, top 2 = 1+1+1+4+3 = 10, unfinished = 2
        Assert.Equal((30L, 30L), (c.Pitches, c.LocatedPitches));
        // inner-zone pitches: top 1: 1+1+3+0 = 5; bottom 1: 1+0+2+2+1 = 6; top 2: 1+0+1+3+3 = 8; unfinished: 1
        Assert.Equal(20L, c.ZonePitches);
        // swings: 7 in play (SF, out, single, out, HR, double, out) + 3 fouls + 4 swinging strikes = 14
        Assert.Equal((14L, 4L, 3L, 7L, 1L), (c.Swings, c.SwingingStrikes, c.Fouls, c.InPlayPitches, c.HitByPitchPitches));
        Assert.Equal((6L, 9L), (c.CalledBalls, c.CalledStrikes));
        // events: 15 approaches + 2 steals + 30 pitches
        Assert.Equal(47L, c.Events);
    }

    [Fact]
    public void Fixture_2_matches_the_production_box_score_on_every_compared_item()
    {
        var game = PlayFixture2();
        var cross = BoxScoreCrossCheck.Compare(game.Recorder.Counters, game.State);

        Assert.True(cross.Passed, string.Join(", ", cross.Mismatches.Select(m => $"{m.Name} {m.FromEvents}/{m.FromBoxScore}")));
        Assert.Equal(15, cross.Items.Count);
        Assert.Equal(30L, cross.Items.Single(i => i.Name == "Pitches").FromBoxScore);          // the reducer's own per-pitch stat entries
        Assert.Equal(11L, cross.Items.Single(i => i.Name == "AB").FromBoxScore);
        Assert.Equal(5L, cross.Items.Single(i => i.Name == "SO").FromBoxScore);
    }

    [Fact]
    public void A_counter_that_disagrees_with_the_box_score_is_reported_by_name()
    {
        var game = PlayFixture2();
        game.Recorder.Counters.Hits++;
        game.Recorder.Counters.Pitches--;

        var cross = BoxScoreCrossCheck.Compare(game.Recorder.Counters, game.State);

        Assert.False(cross.Passed);
        Assert.Equal(["H", "Pitches", "H(in-play events)"], cross.Mismatches.Select(m => m.Name).ToArray());
    }

    [Fact]
    public void Fixture_2_metrics_equal_the_hand_arithmetic()
    {
        var report = H.Ok(MetricsEvaluator.Evaluate(PlayFixture2().Recorder.Counters, NeutralCohort.Create(), H.Tuning, EvaluationPolicy.Unapproved));
        long P(string f) => report.Metrics.Single(m => m.FId == f).MeasuredPpm!.Value;

        Assert.Equal(357_143L, P("F1"));       // 5/14 = 357,142.9
        Assert.Equal(71_429L, P("F2"));        // 1/14 = 71,428.6
        Assert.Equal(71_429L, P("F3"));
        Assert.Equal(71_429L, P("F4"));
        Assert.Equal(333_333L, P("F5"));       // (3 - 1) / (11 - 5 - 1 + 1) = 2/6
        Assert.Equal(666_667L, P("F6"));       // 20/30
        Assert.Equal(482_759L, P("F7"));       // 14 / (30 - 1) = 482,758.6
        Assert.Equal(285_714L, P("F8"));       // 4/14 = 285,714.3
    }

    [Fact]
    public void An_unfinished_game_is_measured_on_what_was_confirmed_and_its_box_score_still_agrees()
    {
        var game = PlayFixture2();

        Assert.Equal(GameStatus.InProgress, game.State.Status);
        Assert.Null(game.State.Final);
        Assert.True(BoxScoreCrossCheck.Compare(game.Recorder.Counters, game.State).Passed);
    }
}
