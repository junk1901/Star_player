using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E (Game Calling v4, GC-B): the commit side. <c>CommitEvidence</c> re-derives the pitcher's Control and the catcher's GameCalling from the REAL state and the pinned set, recomputes the target and
/// location rows, and (GC-S2) replays the draft's draws on a private copy of H0 - so a recorded roll that is not what the generator produced is refused even when it selects the same outcome and
/// every other number is untouched. Every refusal below happens in <c>PitchCommitGate.Stage</c>, i.e. BEFORE the cursor's compare-and-swap, and leaves the aggregate, revision, RNG and last commit as they were.
/// </summary>
public class GameCallingCommitTests
{
    private const string Link = "Commit.candidate_link_mismatch";
    private const string Rng = "Commit.rng_evidence_mismatch";

    private static string[] Modes => ["automated", "non_automated"];

    private static readonly Lazy<JsonElement> Reference = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tests", "Fixtures", "phase3e_game_calling_rng_reference.json"))).RootElement);

    private static (AuthoritativeGame G, PreparedPitch P, NonInPlayPitchCandidate C, Gc.World W) Pitch(string mode, Func<PitchBranchDraft, bool>? want = null, int control = 50, int calling = 50, ulong first = 1)
    {
        var w = Gc.Game(RulesFor(mode), control: control, calling: calling, framing: 50);
        var (g, p, c) = Gc.FindNonInPlay(w, want ?? (_ => true), control, first);
        return (g, p, c, w);
    }

    /// <summary>Stages the attempt on a fresh cursor and proves the refusal code and that nothing shared changed.</summary>
    private static void Refused(AuthoritativeGame g, string code, Func<Result<StagedCommit>> attempt, string label = "")
    {
        var cursor = new GameCursor(g);
        var before = Gc.Basis.Of(cursor);
        var result = attempt();
        Assert.False(result.IsSuccess, $"the forged candidate was staged ({label})");
        Assert.True(code == result.Error!.Value.Code, $"{label}: expected {code}, got {result.Error!.Value.Code} {result.Error.Value.Message}");
        Gc.AssertUnchanged(before, cursor);
    }

    /// <summary>The same in-play candidate whose plan was made from another (forged) preparation; the internal constructors are the only way, as for the other forgeries.</summary>
    private static InPlayPitchCandidate WithPrepared(InPlayPitchCandidate c, PreparedPitch prepared) =>
        new(new BaseInPlayPlan(prepared, c.Plan.Facts), c.Event, c.UnconfirmedStep, c.NextRng, c.Audit);

    private static PreparedPitch Forge(PreparedPitch p, Func<PitchBranchDraft, PitchBranchDraft> edit) => p.With(edit(p.Draft));

    // ---------------------------------------------------------------- the honest path

    [Theory]
    [InlineData("automated")]
    [InlineData("non_automated")]
    public void A_real_v4_pitch_is_staged_published_and_adopts_exactly_the_position_the_resolver_ended_at(string mode)
    {
        var (g, p, c, w) = Pitch(mode, d => d.Kind != PitchBranchKind.InPlay, control: 73, calling: 29);
        var cursor = new GameCursor(g);

        var staged = Gc.Stage(g, p, c, w.Set, 73);
        Assert.True(staged.IsSuccess, staged.Error?.Message);
        Assert.Same(g, cursor.Current);                                                       // staging publishes nothing

        Assert.Same(c.NextRng, staged.Value.Next.Rng);                                        // the adopted position IS the candidate's, never a new draw
        Assert.Equal(p.Draft.NextRng.StateHash, staged.Value.Next.Rng.StateHash);
        Assert.Equal(g.Revision + 1, staged.Value.Next.Revision);
        var published = cursor.Publish(staged.Value);
        Assert.True(published.IsSuccess);
        Assert.Same(staged.Value.Next, cursor.Current);
        Assert.Equal(EventFingerprint.VersionV2, cursor.Current.State.AppliedEvents[^1].FingerprintVersion);
    }

