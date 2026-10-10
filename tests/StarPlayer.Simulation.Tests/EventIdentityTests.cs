using System.Collections.Immutable;
using System.Reflection;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests;

/// <summary>
/// High 2 of the Codex review: an event id may be re-delivered only with the same content. The receipt of every processed event keeps its
/// kind and a deterministic SHA-256 fingerprint of the normalised payload (EventFingerprint, version ef1).
/// </summary>
public class EventIdentityTests
{
    private static InPlayEvent Sample() => new("x", BattedBallType.Ground, InPlayResult.Out, false,
        [Seq(M(3, 4), 1), Seq(M(1, 0, MoveCause.Play, OutKind.Force, A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase)), 2, outAt: 2, method: OutMethod.BaseTouch)],
        [Po(FieldPosition.FirstBase)], null, 3);

    [Fact]
    public void The_same_id_with_the_same_content_is_a_harmless_duplicate_that_adds_nothing()
    {
        var s = new Script();
        s.Ok(new ApproachEvent("same-id", ApproachKind.Normal));
        var before = s.State;

        var again = GameReducer.Apply(s.State, new ApproachEvent("same-id", ApproachKind.Normal));

        Assert.True(again.IsSuccess);
        Assert.True(again.Value.WasDuplicate);
        Assert.Same(before, again.Value.State);
        Assert.Empty(again.Value.Delta.Entries);
        Assert.Empty(again.Value.NewPlays);
        Assert.Single(again.Value.State.AppliedEvents);
    }

    [Fact]
    public void The_same_id_with_another_kind_of_event_is_a_conflict_and_changes_nothing()
    {
        var s = new Script();
        s.Ok(new ApproachEvent("same-id", ApproachKind.Normal));
        var before = s.State;

        var result = GameReducer.Apply(s.State, new PitchEvent("same-id", new PitchSpec(PitchOutcome.Ball)));

        Assert.Equal("Game.event_id_conflict", result.Error!.Value.Code);
        Assert.Same(before, s.State);
        Assert.Equal(("approach", 1), (s.State.AppliedEvents[0].Kind, s.State.AppliedEvents.Count));
        Assert.Equal((0, 0), (s.State.Count.Balls, s.State.Count.Strikes));
        Assert.Empty(s.State.Stats);
    }

    [Fact]
    public void The_same_id_and_kind_with_other_content_is_a_conflict_for_every_kind_of_event()
    {
        var s = new Script();
        s.Ok(new ApproachEvent("a", ApproachKind.Normal));
        s.Ok(new PitchEvent("p", new PitchSpec(PitchOutcome.Ball)));
        s.Ok(new PitchEvent("p2", new PitchSpec(PitchOutcome.CalledStrike)));
        s.Ok(new InPlayEvent("i", BattedBallType.Line, InPlayResult.Single));
        s.Ok(new RunnerPlayEvent("r", RunnerPlayKind.Steal, [M(1, 2, MoveCause.Steal)]));
        s.Ok(new SubstitutionEvent("s", SubstitutionKind.PinchRun, TeamSide.Away, P("ab1"), 2));
        var before = s.State;

        GameEvent[] conflicting =
        [
            new ApproachEvent("a", ApproachKind.Patient),
            new PitchEvent("p", new PitchSpec(PitchOutcome.CalledStrike)),
            new PitchEvent("p2", new PitchSpec(PitchOutcome.CalledStrike, PitchMishap.WildPitch, [M(2, 3, MoveCause.WildPitch)])),
            new InPlayEvent("i", BattedBallType.Line, InPlayResult.Double),
            new RunnerPlayEvent("r", RunnerPlayKind.Steal, [M(1, 2, MoveCause.Steal), M(2, 3, MoveCause.Steal)]),
            new SubstitutionEvent("s", SubstitutionKind.PinchRun, TeamSide.Away, P("ab2"), 2),
        ];
        foreach (var e in conflicting)
            Assert.Equal("Game.event_id_conflict", GameReducer.Apply(s.State, e).Error!.Value.Code);

        Assert.Same(before, s.State);
    }

    [Fact]
    public void A_fingerprint_does_not_depend_on_the_order_of_moves_credits_or_errors_and_not_on_the_id()
    {
        var credits1 = new[] { A(FieldPosition.Shortstop), Po(FieldPosition.SecondBase) };
        var credits2 = new[] { Po(FieldPosition.SecondBase), A(FieldPosition.Shortstop) };
        var a = new InPlayEvent("id-1", BattedBallType.Ground, InPlayResult.Out, false,
            [Seq(M(3, 4), 1), Seq(M(1, 0, MoveCause.Play, OutKind.Force, credits1), 2)], [Po(FieldPosition.FirstBase)], [FieldPosition.Shortstop, FieldPosition.Catcher], 3);
        var b = new InPlayEvent("id-2", BattedBallType.Ground, InPlayResult.Out, false,
            [Seq(M(1, 0, MoveCause.Play, OutKind.Force, credits2), 2), Seq(M(3, 4), 1)], [Po(FieldPosition.FirstBase)], [FieldPosition.Catcher, FieldPosition.Shortstop], 3);

        Assert.Equal(EventFingerprint.Receipt(a).Fingerprint, EventFingerprint.Receipt(b).Fingerprint);
        // an absent list and an empty list are the same
        Assert.Equal(EventFingerprint.Canonical(new InPlayEvent("x", BattedBallType.Line, InPlayResult.Single)),
            EventFingerprint.Canonical(new InPlayEvent("x", BattedBallType.Line, InPlayResult.Single, false, [], [], [], null)));
    }

    [Fact]
    public void The_fingerprint_format_is_a_fixed_contract_with_a_literal_canonical_text_and_hash()
    {
        const string canonical = "ef1|in_play|batted=Ground|result=Out|sacrifice=0|moves=[{1;0;Play;Force;[{SecondBase;Putout},{Shortstop;Assist}];2;2;BaseTouch},{3;4;Play;~;[];1;~;~}]|batter_credits=[{FirstBase;Putout}]|errors=[]|batter_sequence=3";
        var receipt = EventFingerprint.Receipt(Sample());

        Assert.Equal(canonical, EventFingerprint.Canonical(Sample()));
        Assert.Equal("c2693b1ff814ce7f2ae464d805c5b3ec2fe7c95d3407ee1527ced274a824069c", receipt.Fingerprint); // SHA-256 computed independently of the C# code
        Assert.Equal(("x", "in_play", "ef1"), (receipt.EventId, receipt.Kind, receipt.FingerprintVersion));
        Assert.Equal("862f90f35502082812e59c0d559b8f4f63b87c383930a4d160a570707622bdf7", EventFingerprint.Receipt(new ApproachEvent("any", ApproachKind.Normal)).Fingerprint);
    }

    [Fact]
    public void Every_constructor_parameter_of_every_event_changes_the_fingerprint()
    {
        // one variant per parameter; the reflection check below fails if a parameter is added to an event without being listed here
        var variants = new Dictionary<string, (GameEvent Base, GameEvent[] Changed)>
        {
            [nameof(ApproachEvent)] = (new ApproachEvent("x", ApproachKind.Normal), [new ApproachEvent("x", ApproachKind.Aggressive)]),
            [nameof(PitchEvent)] = (new PitchEvent("x", new PitchSpec(PitchOutcome.Ball)), [
                new PitchEvent("x", new PitchSpec(PitchOutcome.Foul)),
                new PitchEvent("x", new PitchSpec(PitchOutcome.Ball, PitchMishap.PassedBall)),
                new PitchEvent("x", new PitchSpec(PitchOutcome.Ball, Moves: [M(1, 2, MoveCause.WildPitch)])),
                new PitchEvent("x", new PitchSpec(PitchOutcome.Ball, DroppedThirdStrike: PitchMishap.WildPitch)),
                new PitchEvent("x", new PitchSpec(PitchOutcome.Ball, StrikeoutCredits: [Po(FieldPosition.Catcher)])),
                new PitchEvent("x", new PitchSpec(PitchOutcome.Ball, StrikeoutCredits: [])),
            ]),
            [nameof(TakeEvent)] = (new TakeEvent("x", new PitchSpec(PitchOutcome.Ball)), [new TakeEvent("x", new PitchSpec(PitchOutcome.CalledStrike))]),
            [nameof(InPlayEvent)] = (Sample(), [
                Sample() with { Batted = BattedBallType.Fly },
                Sample() with { Result = InPlayResult.Single },
                Sample() with { Sacrifice = true },
                Sample() with { Moves = [Seq(M(3, 4), 1)] },
                Sample() with { BatterCredits = [Po(FieldPosition.SecondBase)] },
                Sample() with { Errors = [FieldPosition.Shortstop] },
                Sample() with { BatterSequence = 4 },
            ]),
            [nameof(RunnerPlayEvent)] = (new RunnerPlayEvent("x", RunnerPlayKind.Steal, [M(1, 2, MoveCause.Steal)]), [
                new RunnerPlayEvent("x", RunnerPlayKind.Balk, [M(1, 2, MoveCause.Steal)]),
                new RunnerPlayEvent("x", RunnerPlayKind.Steal, [M(1, 2, MoveCause.Balk)]),
                new RunnerPlayEvent("x", RunnerPlayKind.Steal, [M(1, 2, MoveCause.Steal)], [FieldPosition.Catcher]),
            ]),
            [nameof(SubstitutionEvent)] = (new SubstitutionEvent("x", SubstitutionKind.PinchRun, TeamSide.Away, P("ab1"), 1, 2, 3), [
                new SubstitutionEvent("x", SubstitutionKind.PinchHit, TeamSide.Away, P("ab1"), 1, 2, 3),
                new SubstitutionEvent("x", SubstitutionKind.PinchRun, TeamSide.Home, P("ab1"), 1, 2, 3),
                new SubstitutionEvent("x", SubstitutionKind.PinchRun, TeamSide.Away, P("ab2"), 1, 2, 3),
                new SubstitutionEvent("x", SubstitutionKind.PinchRun, TeamSide.Away, P("ab1"), 2, 2, 3),
                new SubstitutionEvent("x", SubstitutionKind.PinchRun, TeamSide.Away, P("ab1"), 1, 3, 3),
                new SubstitutionEvent("x", SubstitutionKind.PinchRun, TeamSide.Away, P("ab1"), 1, 2, 4),
            ]),
        };

        var eventTypes = typeof(GameEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GameEvent)) && !t.IsAbstract).ToList();
        Assert.Equal(eventTypes.Select(t => t.Name).Order(), variants.Keys.Order());
        foreach (var (name, (baseEvent, changed)) in variants)
        {
            var baseFingerprint = EventFingerprint.Receipt(baseEvent).Fingerprint;
            Assert.All(changed, c => Assert.NotEqual(baseFingerprint, EventFingerprint.Receipt(c).Fingerprint));
            Assert.Equal(changed.Length, changed.Select(c => EventFingerprint.Receipt(c).Fingerprint).Distinct().Count());
            // payload parameters (everything except the id) must each be covered by at least one variant
            var type = eventTypes.Single(t => t.Name == name);
            var payload = type.GetConstructors().Single(c => c.GetParameters().Length > 1 || type == typeof(PitchEvent)).GetParameters().Select(p => p.Name!).Where(n => n != "Id").ToList();
            var expected = name switch
            {
                nameof(PitchEvent) or nameof(TakeEvent) => 1,
                nameof(ApproachEvent) => 1,
                nameof(InPlayEvent) => 7,
                nameof(RunnerPlayEvent) => 3,
                _ => 6,
            };
            Assert.Equal(expected, payload.Count);
        }
    }

    [Fact]
    public void A_rejected_event_leaves_no_receipt_so_a_corrected_event_with_the_same_id_is_accepted()
    {
        var s = new Script();
        var bad = GameReducer.Apply(s.State, new PitchEvent("retry", new PitchSpec(PitchOutcome.Ball))); // no approach chosen yet
        Assert.Equal("Game.pitch_not_legal", bad.Error!.Value.Code);
        Assert.Empty(s.State.AppliedEvents);

        s.Ok(new ApproachEvent("a", ApproachKind.Normal));
        s.Ok(new PitchEvent("retry", new PitchSpec(PitchOutcome.Ball)));

        Assert.Equal(new[] { "a", "retry" }, s.State.AppliedEvents.Select(r => r.EventId));
    }

    [Fact]
    public void After_the_game_is_over_re_delivered_events_change_no_official_record_and_a_new_id_is_refused()
    {
        var s = new Script();
        s.ThreeUp(22);                                      // 0-0 through eleven innings: a tie under the 11-inning limit
        Assert.Equal(GameStatus.Final, s.State.Status);
        s.Finish();
        var final = s.State.Final!;
        var statsBefore = s.State.Stats.Count;

        foreach (var e in s.Log)
        {
            var again = GameReducer.Apply(s.State, e);
            Assert.True(again.IsSuccess && again.Value.WasDuplicate, e.Id);
        }

        var conflict = GameReducer.Apply(s.State, new ApproachEvent(s.Log[0].Id, ApproachKind.Aggressive));
        Assert.Equal("Game.event_id_conflict", conflict.Error!.Value.Code);
        Assert.Equal("Game.already_over", GameReducer.Apply(s.State, new ApproachEvent("brand-new", ApproachKind.Normal)).Error!.Value.Code);
        var finalizedAgain = GameReducer.Finalize(s.State).Value;
        Assert.False(finalizedAgain.WasNew);
        Assert.Same(final, finalizedAgain.Game);
        Assert.Equal(statsBefore, s.State.Stats.Count);
    }

    [Fact]
    public void A_receipt_made_by_another_fingerprint_version_cannot_be_compared_and_is_refused()
    {
        var s = new Script();
        s.Ok(new ApproachEvent("a", ApproachKind.Normal));
        var old = s.State with { AppliedEvents = [s.State.AppliedEvents[0] with { FingerprintVersion = "ef0" }] };

        var result = GameReducer.Apply(old, new ApproachEvent("a", ApproachKind.Normal));

        Assert.Equal("Game.event_fingerprint_version_mismatch", result.Error!.Value.Code);
    }

    [Fact]
    public void The_fingerprint_is_the_same_whenever_and_wherever_it_is_computed()
    {
        var first = EventFingerprint.Receipt(Sample());
        var other = new List<EventReceipt>();
        var threads = Enumerable.Range(0, 4).Select(_ => new Thread(() => { lock (other) other.Add(EventFingerprint.Receipt(Sample())); })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.All(other, r => Assert.Equal(first, r));
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            Assert.Equal(first, EventFingerprint.Receipt(Sample()));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }
}
