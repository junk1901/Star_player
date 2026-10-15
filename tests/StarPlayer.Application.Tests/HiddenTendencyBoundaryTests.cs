using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StarPlayer.Application.Ports;
using StarPlayer.Application.Queries;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Application.Tests;

/// <summary>
/// Phase 3-B1: a batter's hidden zone tendency is resolver input only. WHAT THESE TESTS GUARANTEE (and no more): no field, property, method parameter,
/// return type or constructor parameter declared in the Application or Infrastructure assemblies names the <c>BatterZoneTendency</c> /
/// <c>ZoneReferenceDistribution</c> TYPES (directly, in an array or in a generic argument), and the type's own text form prints no values. They do NOT detect
/// the numbers being copied into some other DTO, string or JSON, and they say nothing about the Godot client (which cannot name Domain at all, see
/// ArchitectureTests). HANDOFF: when an Application query/DTO that could touch a batter first exists (Phase 3-D/5), add a test that serializes the real
/// output (JSON/text, including tooltip and Gemini payloads) and asserts the tendency values are absent.
/// </summary>
public class HiddenTendencyBoundaryTests
{
    private static readonly Assembly[] PublicLayers = [typeof(StarPlayer.Application.Queries.PlayerInfoService).Assembly, typeof(PlayerInfoComposition).Assembly];

