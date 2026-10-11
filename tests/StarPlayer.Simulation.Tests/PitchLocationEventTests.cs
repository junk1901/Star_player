using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// Phase 3-C1: the optional pitch location on PitchEvent/TakeEvent (through PitchSpec) and InPlayEvent, the ef2 fingerprint that identifies it, and the
/// receipt comparison of ef1/ef2 events inside GameReducer.Apply. Nothing here resolves or draws a pitch: every location is supplied by the test.
/// </summary>
public class PitchLocationEventTests
{
    private static PitchLocation Loc(string zone = "in.r2c2", AppliedBatterSide side = AppliedBatterSide.Right) =>
        PitchLocation.FromZone(ZoneModelVersion.Pzl1, PitchZoneId.Create(zone).Value, side).Value;

    private static PitchEvent Ball(string id, PitchLocation? l) => new(id, new PitchSpec(PitchOutcome.Ball, Location: l));

    private static InPlayEvent Single(string id, PitchLocation? l) => new(id, BattedBallType.Line, InPlayResult.Single, Location: l);

    // ------------------------------------------------------------------ version choice and canonical text

    [Fact]
    public void An_event_with_a_location_is_ef2_and_every_other_event_stays_ef1()
    {
        Assert.Equal("ef2", EventFingerprint.Receipt(Ball("p", Loc())).FingerprintVersion);
        Assert.Equal("ef2", EventFingerprint.Receipt(new TakeEvent("t", new PitchSpec(PitchOutcome.Ball, Location: Loc()))).FingerprintVersion);
        Assert.Equal("ef2", EventFingerprint.Receipt(Single("i", Loc())).FingerprintVersion);

        GameEvent[] ef1 =
        [
            Ball("p", null), new TakeEvent("t", new PitchSpec(PitchOutcome.Ball)), Single("i", null), new ApproachEvent("a", ApproachKind.Normal),
            new RunnerPlayEvent("r", RunnerPlayKind.Steal, [M(1, 2, MoveCause.Steal)]), new SubstitutionEvent("s", SubstitutionKind.PinchRun, TeamSide.Away, P("ab1"), 2),
        ];
        Assert.All(ef1, e => Assert.Equal("ef1", EventFingerprint.Receipt(e).FingerprintVersion));
        Assert.Equal("ef1", EventFingerprint.Version);
        Assert.Equal("ef2", EventFingerprint.VersionV2);
    }

    [Fact]
    public void The_ef2_canonical_text_and_hash_are_a_fixed_contract()
    {
        var pitch = EventFingerprint.Receipt(Ball("any-id", Loc("in.r2c2", AppliedBatterSide.Right)));
        Assert.Equal("ef2|pitch|spec={Ball;None;[];None;~}|zone_model=pzl-zones-v1|zone=in.r2c2|side=Right|boundary=Clear", EventFingerprint.Canonical(Ball("x", Loc("in.r2c2"))));
        Assert.Equal("f917cb4e40f980e0574db818d072c2a610bc4e20a55daa38e720799b93a4f5e8", pitch.Fingerprint); // SHA-256 computed independently of the C# code
        Assert.Equal(("any-id", "pitch", "ef2"), (pitch.EventId, pitch.Kind, pitch.FingerprintVersion));

        var inPlay = Single("x", Loc("out.low_out", AppliedBatterSide.Left));
        Assert.Equal("ef2|in_play|batted=Line|result=Single|sacrifice=0|moves=[]|batter_credits=[]|errors=[]|batter_sequence=~|zone_model=pzl-zones-v1|zone=out.low_out|side=Left|boundary=Clear",
            EventFingerprint.Canonical(inPlay));
        Assert.Equal("2442ffd005cd532fd56d0260940280007085b1b2d9d3e97481eb53ea844f387a", EventFingerprint.Receipt(inPlay).Fingerprint);
    }

