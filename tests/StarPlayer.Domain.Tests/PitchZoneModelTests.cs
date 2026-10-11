using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Players;

namespace StarPlayer.Domain.Tests;

/// <summary>Phase 3-B1: the categorical Pitch Zone Lite model <c>pzl-zones-v1</c> (ROADMAP 1.14). No coordinates, no randomness.</summary>
public class PitchZoneModelTests
{
    private static readonly string[] InnerIds =
    [
        "in.r1c1", "in.r1c2", "in.r1c3", "in.r2c1", "in.r2c2", "in.r2c3", "in.r3c1", "in.r3c2", "in.r3c3",
    ];

    private static readonly string[] OuterIds =
    [
        "out.high", "out.high_in", "out.in", "out.low_in", "out.low", "out.low_out", "out.out", "out.high_out",
    ];

    private static PitchZoneId Zone(string text) => PitchZoneId.Create(text).Value;

    [Fact]
    public void The_model_has_exactly_the_seventeen_stable_zone_ids_in_canonical_order_without_duplicates()
    {
        var model = PitchZoneModel.V1;

        Assert.Equal("pzl-zones-v1", model.Version.Value);
        Assert.Equal(InnerIds.Concat(OuterIds), model.Zones.Select(z => z.Id.Value));
        Assert.Equal(17, model.Zones.Select(z => z.Id.Value).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Nine_zones_are_inner_and_eight_are_outer_and_the_inner_index_is_row_major()
    {
        var model = PitchZoneModel.V1;

        Assert.Equal(InnerIds, model.Zones.Where(z => z.Region == PitchZoneRegion.Inner).Select(z => z.Id.Value));
        Assert.Equal(OuterIds, model.Zones.Where(z => z.Region == PitchZoneRegion.Outer).Select(z => z.Id.Value));
        Assert.Equal(Enumerable.Range(0, 9), model.Zones.Where(z => z.Region == PitchZoneRegion.Inner).Select(z => z.InnerIndex));
        Assert.All(model.Zones.Where(z => z.Region == PitchZoneRegion.Outer), z => Assert.Equal(-1, z.InnerIndex));
    }

    [Fact]
    public void Rows_run_high_to_low_and_columns_run_inside_to_outside_relative_to_the_batter()
    {
        var model = PitchZoneModel.V1;

        PitchZoneInfo Info(string id) => model.Info(Zone(id)).Value;

        Assert.Equal((ZoneVertical.High, ZoneLateral.Inside), (Info("in.r1c1").Vertical, Info("in.r1c1").Lateral));
        Assert.Equal((ZoneVertical.Middle, ZoneLateral.Middle), (Info("in.r2c2").Vertical, Info("in.r2c2").Lateral));
        Assert.Equal((ZoneVertical.Low, ZoneLateral.Outside), (Info("in.r3c3").Vertical, Info("in.r3c3").Lateral));
        Assert.Equal((ZoneVertical.High, ZoneLateral.Middle), (Info("out.high").Vertical, Info("out.high").Lateral));
        Assert.Equal((ZoneVertical.High, ZoneLateral.Inside), (Info("out.high_in").Vertical, Info("out.high_in").Lateral));
        Assert.Equal((ZoneVertical.Middle, ZoneLateral.Inside), (Info("out.in").Vertical, Info("out.in").Lateral));
        Assert.Equal((ZoneVertical.Low, ZoneLateral.Inside), (Info("out.low_in").Vertical, Info("out.low_in").Lateral));
        Assert.Equal((ZoneVertical.Low, ZoneLateral.Middle), (Info("out.low").Vertical, Info("out.low").Lateral));
        Assert.Equal((ZoneVertical.Low, ZoneLateral.Outside), (Info("out.low_out").Vertical, Info("out.low_out").Lateral));
        Assert.Equal((ZoneVertical.Middle, ZoneLateral.Outside), (Info("out.out").Vertical, Info("out.out").Lateral));
        Assert.Equal((ZoneVertical.High, ZoneLateral.Outside), (Info("out.high_out").Vertical, Info("out.high_out").Lateral));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("in.r0c1")]
    [InlineData("in.r4c1")]
    [InlineData("in.r1c4")]
    [InlineData("out.top")]
    [InlineData("IN.R1C1")]
    [InlineData(" in.r1c1")]
    [InlineData("in.r1c1 ")]
    [InlineData("out.high-in")]
    [InlineData("zone")]
    public void An_unknown_or_non_canonical_zone_text_is_rejected(string text)
    {
        var made = PitchZoneId.Create(text);

        Assert.False(made.IsSuccess);
        Assert.StartsWith("PitchZoneId.", made.Error!.Value.Code);
    }

    [Fact]
    public void A_null_or_default_zone_id_is_rejected_everywhere_it_is_used()
    {
        Assert.False(PitchZoneId.Create(null).IsSuccess);
        Assert.False(PitchZoneModel.V1.Info(default).IsSuccess);
        Assert.False(PitchZoneModel.V1.Classify(default).IsSuccess);
        Assert.False(PitchZoneModel.V1.ToCatcherView(default, AppliedBatterSide.Right).IsSuccess);
        Assert.False(PitchLocation.FromZone(ZoneModelVersion.Pzl1, default, AppliedBatterSide.Right).IsSuccess);
        Assert.False(TargetZone.Create(ZoneModelVersion.Pzl1, default).IsSuccess);
    }

    [Theory]
    [InlineData("pzl-zones-v2")]
    [InlineData("PZL-ZONES-V1")]
    [InlineData("pzl-zones-v1 ")]
    [InlineData("")]
    [InlineData("unknown")]
    public void An_unsupported_zone_model_version_is_rejected_and_never_defaulted(string text)
    {
        var made = ZoneModelVersion.Create(text);

        Assert.False(made.IsSuccess);
        Assert.False(ZoneModelVersion.Create(null).IsSuccess);
        Assert.False(PitchZoneModel.Resolve(default).IsSuccess);
    }

    [Fact]
    public void The_supported_zone_model_version_resolves_to_the_one_model()
    {
        var version = ZoneModelVersion.Create("pzl-zones-v1");

        Assert.True(version.IsSuccess);
        Assert.Equal(ZoneModelVersion.Pzl1, version.Value);
        Assert.Same(PitchZoneModel.V1, PitchZoneModel.Resolve(version.Value).Value);
    }

    // --- boundary class table ---

    [Fact]
    public void The_boundary_table_is_fixed_by_the_model_version_clear_only_for_the_heart_and_the_four_diagonal_outer_corners()
    {
        var expectedClear = new[] { "in.r2c2", "out.high_in", "out.low_in", "out.low_out", "out.high_out" };
        var model = PitchZoneModel.V1;

        foreach (var zone in model.Zones)
        {
            var expected = expectedClear.Contains(zone.Id.Value) ? BoundaryClass.Clear : BoundaryClass.Borderline;
            Assert.Equal(expected, zone.Boundary);
            Assert.Equal(expected, model.Classify(zone.Id).Value);
        }

        Assert.Equal(5, model.Zones.Count(z => z.Boundary == BoundaryClass.Clear));
        Assert.Equal(12, model.Zones.Count(z => z.Boundary == BoundaryClass.Borderline));
    }

    [Fact]
    public void Classification_is_a_pure_function_of_the_zone_and_independent_of_side_and_call_order()
    {
        var model = PitchZoneModel.V1;
        var first = model.Zones.Select(z => model.Classify(z.Id).Value).ToList();
        var second = model.Zones.Reverse().Select(z => model.Classify(z.Id).Value).Reverse().ToList();

        Assert.Equal(first, second);
        foreach (var zone in model.Zones)
        {
            var right = PitchLocation.FromZone(ZoneModelVersion.Pzl1, zone.Id, AppliedBatterSide.Right).Value;
            var left = PitchLocation.FromZone(ZoneModelVersion.Pzl1, zone.Id, AppliedBatterSide.Left).Value;
            Assert.Equal(right.Boundary, left.Boundary);
            Assert.Equal(zone.Boundary, right.Boundary);
        }
    }

    [Fact]
    public void Only_the_table_combination_of_zone_and_class_is_allowed()
    {
        var borderlineZone = Zone("in.r1c1");
        var clearZone = Zone("in.r2c2");

        Assert.True(PitchLocation.Create(ZoneModelVersion.Pzl1, borderlineZone, AppliedBatterSide.Right, BoundaryClass.Borderline).IsSuccess);
        Assert.True(PitchLocation.Create(ZoneModelVersion.Pzl1, clearZone, AppliedBatterSide.Right, BoundaryClass.Clear).IsSuccess);

        var wrong1 = PitchLocation.Create(ZoneModelVersion.Pzl1, borderlineZone, AppliedBatterSide.Right, BoundaryClass.Clear);
        var wrong2 = PitchLocation.Create(ZoneModelVersion.Pzl1, clearZone, AppliedBatterSide.Right, BoundaryClass.Borderline);
        Assert.Equal("PitchLocation.boundary_mismatch", wrong1.Error!.Value.Code);
        Assert.Equal("PitchLocation.boundary_mismatch", wrong2.Error!.Value.Code);
    }

    [Fact]
    public void An_undefined_side_or_boundary_value_or_default_model_is_rejected()
    {
        var zone = Zone("in.r2c2");

        Assert.Equal("PitchLocation.side_invalid", PitchLocation.Create(ZoneModelVersion.Pzl1, zone, (AppliedBatterSide)9, BoundaryClass.Clear).Error!.Value.Code);
        Assert.Equal("PitchLocation.boundary_invalid", PitchLocation.Create(ZoneModelVersion.Pzl1, zone, AppliedBatterSide.Left, (BoundaryClass)9).Error!.Value.Code);
        Assert.False(PitchLocation.Create(default, zone, AppliedBatterSide.Left, BoundaryClass.Clear).IsSuccess);
        Assert.False(PitchLocation.FromZone(ZoneModelVersion.Pzl1, zone, (AppliedBatterSide)9).IsSuccess);
    }

    [Fact]
    public void A_location_keeps_model_zone_side_and_boundary_and_equal_inputs_give_equal_locations()
    {
        var a = PitchLocation.FromZone(ZoneModelVersion.Pzl1, Zone("out.low_in"), AppliedBatterSide.Left).Value;
        var b = PitchLocation.FromZone(ZoneModelVersion.Pzl1, Zone("out.low_in"), AppliedBatterSide.Left).Value;
        var otherSide = PitchLocation.FromZone(ZoneModelVersion.Pzl1, Zone("out.low_in"), AppliedBatterSide.Right).Value;

        Assert.Equal(a, b);
        Assert.NotEqual(a, otherSide);
        Assert.Equal((ZoneModelVersion.Pzl1, "out.low_in", AppliedBatterSide.Left, BoundaryClass.Clear), (a.Model, a.Zone.Value, a.Side, a.Boundary));
    }

    [Fact]
    public void A_target_zone_is_a_different_type_from_a_location_and_carries_no_side_or_boundary()
    {
        var target = TargetZone.Create(ZoneModelVersion.Pzl1, Zone("out.low")).Value;

        Assert.Equal("out.low", target.Zone.Value);
        Assert.Equal(ZoneModelVersion.Pzl1, target.Model);
        Assert.DoesNotContain(typeof(TargetZone).GetProperties(), p => p.PropertyType == typeof(AppliedBatterSide) || p.PropertyType == typeof(BoundaryClass));
        Assert.False(TargetZone.Create(default, Zone("out.low")).IsSuccess);
    }

    // --- batter side and catcher's view ---

    [Fact]
    public void A_right_handed_batters_inside_is_the_catchers_left_and_a_left_handed_batters_inside_is_the_catchers_right()
    {
        var model = PitchZoneModel.V1;

        Assert.Equal(new CatcherViewPosition(PitchZoneRegion.Inner, CatcherHorizontal.Left, ZoneVertical.Middle), model.ToCatcherView(Zone("in.r2c1"), AppliedBatterSide.Right).Value);
        Assert.Equal(new CatcherViewPosition(PitchZoneRegion.Inner, CatcherHorizontal.Right, ZoneVertical.Middle), model.ToCatcherView(Zone("in.r2c1"), AppliedBatterSide.Left).Value);
        Assert.Equal(new CatcherViewPosition(PitchZoneRegion.Outer, CatcherHorizontal.Right, ZoneVertical.Low), model.ToCatcherView(Zone("out.low_out"), AppliedBatterSide.Right).Value);
        Assert.Equal(new CatcherViewPosition(PitchZoneRegion.Outer, CatcherHorizontal.Left, ZoneVertical.Low), model.ToCatcherView(Zone("out.low_out"), AppliedBatterSide.Left).Value);
    }

    [Fact]
    public void Middle_column_and_vertical_only_zones_are_the_same_in_the_catchers_view_for_both_sides()
    {
        var model = PitchZoneModel.V1;

        foreach (var id in new[] { "in.r1c2", "in.r2c2", "in.r3c2", "out.high", "out.low" })
            Assert.Equal(model.ToCatcherView(Zone(id), AppliedBatterSide.Right).Value, model.ToCatcherView(Zone(id), AppliedBatterSide.Left).Value);
    }

    [Fact]
    public void The_catchers_view_round_trips_for_all_zones_and_both_sides_and_is_one_to_one()
    {
        var model = PitchZoneModel.V1;
        foreach (var side in new[] { AppliedBatterSide.Right, AppliedBatterSide.Left })
        {
            var views = new HashSet<CatcherViewPosition>();
            foreach (var zone in model.Zones)
            {
                var view = model.ToCatcherView(zone.Id, side).Value;
                Assert.True(views.Add(view), $"{zone.Id} collides in the catcher's view");
                Assert.Equal(zone.Id, model.FromCatcherView(view, side).Value);
            }

            Assert.Equal(17, views.Count);
        }
    }

    [Fact]
    public void A_catcher_view_position_that_is_not_a_zone_is_rejected()
    {
        // The centre of the outer ring does not exist (the heart is the inner zone).
        var made = PitchZoneModel.V1.FromCatcherView(new CatcherViewPosition(PitchZoneRegion.Outer, CatcherHorizontal.Center, ZoneVertical.Middle), AppliedBatterSide.Right);

        Assert.Equal("PitchZone.catcher_view_invalid", made.Error!.Value.Code);
        Assert.False(PitchZoneModel.V1.FromCatcherView(new CatcherViewPosition((PitchZoneRegion)9, CatcherHorizontal.Left, ZoneVertical.Low), AppliedBatterSide.Right).IsSuccess);
        Assert.False(PitchZoneModel.V1.ToCatcherView(Zone("in.r1c1"), (AppliedBatterSide)9).IsSuccess);
    }

    [Fact]
    public void A_batter_relative_zone_keeps_its_meaning_when_the_side_changes_only_the_catchers_view_moves()
    {
        var model = PitchZoneModel.V1;
        var inside = Zone("out.in");

        Assert.Equal(ZoneLateral.Inside, model.Info(inside).Value.Lateral);
        Assert.NotEqual(model.ToCatcherView(inside, AppliedBatterSide.Right).Value, model.ToCatcherView(inside, AppliedBatterSide.Left).Value);
    }

    [Theory]
    [InlineData(BattingSide.Right, AppliedBatterSide.Right, true)]
    [InlineData(BattingSide.Right, AppliedBatterSide.Left, false)]
    [InlineData(BattingSide.Left, AppliedBatterSide.Left, true)]
    [InlineData(BattingSide.Left, AppliedBatterSide.Right, false)]
    [InlineData(BattingSide.Switch, AppliedBatterSide.Left, true)]
    [InlineData(BattingSide.Switch, AppliedBatterSide.Right, true)]
    public void The_applied_side_must_be_possible_for_the_declared_batting_side_and_a_switch_hitter_keeps_the_chosen_side(BattingSide declared, AppliedBatterSide applied, bool allowed)
    {
        var made = AppliedBatterSideRules.Validate(declared, applied);

        Assert.Equal(allowed, made.IsSuccess);
        if (allowed)
            Assert.Equal(applied, made.Value);
        else
            Assert.Equal("AppliedBatterSide.not_possible", made.Error!.Value.Code);
    }

    [Fact]
    public void A_one_sided_batters_side_can_be_derived_but_a_switch_hitters_cannot()
    {
        Assert.Equal(AppliedBatterSide.Right, AppliedBatterSideRules.OnlySide(BattingSide.Right).Value);
        Assert.Equal(AppliedBatterSide.Left, AppliedBatterSideRules.OnlySide(BattingSide.Left).Value);
        Assert.Equal("AppliedBatterSide.choice_required", AppliedBatterSideRules.OnlySide(BattingSide.Switch).Error!.Value.Code);
        Assert.False(AppliedBatterSideRules.Validate((BattingSide)9, AppliedBatterSide.Left).IsSuccess);
        Assert.False(AppliedBatterSideRules.Validate(BattingSide.Switch, (AppliedBatterSide)9).IsSuccess);
    }

    [Fact]
    public void A_switch_hitters_two_pitches_with_different_applied_sides_stay_distinguishable_and_reconstructable()
    {
        var model = PitchZoneModel.V1;
        var asRight = PitchLocation.FromZone(ZoneModelVersion.Pzl1, Zone("in.r2c1"), AppliedBatterSide.Right).Value;
        var asLeft = PitchLocation.FromZone(ZoneModelVersion.Pzl1, Zone("in.r2c1"), AppliedBatterSide.Left).Value;

        Assert.NotEqual(asRight, asLeft);
        Assert.NotEqual(model.ToCatcherView(asRight.Zone, asRight.Side).Value, model.ToCatcherView(asLeft.Zone, asLeft.Side).Value);
    }

    [Fact]
    public void The_same_calls_with_the_same_inputs_always_return_equal_results()
    {
        var model = PitchZoneModel.V1;
        for (var run = 0; run < 3; run++)
            foreach (var zone in model.Zones)
            {
                Assert.Equal(model.Classify(zone.Id).Value, model.Classify(zone.Id).Value);
                Assert.Equal(model.ToCatcherView(zone.Id, AppliedBatterSide.Left).Value, model.ToCatcherView(zone.Id, AppliedBatterSide.Left).Value);
            }
    }

    [Fact]
    public void The_model_exposes_read_only_collections()
    {
        var zones = (IList<PitchZoneInfo>)PitchZoneModel.V1.Zones;

        Assert.Throws<NotSupportedException>(() => zones.Clear());
        Assert.Equal(17, PitchZoneModel.V1.Zones.Count);
    }
}
