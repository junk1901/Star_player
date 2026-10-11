using StarPlayer.Domain.Baseball.PitchZone;

namespace StarPlayer.Domain.Tests;

/// <summary>Phase 3-B1: the hidden, bounded, neutral-default nine-zone batter tendency vector. It is data only; no probability uses it yet.</summary>
public class BatterZoneTendencyTests
{
    private static BatterZoneTendency Make(params int[] values) => BatterZoneTendency.Create(values, ZoneReferenceDistribution.UniformV1).Value;

    [Fact]
    public void The_default_vector_is_nine_zeros_and_neutral()
    {
        var neutral = BatterZoneTendency.Neutral;

        Assert.Equal(9, BatterZoneTendency.ZoneCount);
        Assert.Equal(new int[9], neutral.Values);
        Assert.True(neutral.IsNeutral);
        Assert.Equal(ZoneModelVersion.Pzl1, neutral.Model);
        Assert.Equal(ZoneReferenceDistribution.UniformV1.Id, neutral.ReferenceId);
        Assert.Equal(neutral, Make(0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void A_zero_sum_vector_within_the_bound_is_accepted_and_not_neutral()
    {
        var made = BatterZoneTendency.Create([100, 0, 0, 0, 0, 0, 0, 0, -100], ZoneReferenceDistribution.UniformV1);

        Assert.True(made.IsSuccess);
        Assert.False(made.Value.IsNeutral);
        Assert.Equal(100, made.Value.Values[0]);
        Assert.Equal(-100, made.Value.Values[8]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(17)]
    public void A_vector_of_the_wrong_length_is_rejected(int length)
    {
        var made = BatterZoneTendency.Create(new int[length], ZoneReferenceDistribution.UniformV1);

        Assert.Equal("BatterZoneTendency.length_invalid", made.Error!.Value.Code);
    }

    [Fact]
    public void A_missing_vector_or_reference_is_rejected()
    {
        Assert.Equal("BatterZoneTendency.values_missing", BatterZoneTendency.Create(null, ZoneReferenceDistribution.UniformV1).Error!.Value.Code);
        Assert.Equal("BatterZoneTendency.reference_missing", BatterZoneTendency.Create(new int[9], null).Error!.Value.Code);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(-101)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void A_value_outside_the_bound_is_rejected_even_when_the_sum_is_balanced(int bad)
    {
        var values = new int[9];
        values[3] = bad;
        values[4] = bad == int.MinValue ? int.MaxValue : -bad;

        var made = BatterZoneTendency.Create(values, ZoneReferenceDistribution.UniformV1);

        Assert.Equal("BatterZoneTendency.value_out_of_range", made.Error!.Value.Code);
    }

    [Fact]
    public void Only_the_upper_bound_or_only_the_lower_bound_broken_is_each_rejected_on_its_own()
    {
        // Each vector is balanced and has exactly one value outside the bound, so a missing upper or lower check cannot hide behind the other.
        Assert.Equal("BatterZoneTendency.value_out_of_range", BatterZoneTendency.Create([150, -100, -50, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).Error!.Value.Code);
        Assert.Equal("BatterZoneTendency.value_out_of_range", BatterZoneTendency.Create([-150, 100, 50, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).Error!.Value.Code);
    }

    [Fact]
    public void A_vector_that_raises_or_lowers_the_average_effect_is_rejected_by_the_neutrality_policy()
    {
        var up = BatterZoneTendency.Create([10, 0, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1);
        var down = BatterZoneTendency.Create([-5, -5, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1);

        Assert.Equal("BatterZoneTendency.not_average_neutral", up.Error!.Value.Code);
        Assert.Equal("BatterZoneTendency.not_average_neutral", down.Error!.Value.Code);
    }

    [Fact]
    public void Neutrality_is_judged_against_the_reference_distribution_not_the_plain_sum()
    {
        // Reference puts three times the weight on the first zone: +1 there is balanced by -3 on the second (weights 3 and 1).
        var reference = ZoneReferenceDistribution.CreateUnregisteredForTest("pzl-ref-test-weighted", [3, 1, 0, 0, 0, 0, 0, 0, 0]).Value;

        Assert.True(BatterZoneTendency.Create([1, -3, 0, 0, 0, 0, 0, 0, 0], reference).IsSuccess);
        Assert.False(BatterZoneTendency.Create([1, -1, 0, 0, 0, 0, 0, 0, 0], reference).IsSuccess);
        // A zone that never occurs in the reference may carry any in-range value without moving the average.
        Assert.True(BatterZoneTendency.Create([0, 0, 50, 0, 0, 0, 0, 0, 0], reference).IsSuccess);
        Assert.Equal("pzl-ref-test-weighted", BatterZoneTendency.Create([1, -3, 0, 0, 0, 0, 0, 0, 0], reference).Value.ReferenceId);
    }

    [Fact]
    public void A_valid_reference_distribution_needs_nine_non_negative_weights_with_a_positive_total_and_an_id()
    {
        const string uniform = "pzl-ref-uniform-v1";
        Assert.False(ZoneReferenceDistribution.CreateUnregisteredForTest("r", new int[8]).IsSuccess);
        Assert.False(ZoneReferenceDistribution.CreateUnregisteredForTest("r", new int[9]).IsSuccess);
        Assert.False(ZoneReferenceDistribution.CreateUnregisteredForTest("r", [1, 1, 1, 1, 1, 1, 1, 1, -1]).IsSuccess);
        Assert.False(ZoneReferenceDistribution.CreateUnregisteredForTest("r", [int.MaxValue, 1, 1, 1, 1, 1, 1, 1, 1]).IsSuccess);
        Assert.False(ZoneReferenceDistribution.CreateUnregisteredForTest(" ", [1, 1, 1, 1, 1, 1, 1, 1, 1]).IsSuccess);
        Assert.False(ZoneReferenceDistribution.CreateUnregisteredForTest("r", null).IsSuccess);
        Assert.False(ZoneReferenceDistribution.Create(uniform, new int[8]).IsSuccess);
        Assert.False(ZoneReferenceDistribution.Create(uniform, [1, 1, 1, 1, 1, 1, 1, 1, -1]).IsSuccess);
        Assert.False(ZoneReferenceDistribution.Create(uniform, null).IsSuccess);
        Assert.True(ZoneReferenceDistribution.Create(uniform, [1, 1, 1, 1, 1, 1, 1, 1, 1]).IsSuccess);
        Assert.Equal(new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 }, ZoneReferenceDistribution.UniformV1.Weights);
    }

    [Fact]
    public void The_adjustment_of_an_inner_zone_is_its_value_and_every_outer_zone_is_not_adjusted()
    {
        var tendency = Make(10, 0, 0, 0, -10, 0, 0, 0, 0);
        var model = PitchZoneModel.V1;

        foreach (var zone in model.Zones.Where(z => z.Region == PitchZoneRegion.Inner))
            Assert.Equal(tendency.Values[zone.InnerIndex], tendency.AdjustmentFor(zone.Id).Value);
        Assert.Equal(10, tendency.AdjustmentFor(PitchZoneId.Create("in.r1c1").Value).Value);
        Assert.Equal(-10, tendency.AdjustmentFor(PitchZoneId.Create("in.r2c2").Value).Value);
        foreach (var zone in model.Zones.Where(z => z.Region == PitchZoneRegion.Outer))
            Assert.Equal(0, tendency.AdjustmentFor(zone.Id).Value);
        Assert.False(tendency.AdjustmentFor(default).IsSuccess);
    }

    [Fact]
    public void The_vector_is_immutable_a_copy_of_the_input_and_equal_by_content()
    {
        var input = new[] { 5, -5, 0, 0, 0, 0, 0, 0, 0 };
        var tendency = BatterZoneTendency.Create(input, ZoneReferenceDistribution.UniformV1).Value;
        input[0] = 99;

        Assert.Equal(5, tendency.Values[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)tendency.Values).Add(1));
        Assert.Throws<NotSupportedException>(() => ((IList<int>)tendency.Values)[0] = 1);
        Assert.Equal(Make(5, -5, 0, 0, 0, 0, 0, 0, 0), tendency);
        Assert.Equal(Make(5, -5, 0, 0, 0, 0, 0, 0, 0).GetHashCode(), tendency.GetHashCode());
        Assert.NotEqual(Make(-5, 5, 0, 0, 0, 0, 0, 0, 0), tendency);
    }

    [Fact]
    public void The_text_form_never_prints_the_raw_values()
    {
        var text = Make(37, -37, 0, 0, 0, 0, 0, 0, 0).ToString();

        Assert.DoesNotContain("37", text);
        Assert.DoesNotContain("Values", text);
        Assert.Contains("hidden", text);
    }

    [Fact]
    public void The_tendency_is_a_separate_type_with_no_link_to_the_ratings_or_the_contact_skill()
    {
        Assert.DoesNotContain(typeof(BatterZoneTendency).GetProperties(), p => p.PropertyType.Namespace == "StarPlayer.Domain.Players");
        Assert.DoesNotContain(typeof(BatterZoneTendency).GetMethods(), m => m.GetParameters().Any(p => p.ParameterType.Namespace == "StarPlayer.Domain.Players"));
    }
}