    [Fact]
    public void The_ef2_text_is_the_whole_ef1_content_plus_the_four_location_fields_in_a_fixed_order()
    {
        GameEvent[] pairs = [Ball("p", null), new TakeEvent("t", new PitchSpec(PitchOutcome.Foul, Moves: [M(1, 2, MoveCause.WildPitch)])), Single("i", null)];
        foreach (var legacy in pairs)
        {
            GameEvent located = legacy switch
            {
                PitchEvent p => p with { Spec = p.Spec with { Location = Loc("in.r1c3", AppliedBatterSide.Left) } },
                TakeEvent t => t with { Spec = t.Spec with { Location = Loc("in.r1c3", AppliedBatterSide.Left) } },
                InPlayEvent i => i with { Location = Loc("in.r1c3", AppliedBatterSide.Left) },
                _ => throw new InvalidOperationException(),
            };
            var ef1 = EventFingerprint.Canonical(legacy);
            Assert.StartsWith("ef1|", ef1);
            Assert.Equal("ef2|" + ef1[4..] + "|zone_model=pzl-zones-v1|zone=in.r1c3|side=Left|boundary=Borderline", EventFingerprint.Canonical(located));
        }
    }

    [Fact]
    public void Changing_any_of_the_location_fields_changes_the_fingerprint_for_every_kind_of_pitch_event()
    {
        // zone (Clear centre, two Borderline cells, a Clear outer zone) and side; the boundary class follows the zone and is covered by the Clear/Borderline mix
        var variants = new[] { Loc("in.r2c2"), Loc("in.r1c1"), Loc("in.r1c2"), Loc("out.low_out"), Loc("in.r2c2", AppliedBatterSide.Left), Loc("in.r1c1", AppliedBatterSide.Left) };
        Assert.Contains(variants, v => v.Boundary == BoundaryClass.Clear);
        Assert.Contains(variants, v => v.Boundary == BoundaryClass.Borderline);
        Func<PitchLocation?, GameEvent>[] kinds =
        [
            l => Ball("x", l), l => new TakeEvent("x", new PitchSpec(PitchOutcome.Ball, Location: l)), l => Single("x", l),
        ];
        foreach (var make in kinds)
        {
            var hashes = variants.Select(v => EventFingerprint.Receipt(make(v)).Fingerprint).Append(EventFingerprint.Receipt(make(null)).Fingerprint).ToList();
            Assert.Equal(hashes.Count, hashes.Distinct().Count());
        }
    }

    [Fact]
    public void A_location_cannot_be_built_with_an_unknown_model_a_wrong_class_an_unknown_zone_or_an_invalid_side()
    {
        var zone = PitchZoneId.Create("in.r1c1").Value; // Borderline
        Assert.Equal("PitchLocation.boundary_mismatch", PitchLocation.Create(ZoneModelVersion.Pzl1, zone, AppliedBatterSide.Right, BoundaryClass.Clear).Error!.Value.Code);
        Assert.Equal("PitchLocation.side_invalid", PitchLocation.Create(ZoneModelVersion.Pzl1, zone, (AppliedBatterSide)9, BoundaryClass.Borderline).Error!.Value.Code);
        Assert.Equal("PitchLocation.boundary_invalid", PitchLocation.Create(ZoneModelVersion.Pzl1, zone, AppliedBatterSide.Right, (BoundaryClass)9).Error!.Value.Code);
        Assert.Equal("ZoneModelVersion.unsupported", ZoneModelVersion.Create("pzl-zones-v2").Error!.Value.Code);
        Assert.Equal("PitchZoneId.unknown", PitchZoneId.Create("in.r9c9").Error!.Value.Code);
        Assert.Equal("ZoneModelVersion.unsupported", PitchLocation.Create(default, zone, AppliedBatterSide.Right, BoundaryClass.Borderline).Error!.Value.Code);
    }

    // ------------------------------------------------------------------ receipt comparison in GameReducer.Apply

