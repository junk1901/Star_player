using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-B2 changes no rule profile, no RNG contract and no event format. These pins were read from the approved 3-B1 state (GameRules.cs, content/rules and the
/// RNG sources have no diff against it) so any accidental change shows up here as well as in the existing golden-vector and legacy-hash tests.
/// </summary>
public class Phase3B2CompatibilityTests
{
    [Fact]
    public void The_gamerules_v2_profile_hashes_are_unchanged()
    {
        var v2 = GameRulesJson.Read(ScriptedGameSource.ReadResource("game_rules_profiles_v2.json")).Value;
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["test-pzl-automated"] = "7ad9776729b4a71d60af352d620df561d02f699e036fd6f4a4870b82635f4fa7",
                ["test-pzl-non-automated"] = "2d3f7557750c65d920ee79ec0ff57121ae8b8990f13368ae4daa7520492dce2d",
            },
            v2.ToDictionary(p => p.ProfileId, p => p.ContentHash));
        Assert.All(v2, p => Assert.Equal("gamerules-v2", p.RulesSchema));
    }

    [Fact]
    public void The_legacy_gamerules_profiles_still_declare_no_called_pitch_mode_and_keep_seven_hashes()
    {
        var legacy = GameRulesJson.Read(ScriptedGameSource.ReadResource("game_rules_profiles.json")).Value;
        Assert.Equal(7, legacy.Count);
        Assert.All(legacy, p => Assert.Null(p.CalledPitchMode));
        Assert.Equal("9deedbde13ee37719f1f12a9bb6451ad91e5dc870e34ff73eab667f9828dcdbc", legacy.Single(p => p.ProfileId == "kbo-oriented-unverified-v0").ContentHash);
    }

    [Fact]
    public void The_rng_algorithm_key_derivation_snapshot_format_and_stream_names_are_unchanged()
    {
        Assert.Equal("pcg32-xsh-rr-v1", RngVersions.Algorithm);
        Assert.Equal("kd1", RngVersions.KeyDerivation);
        Assert.Equal("rngset1", RngVersions.SnapshotFormat);
        Assert.Equal(
            ["game/pitch@1", "game/contact@1", "game/field@1", "game/runner@1", "game/location@1", "game/ai@1"],
            new[] { GameRngStreams.Pitch, GameRngStreams.Contact, GameRngStreams.Field, GameRngStreams.Runner, GameRngStreams.Location, GameRngStreams.Ai }.Select(s => s.Canonical));
    }

    [Fact]
    public void Nothing_in_the_probability_contract_changes_the_event_fingerprint_versions()
    {
        // ef1 text and the event model are untouched by 3-B2: the probability namespace holds no event type and the event assembly part has no reference to it.
        var eventTypes = typeof(GameEvent).Assembly.GetTypes().Where(t => typeof(GameEvent).IsAssignableFrom(t));
        Assert.All(eventTypes, t => Assert.DoesNotContain(t.GetProperties(), p => p.PropertyType.Namespace == "StarPlayer.Domain.Baseball.Probability"));
    }
}
