using System.Globalization;
using System.Text.Json;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E (Game Calling v4, GC-S2), unit level: <c>CommitEvidence.CheckLink</c> is asked DIRECTLY (step 7b of the commit), so every guard of the actual-location link and of the roll replay is tested on its own and
/// not through the later RNG check of step 10b, which overlaps with some of them. A guard that is removed must make one of these tests fail.
/// </summary>
public class GameCallingLinkTests
{
    private const string Link = "Commit.candidate_link_mismatch";
    private const string Rng = "Commit.rng_evidence_mismatch";

    private static string[] Modes => ["automated", "non_automated"];

    private static readonly Lazy<JsonElement> Reference = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tests", "Fixtures", "phase3e_game_calling_rng_reference.json"))).RootElement);

    private static (AuthoritativeGame G, PreparedPitch P, NonInPlayPitchCandidate C, Gc.World W) Honest(string mode, Func<PitchBranchDraft, bool>? want = null, ulong first = 1, bool take = false, int control = 50)
    {
        var w = Gc.Game(RulesFor(mode), control: control, approach: !take);
        var (g, p, c) = Gc.FindNonInPlay(w, want ?? (_ => true), control, first, take: take);
        return (g, p, c, w);
    }

    /// <summary>Step 7b only: the evidence of this call asked whether the candidate is linked to its source.</summary>
    private static Error? CheckLink(AuthoritativeGame g, PreparedPitch prepared, NonInPlayPitchCandidate candidate, PlayerInputSet? set, int control = 50)
    {
        var (b, p) = Gc.SheetsOf(g.State, control);
        return CommitEvidence.ForNonInPlay(g, prepared, candidate, Gc.T4, b, p, set).CheckLink(g, candidate.Event);
    }

    private static PreparedPitch Forge(PreparedPitch p, Func<PitchBranchDraft, PitchBranchDraft> edit) => p.With(edit(p.Draft));

    // ---------------------------------------------------------------- the honest link

    [Fact]
    public void An_honest_candidate_links_in_every_mode_and_asking_twice_changes_nothing()
    {
        foreach (var mode in Modes)
        {
            var (g, p, c, w) = Honest(mode, d => d.Kind != PitchBranchKind.InPlay);
            var before = Gc.Basis.Of(g);

            Assert.Null(CheckLink(g, p, c, w.Set));
            Assert.Null(CheckLink(g, p, c, w.Set));                                                   // the replay is repeatable: nothing was consumed
            Assert.Same(before.Rng, g.Rng);
            Assert.Equal(before.RngHash, g.Rng.StateHash);
            Assert.Same(p.Draft.NextRng, c.NextRng);                                                  // the candidate's position is still the resolver's H1 instance
        }
    }

    [Fact]
    public void An_honest_in_play_candidate_links_base_and_resolved()
    {
        foreach (var mode in Modes)
        {
            var w = Gc.Game(RulesFor(mode));
            var (g, p, c) = Gc.FindInPlay(w, resolved: false);
            var (b, pitcher) = Gc.SheetsOf(g.State);
            Assert.Null(CommitEvidence.ForInPlay(g, c, Gc.T4, b, pitcher, w.Set).CheckLink(g, c.Event));

            var wr = Gc.Game(RulesFor(mode), approach: true, policy: AdvancementPolicy.VersionV2, varied: true, occupancy: 1);
            var (g2, _, resolved) = Gc.FindInPlay(wr, resolved: true);
            var (b2, pitcher2) = Gc.SheetsOf(g2.State);
            Assert.Null(CommitEvidence.ForInPlay(g2, resolved, Gc.T4, b2, pitcher2, wr.Set).CheckLink(g2, resolved.Event));
            Assert.NotNull(p);
        }
    }

    // ---------------------------------------------------------------- the roll guard (roll alone)

    [Fact]
    public void The_roll_guard_alone_refuses_a_roll_that_stays_inside_its_outcome_interval_on_every_replayed_step()
    {
        foreach (var mode in Modes)
        {
            var (g, p, c, w) = Honest(mode, d => d.Kind != PitchBranchKind.InPlay && d.Steps.Take(3).All(s => Gc.SameIntervalRoll(s) is not null));
            for (var i = 0; i < 3; i++)
            {
                var forgedRoll = Gc.SameIntervalRoll(p.Draft.Steps[i])!.Value;
                var forged = Forge(p, d => d.WithStep(i, s => s with { Roll = forgedRoll }));
                var error = CheckLink(g, forged, c, w.Set);

                Assert.Equal(Rng, error?.Code);                                                       // found at step 7b by the replay, before the later RNG check could ever run
                Assert.Same(p.Draft.NextRng, forged.Draft.NextRng);
                Assert.Equal(p.Draft.Steps[i].Outcome, forged.Draft.Steps[i].Outcome);
            }
        }
    }

    // ---------------------------------------------------------------- the raw-draw guard