    [Fact]
    public void Verification_adds_no_game_draw_the_adopted_streams_grew_by_exactly_the_recorded_raw_draws_and_nothing_else_moved()
    {
        foreach (var mode in Modes)
        {
            var (g, p, c, w) = Pitch(mode, d => d.Kind != PitchBranchKind.InPlay, first: 3);
            var h0 = g.Rng;
            var h0Hash = h0.StateHash;
            var staged = Gc.Stage(g, p, c, w.Set).Value;

            var before = g.Rng.Streams.ToDictionary(s => s.Stream.Canonical, s => s.Draws);
            var after = staged.Next.Rng.Streams.ToDictionary(s => s.Stream.Canonical, s => s.Draws);
            var used = p.Draft.StreamUsage.ToDictionary(u => u.Stream.Canonical, u => u.RawDraws);
            foreach (var stream in after.Keys.Concat(before.Keys).Concat(used.Keys).Distinct())
                Assert.Equal(used.GetValueOrDefault(stream), after.GetValueOrDefault(stream) - before.GetValueOrDefault(stream));
            // the streams a pitch uses are the existing ones; no stream for verification exists
            Assert.All(after.Keys, k => Assert.Contains(k, new[] { "game/target@1", "game/location@1", "game/pitch@1", "game/contact@1", "game/field@1" }));
            Assert.True(used.ContainsKey("game/target@1") && used.ContainsKey("game/location@1"));
            // H0 is untouched by the verification replay (the replay works on a private copy): the very same instance, the same hash, the same per-stream draws
            Assert.Same(h0, g.Rng);
            Assert.Equal(h0Hash, g.Rng.StateHash);
            Assert.Equal(p.StartRngStateHash, g.Rng.StateHash);
            Assert.Equal(before, g.Rng.Streams.ToDictionary(s => s.Stream.Canonical, s => s.Draws));
            Assert.Equal(g.Rng.StateHash, GameRandom.Restore(g.Rng).Value.Snapshot().StateHash);
        }
    }

    [Fact]
    public void Staging_the_same_candidate_twice_gives_the_same_result_and_the_second_publish_loses_the_compare_and_swap()
    {
        var (g, p, c, w) = Pitch("non_automated", d => d.Kind != PitchBranchKind.InPlay);
        var cursor = new GameCursor(g);
        var first = Gc.Stage(g, p, c, w.Set).Value;
        var second = Gc.Stage(g, p, c, w.Set).Value;                                           // verifying twice changes nothing: staging is pure

        Assert.Equal(first.Record.Receipt, second.Record.Receipt);
        Assert.Equal(first.Next.Rng.StateHash, second.Next.Rng.StateHash);
        Assert.True(cursor.Publish(first).IsSuccess);
        var lost = cursor.Publish(second);
        Assert.False(lost.IsSuccess);
        Assert.Equal("Commit.revision_stale", lost.Error!.Value.Code);
        Assert.Same(first.Next, cursor.Current);                                              // exactly one commit won; the loser changed nothing
    }

    // ---------------------------------------------------------------- the independent RNG reference (shares no code with production)

