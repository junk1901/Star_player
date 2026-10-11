using System.Text.Json.Nodes;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C1 second fix round: an explicit JSON <c>null</c> as an ELEMENT of a list inside an event (<c>moves</c>, <c>strikeout_credits</c>, <c>batter_credits</c>,
/// <c>errors</c>, a move's <c>credits</c>), and as an element or value of a scenario's <c>events</c> list, is a located <c>Fixture.field_missing</c> failure on all
/// three event readers (legacy bare array, format 2, scripted game). A list that is itself null stays legal where it was optional, with its old meaning.
/// </summary>
public class EventJsonNestedNullTests
{
    private static readonly string Location = J("'location':{'zone_model':'pzl-zones-v1','zone':'in.r2c2','side':'right','boundary':'clear'}");

    // JSON written with single quotes keeps the test literals readable
    private static string J(string singleQuoted) => singleQuoted.Replace('\'', '"');

    private static string Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                return d.FullName;
        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }

    // ------------------------------------------------------------------ the three readers

    public enum Reader { LegacyArray, Version2, Scenario }

    private static (bool Ok, string Code, string Message) Read(Reader reader, string eventJson)
    {
        switch (reader)
        {
            case Reader.LegacyArray:
            {
                var r = ScriptedGameJson.ReadEvents("[" + eventJson + "]");
                return (r.IsSuccess, r.Error?.Code ?? "", r.Error?.Message ?? "");
            }
            case Reader.Version2:
            {
                var r = ScriptedGameJson.ReadEventsV2("""{"format_version":2,"events":[""" + eventJson + "]}");
                return (r.IsSuccess, r.Error?.Code ?? "", r.Error?.Message ?? "");
            }
            default:
            {
                var r = ScriptedGameJson.Read(ScenarioWith(eventJson), Rules());
                return (r.IsSuccess, r.Error?.Code ?? "", r.Error?.Message ?? "");
            }
        }
    }

    private static IReadOnlyList<GameRuleSnapshot> Rules()
    {
        var rules = GameRulesJson.Read(File.ReadAllText(Path.Combine(Root(), "content", "rules", "game_rules_profiles.json")));
        Assert.True(rules.IsSuccess, rules.Error?.Message);
        return rules.Value;
    }

    // game g001 of the shipped corpus with the given event JSON inserted as its first event
    private static string ScenarioWith(string eventJson, Action<JsonObject>? change = null)
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(Root(), "tests", "Fixtures", "phase2_scripted_games_100.json")))!.AsObject();
        var game = root["games"]!.AsArray()[0]!.AsObject();
        root["games"] = new JsonArray(game.DeepClone());
        var g = root["games"]![0]!.AsObject();
        g["events"]!.AsArray().Insert(0, eventJson == "null" ? null : JsonNode.Parse(eventJson));
        change?.Invoke(g);
        return root.ToJsonString();
    }

    private static string Prefix(Reader reader) => reader == Reader.Scenario ? "g001.events[0]" : "events[0]";

    // a pitch / in-play / runner event with one extra member; the format 2 reader also gets the location it requires
    private static string Ev(Reader reader, string type, string members)
    {
        var loc = reader == Reader.Version2 && type != "runner_play" ? "," + Location : "";
        var core = type switch
        {
            "pitch" => J("'id':'x1','type':'pitch','outcome':'swinging_strike'"),
            "in_play" => J("'id':'x1','type':'in_play','batted':'line','result':'single'"),
            _ => J("'id':'x1','type':'runner_play','kind':'steal'"),
        };
        return "{" + core + loc + (members.Length == 0 ? "" : "," + members) + "}";
    }

    public static IEnumerable<object[]> Readers() => Enum.GetValues<Reader>().Select(r => new object[] { r });

    // ------------------------------------------------------------------ null elements: explicit, located failure on every reader

    [Theory]
    [MemberData(nameof(Readers))]
    public void A_null_move_is_a_located_failure(Reader reader)
    {
        foreach (var type in new[] { "pitch", "in_play", "runner_play" })
        {
            var r = Read(reader, Ev(reader, type, J("'moves':[null]")));
            Assert.False(r.Ok);
            Assert.Equal("Fixture.field_missing", r.Code);
            Assert.Contains(Prefix(reader) + ".moves[0]", r.Message);
        }
    }

    [Theory]
    [MemberData(nameof(Readers))]
    public void A_null_strikeout_credit_is_a_located_failure(Reader reader)
    {
        var r = Read(reader, Ev(reader, "pitch", J("'strikeout_credits':[null]")));
        Assert.False(r.Ok);
        Assert.Equal("Fixture.field_missing", r.Code);
        Assert.Contains(Prefix(reader) + ".strikeout_credits[0]", r.Message);
    }

    [Theory]
    [MemberData(nameof(Readers))]
    public void A_null_batter_credit_is_a_located_failure(Reader reader)
    {
        var r = Read(reader, Ev(reader, "in_play", J("'batter_credits':[null]")));
        Assert.False(r.Ok);
        Assert.Equal("Fixture.field_missing", r.Code);
        Assert.Contains(Prefix(reader) + ".batter_credits[0]", r.Message);
    }

    [Theory]
    [MemberData(nameof(Readers))]
    public void A_null_error_position_is_a_located_failure(Reader reader)
    {
        foreach (var type in new[] { "in_play", "runner_play" })
        {
            var r = Read(reader, Ev(reader, type, J("'errors':[null]")));
            Assert.False(r.Ok);
            Assert.Equal("Fixture.field_missing", r.Code);
            Assert.Contains(Prefix(reader) + ".errors[0]", r.Message);
        }
    }

    [Theory]
    [MemberData(nameof(Readers))]
    public void A_null_credit_inside_a_move_is_a_located_failure(Reader reader)
    {
        var r = Read(reader, Ev(reader, "in_play", J("'moves':[{'from':1,'to':2},{'from':2,'to':3,'credits':[null]}]")));
        Assert.False(r.Ok);
        Assert.Equal("Fixture.field_missing", r.Code);
        Assert.Contains(Prefix(reader) + ".moves[1].credits[0]", r.Message);
    }

    [Theory]
    [MemberData(nameof(Readers))]
    public void A_null_in_the_middle_of_a_valid_list_rejects_the_whole_input_with_the_exact_index(Reader reader)
    {
        var moves = Read(reader, Ev(reader, "in_play", J("'moves':[{'from':1,'to':2},null,{'from':2,'to':3}]")));
        Assert.False(moves.Ok);
        Assert.Equal("Fixture.field_missing", moves.Code);
        Assert.Contains(Prefix(reader) + ".moves[1]", moves.Message);

        var credits = Read(reader, Ev(reader, "in_play",
            J("'batter_credits':[{'position':'first_base','kind':'putout'},{'position':'shortstop','kind':'assist'},null]")));
        Assert.False(credits.Ok);
        Assert.Contains(Prefix(reader) + ".batter_credits[2]", credits.Message);

        var errors = Read(reader, Ev(reader, "in_play", J("'errors':['shortstop',null,'catcher']")));
        Assert.False(errors.Ok);
        Assert.Contains(Prefix(reader) + ".errors[1]", errors.Message);
    }

    // ------------------------------------------------------------------ the events list of a scripted game and of the legacy array

    [Fact]
    public void A_null_event_or_a_null_events_list_in_a_scripted_game_or_a_legacy_array_is_a_located_failure()
    {
        var element = ScriptedGameJson.Read(ScenarioWith("null"), Rules());
        Assert.Equal("Fixture.field_missing", element.Error!.Value.Code);
        Assert.Contains("g001.events[0]", element.Error.Value.Message);

        var list = ScriptedGameJson.Read(ScenarioWith("{}", g => g["events"] = null), Rules());
        Assert.Equal("Fixture.field_missing", list.Error!.Value.Code);
        Assert.Contains("g001.events", list.Error.Value.Message);

        var legacy = ScriptedGameJson.ReadEvents("""[{"id":"a1","type":"approach","approach":"normal"},null]""");
        Assert.Equal("Fixture.field_missing", legacy.Error!.Value.Code);
        Assert.Contains("events[1]", legacy.Error.Value.Message);
    }

    // ------------------------------------------------------------------ compatibility: what stays legal

    [Theory]
    [MemberData(nameof(Readers))]
    public void An_optional_list_that_is_null_absent_or_empty_stays_legal_and_keeps_its_meaning(Reader reader)
    {
        var withNull = Read(reader, Ev(reader, "pitch", J("'moves':null,'strikeout_credits':null")));
        var absent = Read(reader, Ev(reader, "pitch", ""));
        var empty = Read(reader, Ev(reader, "pitch", J("'moves':[],'strikeout_credits':[]")));
        Assert.True(withNull.Ok && absent.Ok && empty.Ok, $"{withNull.Message}|{absent.Message}|{empty.Message}");
        var inPlay = Read(reader, Ev(reader, "in_play", J("'moves':null,'batter_credits':null,'errors':null,'batter_sequence':null")));
        Assert.True(inPlay.Ok, inPlay.Message);
    }

    [Fact]
    public void Null_strikeout_credits_keep_their_old_meaning_default_catcher_putout_and_are_not_turned_into_an_empty_list()
    {
        GameEvent Parse(string members) => ScriptedGameJson.ReadEvents("[" + Ev(Reader.LegacyArray, "pitch", members) + "]").Value[0];

        Assert.Null(((PitchEvent)Parse("")).Spec.StrikeoutCredits);
        Assert.Null(((PitchEvent)Parse(J("'strikeout_credits':null"))).Spec.StrikeoutCredits);
        Assert.Empty(((PitchEvent)Parse(J("'strikeout_credits':[]"))).Spec.StrikeoutCredits!);
        Assert.Single(((PitchEvent)Parse(J("'strikeout_credits':[{'position':'catcher','kind':'putout'}]"))).Spec.StrikeoutCredits!);
    }

    [Fact]
    public void A_well_formed_nested_event_is_read_as_before_by_every_reader()
    {
        var members = J("'moves':[{'from':1,'to':2,'credits':[{'position':'shortstop','kind':'assist'}]}],'batter_credits':[{'position':'first_base','kind':'putout'}],'errors':['shortstop']");
        foreach (var reader in Enum.GetValues<Reader>())
        {
            var r = Read(reader, Ev(reader, "in_play", members));
            // the scenario reader only converts here; the game is not replayed
            Assert.True(r.Ok, $"{reader}: {r.Code} {r.Message}");
        }
    }
}
