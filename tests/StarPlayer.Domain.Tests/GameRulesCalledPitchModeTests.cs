using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;

namespace StarPlayer.Domain.Tests;

/// <summary>Phase 3-B1: C40 owns the versioned called-pitch mode. A legacy snapshot says nothing about it and its hash never changes.</summary>
public class GameRulesCalledPitchModeTests
{
    private static Common.Result<GameRuleSnapshot> Legacy(string id = "legacy-x", string version = "v0") => GameRuleSnapshot.Create(
        id, version, GameRulesStatus.FictionalTest, "시험", ["x"], 3, 4, 3, 9, 11, true, true, true, 15, 3, 3, 9, false, true, false, "fictional-test-v1");

    private static Common.Result<GameRuleSnapshot> WithMode(CalledPitchMode? mode, ZoneModelVersion zone = default, string id = "pzl-x", string version = "test-v2") => GameRuleSnapshot.Create(
        id, version, GameRulesStatus.FictionalTest, "시험", ["x"], 3, 4, 3, 9, 11, true, true, true, 15, 3, 3, 9, false, true, false, "fictional-test-v1",
        mode, zone);

    [Fact]
    public void A_legacy_snapshot_does_not_declare_a_called_pitch_mode_or_zone_model_and_says_so()
    {
        var legacy = Legacy().Value;

        Assert.Null(legacy.CalledPitchMode);
        Assert.Null(legacy.ZoneModel);
        Assert.False(legacy.CalledPitchModeDeclared);
        Assert.Equal("gamerules-v1", legacy.RulesSchema);
    }

    [Fact]
    public void A_legacy_snapshot_is_never_treated_as_an_automated_or_non_automated_zone()
    {
        var framing = Legacy().Value.FramingMayChangeCalls();

        Assert.False(framing.IsSuccess);
        Assert.Equal("GameRules.called_pitch_mode_undeclared", framing.Error!.Value.Code);
    }

    [Fact]
    public void The_legacy_content_hash_is_the_value_computed_before_the_pitch_zone_fields_existed()
    {
        // Independently computed with SHA-256 over the legacy canonical text (tests/Fixtures/... python oracle, 2026-10-11).
        var legacy = Legacy("test-dh-cap11", "test-v1").Value;

        Assert.Equal("877790af4840d1d4b57166ab14d6521a7acaf64a3bd1a24adbfa415feb09509b", GameRuleSnapshot.Create(
            "test-dh-cap11", "test-v1", GameRulesStatus.FictionalTest, "x", ["x"], 3, 4, 3, 9, 11, true, true, true, 15, 3, 3, 9, false, true, false, "fictional-test-v1").Value.ContentHash);
        Assert.Equal(legacy.ContentHash, Legacy("test-dh-cap11", "test-v1").Value.ContentHash);
    }

    [Fact]
    public void An_explicit_mode_with_the_zone_model_builds_a_v2_snapshot_that_keeps_both_choices()
    {
        var automated = WithMode(CalledPitchMode.Automated, ZoneModelVersion.Pzl1).Value;
        var manual = WithMode(CalledPitchMode.NonAutomated, ZoneModelVersion.Pzl1).Value;

        Assert.Equal(CalledPitchMode.Automated, automated.CalledPitchMode);
        Assert.Equal(CalledPitchMode.NonAutomated, manual.CalledPitchMode);
        Assert.Equal(ZoneModelVersion.Pzl1, automated.ZoneModel);
        Assert.True(automated.CalledPitchModeDeclared);
        Assert.Equal("gamerules-v2", automated.RulesSchema);
    }

    [Fact]
    public void The_v2_hash_covers_the_mode_and_the_zone_model_and_differs_from_every_other_choice_and_from_the_legacy_hash()
    {
        var automated = WithMode(CalledPitchMode.Automated, ZoneModelVersion.Pzl1).Value;
        var manual = WithMode(CalledPitchMode.NonAutomated, ZoneModelVersion.Pzl1).Value;
        var legacySameText = Legacy("pzl-x", "test-v2").Value;

        Assert.NotEqual(automated.ContentHash, manual.ContentHash);
        Assert.NotEqual(automated.ContentHash, legacySameText.ContentHash);
        Assert.NotEqual(manual.ContentHash, legacySameText.ContentHash);
        Assert.Equal(automated.ContentHash, WithMode(CalledPitchMode.Automated, ZoneModelVersion.Pzl1).Value.ContentHash);
    }

    [Fact]
    public void Either_new_field_without_the_other_is_rejected_so_a_half_declared_profile_cannot_exist()
    {
        Assert.Equal("GameRules.pitch_model_incomplete", WithMode(CalledPitchMode.Automated).Error!.Value.Code);
        Assert.Equal("GameRules.pitch_model_incomplete", WithMode(null, ZoneModelVersion.Pzl1).Error!.Value.Code);
    }

    [Fact]
    public void An_undefined_mode_value_is_rejected()
    {
        Assert.Equal("GameRules.called_pitch_mode_invalid", WithMode((CalledPitchMode)7, ZoneModelVersion.Pzl1).Error!.Value.Code);
    }

    [Fact]
    public void Automated_mode_means_framing_cannot_change_a_call_and_non_automated_allows_it_within_later_caps()
    {
        Assert.False(WithMode(CalledPitchMode.Automated, ZoneModelVersion.Pzl1).Value.FramingMayChangeCalls().Value);
        Assert.True(WithMode(CalledPitchMode.NonAutomated, ZoneModelVersion.Pzl1).Value.FramingMayChangeCalls().Value);
    }

    [Fact]
    public void Adding_the_new_fields_leaves_every_other_rule_value_identical_to_the_legacy_snapshot()
    {
        var legacy = Legacy("same", "v").Value;
        var v2 = WithMode(CalledPitchMode.Automated, ZoneModelVersion.Pzl1, "same", "v").Value;

        Assert.Equal((legacy.OutsPerHalfInning, legacy.BallsForWalk, legacy.StrikesForStrikeout, legacy.RegulationInnings, legacy.ExtraInningsLimit, legacy.DesignatedHitter),
            (v2.OutsPerHalfInning, v2.BallsForWalk, v2.StrikesForStrikeout, v2.RegulationInnings, v2.ExtraInningsLimit, v2.DesignatedHitter));
        Assert.Equal(legacy.Status, v2.Status);
    }
}