    [Fact]
    public void The_target_and_location_rolls_and_raw_draws_equal_the_independent_pcg32_reference_from_every_recorded_start_position()
    {
        var checkedRecords = 0;
        var multiDraw = 0;
        var w = Gc.Game(NonAutomated);
        foreach (var rec in Reference.Value.GetProperty("records").EnumerateArray())
        {
            var seed = ulong.Parse(rec.GetProperty("seed").GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var prior = new Dictionary<string, int>
            {
                ["game/target@1"] = rec.GetProperty("prior_target_calls").GetInt32(),
                ["game/location@1"] = rec.GetProperty("prior_location_calls").GetInt32(),
            }.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
            var g = Aggregate(w.Driver.State, w.Driver.LastIssued, seed, prior);
            var draft = Gc.Prepare(g, w.Set).Draft;

            var (target, location) = (Gc.Target(draft), Gc.Location(draft));
            var wantTarget = rec.GetProperty("target")[0];
            var wantLocation = rec.GetProperty("location")[0];
            Assert.Equal((wantTarget.GetProperty("roll").GetUInt32(), wantTarget.GetProperty("raw").GetInt64()), (target.Roll, target.RawDraws));
            Assert.Equal((wantLocation.GetProperty("roll").GetUInt32(), wantLocation.GetProperty("raw").GetInt64()), (location.Roll, location.RawDraws));
            if (target.RawDraws > 1 || location.RawDraws > 1)
                multiDraw++;
            checkedRecords++;
        }

        Assert.True(checkedRecords >= 400);
        Assert.True(multiDraw >= 2, "the reference must contain genuine rejected draws (raw draws > 1)");
    }

    /// <summary>
    /// SCOPE (Codex final review F-02). The independent PCG32 reference holds <c>records</c> start positions; the test above compares the resolver's target/location roll and raw draws with ALL of them. This
    /// test is narrower: it runs the commit path only for the positions the filter <c>prior_target_calls &lt;= 1</c> keeps (<c>eligible</c>), and of those only for the ones a candidate could be built for
    /// (<c>staged</c>); the rest are skipped, not counted as accepted (<c>skippedNoCandidate</c>: the candidate builder refused, for example an in-play plan that needs an unresolved advancement decision).
    /// The three numbers are asserted to add up and printed in the failure text, so the claim "commit replay accepted N positions" can never be read as "all records".
    /// </summary>
    [Fact]
    public void The_commit_replay_accepts_every_eligible_reference_start_position_that_has_a_candidate_including_the_ones_with_a_rejected_draw()
    {
        var w = Gc.Game(NonAutomated);
        var all = Reference.Value.GetProperty("records").GetArrayLength();
        var eligible = 0;
        var skippedNoCandidate = 0;
        var staged = 0;
        var rejectedStaged = 0;
        foreach (var rec in Reference.Value.GetProperty("records").EnumerateArray().Where(r => r.GetProperty("prior_target_calls").GetInt32() <= 1))
        {
            eligible++;
            var seed = ulong.Parse(rec.GetProperty("seed").GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var prior = new Dictionary<string, int> { ["game/target@1"] = rec.GetProperty("prior_target_calls").GetInt32(), ["game/location@1"] = rec.GetProperty("prior_location_calls").GetInt32() }
                .Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
            var g = Aggregate(w.Driver.State, w.Driver.LastIssued, seed, prior);
            var prepared = Gc.Prepare(g, w.Set);
            if (prepared.Draft.Kind == PitchBranchKind.InPlay)
            {
                if (Gc.BuildInPlay(g, prepared, w.Set) is { IsSuccess: true, Value: InPlayPitchCandidate baseCandidate })
                    Assert.True(Gc.Stage(g, baseCandidate, w.Set).IsSuccess, $"seed {seed} prior {prior.Count}: in play");
                else
                {
                    skippedNoCandidate++;
                    continue;
                }
            }
            else
            {
                var built = Gc.BuildNonInPlay(g, prepared, w.Set);
                if (!built.IsSuccess)
                {
                    skippedNoCandidate++;
                    continue;
                }
                var result = Gc.Stage(g, prepared, built.Value, w.Set);
                Assert.True(result.IsSuccess, $"seed {seed}: {result.Error?.Code} {result.Error?.Message}");
            }

            staged++;
            if (Gc.Target(prepared.Draft).RawDraws > 1 || Gc.Location(prepared.Draft).RawDraws > 1)
                rejectedStaged++;
        }

        Assert.True(eligible < all, "the filter must exclude some reference records; otherwise this comment is wrong");
        Assert.Equal(eligible, staged + skippedNoCandidate);
        Assert.True(staged >= 200, $"records {all}, eligible {eligible}, staged {staged}, skipped {skippedNoCandidate}");
        Assert.True(rejectedStaged >= 2);
    }

    // ---------------------------------------------------------------- GC-F02: the roll must be what the generator produced

    private static (AuthoritativeGame G, PreparedPitch P, NonInPlayPitchCandidate C, Gc.World W, PitchBranchStep Step, uint Forged) SameIntervalCase(string mode, int stepIndex)
    {
        var (g, p, c, w) = Pitch(mode, d => d.Kind != PitchBranchKind.InPlay && d.Steps.Length > stepIndex && Gc.SameIntervalRoll(d.Steps[stepIndex]) is not null);
        return (g, p, c, w, p.Draft.Steps[stepIndex], Gc.SameIntervalRoll(p.Draft.Steps[stepIndex])!.Value);
    }

    [Theory]
    [InlineData("automated", 0)]
    [InlineData("automated", 1)]
    [InlineData("non_automated", 0)]
    [InlineData("non_automated", 1)]
    public void GC_F02_a_roll_changed_inside_the_same_outcome_interval_is_refused_although_outcome_h1_and_usage_are_untouched(string mode, int stepIndex)
    {
        var (g, p, c, w, step, forgedRoll) = SameIntervalCase(mode, stepIndex);
        var dist = Distribution.Create(step.Trace.Outcomes, step.Trace.Final.ToList()).Value;

        // the counterexample really is one: the forged roll picks the SAME outcome from the recorded table, the position H1 and every consumption number are the original ones
        Assert.NotEqual(step.Roll, forgedRoll);
        Assert.Equal(step.Outcome, dist.Select(forgedRoll).Value);
        Assert.Equal(step.Outcome, dist.Select(step.Roll).Value);
        var forged = Forge(p, d => d.WithStep(stepIndex, s => s with { Roll = forgedRoll }));
        Assert.Same(p.Draft.NextRng, forged.Draft.NextRng);
        Assert.Equal(p.Draft.NextBoundedCalls, forged.Draft.NextBoundedCalls);
        Assert.True(p.Draft.StreamUsage.SequenceEqual(forged.Draft.StreamUsage));
        Assert.Equal(step.Outcome, forged.Draft.Steps[stepIndex].Outcome);

        Assert.True(Gc.Stage(g, p, c, w.Set).IsSuccess);                                      // control: the honest candidate is accepted
        Refused(g, Rng, () => Gc.Stage(g, forged, c, w.Set));                                 // the forged roll is not what the stream produces
    }

    [Fact]
    public void GC_F02_a_roll_changed_inside_the_interval_of_a_later_stage_is_refused_too()
    {
        foreach (var mode in Modes)
            foreach (var stage in new[] { StageIds.BatterDecision, StageIds.CalledPitch, StageIds.SwingResult })
            {
                var (g, p, c, w) = Pitch(mode, d => d.Kind != PitchBranchKind.InPlay && d.Steps.Any(s => s.Stage == stage && Gc.SameIntervalRoll(s) is not null));
                var index = p.Draft.Steps.ToList().FindIndex(s => s.Stage == stage);
                var forgedRoll = Gc.SameIntervalRoll(p.Draft.Steps[index])!.Value;

                Refused(g, Rng, () => Gc.Stage(g, Forge(p, d => d.WithStep(index, s => s with { Roll = forgedRoll })), c, w.Set));
            }
    }

    [Fact]
    public void A_roll_from_a_different_outcome_interval_or_changed_together_with_the_recorded_distribution_is_refused()
    {
        foreach (var mode in Modes)
        {
            var (g, p, c, w) = Pitch(mode, d => d.Kind != PitchBranchKind.InPlay);
            var location = Gc.Location(p.Draft);
            var other = Gc.OtherIntervalRoll(location);

            // another interval, outcome unchanged in the record: the table no longer agrees with the recorded roll
            Refused(g, Rng, () => Gc.Stage(g, Forge(p, d => d.WithStep(1, s => s with { Roll = other })), c, w.Set));

            // the roll AND the recorded distribution are rewritten so that the new roll "selects" the recorded outcome: the recomputed table from the real inputs is the authority
            var final = location.Trace.Final.ToArray();
            var outcomeIndex = location.Trace.Outcomes.ToList().IndexOf(location.Outcome);
            var donor = final.Select((v, i) => (v, i)).First(x => x.i != outcomeIndex && x.v > 1).i;
            final[outcomeIndex]++; final[donor]--;
            Refused(g, Link, () => Gc.Stage(g, Forge(p, d => d.WithStep(1, s => s with { Roll = other, Trace = s.Trace with { Final = final } })), c, w.Set));
        }
    }

    [Fact]
    public void GC_F02_raw_draws_that_disagree_with_the_replay_and_a_position_h1_that_disagrees_with_the_replay_are_refused()
    {
        foreach (var mode in Modes)
        {
            var (g, p, c, w) = Pitch(mode, d => d.Kind != PitchBranchKind.InPlay);

            // the location step claims one more raw draw than the generator used (the usage record is left alone)
            Refused(g, Rng, () => Gc.Stage(g, Forge(p, d => d.WithStep(1, s => s with { RawDraws = s.RawDraws + 1 })), c, w.Set));
            Refused(g, Rng, () => Gc.Stage(g, Forge(p, d => d.WithStep(0, s => s with { RawDraws = s.RawDraws + 1 })), c, w.Set));

            // the position is replaced by another valid position (one more raw draw on an unrelated stream) in the draft AND the candidate: a different H1
            var other = Advance(p.Draft.NextRng, (GameRngStreams.Ai, 1));
            var forgedDraft = p.Draft with { NextRng = other };
            var forgedCandidate = c.WithRng(other);
            Refused(g, Rng, () => Gc.Stage(g, p.With(forgedDraft), forgedCandidate, w.Set));
        }
    }

    [Fact]
    public void GC_F02_an_in_play_candidate_is_replayed_the_same_way_base_and_resolved()
    {
        foreach (var mode in Modes)
        {
            var w = Gc.Game(RulesFor(mode), approach: true);
            var (g, p, candidate) = Gc.FindInPlay(w, resolved: false, d => Gc.SameIntervalRoll(d.Steps[1]) is not null);
            Assert.True(Gc.Stage(g, candidate, w.Set).IsSuccess);
            var forgedRoll = Gc.SameIntervalRoll(Gc.Location(p.Draft))!.Value;
            var forged = WithPrepared(candidate, Forge(p, d => d.WithStep(1, s => s with { Roll = forgedRoll })));
            Refused(g, Rng, () => Gc.Stage(g, forged, w.Set));

            var wr = Gc.Game(RulesFor(mode), approach: true, policy: AdvancementPolicy.VersionV2, varied: true, occupancy: 1, outs: 0);   // a runner on first: a hit can leave an extra-base decision open
            var (g2, p2, resolved) = Gc.FindInPlay(wr, resolved: true, d => Gc.SameIntervalRoll(d.Steps[0]) is not null);
            Assert.True(Gc.Stage(g2, resolved, wr.Set).IsSuccess);
            var roll2 = Gc.SameIntervalRoll(Gc.Target(p2.Draft))!.Value;
            Refused(g2, Rng, () => Gc.Stage(g2, WithPrepared(resolved, Forge(p2, d => d.WithStep(0, s => s with { Roll = roll2 }))), wr.Set));
        }
    }

    // ---------------------------------------------------------------- recomputation of the target and location rows from the real inputs

    [Fact]
    public void The_recorded_target_and_location_rows_are_recomputed_and_every_forged_part_is_refused_before_the_swap()
    {
        foreach (var mode in Modes)
        {
            var (g, p, c, w) = Pitch(mode, d => d.Kind != PitchBranchKind.InPlay && Gc.AimedClass(d) == BoundaryClass.Borderline, control: 70, calling: 90);
            var location = Gc.Location(p.Draft);
            var otherZone = PitchZoneModel.V1.Zones.Select(z => z.Id.Value).First(z => z != location.Outcome && z != p.Draft.Target.Zone.Value);
            var model = Gx.Tuning4.ZoneModel;

            var cases = new (string Name, Func<PitchBranchDraft, PitchBranchDraft> Edit, string Code)[]
            {
                ("target zone", d => d with { Target = TargetZone.Create(model, PitchZoneId.Create(otherZone).Value).Value }, Link),
                ("condition key", d => d.WithStep(1, s => s with { Condition = otherZone, Trace = s.Trace with { Condition = otherZone } }), Link),
                ("outcome zone", d => d.WithStep(1, s => s with { Outcome = otherZone }) with { Location = PitchLocation.FromZone(model, PitchZoneId.Create(otherZone).Value, d.Location.Side).Value }, Link),
                ("location only", d => d with { Location = PitchLocation.FromZone(model, PitchZoneId.Create(otherZone).Value, d.Location.Side).Value }, Link),
                ("modifier input (control)", d => d.WithStep(1, s => s with { Trace = s.Trace with { Modifiers = Edit(s, "pitcher.control", m => m with { Input = "71" }) } }), Link),
                ("modifier input (game calling)", d => d.WithStep(1, s => s with { Trace = s.Trace with { Modifiers = Edit(s, "catcher.game_calling", m => m with { Input = "89" }) } }), Link),
                ("applied flag", d => d.WithStep(1, s => s with { Trace = s.Trace with { Modifiers = Edit(s, "catcher.game_calling", m => m with { Applied = !m.Applied }) } }), Link),
                ("control factor", d => d.WithStep(1, s => s with { Trace = s.Trace with { Modifiers = Edit(s, "pitcher.control", m => m with { FactorPpm = m.FactorPpm + 1 }) } }), Link),
                ("game calling factor", d => d.WithStep(1, s => s with { Trace = s.Trace with { Modifiers = Edit(s, "catcher.game_calling", m => m with { FactorPpm = 1_000_000 }) } }), Link),
                ("combined factor", d => d.WithStep(1, s => s with { Trace = s.Trace with { CombinedFactorPpm = s.Trace.CombinedFactorPpm.Select(f => f == 1_000_000 ? f : f + 1).ToArray() } }), Link),
                ("combined cap flag", d => d.WithStep(1, s => s with { Trace = s.Trace with { CombinedCapped = s.Trace.CombinedCapped.Select((x, i) => i == 0 ? !x : x).ToArray() } }), Link),
                ("distribution", d => d.WithStep(1, s => s with { Trace = s.Trace with { Final = Move(s.Trace.Final) } }), Link),
                ("baseline", d => d.WithStep(1, s => s with { Trace = s.Trace with { Baseline = Move(s.Trace.Baseline) } }), Link),
                ("modifier list emptied", d => d.WithStep(1, s => s with { Trace = s.Trace with { Modifiers = [] } }), Link),
                ("modifier list extended", d => d.WithStep(1, s => s with { Trace = s.Trace with { Modifiers = s.Trace.Modifiers.Append(new TraceModifier("catcher.framing", "50", 1_000_000, false, ["strike"])).ToArray() } }), Link),
                ("stream", d => d.WithStep(1, s => s with { Stream = GameRngStreams.Pitch }), Link),
                ("tuning hash in the trace", d => d.WithStep(1, s => s with { Trace = s.Trace with { TuningHash = new string('0', 64) } }), Link),
                ("steps swapped", d => d with { Steps = d.Steps.SetItem(0, d.Steps[1]).SetItem(1, d.Steps[0]) }, Link),
                ("location step missing", d => d with { Steps = d.Steps.RemoveAt(1) }, Link),
                ("location step duplicated", d => d with { Steps = d.Steps.Insert(2, d.Steps[1]) }, Link),
                ("target step missing", d => d with { Steps = d.Steps.RemoveAt(0) }, Link),
                ("intent", d => d with { Intent = PitchIntent.Attack }, Link),
            };

            Assert.True(Gc.Stage(g, p, c, w.Set, 70).IsSuccess);                              // control: the honest candidate is accepted
            foreach (var (name, edit, code) in cases)
            {
                var forged = Forge(p, edit);
                Refused(g, code, () => Gc.Stage(g, forged, c, w.Set, 70), $"{mode}: {name}");
            }
        }
    }

    private static ImmutableArray<int> Move(IReadOnlyList<int> ppm)
    {
        var a = ppm.ToArray();
        var from = Array.FindIndex(a, v => v > 1);
        var to = a.Length - 1 == from ? 0 : a.Length - 1;
        a[from]--; a[to]++;
        return [.. a];
    }

    private static IReadOnlyList<TraceModifier> Edit(PitchBranchStep s, string slot, Func<TraceModifier, TraceModifier> edit) =>
        s.Trace.Modifiers.Select(m => m.Slot == slot ? edit(m) : m).ToArray();

    // ---------------------------------------------------------------- GC-F01: the Control and the catcher come from the real state and the pinned set

    [Fact]
    public void GC_F01_the_same_pitcher_with_another_control_is_refused_at_the_commit_in_every_form()
    {
        foreach (var mode in Modes)
        {
            var (g, p, c, w) = Pitch(mode, d => d.Kind != PitchBranchKind.InPlay, control: 50);
            Assert.True(Gc.Stage(g, p, c, w.Set, 50).IsSuccess);

            // (1) the sheet handed to the commit shows Control 80 for the SAME player id and hand
            var sheet80 = Gc.Sheet(g.State.Fielding.Pitcher, new Gc.Spec(80));
            Refused(g, "PitchCandidate.pitcher_control_changed", () => Gc.Stage(g, p, c, w.Set, 50, null, sheet80));
            // (2) a set that pins 80 for him: another identity
            var set80 = Gc.SetFor(Fr.AtStart(RulesFor(mode), null).State, 80, 50, 50);
            Refused(g, "PitchCandidate.player_inputs_changed", () => Gc.Stage(g, p, c, set80, 80));
            Refused(g, "PitchCandidate.player_inputs_changed", () => Gc.Stage(g, p, c, null, 50));
            // (3) the recorded (frozen) Control is rewritten
            var real = p.PitcherInput!;
            Refused(g, "PitchCandidate.pitcher_control_changed", () => Gc.Stage(g, p.WithPitcher(new FrozenPitcherInput(real.GameId, real.InputSetId, real.Pitcher, 80)), c, w.Set, 50));
            Refused(g, "PitchCandidate.pitcher_control_changed", () => Gc.Stage(g, p.WithPitcher(new FrozenPitcherInput(real.GameId, real.InputSetId, real.Pitcher, 80)), c, w.Set, 80));
        }
    }

    [Fact]
    public void GC_F01_control_and_the_recorded_distribution_forged_together_are_still_refused_by_the_recomputation_from_the_pinned_set()
    {
        foreach (var mode in Modes)
        {
            // a real Control-80 pitch of the same seed supplies a self-consistent location trace (input "80", its factor, its distribution)
            var strong = Gc.Game(RulesFor(mode), control: 80);
            var weak = Gc.Game(RulesFor(mode), control: 50);
            ulong seed = 1;
            for (; seed < 400; seed++)
            {
                var a = Gc.Prepare(Aggregate(weak.Driver, seed), weak.Set, 50).Draft;
                var b = Gc.Prepare(Aggregate(strong.Driver, seed), strong.Set, 80).Draft;
                if (a.Kind != PitchBranchKind.InPlay && Gc.Location(a).Trace.Final.SequenceEqual(Gc.Location(b).Trace.Final) == false && Gc.Location(a).Outcome == Gc.Location(b).Outcome)
                    break;
            }

            var g = Aggregate(weak.Driver, seed);
            var p = Gc.Prepare(g, weak.Set, 50);
            var c = Gc.BuildNonInPlay(g, p, weak.Set).Value;
            var donor = Gc.Location(Gc.Prepare(Aggregate(strong.Driver, seed), strong.Set, 80).Draft);
            Assert.Equal("80", donor.Trace.Modifiers.Single(m => m.Slot == "pitcher.control").Input);

            // the frozen pitcher input still says 50 (so CheckPrepared passes): only the recomputation from the real set can see the lie
            var forged = Forge(p, d => d.WithStep(1, s => s with { Trace = donor.Trace }));
            Assert.NotEqual(Gc.Location(p.Draft).Trace.Final, Gc.Location(forged.Draft).Trace.Final);
            Refused(g, Link, () => Gc.Stage(g, forged, c, weak.Set, 50));
        }
    }

    [Fact]
    public void GC_F01_game_calling_and_the_recorded_distribution_forged_together_are_refused_for_the_same_catcher()
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
                if (a.Kind != PitchBranchKind.InPlay && Gc.AimedClass(a) == BoundaryClass.Borderline && Gc.Location(a).Outcome == Gc.Location(b).Outcome)
                    break;
            }

            var g = Aggregate(real.Driver, seed);
            var p = Gc.Prepare(g, real.Set);
            var c = Gc.BuildNonInPlay(g, p, real.Set).Value;
            var donor = Gc.Location(Gc.Prepare(Aggregate(high.Driver, seed), high.Set).Draft);
            Assert.True(donor.Trace.Modifiers.Single(m => m.Slot == "catcher.game_calling").Applied);

            // (a) the trace only (frozen record and set say 50): the recomputation sees it
            Refused(g, Link, () => Gc.Stage(g, Forge(p, d => d.WithStep(1, s => s with { Trace = donor.Trace })), c, real.Set));
            // (b) the frozen catcher record says 100 and the trace agrees: the real set still says 50 (same identity, so the set-level checks pass; the re-derivation does not)
            var frozen = p.Catcher!;
            var both = Forge(p, d => d.WithStep(1, s => s with { Trace = donor.Trace })).WithCatcher(new FrozenCatcherInput(frozen.GameId, frozen.InputSetId, frozen.Catcher, frozen.ReceivingFraming, 100));
            Refused(g, "PitchCandidate.game_calling_changed", () => Gc.Stage(g, both, c, real.Set));
        }
    }

    [Fact]
    public void A_real_catcher_change_or_another_catcher_id_is_refused_at_the_commit_even_with_the_same_game_calling()
    {
        foreach (var mode in Modes)
        {
            var w = Gc.Game(RulesFor(mode), approach: false);                              // a substitution is legal only at the start of a plate appearance: the pitch is an explicit Take
            var (g, p, c) = Gc.FindNonInPlay(w, _ => true, take: true);
            var frozen = p.Catcher!;

            // an aggregate whose state has the catcher swapped for the first baseman (a real reducer event); the preparation belongs to the old catcher
            var id = GameEventIds.Create(Gid, g.LastEventSequence).Value.Next().Value;
            var side = g.State.FieldingSide;
            var a = g.State.Team(side).SlotAt(FieldPosition.Catcher)!.Order;
            var b = g.State.Team(side).SlotAt(FieldPosition.FirstBase)!.Order;
            var swappedState = Ok(GameReducer.Apply(g.State, new SubstitutionEvent(id, SubstitutionKind.PositionSwap, side, default, null, a, b))).State;
            var moved = Ok(AuthoritativeGame.Restore(swappedState, g.Rng, g.LastEventSequence + 1, g.Revision));
            Assert.Equal(g.Rng.StateHash, moved.Rng.StateHash);
            Refused(moved, "PitchCandidate.catcher_changed", () => Gc.Stage(moved, p, c, w.Set));

            // a record that names another catcher id (same rating): the real state decides
            var otherCatcher = BasePlayers.First(x => x != frozen.Catcher);
            Refused(g, "PitchCandidate.catcher_changed", () => Gc.Stage(g, p.WithCatcher(new FrozenCatcherInput(frozen.GameId, frozen.InputSetId, otherCatcher, frozen.ReceivingFraming, frozen.GameCalling)), c, w.Set));
            // a frozen input of another game, or of another input set
            Refused(g, "PitchCandidate.catcher_game_mismatch", () => Gc.Stage(g, p.WithCatcher(new FrozenCatcherInput("game:other", frozen.InputSetId, frozen.Catcher, frozen.ReceivingFraming, frozen.GameCalling)), c, w.Set));
            Refused(g, "PitchCandidate.player_inputs_changed", () => Gc.Stage(g, p.WithCatcher(new FrozenCatcherInput(frozen.GameId, new string('f', 64), frozen.Catcher, frozen.ReceivingFraming, frozen.GameCalling)), c, w.Set));
            // no catcher input where the model needs one
            Refused(g, "PitchCandidate.catcher_input_mismatch", () => Gc.Stage(g, p.WithCatcher(null), c, w.Set));
        }
    }

    [Fact]
    public void A_real_pitching_change_between_the_preparation_and_the_commit_is_refused()
    {
        var w = Gc.Game(NonAutomated, control: 60, approach: false);
        var g = Aggregate(w.Driver, 21);
        var prepared = Gc.Prepare(g, w.Set, 60, take: true);
        var built = Gc.BuildNonInPlay(g, prepared, w.Set, 60);
        Assert.True(built.IsSuccess);

        w.Driver.Substitute(SubstitutionKind.PitchingChange, g.State.FieldingSide, PlayerId.Create(g.State.FieldingSide == TeamSide.Home ? "hb1" : "ab1").Value);
        var moved = Aggregate(w.Driver.State, w.Driver.LastIssued, 21);
        Refused(moved, "PitchCandidate.pitcher_changed", () => Gc.Stage(moved, prepared, built.Value, w.Set, 60));
    }

    // ---------------------------------------------------------------- the old evidence is kept: the called pitch is still re-derived

    [Fact]
    public void The_framing_verification_of_the_called_pitch_is_unchanged_in_a_v4_non_automated_game()
    {
        var (g, p, c, w) = Pitch("non_automated", d => d.Kind is PitchBranchKind.CalledBall or PitchBranchKind.CalledStrike, control: 50, calling: 50);
        var called = Fr.Called(p.Draft)!;
        Assert.True(Gc.Stage(g, p, c, w.Set).IsSuccess);

        // the recorded called-pitch distribution rewritten to a Framing the real catcher does not have
        var shifted = Forge(p, d => d.WithStep(d.Steps.ToList().IndexOf(called), s => s with { Trace = s.Trace with { Final = Move(s.Trace.Final) } }));
        Refused(g, Link, () => Gc.Stage(g, shifted, c, w.Set));
        // a called-pitch roll changed within its interval is caught by the same replay
        if (Gc.SameIntervalRoll(called) is { } roll)
            Refused(g, Rng, () => Gc.Stage(g, Forge(p, d => d.WithStep(d.Steps.ToList().IndexOf(called), s => s with { Roll = roll })), c, w.Set));
    }

    // ---------------------------------------------------------------- cancellation and the cursor

    [Fact]
    public void Cancelling_at_any_stage_of_the_commit_changes_nothing_and_a_later_commit_still_works()
    {
        var (g, p, c, w) = Pitch("non_automated", d => d.Kind != PitchBranchKind.InPlay);
        var (b, pitcher) = Gc.SheetsOf(g.State);
        foreach (var stage in new[] { CommitGate.StageName.Start, CommitGate.StageName.Validated, CommitGate.StageName.Applied })
        {
            var cursor = new GameCursor(g);
            var before = Gc.Basis.Of(cursor);
            using var cts = new CancellationTokenSource();
            Assert.ThrowsAny<OperationCanceledException>(() => PitchCommitGate.Stage(g, p, c, Gc.T4, b, pitcher, s => { if (s == stage) cts.Cancel(); }, cts.Token, w.Set));
            Gc.AssertUnchanged(before, cursor);
        }

        Assert.True(Gc.Stage(g, p, c, w.Set).IsSuccess);                                      // nothing was left behind by the cancelled attempts
    }
}