    // a Take is the explicit one-pitch action of a plate appearance that has no approach yet; every other pitch event follows an approach
    private static Script Started(GameEvent e) => e is TakeEvent ? new Script() : new Script().Approach();

    [Fact]
    public void An_ef2_event_delivered_again_unchanged_is_a_duplicate_that_changes_nothing()
    {
        foreach (var make in new Func<GameEvent>[] { () => Ball("p", Loc()), () => new TakeEvent("t", new PitchSpec(PitchOutcome.Ball, Location: Loc())), () => Single("i", Loc("in.r1c1")) })
        {
            var s = Started(make());
            s.Ok(make());
            var before = s.State;
            var again = GameReducer.Apply(s.State, make());
            Assert.True(again.IsSuccess && again.Value.WasDuplicate);
            Assert.Same(before, again.Value.State);
            Assert.Equal("ef2", before.AppliedEvents[^1].FingerprintVersion);
        }
    }

    [Fact]
    public void An_ef2_event_with_another_zone_or_another_batter_side_is_a_content_conflict()
    {
        foreach (var make in new Func<PitchLocation, GameEvent>[] { l => Ball("p", l), l => new TakeEvent("p", new PitchSpec(PitchOutcome.Ball, Location: l)), l => Single("p", l) })
        {
            var s = Started(make(Loc()));
            s.Ok(make(Loc("in.r2c2", AppliedBatterSide.Right)));
            var before = s.State;
            foreach (var changed in new[] { Loc("in.r1c1", AppliedBatterSide.Right), Loc("in.r2c2", AppliedBatterSide.Left), Loc("out.low_out", AppliedBatterSide.Right) })
            {
                var result = GameReducer.Apply(s.State, make(changed));
                Assert.Equal("Game.event_id_conflict", result.Error!.Value.Code);
            }

            Assert.Same(before, s.State);
        }
    }

    [Fact]
    public void The_same_id_with_other_content_or_another_kind_is_a_conflict_even_when_the_location_is_equal()
    {
        var s = Started(Ball("p", null));
        s.Ok(Ball("p", Loc()));
        Assert.Equal("Game.event_id_conflict", GameReducer.Apply(s.State, new PitchEvent("p", new PitchSpec(PitchOutcome.Foul, Location: Loc()))).Error!.Value.Code);
        Assert.Equal("Game.event_id_conflict", GameReducer.Apply(s.State, new TakeEvent("p", new PitchSpec(PitchOutcome.Ball, Location: Loc()))).Error!.Value.Code);
        Assert.Equal("Game.event_id_conflict", GameReducer.Apply(s.State, Single("p", Loc())).Error!.Value.Code);
        Assert.Equal("Game.event_id_conflict", GameReducer.Apply(s.State, new PitchEvent("p", new PitchSpec(PitchOutcome.Ball, PitchMishap.WildPitch, Location: Loc()))).Error!.Value.Code);
    }

    [Fact]
    public void An_ef1_receipt_redelivered_with_a_location_added_is_a_version_mismatch_never_a_duplicate()
    {
        var s = Started(Ball("p", null));
        s.Ok(Ball("p", null));
        s.Ok(Single("i", null));
        var before = s.State;
        Assert.Equal("ef1", before.AppliedEvents[1].FingerprintVersion);

        Assert.Equal("Game.event_fingerprint_version_mismatch", GameReducer.Apply(s.State, Ball("p", Loc())).Error!.Value.Code);
        Assert.Equal("Game.event_fingerprint_version_mismatch", GameReducer.Apply(s.State, Single("i", Loc())).Error!.Value.Code);
        Assert.Same(before, s.State);
        // the unchanged ef1 events are still plain duplicates and their receipts were not converted
        Assert.True(GameReducer.Apply(s.State, Ball("p", null)).Value.WasDuplicate);
        Assert.Equal(new[] { "ef1", "ef1", "ef1" }, s.State.AppliedEvents.Select(r => r.FingerprintVersion));
    }

