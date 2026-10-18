using System.Collections.Immutable;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>F-C2-B B-2: the two calibration cohorts of ADR-008 (FC-D1 <c>neutral-mixed-v1</c>, FC-D2 <c>virtual-league-v1</c>), their per-game rotation, input hash binding and isolation.</summary>
public class CalibrationCohortTests
{
    private static PlayerId P(string id) => H.Ok(PlayerId.Create(id));

    private static IEnumerable<PlayerSheet> Batters(CohortInputs c) => c.Away.Lineup.Concat(c.Home.Lineup).Select(s => c.Sheets[s.Player]);

    [Fact]
    public void Neutral_mixed_is_all_fifty_right_handed_batters_and_a_three_right_four_left_rotation()
    {
        var c = CalibrationCohorts.NeutralMixed();
        Assert.Equal(("neutral-mixed-v1", CohortKind.Neutral, "sp7-3r4l-v1", false), (c.CohortId, c.Kind, c.Rotation!.Id, c.Rotation.RotateBattingOrder));
        Assert.All(c.Sheets.Values, s =>
        {
            if (s.Ratings.Hitter is { } h) Assert.All(new[] { h.Contact, h.Power, h.Eye, h.Speed, h.Fielding, h.Arm }, v => Assert.Equal(50, v.Value));
            if (s.Ratings.Pitcher is { } p) Assert.All(new[] { p.Stuff, p.Control, p.Velocity, p.Endurance }, v => Assert.Equal(50, v.Value));
            if (s.Ratings.Catcher is { } k) Assert.All(new[] { k.ReceivingFraming, k.Blocking, k.PopTime, k.GameCalling }, v => Assert.Equal(50, v.Value));
            Assert.Equal(BattingSide.Right, s.Player.Bats);
        });
        foreach (var starters in new[] { c.Rotation.AwayStarters, c.Rotation.HomeStarters })
            Assert.Equal(new[] { ThrowingHand.Right, ThrowingHand.Right, ThrowingHand.Right, ThrowingHand.Left, ThrowingHand.Left, ThrowingHand.Left, ThrowingHand.Left },
                starters.Select(p => c.Sheets[p].Player.Throws).ToArray());
        Assert.Equal(2 * (9 + 7 + 1 + 1), c.Sheets.Count);                                             // lineup, rotation, reliever, bench hitter per team

        for (var g = 1; g <= 21; g++)
        {
            var (away, home) = c.SetupFor(g);
            var slot = (g - 1) % 7;
            Assert.Equal((P($"nm-a-sp{slot + 1}"), P($"nm-h-sp{slot + 1}")), (away.StartingPitcher, home.StartingPitcher));
            Assert.Equal(slot < 3 ? HandednessStratum.Same : HandednessStratum.Opposite, c.StratumOf(g));
            Assert.Equal(c.Away.Lineup.Select(s => s.Player), away.Lineup.OrderBy(s => s.Order).Select(s => s.Player));   // no batting-order rotation in the neutral cohort
            foreach (var (team, baseTeam) in new[] { (away, c.Away), (home, c.Home) })                  // every game rosters the whole team, nobody twice
                Assert.Equal(Roster(baseTeam).Order(), Roster(team).Order());
        }

        Assert.Equal((3, 4), (Enumerable.Range(1, 7).Count(g => c.StratumOf(g) == HandednessStratum.Same), Enumerable.Range(1, 7).Count(g => c.StratumOf(g) == HandednessStratum.Opposite)));
        var (a1, h1) = c.SetupFor(1);                                                                 // game 1 is the base setup
        foreach (var (x, y) in new[] { (a1, c.Away), (h1, c.Home) })
            Assert.Equal((y.Lineup.ToList(), y.StartingPitcher, y.Bench.ToList()), (x.Lineup.ToList(), x.StartingPitcher, x.Bench.ToList()), new SetupComparer());
        Assert.Equal(HandednessStratum.Same, NeutralCohort.Create().StratumOf(5));                    // neutral-v1: every game same-hand, no rotation
        Assert.Throws<ArgumentOutOfRangeException>(() => c.SetupFor(0));
    }

