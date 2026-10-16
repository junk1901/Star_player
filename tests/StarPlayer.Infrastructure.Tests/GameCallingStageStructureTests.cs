using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E (Game Calling v4, Codex final review F-01): the stage/stream STRUCTURE of a v4 pitch candidate. The commit used to replay every step after the first two on the stream the candidate itself named,
/// so a candidate with one more step (for example a real <c>game/ai@1</c> draw) whose roll, raw draws, consumption record and H1 were all made consistent with the real generator was accepted. Here every forgery is
/// built from a REAL replay of the extra or changed steps on H0 (nothing is a made-up constant), so the only thing wrong with it is the structure. The allowed structure is fixed per actual branch:
/// HBP = target, location, batter_decision; called ball/strike = those + called_pitch; swinging strike/foul = those + swing_result; in play = those + swing_result, batted_ball, defense_result,
/// each on its own stream. Every refusal happens in <c>PitchCommitGate.Stage</c>, before the compare-and-swap, and leaves the aggregate, revision, cursor, RNG and last commit as they were.
/// </summary>
public class GameCallingStageStructureTests
{
    private const string Link = "Commit.candidate_link_mismatch";

    private static readonly (string Stage, string Stream)[] Hbp =
        [(StageIds.TargetZone, "game/target@1"), (StageIds.ActualLocation, "game/location@1"), (StageIds.BatterDecision, "game/pitch@1")];

    private static readonly (string Stage, string Stream)[] Called = [.. Hbp, (StageIds.CalledPitch, "game/pitch@1")];
    private static readonly (string Stage, string Stream)[] Swing = [.. Hbp, (StageIds.SwingResult, "game/contact@1")];
    private static readonly (string Stage, string Stream)[] InPlay = [.. Swing, (StageIds.BattedBall, "game/contact@1"), (StageIds.DefenseResult, "game/field@1")];

    private static (string Stage, string Stream)[] Contract(PitchBranchKind kind) => kind switch
    {
        PitchBranchKind.HitByPitch => Hbp,
        PitchBranchKind.CalledBall or PitchBranchKind.CalledStrike => Called,
        PitchBranchKind.SwingingStrike or PitchBranchKind.Foul => Swing,
        _ => InPlay,
    };

    /// <summary>One real honest candidate and the way to stage it with a different draft (the candidate follows the draft's position and call count, as a forger would make it).</summary>
    private sealed record Subject(string Label, AuthoritativeGame G, PreparedPitch P, Func<PitchBranchDraft, Result<StagedCommit>> StageWith);

    private static Subject NonInPlay(string label, Gc.World w, Func<PitchBranchDraft, bool> want, bool take = false)
    {
        var (g, p, c) = Gc.FindNonInPlay(w, want, take: take);
        return new Subject(label, g, p, d => Gc.Stage(g, p.With(d), new NonInPlayPitchCandidate(c.Event, c.UnconfirmedStep, d.NextRng, c.Kind, d.NextBoundedCalls), w.Set));
    }

    private static Subject InPlayBase(string label, Gc.World w)
    {
        var (g, p, c) = Gc.FindInPlay(w, resolved: false);
        return new Subject(label, g, p, d =>
        {
            var forged = p.With(d);
            return Gc.Stage(g, new InPlayPitchCandidate(new BaseInPlayPlan(forged, c.Plan.Facts), c.Event, c.UnconfirmedStep, d.NextRng, c.Audit with { FinalRngStateHash = d.NextRng.StateHash }), w.Set);
        });
    }

    private static List<Subject> Subjects()
    {
        var all = new List<Subject>();
        foreach (var mode in new[] { "automated", "non_automated" })
        {
            var w = Gc.Game(RulesFor(mode));
            all.Add(NonInPlay($"{mode}/hbp", w, d => d.Kind == PitchBranchKind.HitByPitch));
            all.Add(NonInPlay($"{mode}/called ball", w, d => d.Kind == PitchBranchKind.CalledBall));
            all.Add(NonInPlay($"{mode}/called strike", w, d => d.Kind == PitchBranchKind.CalledStrike));
            all.Add(NonInPlay($"{mode}/swinging strike", w, d => d.Kind == PitchBranchKind.SwingingStrike));
            all.Add(NonInPlay($"{mode}/foul", w, d => d.Kind == PitchBranchKind.Foul));
            all.Add(InPlayBase($"{mode}/in play", Gc.Game(RulesFor(mode), approach: true)));
            all.Add(NonInPlay($"{mode}/explicit take", Gc.Game(RulesFor(mode), approach: false), d => d.Kind is PitchBranchKind.CalledBall or PitchBranchKind.CalledStrike, take: true));
        }

        return all;
    }

