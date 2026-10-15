using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-D-1: the batter's hidden zone tendency is supplied to <c>swing_result</c> and <c>batted_ball</c> for every pitch. Expected shifts are plain integer arithmetic
/// from the ADR-006 item 9 contract (shift = value x cap / 100 ppm of the favourable mass), read off the resolver's own trace of the unshifted distribution; no expectation
/// is computed with <see cref="StageProbability"/>. The independent-oracle golden scenarios (PitchBranchResolverTests) cover the no-tendency path.
/// </summary>
public class PitchBranchTendencyTests
{
    // tuning-virtual-proto-v1 tendency caps (ADR-006 item 9); favourable outcomes per stage
    private const int SwingCap = 60_000;
    private const int BattedCap = 40_000;
    private static readonly string[] SwingFavoured = ["foul", "in_play"];
    private static readonly string[] BattedFavoured = ["ground_hard", "line_hard", "fly_hard"];

    private const int Seeds = 3000;

    private static BatterZoneTendency Vector(params int[] values) => BatterZoneTendency.Create(values, ZoneReferenceDistribution.UniformV1).Value;

    /// <summary>Row-major inner cells r1c1..r3c3; the weighted sum under the uniform reference is 0.</summary>
    private static readonly BatterZoneTendency Mixed = Vector(-60, 10, 20, 30, 40, -50, -40, 50, 0);

    private static readonly BatterZoneTendency Plus = Vector(-100, 0, 0, 0, 100, 0, 0, 0, 0);   // r2c2 +100
    private static readonly BatterZoneTendency Minus = Vector(100, 0, 0, 0, -100, 0, 0, 0, 0);  // r2c2 -100

    private static PitchBranchDraft Draw(int seed, BatterZoneTendency? tendency)
    {
        var game = $"tend-{seed}";
        return Resolved(Request(InSequenceState(Automated, game), Snapshot((ulong)seed, game)) with { BatterTendency = tendency });
    }

    /// <summary>Row-major index of an inner cell ("in.r{row}c{col}"); -1 for an outer zone.</summary>
    private static int InnerIndex(string zone) =>
        zone.StartsWith("in.r", StringComparison.Ordinal) ? (zone[4] - '1') * 3 + (zone[6] - '1') : -1;

    private static string Trace(PitchBranchDraft d) =>
        string.Join("/", d.Steps.Select(s => $"{s.Stage}:{s.Outcome}:{s.Roll}:{string.Join(",", s.Trace.Final)}:{s.Trace.GuardShiftPpm}:{s.Trace.TendencyShiftPpm}"));

    private static int Mass(PitchBranchStep step, IReadOnlyList<int> values, string[] favoured) =>
        step.Trace.Outcomes.Select((o, i) => favoured.Contains(o) ? values[i] : 0).Sum();

    private static bool IsTendencyStage(string stage) => stage is StageIds.SwingResult or StageIds.BattedBall;

    [Fact]
    public void No_tendency_and_the_explicit_neutral_vector_give_the_same_draft_and_the_same_distributions()
    {
        for (var seed = 1; seed <= 600; seed++)
        {
            var none = Draw(seed, null);
            var neutral = Draw(seed, BatterZoneTendency.Neutral);
            Assert.Equal(Summary(none), Summary(neutral));
            Assert.Equal(Trace(none), Trace(neutral));
        }
    }

    [Fact]
    public void A_neutral_tendency_moves_no_probability_in_any_stage()
    {
        var steps = 0;
        for (var seed = 1; seed <= Seeds; seed++)
            foreach (var step in Draw(seed, null).Steps)
            {
                Assert.Equal(0, step.Trace.GuardShiftPpm);
                Assert.Equal(0, step.Trace.TendencyShiftPpm);
                Assert.Equal(step.Trace.AfterModifiers, step.Trace.Final);
                steps++;
            }

        Assert.True(steps > Seeds);
    }