    private static IEnumerable<Type> Mentions(Type type)
    {
        yield return type;
        if (type.HasElementType)
            foreach (var inner in Mentions(type.GetElementType()!))
                yield return inner;
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments())
                foreach (var inner in Mentions(argument))
                    yield return inner;
    }

    // Phase 3-B2 adds the resolver-internal probability types that carry or derive from the hidden vector: the tendency context (zone + vector), and the calculation trace /
    // stage result (which records the signed tendency shift of one zone). None of them may appear in an Application or Infrastructure member signature.
    // Phase 3-C2A adds the pitch-branch draft, which carries the per-step traces and exact rolls: it is resolver-internal as well.
    // Phase 3-C2B-1 adds the prepared pitch (it holds the draft) and the unconfirmed event candidate: both stay Simulation-internal too.
    // Phase 3-C2B-2 adds the base in-play plan, its audit record (exact rolls and RNG hashes), the candidate and the pending advancement: all Simulation-internal as well.
    private static readonly Type[] HiddenTypes =
        [typeof(BatterZoneTendency), typeof(ZoneReferenceDistribution), typeof(TendencyContext), typeof(ProbabilityTrace), typeof(StageResult),
         typeof(StarPlayer.Simulation.Baseball.PitchBranchDraft), typeof(StarPlayer.Simulation.Baseball.PitchBranchStep),
         typeof(StarPlayer.Simulation.Baseball.PreparedPitch), typeof(StarPlayer.Simulation.Baseball.NonInPlayPitchCandidate),
         typeof(StarPlayer.Simulation.Baseball.BaseInPlayPlan), typeof(StarPlayer.Simulation.Baseball.InPlayAudit), typeof(StarPlayer.Simulation.Baseball.InPlayPlanOutcome),
         typeof(StarPlayer.Simulation.Baseball.InPlayPitchCandidate), typeof(StarPlayer.Simulation.Baseball.PendingAdvancement),
         // Phase 3-C2B-3 adds the advancement audit (exact rolls, RNG hashes, drawn versus applied results), which is Simulation-internal as well
         typeof(StarPlayer.Simulation.Baseball.AdvancementAudit), typeof(StarPlayer.Simulation.Baseball.AdvancementDecisionAudit), typeof(StarPlayer.Simulation.Baseball.AdvancementMoveAudit),
         // Phase 3-C3-A adds the authoritative game, its cursor, the staged commit and record and the gate: public types with internal construction, Simulation-internal as well (D-8)
         typeof(StarPlayer.Simulation.Baseball.AuthoritativeGame), typeof(StarPlayer.Simulation.Baseball.GameCursor), typeof(StarPlayer.Simulation.Baseball.StagedCommit),
         typeof(StarPlayer.Simulation.Baseball.CommitRecord), typeof(StarPlayer.Simulation.Baseball.CommitGate),
         // Phase 3-C3-B adds the pitch entry: its signatures name the prepared pitch and the candidates, so it is Simulation-internal as well
         typeof(StarPlayer.Simulation.Baseball.PitchCommitGate),
         // Phase 3-D v2 (S1): the player input set holds EXACT base fielding / speed / arm ratings of NPCs; it and its frozen fielder inputs are resolver-internal, like the draft
         typeof(StarPlayer.Simulation.Baseball.PlayerInputSet), typeof(StarPlayer.Simulation.Baseball.PlayerRunFieldInput), typeof(StarPlayer.Simulation.Baseball.PitchFieldingInputs),
         typeof(StarPlayer.Simulation.Baseball.FieldingInput),
         // Phase 3-D (BatterTendency v2, D-T2) adds the per-player profile set and its entry: hidden exactly like the vector they hold
         typeof(BatterZoneProfiles), typeof(BatterZoneProfileEntry)];

    private static bool IsHidden(Type type) => Mentions(type).Any(t => HiddenTypes.Contains(t));

    // D-T2: the ONLY places in Application/Infrastructure that may name a hidden type in a member signature, by exact declaring type and exact member name. The world holder keeps the
    // validated set (its record members: constructor, deconstruction, the property, its accessors and backing field); the data pack reader has one private conversion that builds it.
    // Everything else, every other namespace and every other member of these two types stays forbidden. A new entry is an architecture decision, not a convenience.
    private static readonly Dictionary<string, string[]> Designated = new()
    {
        ["StarPlayer.Application.Ports.PlayerWorld"] = [".ctor", "Deconstruct", "BatterTendencies", "get_BatterTendencies", "set_BatterTendencies", "<BatterTendencies>k__BackingField"],
        ["StarPlayer.Infrastructure.Fixtures.WorldJson"] = ["ToProfiles"],
    };

    private static bool IsDesignated(Type type, string member) => Designated.TryGetValue(type.FullName!, out var members) && members.Contains(member);

    private const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static List<string> Leaks(bool includeDesignated)
    {
        var leaks = new List<string>();
        void Add(Type type, string member)
        {
            if (includeDesignated || !IsDesignated(type, member))
                leaks.Add($"{type.FullName}.{member}");
        }

        foreach (var type in PublicLayers.SelectMany(a => a.GetTypes()))
        {
            foreach (var field in type.GetFields(AllMembers).Where(f => IsHidden(f.FieldType)))
                Add(type, field.Name);
            foreach (var property in type.GetProperties(AllMembers).Where(p => IsHidden(p.PropertyType)))
                Add(type, property.Name);
            foreach (var method in type.GetMethods(AllMembers).Cast<MethodBase>().Concat(type.GetConstructors(AllMembers)))
                if (method.GetParameters().Any(p => IsHidden(p.ParameterType)) || (method is MethodInfo info && IsHidden(info.ReturnType)))
                    Add(type, method.Name);
        }

        return leaks;
    }

    [Fact]
    public void No_application_or_infrastructure_member_signature_names_the_tendency_or_reference_types()
    {
        Assert.Empty(Leaks(includeDesignated: false));
    }

    [Fact]
    public void The_designated_holders_are_exactly_the_two_approved_internal_places_and_each_is_really_used()
    {
        // the exception list must not be a stale or inflated allowance: every entry names a member that exists and does name a hidden type
        Assert.Equal(Designated.SelectMany(d => d.Value.Select(m => $"{d.Key}.{m}")).Order(), Leaks(includeDesignated: true).Distinct().Order());

        var world = typeof(StarPlayer.Application.Ports.PlayerWorld);
        Assert.False(world.IsVisible); // internal: the Godot client cannot name it (TrustedBoundaryTests also pins this)
        Assert.True(world.IsNotPublic);
        var reader = typeof(PlayerInfoComposition).Assembly.GetTypes().Single(t => t.FullName == "StarPlayer.Infrastructure.Fixtures.WorldJson");
        Assert.False(reader.IsVisible);
        Assert.True(reader.GetMethod("ToProfiles", BindingFlags.NonPublic | BindingFlags.Static)!.IsPrivate); // a private step of the reader, not an entry point
    }

    [Fact]
    public void The_exceptions_are_scoped_to_member_names_not_to_a_whole_type_or_namespace()
    {
        // a hypothetical new member of a designated type, or the same member name on another type, would still be a leak
        Assert.False(IsDesignated(typeof(StarPlayer.Application.Ports.PlayerWorld), "NewHiddenMember"));
        Assert.False(IsDesignated(typeof(StarPlayer.Application.Queries.PlayerInfoService), "BatterTendencies"));
        Assert.False(IsDesignated(typeof(PlayerInfoComposition), "ToProfiles"));
        Assert.All(Designated.Keys, key => Assert.True(key is "StarPlayer.Application.Ports.PlayerWorld" or "StarPlayer.Infrastructure.Fixtures.WorldJson"));
    }

    [Fact]
    public void The_player_sheet_and_every_public_player_view_carry_no_hidden_type()
    {
        var sheetLayers = typeof(StarPlayer.Domain.Players.PlayerSheet).Assembly.GetTypes().Where(t => t.Namespace == "StarPlayer.Domain.Players");
        var views = typeof(StarPlayer.Application.Queries.PlayerPublicView).Assembly.GetTypes().Where(t => t.Namespace is "StarPlayer.Application.Queries" or "StarPlayer.Application.Sessions" or "StarPlayer.Application.Projections");
        var offenders = new List<string>();
        foreach (var type in sheetLayers.Concat(views))
        {
            foreach (var field in type.GetFields(AllMembers).Where(f => IsHidden(f.FieldType)))
                offenders.Add($"{type.FullName}.{field.Name}");
            foreach (var property in type.GetProperties(AllMembers).Where(p => IsHidden(p.PropertyType)))
                offenders.Add($"{type.FullName}.{property.Name}");
            foreach (var method in type.GetMethods(AllMembers).Cast<MethodBase>().Concat(type.GetConstructors(AllMembers)))
                if (method.GetParameters().Any(p => IsHidden(p.ParameterType)) || (method is MethodInfo info && IsHidden(info.ReturnType)))
                    offenders.Add($"{type.FullName}.{method.Name}");
        }

        Assert.NotEmpty(sheetLayers);
        Assert.Empty(offenders);
    }

    [Fact]
    public void The_signature_guard_recognises_the_hidden_types_directly_in_arrays_and_in_generic_arguments()
    {
        Assert.True(IsHidden(typeof(BatterZoneTendency)));
        Assert.True(IsHidden(typeof(List<BatterZoneTendency>)));
        Assert.True(IsHidden(typeof(BatterZoneTendency[])));
        Assert.True(IsHidden(typeof(Dictionary<string, ZoneReferenceDistribution>)));
        Assert.True(IsHidden(typeof(TendencyContext)));
        Assert.True(IsHidden(typeof(IReadOnlyList<ProbabilityTrace>)));
        Assert.True(IsHidden(typeof(Task<StageResult>)));
        Assert.False(IsHidden(typeof(PitchLocation)));
    }

    [Fact]
    public void The_tendency_text_form_does_not_print_its_values()
    {
        var tendency = BatterZoneTendency.Create([77, -77, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).Value;

        Assert.DoesNotContain("77", tendency.ToString());
    }

    // ---------------------------------------------------------------- Phase 3-D: the profile set and the world that holds it

    [Fact]
    public void The_signature_guard_recognises_the_profile_set_and_its_entry_directly_in_arrays_and_in_generic_arguments()
    {
        Assert.True(IsHidden(typeof(BatterZoneProfiles)));
        Assert.True(IsHidden(typeof(BatterZoneProfileEntry)));
        Assert.True(IsHidden(typeof(IReadOnlyList<BatterZoneProfileEntry>)));
        Assert.True(IsHidden(typeof(Dictionary<string, BatterZoneProfiles>)));
        Assert.True(IsHidden(typeof(BatterZoneProfiles[])));
        Assert.True(IsHidden(typeof(Task<BatterZoneProfiles>)));
    }

    private const string HiddenA = "77", HiddenB = "-83";

    /// <summary>The embedded v1 pack as v2 with the given rows. Rows are written by hand here, so no production writer vouches for them.</summary>
    private static string V2Pack(int first, int second)
    {
        var root = JsonNode.Parse(FixturePlayerWorldSource.ReadEmbeddedPack())!;
        root["format_version"] = 2;
        root["batter_zone_tendencies"] = JsonNode.Parse(
            $"[{{\"player_id\":\"p-0001\",\"zone_model\":\"pzl-zones-v1\",\"reference_id\":\"pzl-ref-uniform-v1\",\"values\":[{first},{-first},0,0,0,0,0,0,0]}}," +
            $"{{\"player_id\":\"p-0005\",\"zone_model\":\"pzl-zones-v1\",\"reference_id\":\"pzl-ref-uniform-v1\",\"values\":[0,0,{second},{-second},0,0,0,0,0]}}]");
        return root.ToJsonString();
    }

    private static string AllPublicOutput(string json, bool cheat)
    {
        var service = new PlayerInfoService(new FixturePlayerWorldSource(json), new StarPlayer.Application.Sessions.PlayerSessionOptions(CheatMode: cheat));
        Assert.True(service.IsReady, service.LoadError);
        var everything = new List<object> { service.GetSummary()!, service.ListPlayers() };
        everything.AddRange(service.ListPlayers().Select(p => (object)service.GetPlayer(new StarPlayer.Application.Queries.PlayerQuery(p.PlayerId))));
        return JsonSerializer.Serialize(everything);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Two_worlds_that_differ_only_in_the_hidden_tendency_give_identical_public_output_even_in_cheat_mode(bool cheat)
    {
        var a = AllPublicOutput(V2Pack(77, 83), cheat);
        var b = AllPublicOutput(V2Pack(-12, 5), cheat);
        var none = AllPublicOutput(FixturePlayerWorldSource.ReadEmbeddedPack(), cheat);

        Assert.Equal(a, b);
        Assert.Equal(none, a); // adding hidden profiles to a world changes nothing a client can query
        Assert.True(a.Length > 1000); // a real, non-trivial output was compared
    }

    [Fact]
    public void The_world_holder_and_the_profile_set_never_print_the_hidden_values()
    {
        var a = new FixturePlayerWorldSource(V2Pack(77, 83)).Load().Value;
        var b = new FixturePlayerWorldSource(V2Pack(-12, 5)).Load().Value;

        Assert.Equal(a.ToString(), b.ToString()); // the record's generated text is the same whatever the tendencies are
        Assert.Contains("BatterZoneProfiles(hidden)", a.ToString());
        Assert.DoesNotContain(HiddenA, a.ToString());
        Assert.DoesNotContain(HiddenB, a.ToString());
        Assert.Equal("BatterZoneProfiles(hidden)", a.BatterTendencies.ToString());
        Assert.Equal("BatterZoneProfileEntry(hidden)", a.BatterTendencies.Entries[0].ToString());
        Assert.DoesNotContain("p-0001", a.BatterTendencies.ToString());
        Assert.DoesNotContain(HiddenA, a.BatterTendencies.Entries[0].ToString());
    }

    [Fact]
    public void A_load_error_names_the_row_but_never_prints_the_hidden_values()
    {
        var bad = JsonNode.Parse(V2Pack(77, 83))!;
        bad["batter_zone_tendencies"]![0]!["values"]!.AsArray()[2] = 999; // out of range
        var outOfRange = new FixturePlayerWorldSource(bad.ToJsonString()).Load();
        var notNeutral = new FixturePlayerWorldSource(V2Pack(77, 83).Replace("[77,-77,0", "[77,-76,0", StringComparison.Ordinal)).Load();

        foreach (var failed in new[] { outOfRange, notNeutral })
        {
            Assert.False(failed.IsSuccess);
            Assert.StartsWith("BatterZoneTendency.", failed.Error!.Value.Code);
            Assert.Contains("batter_zone_tendencies[0]", failed.Error.Value.Message);
            foreach (var secret in new[] { "999", "77", "76" })
                Assert.DoesNotContain(secret, failed.Error.Value.Message);
        }
    }
}
