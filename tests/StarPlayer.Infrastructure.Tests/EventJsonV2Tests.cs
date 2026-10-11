using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C1: the explicitly versioned event JSON (format_version 2) that carries the pitch location, next to the unchanged legacy bare array.
/// A pitch-bearing event in format 2 without a valid location is an error and nothing is defaulted, corrected or promoted from legacy data.
/// </summary>
public class EventJsonV2Tests
{
    private static PitchLocation Loc(string zone, AppliedBatterSide side) =>
        PitchLocation.FromZone(ZoneModelVersion.Pzl1, PitchZoneId.Create(zone).Value, side).Value;

    private static List<GameEvent> Sample() =>
    [
        new ApproachEvent("a1", ApproachKind.Patient),
        new PitchEvent("p1", new PitchSpec(PitchOutcome.Ball, Location: Loc("in.r1c1", AppliedBatterSide.Left))),
        new PitchEvent("p2", new PitchSpec(PitchOutcome.Foul, PitchMishap.None, null, PitchMishap.None, null, Loc("out.low_out", AppliedBatterSide.Right))),
        new InPlayEvent("i1", BattedBallType.Ground, InPlayResult.Single, false, [new RunnerMove(1, 2)], null, null, null, Loc("in.r2c2", AppliedBatterSide.Right)),
        new RunnerPlayEvent("r1", RunnerPlayKind.Steal, [new RunnerMove(1, 2, MoveCause.Steal)]),
        new SubstitutionEvent("s1", SubstitutionKind.PinchRun, TeamSide.Away, PlayerId.Create("ab1").Value, 2),
        new TakeEvent("t1", new PitchSpec(PitchOutcome.CalledStrike, Location: Loc("in.r3c3", AppliedBatterSide.Left))),
    ];

    private static string Json(Action<JsonObject> change)
    {
        var root = JsonNode.Parse(ScriptedGameJson.WriteEventsV2(Sample()))!.AsObject();
        change(root);
        return root.ToJsonString();
    }

    private static JsonObject Event(JsonObject root, string id) => root["events"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == id)!.AsObject();

    private static string CodeOf(string json) => ScriptedGameJson.ReadEventsV2(json).Error!.Value.Code;

    // ------------------------------------------------------------------ round trip

    [Fact]
    public void A_version_2_event_list_round_trips_with_every_location_and_the_same_receipts()
    {
        var events = Sample();
        var json = ScriptedGameJson.WriteEventsV2(events);
        var read = ScriptedGameJson.ReadEventsV2(json);

        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(events.Count, read.Value.Count);
        for (var i = 0; i < events.Count; i++)
        {
            Assert.Equal(GameEventLocation.Of(events[i]), GameEventLocation.Of(read.Value[i]));
            Assert.Equal(EventFingerprint.Receipt(events[i]), EventFingerprint.Receipt(read.Value[i]));
        }

        Assert.Equal(new[] { "ef1", "ef2", "ef2", "ef2", "ef1", "ef1", "ef2" }, read.Value.Select(e => EventFingerprint.Receipt(e).FingerprintVersion));
        Assert.Equal(json, ScriptedGameJson.WriteEventsV2(read.Value));
        var root = JsonNode.Parse(json)!.AsObject();
        Assert.Equal(2, root["format_version"]!.GetValue<int>());
        Assert.Equal("""{"zone_model":"pzl-zones-v1","zone":"in.r1c1","side":"left","boundary":"borderline"}""", Event(root, "p1")["location"]!.ToJsonString());
        Assert.Null(Event(root, "a1")["location"]);
    }