    private sealed class SetupComparer : IEqualityComparer<(List<StarPlayer.Domain.Baseball.Game.LineupSlot>, PlayerId, List<PlayerId>)>
    {
        public bool Equals((List<StarPlayer.Domain.Baseball.Game.LineupSlot>, PlayerId, List<PlayerId>) a, (List<StarPlayer.Domain.Baseball.Game.LineupSlot>, PlayerId, List<PlayerId>) b) =>
            a.Item1.SequenceEqual(b.Item1) && a.Item2 == b.Item2 && a.Item3.SequenceEqual(b.Item3);

        public int GetHashCode((List<StarPlayer.Domain.Baseball.Game.LineupSlot>, PlayerId, List<PlayerId>) obj) => 0;
    }

    private static IEnumerable<string> Roster(StarPlayer.Domain.Baseball.Game.TeamSetup t) => t.Lineup.Select(s => s.Player.Value).Append(t.StartingPitcher.Value).Concat(t.Bench.Select(b => b.Value));

    [Fact]
    public void Virtual_league_uses_only_the_approved_value_set_shifts_and_starters()
    {
        var c = CalibrationCohorts.VirtualLeague();
        int[] set = [35, 40, 45, 50, 50, 50, 55, 60, 65];
        Assert.Equal(("virtual-league-v1", CohortKind.VirtualLeague, "sp7-3r4l-bo9-v1", true), (c.CohortId, c.Kind, c.Rotation!.Id, c.Rotation.RotateBattingOrder));
        foreach (var team in new[] { c.Away, c.Home })
        {
            var hitters = team.Lineup.OrderBy(s => s.Order).Select(s => c.Sheets[s.Player].Ratings.Hitter!).ToList();
            int[][] columns = [.. new Func<HitterRatings, SkillValue>[] { h => h.Contact, h => h.Power, h => h.Eye, h => h.Speed, h => h.Fielding, h => h.Arm }.Select(f => hitters.Select(h => f(h).Value).ToArray())];
            int[] shifts = [0, 2, 4, 1, 3, 5];
            for (var k = 0; k < 6; k++)
            {
                Assert.Equal(set, columns[k].Order().ToArray());                                        // each ability uses the value set once per team: mean exactly 50
                Assert.All(Enumerable.Range(0, 9), i => Assert.Equal(set[(i + shifts[k]) % 9], columns[k][i]));
            }

            // no perfect correlation between abilities (FC-D2.2); the proposal's maximum is 0.357
            for (var a = 0; a < 6; a++)
                for (var b = a + 1; b < 6; b++)
                    Assert.True(Math.Abs(Pearson(columns[a], columns[b])) < 0.358, $"abilities {a}/{b}");
        }

        int[][] starters = [[40, 50, 60], [50, 60, 40], [60, 40, 50], [35, 45, 55], [45, 55, 65], [55, 65, 35], [65, 35, 45]];
        foreach (var rotation in new[] { c.Rotation.AwayStarters, c.Rotation.HomeStarters })
        {
            var p = rotation.Select(id => c.Sheets[id].Ratings.Pitcher!).ToList();
            Assert.Equal(starters, p.Select(r => new[] { r.Stuff.Value, r.Control.Value, r.Velocity.Value }).ToArray());
            foreach (var group in new[] { p.Take(3).ToList(), p.Skip(3).ToList() })                       // each handedness group averages 50
                Assert.All(new Func<PitcherRatings, int>[] { r => r.Stuff.Value, r => r.Control.Value, r => r.Velocity.Value }, f => Assert.Equal(50.0, group.Average(f)));
            Assert.Equal(new[] { ThrowingHand.Right, ThrowingHand.Right, ThrowingHand.Right, ThrowingHand.Left, ThrowingHand.Left, ThrowingHand.Left, ThrowingHand.Left }, rotation.Select(id => c.Sheets[id].Player.Throws).ToArray());
        }

        // no rating outside the value set anywhere (FC-D2.6), and every batter bats right
        Assert.All(c.Sheets.Values.SelectMany(s => Values(s.Ratings)), v => Assert.Contains(v, set));
        Assert.All(Batters(c), s => Assert.Equal(BattingSide.Right, s.Player.Bats));
    }

