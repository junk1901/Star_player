using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-B2: the pre-declared validation scenarios of the tuning profile and the link between a distribution and <c>NextBounded</c>. Every scenario here draws
/// ONLY from a throw-away generator that the test creates with the scenario's own fixed seed and context key and a test-only stream name; it never touches a
/// <c>game/*</c> stream, and no production code path draws anything. The tolerance rule was fixed in the data before any scenario was run and is not adjusted by results.
/// </summary>
public class TuningScenarioTests
{
    private static readonly RngStreamId CheckStream = RngStreamId.Create("test/tuning_check", 1).Value;

    private static readonly TuningProfile Profile = TuningProfileTests.Profile();

    private static long[] Observe(ScenarioSpec spec, out long rawDraws)
    {
        var distribution = Profile.Baseline(spec.Stage, spec.Condition).Value;
        var random = GameRandom.Create(spec.Seed, spec.ContextKey).Value;
        var stream = random.Stream(CheckStream);
        var counts = new long[distribution.Count];
        for (var n = 0; n < spec.Samples; n++)
            counts[distribution.SelectIndex(stream.NextBounded(ProbabilityScale.RollBound)).Value]++;
        rawDraws = stream.Draws;
        return counts;
    }

    // The declared rule "binomial-4.5-sigma-ceiling-v1": |observed - expected| <= ceil(4.5 * sqrt(N * p * (1 - p))); an outcome with p = 0 or p = 1 must match exactly.
    private static long Tolerance(int samples, int ppm)
    {
        if (ppm is 0 or ProbabilityScale.Denominator)
            return 0;
        var p = ppm / (double)ProbabilityScale.Denominator;
        return (long)Math.Ceiling(4.5 * Math.Sqrt(samples * p * (1 - p)));
    }

    public static IEnumerable<object[]> Scenarios() => Profile.Scenarios.Select(s => new object[] { s.Id });

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Each_declared_scenario_reproduces_its_baseline_row_within_the_predeclared_tolerance(string id)
    {
        var spec = Profile.Scenarios.Single(s => s.Id == id);
        var distribution = Profile.Baseline(spec.Stage, spec.Condition).Value;

        var counts = Observe(spec, out _);

        Assert.Equal(spec.Samples, counts.Sum());
        for (var i = 0; i < counts.Length; i++)
        {
            var expected = spec.Samples * (double)distribution.Ppm[i] / ProbabilityScale.Denominator;
            var tolerance = Tolerance(spec.Samples, distribution.Ppm[i]);
            Assert.True(Math.Abs(counts[i] - expected) <= tolerance, $"{spec.Id}/{distribution.Outcomes[i]}: observed {counts[i]}, expected {expected:F1} ± {tolerance}");
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void A_scenario_is_exactly_reproducible_from_its_seed(string id)
    {
        var spec = Profile.Scenarios.Single(s => s.Id == id);
        var first = Observe(spec, out var draws1);
        var second = Observe(spec, out var draws2);
        Assert.Equal(first, second);
        Assert.Equal(draws1, draws2);
    }

    [Fact]
    public void A_zero_probability_outcome_is_never_observed_and_a_certain_one_always_is()
    {
        var spec = Profile.Scenarios.Single(s => s.Id == "s4_called_pitch_nonauto_border");
        var certain = Profile.Baseline(StageIds.CalledPitch, "automated:in.r1c2").Value;
        var random = GameRandom.Create(spec.Seed, spec.ContextKey).Value;
        var stream = random.Stream(CheckStream);
        for (var n = 0; n < 50_000; n++)
            Assert.Equal("strike", certain.Select(stream.NextBounded(ProbabilityScale.RollBound)).Value);
    }

    [Fact]
    public void The_scenario_contexts_and_seeds_are_distinct_so_no_two_scenarios_share_a_sequence()
    {
        Assert.Equal(Profile.Scenarios.Count, Profile.Scenarios.Select(s => (s.Seed, s.ContextKey)).Distinct().Count());
        Assert.Equal(Profile.Scenarios.Count, Profile.Scenarios.Select(s => s.Seed).Distinct().Count());
    }

    [Fact]
    public void The_roll_contract_is_one_NextBounded_call_per_decision_and_its_raw_draw_overhead_is_small_and_known()
    {
        // NextBounded rejects raw outputs below (2^32 - bound) % bound. For bound 1,000,000 that threshold is 967,296, i.e. about 0.0225% of raw outputs are skipped.
        // A decision is therefore ONE NextBounded call, but its stream cursor can advance by more than one raw draw. A later package (3-C) must count decisions
        // by calls and treat Draws as the audit value, never assume Draws == decisions.
        Assert.Equal(967_296u, unchecked(0u - ProbabilityScale.RollBound) % ProbabilityScale.RollBound);

        var spec = Profile.Scenarios[0];
        var random = GameRandom.Create(spec.Seed, spec.ContextKey).Value;
        var stream = random.Stream(CheckStream);
        const int calls = 200_000;
        for (var n = 0; n < calls; n++)
            Assert.True(stream.NextBounded(ProbabilityScale.RollBound) < ProbabilityScale.RollBound);
        Assert.True(stream.Draws >= calls);
        Assert.True(stream.Draws - calls < 300, $"raw draws {stream.Draws} for {calls} calls");
    }

    [Fact]
    public void Computing_and_selecting_probabilities_consumes_no_gameplay_randomness()
    {
        var random = GameRandom.Create(20261011, "game:probe:consumption").Value;
        foreach (var stream in new[] { GameRngStreams.Pitch, GameRngStreams.Contact, GameRngStreams.Field, GameRngStreams.Runner, GameRngStreams.Location, GameRngStreams.Ai })
            random.Stream(stream);
        var before = random.Snapshot().StateHash;

        foreach (var stage in ProbabilityModel.V1.Stages)
            foreach (var condition in stage.Conditions)
            {
                var result = StageProbability.Compute(Profile, stage.Id, condition.Key).Value;
                foreach (var roll in new uint[] { 0, 1, 499_999, 999_999 })
                    Assert.True(result.Distribution.Select(roll).IsSuccess);
            }

        Assert.Equal(before, random.Snapshot().StateHash);
        Assert.Empty(random.Snapshot().Streams);
        foreach (var stream in new[] { GameRngStreams.Pitch, GameRngStreams.Location })
            Assert.Equal(0, random.Stream(stream).Draws);
    }

    [Fact]
    public void The_roll_boundaries_of_a_computed_distribution_follow_the_cumulative_ppm_exactly()
    {
        var result = StageProbability.Compute(Profile, StageIds.DefenseResult, "fly_hard").Value.Distribution;
        var cumulative = 0;
        for (var i = 0; i < result.Count; i++)
        {
            if (result.Ppm[i] == 0)
                continue;
            Assert.Equal(result.Outcomes[i], result.Select((uint)cumulative).Value);
            Assert.Equal(result.Outcomes[i], result.Select((uint)(cumulative + result.Ppm[i] - 1)).Value);
            cumulative += result.Ppm[i];
        }

        Assert.Equal(ProbabilityScale.Denominator, cumulative);
    }
}