    [Fact]
    public void A_version_2_game_replays_to_the_same_state_and_mixed_receipts_survive_their_own_json()
    {
        var setup = new Func<GameState>(() =>
        {
            var team = new Func<string, TeamSetup>(prefix => new TeamSetup(
                Enumerable.Range(1, 9).Select(n => new LineupSlot(n, PlayerId.Create($"{prefix}{n}").Value,
                    new[] { FieldPosition.CenterField, FieldPosition.Shortstop, FieldPosition.RightField, FieldPosition.FirstBase, FieldPosition.DesignatedHitter,
                        FieldPosition.LeftField, FieldPosition.ThirdBase, FieldPosition.SecondBase, FieldPosition.Catcher }[n - 1])).ToList(),
                PlayerId.Create(prefix + "p").Value, Enumerable.Range(1, 5).Select(n => PlayerId.Create($"{prefix}b{n}").Value).ToList()));
            return GameReducer.Start(new GameStart("g", ProfileDh(), team("a"), team("h"))).Value;
        });

        var events = new List<GameEvent>
        {
            new TakeEvent("t1", new PitchSpec(PitchOutcome.CalledStrike, Location: Loc("in.r2c2", AppliedBatterSide.Right))),
            new ApproachEvent("a1", ApproachKind.Normal),
            new PitchEvent("p1", new PitchSpec(PitchOutcome.Ball, Location: Loc("in.r1c3", AppliedBatterSide.Right))),
            new PitchEvent("p2", new PitchSpec(PitchOutcome.Ball, Location: Loc("out.high", AppliedBatterSide.Right))),
            new InPlayEvent("i1", BattedBallType.Fly, InPlayResult.Out, false, null, [new PositionCredit(FieldPosition.LeftField, CreditKind.Putout)], null, null, Loc("in.r1c2", AppliedBatterSide.Right)),
        };
        var read = ScriptedGameJson.ReadEventsV2(ScriptedGameJson.WriteEventsV2(events)).Value;

        GameState Run(IEnumerable<GameEvent> list)
        {
            var state = setup();
            foreach (var e in list)
            {
                var step = GameReducer.Apply(state, e);
                Assert.True(step.IsSuccess, $"{e.Id}: {step.Error?.Code}");
                state = step.Value.State;
            }

            return state;
        }

        var original = Run(events);
        var again = Run(read);
        Assert.Equal(original.AppliedEvents, again.AppliedEvents);
        Assert.Equal(original.Stats, again.Stats);
        Assert.Equal(new[] { "ef2", "ef1", "ef2", "ef2", "ef2" }, original.AppliedEvents.Select(r => r.FingerprintVersion));

        var receipts = EventReceiptJson.Read(EventReceiptJson.Write(original.AppliedEvents));
        Assert.True(receipts.IsSuccess, receipts.Error?.Message);
        Assert.Equal(original.AppliedEvents, receipts.Value);
        foreach (var e in read)
            Assert.True(GameReducer.Apply(again, e).Value.WasDuplicate, e.Id);
    }

