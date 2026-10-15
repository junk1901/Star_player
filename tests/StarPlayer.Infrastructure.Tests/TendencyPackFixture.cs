using System.Text.Json.Nodes;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Test-only DataPack v2 inputs (Phase 3-D). They are built from the UNCHANGED embedded v1 pack by editing its parsed JSON, so the official fictional pack is never altered and every
/// non-neutral vector below is a hand-written literal of this test project (no production writer or tendency code produces an expected value).
/// </summary>
internal static class TendencyPackFixture
{
    public const string Model = "pzl-zones-v1";
    public const string UniformRef = "pzl-ref-uniform-v1";

    /// <summary>Hand-written zero-sum vectors (the weighted sum under the uniform reference is 0), different per hitter so a mix-up is visible.</summary>
    public static readonly int[] Pull = [-60, 10, 20, 30, 40, -50, -40, 50, 0];
    public static readonly int[] Away = [80, -30, 0, -20, 0, 0, 20, 0, -50];
    public static readonly int[] CenterPlus = [-100, 0, 0, 0, 100, 0, 0, 0, 0];
    public static readonly int[] CenterMinus = [100, 0, 0, 0, -100, 0, 0, 0, 0];

    public static JsonNode V1() => JsonNode.Parse(FixturePlayerWorldSource.ReadEmbeddedPack())!;

    public static JsonObject Row(string player, int[] values, string model = Model, string reference = UniformRef) =>
        new() { ["player_id"] = player, ["zone_model"] = model, ["reference_id"] = reference, ["values"] = new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()) };

    /// <summary>The embedded pack as format 2. <paramref name="rows"/> null leaves the collection OUT of the file; an empty list writes <c>[]</c>.</summary>
    public static string V2(IEnumerable<JsonNode>? rows, Action<JsonNode>? edit = null)
    {
        var root = V1();
        root["format_version"] = 2;
        if (rows is not null)
            root["batter_zone_tendencies"] = new JsonArray(rows.Select(r => (JsonNode?)r.DeepClone()).ToArray());
        edit?.Invoke(root);
        return root.ToJsonString();
    }

    /// <summary>Format 2 with the collection OUT of the file (optionally edited further).</summary>
    public static string Omitted(Action<JsonNode>? edit = null) => V2((IEnumerable<JsonNode>?)null, edit);

    public static string V2(params JsonNode[] rows) => V2((IEnumerable<JsonNode>)rows);

    /// <summary>Copies hitter <paramref name="template"/> (and its hidden development row) to a new player id, so a world can contain the ids of a game fixture.</summary>
    public static void AddPlayerLike(JsonNode root, string template, string newId)
    {
        var players = root["players"]!.AsArray();
        var player = players.Single(p => p!["player_id"]!.GetValue<string>() == template)!.DeepClone();
        player["player_id"] = newId;
        players.Add(player);
        var profiles = root["development_profiles"]!.AsArray();
        var profile = profiles.Single(d => d!["player_id"]!.GetValue<string>() == template)!.DeepClone();
        profile["player_id"] = newId;
        profiles.Add(profile);
    }
}