    private static IEnumerable<int> Values(Ratings r) =>
        (r.Hitter is { } h ? new[] { h.Contact, h.Power, h.Eye, h.Speed, h.Fielding, h.Arm } : [])
        .Concat(r.Pitcher is { } p ? new[] { p.Stuff, p.Control, p.Velocity, p.Endurance }.Concat(p.Repertoire.Pitches.Select(x => x.Grade)) : [])
        .Concat(r.Catcher is { } k ? new[] { k.ReceivingFraming, k.Blocking, k.PopTime, k.GameCalling } : []).Select(v => v.Value);

    private static double Pearson(int[] x, int[] y)
    {
        double mx = x.Average(), my = y.Average();
        var cov = x.Zip(y, (a, b) => (a - mx) * (b - my)).Sum();
        return cov / Math.Sqrt(x.Sum(a => (a - mx) * (a - mx)) * y.Sum(b => (b - my) * (b - my)));
    }

    [Fact]
    public void The_virtual_league_batting_order_rotates_with_the_players_positions_over_a_63_game_cycle()
    {
        var c = CalibrationCohorts.VirtualLeague();
        var baseOrder = c.Away.Lineup.OrderBy(s => s.Order).ToList();
        var seen = new Dictionary<(string Player, int Slot), int>();
        var pairs = new HashSet<(int Shift, string Starter)>();
        for (var g = 1; g <= 63; g++)
        {
            var away = c.SetupFor(g).Away;
            var shift = (g - 1) % 9;
            foreach (var slot in away.Lineup)
            {
                var expected = baseOrder[(slot.Order - 1 + shift) % 9];
                Assert.Equal((expected.Player, expected.Position), (slot.Player, slot.Position));             // positions stay with the players
                seen[(slot.Player.Value, slot.Order)] = seen.GetValueOrDefault((slot.Player.Value, slot.Order)) + 1;
            }

            Assert.True(pairs.Add((shift, away.StartingPitcher.Value)));                                   // LCM(7, 9) = 63: every starter meets every order shift once
            Assert.Equal(g % 7 is 1 or 2 or 3 ? HandednessStratum.Same : HandednessStratum.Opposite, c.StratumOf(g));
        }

        Assert.Equal(81, seen.Count);
        Assert.All(seen.Values, n => Assert.Equal(7, n));                                                  // each batter bats in each slot 7 times per cycle
    }

    [Fact]
    public void The_input_hash_binds_the_rotation_is_deterministic_and_leaves_neutral_v1_unchanged()
    {
        Assert.Equal("9343ad41d1e808c0afc2f47a01e8c9f54c1028656dcb6f75f44e48a21305cb70", NeutralCohort.Create().InputsHash);   // the F-A/B hash every legacy manifest records
        var nm = CalibrationCohorts.NeutralMixed();
        var vl = CalibrationCohorts.VirtualLeague();
        Assert.Equal(nm.InputsHash, CalibrationCohorts.NeutralMixed().InputsHash);
        Assert.Equal(vl.InputsHash, CalibrationCohorts.VirtualLeague().InputsHash);
        Assert.Equal(3, new[] { NeutralCohort.Create().InputsHash, nm.InputsHash, vl.InputsHash }.Distinct().Count());

        CohortInputs With(CohortRotation? r) => H.Ok(CohortInputs.Create(nm.CohortId, nm.Kind, nm.Description, nm.Away, nm.Home, nm.Sheets.Values, r));
        var r0 = nm.Rotation!;
        Assert.Equal(nm.InputsHash, With(r0).InputsHash);
        var reordered = r0 with { AwayStarters = [r0.AwayStarters[0], .. r0.AwayStarters.Skip(1).Reverse()] };
        var variants = new[] { With(r0 with { Id = "sp7-3r4l-v2" }), With(r0 with { RotateBattingOrder = true }), With(reordered), With(r0 with { HomeStarters = [r0.HomeStarters[0]] }) };
        Assert.All(variants, v => Assert.NotEqual(nm.InputsHash, v.InputsHash));
        Assert.NotEqual(nm.InputsHash, H.Ok(CohortInputs.Create(nm.CohortId, nm.Kind, nm.Description, nm.Away, nm.Home, nm.Sheets.Values)).InputsHash);   // dropping the rotation is another input

        string Bad(CohortRotation r) => CohortInputs.Create(nm.CohortId, nm.Kind, nm.Description, nm.Away, nm.Home, nm.Sheets.Values, r).Error!.Value.Code;
        Assert.Equal("Harness.cohort_rotation_invalid", Bad(r0 with { AwayStarters = r0.AwayStarters.Skip(1).ToImmutableArray() }));               // must start with the base starter
        Assert.Equal("Harness.cohort_rotation_invalid", Bad(r0 with { AwayStarters = [r0.AwayStarters[0], r0.AwayStarters[1], r0.AwayStarters[1]] }));   // duplicate
        Assert.Equal("Harness.cohort_rotation_invalid", Bad(r0 with { AwayStarters = [r0.AwayStarters[0], r0.HomeStarters[1]] }));                 // another team's pitcher
        Assert.Equal("Harness.cohort_rotation_invalid", Bad(r0 with { Id = "" }));
        Assert.Equal("Harness.cohort_rotation_invalid", Bad(r0 with { HomeStarters = [] }));
    }