    [Fact]
    public void The_raw_draw_guard_alone_refuses_a_raw_draw_count_the_generator_did_not_use()
    {
        foreach (var mode in Modes)
        {
            var (g, p, c, w) = Honest(mode, d => d.Kind != PitchBranchKind.InPlay);
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                Assert.Equal(Rng, CheckLink(g, Forge(p, d => d.WithStep(index, s => s with { RawDraws = s.RawDraws + 1 })), c, w.Set)?.Code);
            }
        }
    }

    [Fact]
    public void Swapping_the_raw_draws_of_two_steps_on_one_stream_keeps_the_streams_total_yet_the_replay_refuses_it()
    {
        // seeds where the batter decision and the called pitch of an explicit Take (both on game/pitch@1) consumed different numbers of raw outputs (one of them was rejected)
        var seeds = Reference.Value.GetProperty("pitch_stream_swaps").EnumerateArray().Select(e => ulong.Parse(e.GetProperty("seed").GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToList();
        var exercised = 0;
        var w = Gc.Game(NonAutomated, approach: false);
        foreach (var seed in seeds)
        {
            var g = Aggregate(w.Driver, seed);
            var p = Gc.Prepare(g, w.Set, take: true);
            var decision = p.Draft.Steps.FirstOrDefault(s => s.Stage == StageIds.BatterDecision);
            var called = p.Draft.Steps.FirstOrDefault(s => s.Stage == StageIds.CalledPitch);
            if (decision is null || called is null || p.Draft.Kind == PitchBranchKind.InPlay || decision.RawDraws == called.RawDraws)
                continue;
            var built = Gc.BuildNonInPlay(g, p, w.Set);
            if (!built.IsSuccess)
                continue;

            var (di, ci) = (p.Draft.Steps.ToList().IndexOf(decision), p.Draft.Steps.ToList().IndexOf(called));
            var swapped = Forge(p, d => d.WithStep(di, s => s with { RawDraws = called.RawDraws }).WithStep(ci, s => s with { RawDraws = decision.RawDraws }));
            // the usage record stays correct (the sums did not change), the position is H1: only the per-step replay can see that the numbers belong to the other step
            Assert.Equal(p.Draft.StreamUsage.Select(u => u.RawDraws), swapped.Draft.StreamUsage.Select(u => u.RawDraws));
            Assert.Null(CheckLink(g, p, built.Value, w.Set));
            Assert.Equal(Rng, CheckLink(g, swapped, built.Value, w.Set)?.Code);
            exercised++;
        }

        Assert.True(exercised >= 2, "at least two reference seeds with a rejected draw on the shared pitch stream must be exercised");
    }

    // ---------------------------------------------------------------- the position guard

    [Fact]
    public void The_position_guard_alone_refuses_an_h1_that_is_not_where_the_replay_of_the_recorded_steps_ends()
    {
        foreach (var mode in Modes)
        {
            var (g, p, c, w) = Honest(mode, d => d.Kind != PitchBranchKind.InPlay);
            var other = Advance(p.Draft.NextRng, (GameRngStreams.Ai, 1));
            var forged = p.With(p.Draft with { NextRng = other });
            var forgedCandidate = c.WithRng(other);

            Assert.Equal(Rng, CheckLink(g, forged, forgedCandidate, w.Set)?.Code);
            // and a position one raw draw SHORT of the replay on a stream the pitch used
            var used = p.Draft.StreamUsage[0];
            var short_ = Advance(g.Rng, Usage(p.Draft).Select((u, i) => i == 0 ? (u.Stream, u.Raw - 1) : u).ToArray());
            Assert.Equal(Rng, CheckLink(g, p.With(p.Draft with { NextRng = short_ }), c.WithRng(short_), w.Set)?.Code);
            Assert.Equal(used.Stream, p.Draft.StreamUsage[0].Stream);
        }
    }

    // ---------------------------------------------------------------- the expectation is the real state and the pinned set, never what the preparation says

    [Fact]
    public void A_prepared_pitch_and_a_trace_that_agree_on_a_wrong_control_are_refused_because_the_expectation_is_the_pinned_set()
    {
        foreach (var mode in Modes)
        {
            var strong = Gc.Game(RulesFor(mode), control: 80);
            var weak = Gc.Game(RulesFor(mode), control: 50);
            ulong seed = 1;
            for (; seed < 400; seed++)
            {
                var a = Gc.Prepare(Aggregate(weak.Driver, seed), weak.Set, 50).Draft;
                var b = Gc.Prepare(Aggregate(strong.Driver, seed), strong.Set, 80).Draft;
                if (a.Kind != PitchBranchKind.InPlay && Gc.Location(a).Outcome == Gc.Location(b).Outcome && !Gc.Location(a).Trace.Final.SequenceEqual(Gc.Location(b).Trace.Final))
                    break;
            }

            var g = Aggregate(weak.Driver, seed);
            var p = Gc.Prepare(g, weak.Set, 50);
            var c = Gc.BuildNonInPlay(g, p, weak.Set).Value;
            var donor = Gc.Location(Gc.Prepare(Aggregate(strong.Driver, seed), strong.Set, 80).Draft);
            var real = p.PitcherInput!;

            // everything the preparation and the trace record says "Control 80" ...
            var lie = Forge(p, d => d.WithStep(1, s => s with { Trace = donor.Trace })).WithPitcher(new FrozenPitcherInput(real.GameId, real.InputSetId, real.Pitcher, 80));
            // ... and the link, which looks at the real state and the pinned set (Control 50), still refuses
            Assert.Equal(Link, CheckLink(g, lie, c, weak.Set, 50)?.Code);
            Assert.Null(CheckLink(g, p, c, weak.Set, 50));
        }
    }

    [Fact]
    public void A_prepared_pitch_and_a_trace_that_agree_on_a_wrong_game_calling_are_refused_the_same_way()
    {
        foreach (var mode in Modes)
        {
            var high = Gc.Game(RulesFor(mode), calling: 100);
            var real = Gc.Game(RulesFor(mode), calling: 50);
            ulong seed = 1;
            for (; seed < 400; seed++)
            {
                var a = Gc.Prepare(Aggregate(real.Driver, seed), real.Set).Draft;
                var b = Gc.Prepare(Aggregate(high.Driver, seed), high.Set).Draft;
                if (a.Kind != PitchBranchKind.InPlay && Gc.AimedClass(a) == StarPlayer.Domain.Baseball.PitchZone.BoundaryClass.Borderline && Gc.Location(a).Outcome == Gc.Location(b).Outcome)
                    break;
            }

            var g = Aggregate(real.Driver, seed);
            var p = Gc.Prepare(g, real.Set);
            var c = Gc.BuildNonInPlay(g, p, real.Set).Value;
            var donor = Gc.Location(Gc.Prepare(Aggregate(high.Driver, seed), high.Set).Draft);
            var frozen = p.Catcher!;
            var lie = Forge(p, d => d.WithStep(1, s => s with { Trace = donor.Trace })).WithCatcher(new FrozenCatcherInput(frozen.GameId, frozen.InputSetId, frozen.Catcher, frozen.ReceivingFraming, 100));

            Assert.Equal(Link, CheckLink(g, lie, c, real.Set)?.Code);
            Assert.Null(CheckLink(g, p, c, real.Set));
        }
    }

    [Fact]
    public void The_link_needs_the_pinned_set_and_refuses_a_missing_foreign_or_older_format_set()
    {
        var (g, p, c, w) = Honest("non_automated", d => d.Kind != PitchBranchKind.InPlay);
        Assert.Equal(Link, CheckLink(g, p, c, null)?.Code);
        Assert.Equal(Link, CheckLink(g, p, c, Gc.SetV3(_ => new Gc.Spec(50, (51, 51))))?.Code);          // a v3 set, but not the one the game pinned
        Assert.Equal(Link, CheckLink(g, p, c, Fr.SetV2(_ => 50))?.Code);
        Assert.Null(CheckLink(g, p, c, w.Set));
    }

    // ---------------------------------------------------------------- the recomputed rows, one by one

    [Fact]
    public void Each_part_of_the_recorded_location_row_is_compared_with_the_recomputation()
    {
        var (g, p, c, w) = Honest("non_automated", d => d.Kind != PitchBranchKind.InPlay && Gc.AimedClass(d) == StarPlayer.Domain.Baseball.PitchZone.BoundaryClass.Borderline);
        Assert.Null(CheckLink(g, p, c, w.Set));
        Func<PitchBranchStep, PitchBranchStep>[] edits =
        [
            s => s with { Condition = "in.r2c2", Trace = s.Trace with { Condition = "in.r2c2" } },
            s => s with { Trace = s.Trace with { Modifiers = s.Trace.Modifiers.Select(m => m.Slot == "catcher.game_calling" ? m with { Applied = !m.Applied } : m).ToArray() } },
            s => s with { Trace = s.Trace with { Modifiers = s.Trace.Modifiers.Select(m => m.Slot == "pitcher.control" ? m with { Input = "51" } : m).ToArray() } },
            s => s with { Trace = s.Trace with { Modifiers = s.Trace.Modifiers.Take(1).ToArray() } },
            s => s with { Trace = s.Trace with { CombinedFactorPpm = s.Trace.CombinedFactorPpm.Select(f => f + 1).ToArray() } },
            s => s with { Trace = s.Trace with { Final = s.Trace.Final.Select((v, i) => i == 0 ? v + 1 : i == 1 ? v - 1 : v).ToArray() } },
            s => s with { Stream = GameRngStreams.Contact },
            s => s with { Stage = StageIds.CalledPitch },
        ];
        foreach (var edit in edits)
            Assert.Equal(Link, CheckLink(g, Forge(p, d => d.WithStep(1, edit)), c, w.Set)?.Code);
    }
}