    /// <summary>
    /// A draft whose steps are the given list, made fully consistent with the real generator: every step's roll and raw draws are what the step's stream produces next when the list is replayed from H0 in order,
    /// the usage record, the call count and the final position H1 follow. The outcome, condition and trace of each step are kept as given.
    /// </summary>
    private static PitchBranchDraft Rebuild(AuthoritativeGame g, PitchBranchDraft d, IEnumerable<PitchBranchStep> steps)
    {
        var replay = GameRandom.Restore(g.Rng).Value;
        var list = new List<PitchBranchStep>();
        foreach (var s in steps)
        {
            var stream = replay.Stream(s.Stream);
            var before = stream.Draws;
            var roll = stream.NextBounded(ProbabilityScale.RollBound);
            list.Add(s with { Roll = roll, RawDraws = stream.Draws - before });
        }

        var usage = list.GroupBy(s => s.Stream).OrderBy(x => x.Key.Canonical, StringComparer.Ordinal)
            .Select(x => new PitchBranchStreamUse(x.Key, x.Count(), x.Sum(s => s.RawDraws))).ToImmutableArray();
        return d with { Steps = [.. list], NextBoundedCalls = list.Count, StreamUsage = usage, NextRng = replay.Snapshot() };
    }

    private static void RefusedUnchanged(Subject s, PitchBranchDraft forged, string label, string? code = null)
    {
        var cursor = new GameCursor(s.G);
        var before = Gc.Basis.Of(cursor);
        var result = s.StageWith(forged);
        Assert.False(result.IsSuccess, $"{s.Label}: the forged candidate was staged ({label})");
        if (code is not null)
            Assert.True(code == result.Error!.Value.Code, $"{s.Label}: {label}: expected {code}, got {result.Error!.Value.Code} {result.Error.Value.Message}");
        else
            Assert.StartsWith("Commit.", result.Error!.Value.Code);
        Gc.AssertUnchanged(before, cursor);
    }

    // ---------------------------------------------------------------- the honest branches

    [Fact]
    public void Every_honest_branch_has_exactly_its_allowed_stages_in_order_on_the_expected_streams_and_is_staged_and_published()
    {
        var seen = new HashSet<PitchBranchKind>();
        foreach (var s in Subjects())
        {
            var draft = s.P.Draft;
            var expected = Contract(draft.Kind);
            Assert.True(expected.SequenceEqual(draft.Steps.Select(x => (x.Stage, x.Stream.Canonical))), $"{s.Label}: the resolver's own structure differs from the contract table");
            Assert.Equal(expected.Length, draft.NextBoundedCalls);

            // the rebuilt-from-H0 draft IS the honest draft: the forging helper changes nothing by itself
            var same = Rebuild(s.G, draft, draft.Steps);
            Assert.Equal(draft.NextRng.StateHash, same.NextRng.StateHash);
            Assert.True(draft.Steps.Select(x => (x.Roll, x.RawDraws)).SequenceEqual(same.Steps.Select(x => (x.Roll, x.RawDraws))));

            var staged = s.StageWith(draft);
            Assert.True(staged.IsSuccess, $"{s.Label}: {staged.Error?.Code} {staged.Error?.Message}");
            Assert.True(new GameCursor(s.G).Publish(staged.Value).IsSuccess);
            seen.Add(draft.Kind);
        }

        Assert.Equal(6, seen.Count);                                                            // hbp, called ball, called strike, swinging strike, foul, in play
    }

    // ---------------------------------------------------------------- F-01: the extra step with a real, consistent draw