    [Fact]
    public void Rotated_games_are_legal_on_the_real_engine_deterministic_and_actually_use_the_rotated_starter()
    {
        foreach (var (cohort, automated, index) in new[] { (CalibrationCohorts.NeutralMixed(), true, 4), (CalibrationCohorts.VirtualLeague(), false, 2) })
        {
            var config = H.Config("rot", cohort, H.Rules(automated));
            var a = H.Play(config, 2026101801, index);
            Assert.True(a.Completed, a.Failure?.Message);
            Assert.Equal(a.ResultHash, H.Play(config, 2026101801, index).ResultHash);                    // deterministic
        }

        // same cohort id, same GameIds and seeds; only the rating of starter #2 differs: game 1 (starter #1) is identical, game 2 (starter #2) is not
        var nm = CalibrationCohorts.NeutralMixed();
        var sp2 = new[] { P("nm-a-sp2"), P("nm-h-sp2") };
        var strong = nm.Sheets.Values.Select(s => sp2.Contains(s.Id) ? H.WithRatings(s, null, NeutralCohort.Pitcher(95, 95, 95), null) : s).ToList();
        var other = H.Ok(CohortInputs.Create(nm.CohortId, nm.Kind, nm.Description, nm.Away, nm.Home, strong, nm.Rotation));
        Assert.NotEqual(nm.InputsHash, other.InputsHash);
        var (c1, c2) = (H.Config("rot", nm, H.Rules(true)), H.Config("rot", other, H.Rules(true)));
        Assert.Equal(H.Play(c1, 7, 1).ResultHash, H.Play(c2, 7, 1).ResultHash);
        Assert.NotEqual(H.Play(c1, 7, 2).ResultHash, H.Play(c2, 7, 2).ResultHash);
    }

    [Fact]
    public void Cohort_runs_stay_separate_by_game_id_config_hash_and_plan()
    {
        var nm = H.Ok(RunConfig.Create("x", "dry", CalibrationCohorts.NeutralMixed(), H.Rules(true), H.Tuning, H.Park(), competitionKey: "fc2-dry-na"));
        var vl = H.Ok(RunConfig.Create("x", "dry", CalibrationCohorts.VirtualLeague(), H.Rules(true), H.Tuning, H.Park(), competitionKey: "fc2-dry-na"));
        Assert.NotEqual(nm.Spec(1, 5).GameId, vl.Spec(1, 5).GameId);                                    // the cohort id is in every GameId (another RNG context)
        Assert.NotEqual(nm.ConfigHash([nm.Spec(1, 5)]), vl.ConfigHash([vl.Spec(1, 5)]));
        var plan = new StopPlan("fc2-dry", 1, 10, [vl.Spec(1, 5)], "seeds/x.json", new string('a', 64));
        Assert.Throws<ArgumentException>(() => SimRunner.RunStopAware(nm, plan));                        // a plan of another cohort is never run
        // the official strata name exactly the two cohorts in the two modes
        Assert.Equal(new[] { "L1", "L2", "L3", "L4" }, CalibrationStrata.All.Select(s => s.Id).ToArray());
        Assert.Equal(4, CalibrationStrata.All.Select(s => (s.CohortId, s.RulesProfileId)).Distinct().Count());
        Assert.Equal(OfficialProfile.FamilyCohorts["fc3-cal"].Order(), CalibrationStrata.All.Select(s => s.CohortId).Distinct().Order());
    }
}