    private static GameRuleSnapshot ProfileDh()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
            {
                var rules = GameRulesJson.Read(File.ReadAllText(Path.Combine(d.FullName, "content", "rules", "game_rules_profiles.json")));
                Assert.True(rules.IsSuccess, rules.Error?.Message);
                return rules.Value.First(r => r.DesignatedHitter);
            }

        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }

    // ------------------------------------------------------------------ strict reading of format 2

    [Theory]
    [InlineData("p1")]
    [InlineData("p2")]
    [InlineData("i1")]
    [InlineData("t1")]
    public void A_version_2_pitch_event_without_a_location_is_an_error(string id)
    {
        Assert.Equal("Fixture.location_missing", CodeOf(Json(root => Event(root, id).Remove("location"))));
    }

    [Theory]
    [InlineData("p1")]
    [InlineData("i1")]
    [InlineData("t1")]
    public void A_version_2_pitch_event_with_a_null_location_is_an_error(string id)
    {
        Assert.Equal("Fixture.location_missing", CodeOf(Json(root => Event(root, id)["location"] = null)));
    }

    [Fact]
    public void An_unsupported_zone_model_an_unknown_zone_a_bad_side_and_a_wrong_class_are_each_rejected_without_correction()
    {
        Assert.Equal("ZoneModelVersion.unsupported", CodeOf(Json(root => Event(root, "p1")["location"]!["zone_model"] = "pzl-zones-v2")));
        Assert.Equal("PitchZoneId.unknown", CodeOf(Json(root => Event(root, "p1")["location"]!["zone"] = "in.r9c9")));
        Assert.Equal("Fixture.enum_invalid", CodeOf(Json(root => Event(root, "p1")["location"]!["side"] = "switch")));
        Assert.Equal("Fixture.enum_invalid", CodeOf(Json(root => Event(root, "p1")["location"]!["side"] = "Left")));      // not the snake_case spelling
        Assert.Equal("Fixture.enum_invalid", CodeOf(Json(root => Event(root, "p1")["location"]!["boundary"] = "fuzzy")));
        Assert.Equal("PitchLocation.boundary_mismatch", CodeOf(Json(root => Event(root, "p1")["location"]!["boundary"] = "clear")));   // in.r1c1 is borderline
        Assert.Equal("PitchLocation.boundary_mismatch", CodeOf(Json(root => Event(root, "i1")["location"]!["boundary"] = "borderline"))); // in.r2c2 is clear
    }

    [Fact]
    public void A_location_member_that_is_missing_or_unknown_is_a_json_error()
    {
        Assert.Equal("Fixture.json_invalid", CodeOf(Json(root => Event(root, "p1")["location"]!.AsObject().Remove("side"))));
        Assert.Equal("Fixture.json_invalid", CodeOf(Json(root => Event(root, "p1")["location"]!.AsObject().Remove("boundary"))));
        Assert.Equal("Fixture.json_invalid", CodeOf(Json(root => Event(root, "p1")["location"]!["x"] = 5)));
    }

    [Fact]
    public void A_non_pitch_event_neither_needs_nor_accepts_a_location()
    {
        Assert.True(ScriptedGameJson.ReadEventsV2(Json(root => { })).IsSuccess);
        var loc = """{"zone_model":"pzl-zones-v1","zone":"in.r2c2","side":"right","boundary":"clear"}""";
        foreach (var id in new[] { "a1", "r1", "s1" })
            Assert.Equal("Fixture.location_unexpected", CodeOf(Json(root => Event(root, id)["location"] = JsonNode.Parse(loc))));
    }

    [Fact]
    public void Only_format_version_2_is_read_as_version_2_and_a_legacy_array_is_never_promoted()
    {
        Assert.Equal("Fixture.version_unsupported", CodeOf(Json(root => root["format_version"] = 1)));
        Assert.Equal("Fixture.version_unsupported", CodeOf(Json(root => root["format_version"] = 3)));
        Assert.Equal("Fixture.json_invalid", CodeOf(Json(root => root.Remove("format_version"))));
        Assert.Equal("Fixture.json_invalid", CodeOf(Json(root => root["extra"] = 1)));
        var legacy = ScriptedGameJson.WriteEvents([new ApproachEvent("a1", ApproachKind.Normal), new PitchEvent("p1", new PitchSpec(PitchOutcome.Ball))]);
        Assert.Equal("Fixture.version_unsupported", CodeOf(legacy));
    }

    // ------------------------------------------------------------------ legacy format 1 stays legacy

    [Fact]
    public void The_legacy_reader_reads_bare_arrays_gives_them_no_location_and_refuses_both_a_location_member_and_a_version_2_envelope()
    {
        var legacy = ScriptedGameJson.WriteEvents([new ApproachEvent("a1", ApproachKind.Normal), new PitchEvent("p1", new PitchSpec(PitchOutcome.Ball)), new InPlayEvent("i1", BattedBallType.Line, InPlayResult.Single)]);
        var read = ScriptedGameJson.ReadEvents(legacy);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.All(read.Value, e => Assert.Null(GameEventLocation.Of(e)));
        Assert.All(read.Value, e => Assert.Equal("ef1", EventFingerprint.Receipt(e).FingerprintVersion));
        Assert.DoesNotContain("location", legacy);

        var withLocation = JsonNode.Parse(legacy)!.AsArray();
        withLocation[1]!["location"] = JsonNode.Parse("""{"zone_model":"pzl-zones-v1","zone":"in.r2c2","side":"right","boundary":"clear"}""");
        Assert.Equal("Fixture.json_invalid", ScriptedGameJson.ReadEvents(withLocation.ToJsonString()).Error!.Value.Code);
        Assert.Equal("Fixture.version_unsupported", ScriptedGameJson.ReadEvents(ScriptedGameJson.WriteEventsV2(Sample())).Error!.Value.Code);
    }

    [Fact]
    public void The_legacy_writer_refuses_an_event_with_a_location_instead_of_dropping_it_and_the_version_2_writer_refuses_a_pitch_without_one()
    {
        var legacyWriter = Assert.Throws<GameJsonException>(() => ScriptedGameJson.WriteEvents(Sample()));
        Assert.Equal("Fixture.location_requires_v2", legacyWriter.Code);
        Assert.Contains("p1", legacyWriter.Message);

        foreach (GameEvent bare in new GameEvent[]
        {
            new PitchEvent("p", new PitchSpec(PitchOutcome.Ball)), new TakeEvent("t", new PitchSpec(PitchOutcome.Ball)), new InPlayEvent("i", BattedBallType.Line, InPlayResult.Single),
        })
            Assert.Equal("Fixture.location_missing", Assert.Throws<GameJsonException>(() => ScriptedGameJson.WriteEventsV2([bare])).Code);

        // events that are not pitches need no location in either writer
        var nonPitch = new GameEvent[] { new ApproachEvent("a", ApproachKind.Normal), new RunnerPlayEvent("r", RunnerPlayKind.Steal, [new RunnerMove(1, 2, MoveCause.Steal)]) };
        Assert.True(ScriptedGameJson.ReadEventsV2(ScriptedGameJson.WriteEventsV2(nonPitch)).IsSuccess);
        Assert.True(ScriptedGameJson.ReadEvents(ScriptedGameJson.WriteEvents(nonPitch)).IsSuccess);
    }

    [Fact]
    public void Receipts_of_both_fingerprint_versions_are_kept_as_written()
    {
        var mixed = new[]
        {
            EventFingerprint.Receipt(new ApproachEvent("a", ApproachKind.Normal)),
            EventFingerprint.Receipt(new PitchEvent("p", new PitchSpec(PitchOutcome.Ball, Location: Loc("in.r2c2", AppliedBatterSide.Right)))),
            EventFingerprint.Receipt(new PitchEvent("q", new PitchSpec(PitchOutcome.Ball))),
        };
        var read = EventReceiptJson.Read(EventReceiptJson.Write(mixed));
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(mixed, read.Value);
        Assert.Equal(new[] { "ef1", "ef2", "ef1" }, read.Value.Select(r => r.FingerprintVersion));
    }

    // ------------------------------------------------------------------ explicit null in the envelope (Codex Medium, Phase 3-C1 fix round)

    private const string ValidEvent = """{"id":"a1","type":"approach","approach":"normal"}""";

    private static Error Rejected(string json)
    {
        var result = ScriptedGameJson.ReadEventsV2(json); // must return a failure, never throw
        Assert.False(result.IsSuccess);
        return result.Error!.Value;
    }

    [Fact]
    public void A_null_events_collection_is_a_located_field_missing_error_and_not_an_exception()
    {
        var error = Rejected("""{"format_version":2,"events":null}""");
        Assert.Equal("Fixture.field_missing", error.Code);
        Assert.Contains("events", error.Message);
    }

    [Theory]
    [InlineData("""{"format_version":2,"events":[null]}""", "events[0]")]
    [InlineData("""{"format_version":2,"events":[null,{"id":"a1","type":"approach","approach":"normal"}]}""", "events[0]")]
    [InlineData("""{"format_version":2,"events":[{"id":"a1","type":"approach","approach":"normal"},null]}""", "events[1]")]
    [InlineData("""{"format_version":2,"events":[{"id":"a1","type":"approach","approach":"normal"},{"id":"a2","type":"approach","approach":"normal"},null,{"id":"a3","type":"approach","approach":"normal"}]}""", "events[2]")]
    public void A_null_element_rejects_the_whole_list_with_its_exact_index_and_never_a_partial_success(string json, string path)
    {
        var error = Rejected(json);
        Assert.Equal("Fixture.field_missing", error.Code);
        Assert.Contains(path, error.Message);
    }

    [Theory]
    [InlineData("""{"format_version":2}""")]
    [InlineData("""{"format_version":2,"events":5}""")]
    [InlineData("""{"format_version":2,"events":"x"}""")]
    [InlineData("""{"format_version":2,"events":{}}""")]
    [InlineData("""{"format_version":2,"events":[5]}""")]
    [InlineData("""{"format_version":2,"events":["x"]}""")]
    public void A_missing_or_wrongly_typed_events_member_keeps_the_existing_json_invalid_error(string json)
    {
        Assert.Equal("Fixture.json_invalid", Rejected(json).Code);
    }

    [Fact]
    public void A_valid_version_2_event_array_and_an_empty_one_are_read_unchanged()
    {
        var one = ScriptedGameJson.ReadEventsV2("""{"format_version":2,"events":[""" + ValidEvent + "]}");
        Assert.True(one.IsSuccess, one.Error?.Message);
        Assert.Equal(new ApproachEvent("a1", ApproachKind.Normal), Assert.Single(one.Value));
        var none = ScriptedGameJson.ReadEventsV2("""{"format_version":2,"events":[]}""");
        Assert.True(none.IsSuccess && none.Value.Count == 0);
    }
}