    [Fact]
    public void Swing_result_and_batted_ball_shift_the_favourable_mass_by_exactly_value_times_cap_over_100_and_no_other_stage_moves()
    {
        int swingInner = 0, battedInner = 0, outerSteps = 0, otherSteps = 0;
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var draft = Draw(seed, Mixed);
            var index = InnerIndex(draft.Location.Zone.Value);
            foreach (var step in draft.Steps)
            {
                var t = step.Trace;
                Assert.Equal(0, t.GuardShiftPpm); // the virtual baselines never reach the tendency window edge
                if (!IsTendencyStage(step.Stage) || index < 0)
                {
                    Assert.Equal(0, t.TendencyShiftPpm);
                    Assert.Equal(t.AfterModifiers, t.Final);
                    if (IsTendencyStage(step.Stage)) outerSteps++; else otherSteps++;
                    continue;
                }

                var swing = step.Stage == StageIds.SwingResult;
                var delta = Mixed.Values[index] * ((swing ? SwingCap : BattedCap) / 100);
                var favoured = swing ? SwingFavoured : BattedFavoured;
                Assert.Equal(delta, t.TendencyShiftPpm);
                Assert.Equal(Mass(step, t.AfterModifiers, favoured) + delta, Mass(step, t.Final, favoured));
                Assert.Equal(1_000_000, t.Final.Sum());
                Assert.All(t.Final, p => Assert.True(p > 0)); // no outcome is lost
                if (swing) swingInner++; else battedInner++;
            }
        }

        // the sampled pitches really cover both tendency stages in inner cells, outer cells and the untouched stages
        Assert.True(swingInner > 200 && battedInner > 50 && outerSteps > 50 && otherSteps > Seeds);
    }

    [Fact]
    public void A_tendency_consumes_no_random_number_and_never_changes_a_roll()
    {
        var diverged = 0;
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var neutral = Draw(seed, null);
            var shifted = Draw(seed, Mixed);
            // a step of the same stage is rolled from the same stream position, so its roll is identical; only the outcome picked from the roll may differ
            foreach (var (a, b) in neutral.Steps.Zip(shifted.Steps).TakeWhile(p => p.First.Stage == p.Second.Stage))
            {
                Assert.Equal(a.Stream, b.Stream);
                Assert.Equal(a.Roll, b.Roll);
                Assert.Equal(a.RawDraws, b.RawDraws);
            }

            if (neutral.Steps.Select(s => s.Stage).SequenceEqual(shifted.Steps.Select(s => s.Stage)))
            {
                Assert.Equal(neutral.NextBoundedCalls, shifted.NextBoundedCalls);
                Assert.Equal(neutral.NextRng.Canonical(), shifted.NextRng.Canonical());
            }
            else
                diverged++;
        }

        Assert.True(diverged > 0); // the tendency does change some outcomes (otherwise this test proves nothing)
    }

    [Fact]
    public void The_shift_is_monotonic_in_the_tendency_value_for_the_same_pitch()
    {
        var checkedPitches = 0;
        for (var seed = 1; seed <= Seeds && checkedPitches < 40; seed++)
        {
            var neutral = Draw(seed, null);
            if (neutral.Location.Zone.Value != "in.r2c2" || neutral.Steps.All(s => s.Stage != StageIds.SwingResult))
                continue;
            // the first swing_result is drawn at the same stream position whatever the tendency is, so its three distributions are comparable
            var mass = new[] { Minus, BatterZoneTendency.Neutral, Plus }.Select(v =>
            {
                var step = Draw(seed, v).Steps.First(s => s.Stage == StageIds.SwingResult);
                return Mass(step, step.Trace.Final, SwingFavoured);
            }).ToArray();
            Assert.Equal(mass[1] - SwingCap, mass[0]);
            Assert.Equal(mass[1] + SwingCap, mass[2]);
            checkedPitches++;
        }

        Assert.Equal(40, checkedPitches);
    }

    [Fact]
    public void The_same_request_gives_the_same_draft_every_time()
    {
        for (var seed = 1; seed <= 200; seed++)
            Assert.Equal(Trace(Draw(seed, Mixed)), Trace(Draw(seed, Mixed)));
    }

    [Fact]
    public void The_hidden_vector_is_not_printed_or_stored_by_the_request_or_the_draft()
    {
        var request = Request(InSequenceState(Automated, "tend-hidden"), Snapshot(1, "tend-hidden")) with { BatterTendency = Mixed };
        Assert.Contains("BatterZoneTendency(hidden)", request.ToString());
        Assert.DoesNotContain(typeof(PitchBranchDraft).GetProperties(), p => p.PropertyType == typeof(BatterZoneTendency));
        Assert.DoesNotContain(typeof(PitchBranchStep).GetProperties(), p => p.PropertyType == typeof(BatterZoneTendency));
    }
}
