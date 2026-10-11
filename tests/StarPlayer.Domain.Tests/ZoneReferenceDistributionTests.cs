using System.Reflection;
using StarPlayer.Domain.Baseball.PitchZone;

namespace StarPlayer.Domain.Tests;

/// <summary>
/// Phase 3-B1 fix round (Codex High): a reference id and its weights are one inseparable definition. <c>pzl-ref-uniform-v1</c> means exactly nine ones,
/// so the same stored <c>ReferenceId</c> can never have judged a vector under different weights. A new reference needs a new id and a fixed registered definition.
/// </summary>
public class ZoneReferenceDistributionTests
{
    private static readonly int[] UniformWeights = [1, 1, 1, 1, 1, 1, 1, 1, 1];

    [Fact]
    public void The_codex_counterexample_a_reused_uniform_id_with_other_weights_is_rejected_at_creation()
    {
        var forged = ZoneReferenceDistribution.Create("pzl-ref-uniform-v1", [3, 1, 1, 1, 1, 1, 1, 1, 1]);

        Assert.False(forged.IsSuccess);
        Assert.Equal("ZoneReferenceDistribution.weights_mismatch", forged.Error!.Value.Code);
    }

    [Fact]
    public void The_counterexample_tendency_is_rejected_against_the_real_uniform_reference_because_its_weighted_sum_is_minus_two()
    {
        // 1*1 + (-3)*1 = -2, not 0.
        var made = BatterZoneTendency.Create([1, -3, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1);

        Assert.Equal("BatterZoneTendency.not_average_neutral", made.Error!.Value.Code);
    }

    [Fact]
    public void The_registered_uniform_reference_comes_from_its_id_and_from_its_id_with_its_exact_weights_as_the_same_definition()
    {
        var byCreate = ZoneReferenceDistribution.Create("pzl-ref-uniform-v1", UniformWeights);
        var byResolve = ZoneReferenceDistribution.Resolve("pzl-ref-uniform-v1");

        Assert.True(byCreate.IsSuccess);
        Assert.True(byResolve.IsSuccess);
        Assert.Equal(UniformWeights, byCreate.Value.Weights);
        Assert.Equal(byCreate.Value.Weights, byResolve.Value.Weights);
        Assert.Same(ZoneReferenceDistribution.UniformV1, byResolve.Value);
        Assert.Equal(UniformWeights, ZoneReferenceDistribution.UniformV1.Weights);
    }

    [Fact]
    public void A_balanced_vector_and_the_all_zero_vector_are_accepted_and_the_counterexample_vector_is_not()
    {
        Assert.True(BatterZoneTendency.Create([1, -1, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).IsSuccess);
        Assert.True(BatterZoneTendency.Create(new int[9], ZoneReferenceDistribution.UniformV1).IsSuccess);
        Assert.False(BatterZoneTendency.Create([1, -3, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).IsSuccess);
    }

    [Theory]
    [InlineData("pzl-ref-other-v1")]
    [InlineData("pzl-ref-uniform-v2")]
    [InlineData("PZL-REF-UNIFORM-V1")]
    [InlineData("pzl-ref-uniform-v1 ")]
    [InlineData("r")]
    [InlineData("")]
    public void An_unknown_reference_id_is_rejected_by_both_entry_points_and_never_replaced_by_a_default(string id)
    {
        Assert.False(ZoneReferenceDistribution.Create(id, UniformWeights).IsSuccess);
        Assert.False(ZoneReferenceDistribution.Resolve(id).IsSuccess);
        Assert.False(ZoneReferenceDistribution.Resolve(null).IsSuccess);
    }

    [Fact]
    public void One_reference_id_always_means_one_set_of_weights_whatever_route_built_it()
    {
        foreach (var weights in new[] { new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2 }, new[] { 1, 1, 1, 1, 1, 1, 1, 1, 0 }, new[] { 0, 0, 0, 0, 0, 0, 0, 0, 1 } })
            Assert.False(ZoneReferenceDistribution.Create("pzl-ref-uniform-v1", weights).IsSuccess);

        var publicRoutes = new[]
        {
            ZoneReferenceDistribution.UniformV1,
            ZoneReferenceDistribution.Resolve("pzl-ref-uniform-v1").Value,
            ZoneReferenceDistribution.Create("pzl-ref-uniform-v1", UniformWeights).Value,
        };
        Assert.All(publicRoutes, r => Assert.Equal(UniformWeights, r.Weights));
    }

    [Fact]
    public void The_public_surface_has_no_way_to_build_an_unregistered_reference()
    {
        var publicStatics = typeof(ZoneReferenceDistribution).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["Create", "Resolve", "get_UniformV1"], publicStatics);
        Assert.Empty(typeof(ZoneReferenceDistribution).GetConstructors());
        Assert.False(ZoneReferenceDistribution.Create("pzl-ref-test-weighted", [3, 1, 0, 0, 0, 0, 0, 0, 0]).IsSuccess);
    }

    [Fact]
    public void A_reference_cannot_be_changed_from_outside_neither_through_its_weights_nor_through_the_input_array()
    {
        var input = (int[])UniformWeights.Clone();
        var made = ZoneReferenceDistribution.Create("pzl-ref-uniform-v1", input).Value;
        input[0] = 50;

        Assert.Equal(1, made.Weights[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)made.Weights)[0] = 50);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)made.Weights).Add(1));
        Assert.Equal(UniformWeights, ZoneReferenceDistribution.UniformV1.Weights);
        Assert.DoesNotContain(typeof(ZoneReferenceDistribution).GetProperties(), p => p.SetMethod is { IsPublic: true });
    }

    [Fact]
    public void A_tendency_copies_its_input_array_and_the_caller_cannot_change_it_afterwards()
    {
        var input = new[] { 1, -1, 0, 0, 0, 0, 0, 0, 0 };
        var tendency = BatterZoneTendency.Create(input, ZoneReferenceDistribution.UniformV1).Value;
        input[0] = 100;
        input[1] = 100;

        Assert.Equal([1, -1, 0, 0, 0, 0, 0, 0, 0], tendency.Values);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)tendency.Values)[0] = 100);
    }

    [Fact]
    public void The_weighted_sum_cannot_overflow_even_at_the_largest_weights_and_values()
    {
        var maxWeights = Enumerable.Repeat(ZoneReferenceDistribution.MaxWeight, 9).ToArray();
        var reference = ZoneReferenceDistribution.CreateUnregisteredForTest("pzl-ref-test-max", maxWeights).Value;

        Assert.True(BatterZoneTendency.Create([100, 100, 100, 100, -100, -100, -100, -100, 0], reference).IsSuccess);
        Assert.False(BatterZoneTendency.Create([100, 100, 100, 100, 100, 100, 100, 100, 100], reference).IsSuccess);
        Assert.Equal("BatterZoneTendency.value_out_of_range", BatterZoneTendency.Create(Enumerable.Repeat(int.MaxValue, 9), reference).Error!.Value.Code);
        // The sum is computed in a long and the inputs are range-checked first: 9 * 100 * 1_000_000 = 9e8, far below long.MaxValue.
        Assert.True(9L * BatterZoneTendency.MaxMagnitude * ZoneReferenceDistribution.MaxWeight < long.MaxValue / 1000);
    }

    [Fact]
    public void A_stored_reference_id_always_resolves_to_the_weights_that_judged_the_vector()
    {
        var tendency = BatterZoneTendency.Create([7, -7, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).Value;
        var reference = ZoneReferenceDistribution.Resolve(tendency.ReferenceId).Value;

        long sum = 0;
        for (var i = 0; i < 9; i++)
            sum += (long)tendency.Values[i] * reference.Weights[i];
        Assert.Equal(0, sum);
    }
}