    [Fact]
    public void An_ef2_receipt_redelivered_with_its_location_removed_is_a_version_mismatch()
    {
        var s = Started(Ball("p", null));
        s.Ok(Ball("p", Loc()));
        s.Ok(Single("i", Loc("in.r1c1")));
        var before = s.State;

        Assert.Equal("Game.event_fingerprint_version_mismatch", GameReducer.Apply(s.State, Ball("p", null)).Error!.Value.Code);
        Assert.Equal("Game.event_fingerprint_version_mismatch", GameReducer.Apply(s.State, Single("i", null)).Error!.Value.Code);
        Assert.Same(before, s.State);
    }

    [Fact]
    public void Receipts_of_both_versions_live_side_by_side_and_replay_is_identical()
    {
        var s = new Script();
        s.Ok(new TakeEvent("t1", new PitchSpec(PitchOutcome.CalledStrike, Location: Loc("in.r2c2")))); // ef2 take
        s.Approach().Ok(Ball("p1", null));                         // legacy ef1 pitch
        s.Ok(Ball("p2", Loc("in.r1c1")));                          // ef2 pitch
        s.Ok(Single("i1", Loc("in.r2c3", AppliedBatterSide.Left)));
        s.Approach().Ok(new InPlayEvent("i2", BattedBallType.Line, InPlayResult.Single, Moves: [M(1, 2)]));                     // legacy ef1 in-play
        Assert.Equal(new[] { "ef2", "ef1", "ef1", "ef2", "ef2", "ef1", "ef1" }, s.State.AppliedEvents.Select(r => r.FingerprintVersion));

        foreach (var e in s.Log)
        {
            var again = GameReducer.Apply(s.State, e);
            Assert.True(again.IsSuccess && again.Value.WasDuplicate, e.Id);
        }

        var replay = new Script();
        foreach (var e in s.Log)
            replay.Ok(e);
        Assert.Equal(s.State.AppliedEvents, replay.State.AppliedEvents);
    }

    [Fact]
    public void A_location_does_not_change_counts_scoring_or_the_pitch_total_and_an_in_play_event_is_one_pitch()
    {
        GameEvent[] Plain() =>
        [
            new TakeEvent("t", new PitchSpec(PitchOutcome.Ball)), new ApproachEvent("a", ApproachKind.Normal), new PitchEvent("p1", new PitchSpec(PitchOutcome.CalledStrike)),
            new PitchEvent("p2", new PitchSpec(PitchOutcome.Foul)), new InPlayEvent("i", BattedBallType.Line, InPlayResult.Double),
        ];
        var located = Plain().Select(GameEvent (e) => e switch
        {
            PitchEvent p => p with { Spec = p.Spec with { Location = Loc("in.r1c1") } },
            TakeEvent t => t with { Spec = t.Spec with { Location = Loc("in.r3c3", AppliedBatterSide.Left) } },
            InPlayEvent i => i with { Location = Loc("in.r2c2") },
            _ => e,
        }).ToArray();

        var a = new Script();
        var b = new Script();
        foreach (var e in Plain()) a.Ok(e);
        foreach (var e in located) b.Ok(e);

        Assert.Equal(a.State.Stats, b.State.Stats);
        Assert.Equal((a.State.Count, a.State.AwayRuns, a.State.HomeRuns, a.State.Outs), (b.State.Count, b.State.AwayRuns, b.State.HomeRuns, b.State.Outs));
        Assert.Equal(a.State.AppliedEvents.Count, b.State.AppliedEvents.Count);
        // 4 actual pitches in both: a taken ball, a strike, a foul and the in-play event itself; the location added no pitch
        Assert.Equal(4, b.State.Stats.Where(x => x.Key == StatKey.PitchPitches && x.Player != default).Sum(x => x.Amount));
    }
}
