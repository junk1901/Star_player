using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Tests.Randomness;

public class Pcg32Tests
{
    // Published by the PCG author (pcg-random.org, "Using PCG (C Basic)", pcg32-demo round 1; seed 42, sequence 54). Typed here by hand
    // from that page, not copied from this implementation.
    private static readonly uint[] PublishedU32 = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e];
    private static readonly int[] PublishedRolls = [3, 4, 1, 1, 2, 2, 3, 2, 4, 3, 2, 4, 3, 3, 5, 2, 3, 1, 3, 1, 5, 1, 4, 1, 5, 6, 4, 6, 6, 2, 6, 3, 3];

    [Fact]
    public void Matches_the_published_pcg32_reference_output()
    {
        var rng = new Pcg32(42, 54);
        Assert.Equal(PublishedU32, Enumerable.Range(0, 6).Select(_ => rng.NextUInt32()).ToArray());
    }

    [Fact]
    public void Bounded_rolls_match_the_published_reference_output()
    {
        // The demo draws 6 words and 65 coin flips (bound 2, one draw each) before the 33 dice rolls: 71 raw draws.
        var rng = new Pcg32(42, 54);
        for (int i = 0; i < 71; i++)
            rng.NextUInt32();
        Assert.Equal(PublishedRolls, Enumerable.Range(0, 33).Select(_ => (int)rng.NextBounded(6) + 1).ToArray());
    }

    [Fact]
    public void Matches_independent_python_seed_vectors_including_extreme_seeds()
    {
        foreach (var v in RngFixture.Golden.GetProperty("seed_vectors").EnumerateArray())
        {
            var rng = new Pcg32(RngFixture.Hex64(v.GetProperty("initstate")), RngFixture.Hex64(v.GetProperty("initseq")));
            Assert.Equal(RngFixture.Hex64(v.GetProperty("increment")), rng.Increment);
            Assert.Equal(RngFixture.Hex64(v.GetProperty("state_after_seeding")), rng.State);
            Assert.Equal(v.GetProperty("u32").EnumerateArray().Select(RngFixture.Hex32).ToArray(),
                Enumerable.Range(0, 8).Select(_ => rng.NextUInt32()).ToArray());
        }
    }

    [Fact]
    public void Increment_is_always_odd()
    {
        foreach (ulong seq in new ulong[] { 0, 1, 2, ulong.MaxValue, 0x8000000000000000 })
            Assert.Equal(1UL, new Pcg32(1, seq).Increment & 1UL);
    }

    [Fact]
    public void Same_seed_gives_the_same_sequence_and_different_seeds_differ()
    {
        var a = new Pcg32(7, 9);
        var b = new Pcg32(7, 9);
        var c = new Pcg32(7, 10);
        uint[] sa = Enumerable.Range(0, 100).Select(_ => a.NextUInt32()).ToArray();
        Assert.Equal(sa, Enumerable.Range(0, 100).Select(_ => b.NextUInt32()).ToArray());
        Assert.NotEqual(sa, Enumerable.Range(0, 100).Select(_ => c.NextUInt32()).ToArray());
    }

    [Fact]
    public void Bounded_values_and_draw_counts_match_independent_python_vectors()
    {
        foreach (var v in RngFixture.Golden.GetProperty("bounded_vectors").EnumerateArray())
        {
            var rng = new Pcg32(RngFixture.Hex64(v.GetProperty("initstate")), RngFixture.Hex64(v.GetProperty("initseq")));
            uint bound = v.GetProperty("bound").GetUInt32();
            uint[] values = v.GetProperty("values").EnumerateArray().Select(e => e.GetUInt32()).ToArray();
            long[] draws = v.GetProperty("cumulative_draws").EnumerateArray().Select(e => e.GetInt64()).ToArray();
            for (int i = 0; i < values.Length; i++)
            {
                Assert.Equal(values[i], rng.NextBounded(bound));
                Assert.Equal(draws[i], rng.Draws);
            }
        }
    }

    [Fact]
    public void Rejection_path_is_exercised_and_costs_extra_draws()
    {
        // 0x80000001 and 0xC0000001 reject a large share of raw outputs: the vectors must contain calls that consumed more than one draw.
        foreach (uint bound in new uint[] { 0x80000001, 0xC0000001 })
        {
            var v = RngFixture.Golden.GetProperty("bounded_vectors").EnumerateArray().Single(e => e.GetProperty("bound").GetUInt32() == bound);
            long[] draws = v.GetProperty("cumulative_draws").EnumerateArray().Select(e => e.GetInt64()).ToArray();
            Assert.True(draws[^1] > draws.Length, $"bound {bound:x}: no rejection in the vector, the rejection branch would be untested");
        }
    }

    [Fact]
    public void Bounded_never_returns_the_bound_and_covers_the_edges()
    {
        var rng = new Pcg32(3, 5);
        foreach (uint bound in new uint[] { 1, 2, 3, 255, 256, 257, 0x7FFFFFFF, 0x80000000, 0x80000001, 0xFFFFFFFE, 0xFFFFFFFF })
            for (int i = 0; i < 200; i++)
                Assert.True(rng.NextBounded(bound) < bound);
        var small = new Pcg32(11, 13);
        var seen = new HashSet<uint>(Enumerable.Range(0, 500).Select(_ => small.NextBounded(5)));
        Assert.Equal(new uint[] { 0, 1, 2, 3, 4 }, seen.Order().ToArray());
    }

    [Fact]
    public void Bound_of_one_returns_zero_and_consumes_exactly_one_draw()
    {
        var rng = new Pcg32(1, 2);
        Assert.Equal(0u, rng.NextBounded(1));
        Assert.Equal(1, rng.Draws);
    }

    [Fact]
    public void Bounded_is_not_biased_for_a_non_power_of_two_bound()
    {
        var rng = new Pcg32(2026, 10);
        const int n = 120_000;
        var counts = new int[6];
        for (int i = 0; i < n; i++)
            counts[rng.NextBounded(6)]++;
        // Fixed seed, so this is deterministic; each face is within 3 % of n/6.
        Assert.All(counts, c => Assert.InRange(c, n / 6 * 97 / 100, n / 6 * 103 / 100));
    }

    [Fact]
    public void Zero_bound_is_rejected()
    {
        var rng = new Pcg32(1, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextBounded(0));
        Assert.Equal(0, rng.Draws);
    }

    [Fact]
    public void Draw_count_counts_raw_outputs_and_not_the_seeding_steps()
    {
        var rng = new Pcg32(1, 2);
        Assert.Equal(0, rng.Draws);
        rng.NextUInt32();
        rng.NextUInt32();
        Assert.Equal(2, rng.Draws);
    }

    [Fact]
    public void Advance_matches_stepping_and_the_python_vectors()
    {
        foreach (var v in RngFixture.Golden.GetProperty("advance_vectors").EnumerateArray())
        {
            var rng = new Pcg32(RngFixture.Hex64(v.GetProperty("initstate")), RngFixture.Hex64(v.GetProperty("initseq")));
            ulong delta = v.GetProperty("delta").GetUInt64();
            rng.Advance(delta);
            Assert.Equal(RngFixture.Hex64(v.GetProperty("state")), rng.State);
            Assert.Equal((long)delta, rng.Draws);
            Assert.Equal(RngFixture.Hex32(v.GetProperty("next_u32")), rng.NextUInt32());
        }
    }

    [Fact]
    public void FromState_rebuilds_the_exact_position_and_rejects_bad_fields()
    {
        var rng = new Pcg32(5, 6);
        for (int i = 0; i < 10; i++)
            rng.NextUInt32();
        var rebuilt = Pcg32.FromState(rng.State, rng.Increment, rng.Draws).Value;
        for (int i = 0; i < 20; i++)
            Assert.Equal(rng.NextUInt32(), rebuilt.NextUInt32());

        Assert.Equal("Rng.increment_even", Pcg32.FromState(1, 2, 0).Error!.Value.Code);
        Assert.Equal("Rng.draws_negative", Pcg32.FromState(1, 3, -1).Error!.Value.Code);
    }
}