    [Fact]
    public void F01_an_extra_real_ai_draw_with_roll_raw_draws_usage_calls_and_h1_made_consistent_is_refused_for_every_branch()
    {
        foreach (var s in Subjects())
        {
            var d = s.P.Draft;
            var extra = d.Steps[^1] with { Stream = GameRngStreams.Ai };                         // a legal-looking step of the last stage, drawn on a stream no pitch branch uses
            var forged = Rebuild(s.G, d, d.Steps.Append(extra));

            // the counterexample really is one: a real bounded game/ai draw, usage and calls follow, H1 is the position after it, the event and the preview are the honest ones
            var ai = forged.Steps[^1];
            var probe = GameRandom.Restore(d.NextRng).Value.Stream(GameRngStreams.Ai);
            var drawsBefore = probe.Draws;
            Assert.Equal(probe.NextBounded(ProbabilityScale.RollBound), ai.Roll);
            Assert.Equal(probe.Draws - drawsBefore, ai.RawDraws);
            Assert.Equal(d.NextBoundedCalls + 1, forged.NextBoundedCalls);
            Assert.Equal(d.Steps.Length + 1, forged.Steps.Length);
            Assert.Contains(forged.StreamUsage, u => u.Stream == GameRngStreams.Ai && u.Calls == 1 && u.RawDraws == ai.RawDraws);
            Assert.NotEqual(d.NextRng.StateHash, forged.NextRng.StateHash);

            RefusedUnchanged(s, forged, "extra game/ai step", Link);
        }
    }

    // ---------------------------------------------------------------- the other structural forgeries, each fully consistent with a real replay

    [Fact]
    public void Added_missing_duplicated_reordered_restreamed_and_renamed_stages_are_refused_even_when_every_draw_is_consistent()
    {
        var cases = new (string Name, Func<List<PitchBranchStep>, List<PitchBranchStep>?> Edit)[]
        {
            ("extra pitch-stream step appended", l => [.. l, l[^1] with { Stream = GameRngStreams.Pitch }]),
            ("extra contact-stream step appended", l => [.. l, l[^1] with { Stream = GameRngStreams.Contact }]),
            ("extra target-stream step appended", l => [.. l, l[^1] with { Stream = GameRngStreams.Target }]),
            ("ai step inserted before the target", l => [l[0] with { Stream = GameRngStreams.Ai }, .. l]),
            ("ai step inserted after the location", l => [l[0], l[1], l[1] with { Stream = GameRngStreams.Ai }, .. l.Skip(2)]),
            ("last step removed", l => l.Take(l.Count - 1).ToList()),
            ("last step duplicated", l => [.. l, l[^1]]),
            ("decision and next step swapped", l => l.Count < 4 ? null : [l[0], l[1], l[3], l[2], .. l.Skip(4)]),
            ("batter_decision on another stream", l => l.Select((x, i) => i == 2 ? x with { Stream = GameRngStreams.Contact } : x).ToList()),
            ("last step on the ai stream instead of its own", l => l.Select((x, i) => i == l.Count - 1 ? x with { Stream = GameRngStreams.Ai } : x).ToList()),
            ("last step of a wrong stage kind", l => l.Select((x, i) => i == l.Count - 1 ? x with { Stage = StageIds.Advancement } : x).ToList()),
            ("a stage of another branch in place of the last", l => l.Count < 4 ? null : l.Select((x, i) => i == l.Count - 1 ? x with { Stage = x.Stage == StageIds.CalledPitch ? StageIds.SwingResult : StageIds.CalledPitch } : x).ToList()),
        };

        foreach (var s in Subjects())
            foreach (var (name, edit) in cases)
            {
                if (edit([.. s.P.Draft.Steps]) is not { } steps)
                    continue;
                RefusedUnchanged(s, Rebuild(s.G, s.P.Draft, steps), name, Link);   // the structure check refuses it before any replay; a replay mismatch (rng code) alone would not be enough
            }
    }

    [Fact]
    public void A_branch_label_that_disagrees_with_the_recorded_steps_is_refused()
    {
        foreach (var s in Subjects())
            foreach (var kind in Enum.GetValues<PitchBranchKind>().Where(k => k != s.P.Draft.Kind))
                RefusedUnchanged(s, s.P.Draft with { Kind = kind }, $"label {kind}");
    }
}
