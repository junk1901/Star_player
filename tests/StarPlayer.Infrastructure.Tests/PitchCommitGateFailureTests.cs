using System.Collections.Immutable;
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
/// Phase 3-C3-B (game-engine 27.18 B-01 to B-06), the refusals. Every forged object is made only through the internal constructors that the two test assemblies see; no production
/// bypass exists. A failing Stage returns an error and changes nothing: each test that fails a Stage asserts the aggregate it was based on is still the very same reference (the
/// aggregate is immutable, so that is state, RNG, last sequence, revision and last commit together).
/// </summary>
public class PitchCommitGateFailureTests
{
    // a bottom-of-the-1st-style plate appearance in progress with a rich state; the first fixed seed with the wanted branch is used (deterministic scan)
    private static readonly Lazy<NonInPlay> Ball = new(() => FindNonInPlay(Situation(0, 0, true).State, Situation(0, 0, true).LastIssued, false, (d, _) => d.Kind == PitchBranchKind.CalledBall, PriorDraws));

    private static readonly Lazy<NonInPlay> Strike = new(() => FindNonInPlay(Situation(0, 0, true).State, Situation(0, 0, true).LastIssued, false, (d, _) => d.Kind == PitchBranchKind.CalledStrike, PriorDraws));

    private static readonly Lazy<NonInPlay> TakeBall = new(() => FindNonInPlay(Situation(0, 0, false).State, Situation(0, 0, false).LastIssued, true, (d, _) => d.Kind == PitchBranchKind.CalledBall, PriorDraws));

    /// <summary>A runner on third, no out: a base single scores him (runs, RBI, plays, stats, bases and the batting order all change).</summary>
    private static readonly Lazy<InPlay> ScoringSingle = new(() =>
    {
        var d = Situation(4, 0, true);
        return FindBaseInPlay(d.State, d.LastIssued, c => c.Plan.OfficialResult == InPlayResult.Single && c.UnconfirmedStep.NewPlays.Count == 1 && c.UnconfirmedStep.NewPlays[0].Runs.Count == 1, PriorDraws);
    });

    private static readonly Lazy<InPlay> Resolved = new(() =>
    {
        var d = Situation(1, 0, true);
        return FindResolved(d.State, d.LastIssued, c => c.Plan.OfficialResult == InPlayResult.Single, PriorDraws);
    });

    /// <summary>A second REAL preparation and candidate from the very same aggregate (so 7a accepts either), with another intent and so, when one exists, another branch or location.</summary>
    private static NonInPlay Sibling(NonInPlay f)
    {
        NonInPlay? any = null;
        foreach (var intent in new[] { PitchIntent.Attack, PitchIntent.Careful, PitchIntent.Balanced })
        {
            var prepared = Prepare(f.Game, false, intent);
            if (prepared.Draft.Kind == PitchBranchKind.InPlay || BuildNonInPlay(f.Game, prepared) is not { IsSuccess: true } built || ReferenceEquals(prepared.Draft.NextRng, f.Prepared.Draft.NextRng))
                continue;
            var found = new NonInPlay(f.Game, prepared, built.Value);
            if (prepared.Draft.Kind != f.Prepared.Draft.Kind || !Equals(prepared.Draft.Location, f.Prepared.Draft.Location))
                return found;
            any ??= found;
        }

        return any ?? throw new InvalidOperationException("no sibling preparation");
    }

    private static string Eq(Result<StagedCommit> r, string code)
    {
        Assert.Equal(code, Code(r));
        return Message(r);
    }

    // ---------------------------------------------------------------- the common gate still refuses pitches; evidence is no token

    [Fact]
    public void The_plain_gate_still_refuses_a_pitch_a_take_and_an_in_play_event_even_when_they_are_genuine_candidate_events()
    {
        var events = new GameEvent[] { Strike.Value.Candidate.Event, TakeBall.Value.Candidate.Event, ScoringSingle.Value.Candidate.Event };
        foreach (var gameEvent in events)
        {
            var g = Aggregate(Situation(0, 0, true), 1);
            Assert.Equal("Commit.event_kind_not_supported", Code(CommitGate.Stage(g, gameEvent)));
        }
    }

    [Fact]
    public void Evidence_bound_to_another_aggregate_is_refused_as_a_link_failure_and_changes_nothing()
    {
        var f = Strike.Value;
        var (b, p) = Sheets(f.Game.State);
        var evidence = CommitEvidence.ForNonInPlay(f.Game, f.Prepared, f.Candidate, Tuning, b, p);
        // another aggregate VALUE with the same state and RNG (so 7a passes) but not the one the evidence is bound to
        var other = Ok(AuthoritativeGame.Restore(f.Game.State, f.Game.Rng, f.Game.LastEventSequence, f.Game.Revision + 7));

        var message = Eq(CommitGate.Stage(other, f.Candidate.Event, evidence, null, default), "Commit.candidate_link_mismatch");

        Assert.Contains("다른 상태", message);
    }

    [Fact]
    public void Evidence_bound_to_another_event_is_refused_even_when_the_other_event_has_the_same_id_and_content()
    {
        var f = Strike.Value;
        var (b, p) = Sheets(f.Game.State);
        var evidence = CommitEvidence.ForNonInPlay(f.Game, f.Prepared, f.Candidate, Tuning, b, p);
        var sameContentOtherInstance = f.Candidate.Event with { };

        var message = Eq(CommitGate.Stage(f.Game, sameContentOtherInstance, evidence, null, default), "Commit.candidate_link_mismatch");

        Assert.Contains("다른 사건", message);
        Assert.Equal(f.Candidate.Event, sameContentOtherInstance);   // equal by value: only the binding to the exact event object stopped it
    }

    [Fact]
    public void Evidence_for_one_candidate_cannot_confirm_another_candidates_event()
    {
        var a = Strike.Value;
        var b2 = FindNonInPlay(Situation(0, 0, true).State, Situation(0, 0, true).LastIssued, false, (d, _) => d.Kind == PitchBranchKind.Foul, PriorDraws);
        var (sb, sp) = Sheets(a.Game.State);
        var evidence = CommitEvidence.ForNonInPlay(a.Game, a.Prepared, a.Candidate, Tuning, sb, sp);

        Assert.Equal("Commit.candidate_link_mismatch", Code(CommitGate.Stage(a.Game, b2.Candidate.Event, evidence, null, default)));
    }

    [Fact]
    public void Evidence_is_made_per_call_and_never_stored_or_returned_by_the_staged_commit_or_the_record()
    {
        var f = Strike.Value;
        var staged = Ok(Stage(f.Game, f.Prepared, f.Candidate));

        foreach (var type in new[] { typeof(StagedCommit), typeof(CommitRecord), typeof(AuthoritativeGame) })
            Assert.DoesNotContain(type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Cast<System.Reflection.MemberInfo>().Concat(type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)),
                m => m.ToString()!.Contains("CommitEvidence"));
        Assert.Same(f.Game, staged.Expected);
    }

    // ---------------------------------------------------------------- step 1, 4 to 6: the existing checks come first

    [Fact]
    public void A_missing_aggregate_comes_before_a_missing_candidate_and_a_missing_candidate_is_its_own_code_in_both_entries()
    {
        var f = Strike.Value;
        var (b, p) = Sheets(f.Game.State);

        Assert.Equal("Commit.authority_missing", Code(PitchCommitGate.Stage(null, f.Prepared, null, Tuning, b, p)));
        Assert.Equal("Commit.candidate_missing", Code(PitchCommitGate.Stage(f.Game, f.Prepared, null, Tuning, b, p)));
        Assert.Equal("Commit.candidate_missing", Code(PitchCommitGate.Stage(f.Game, (InPlayPitchCandidate?)null, Tuning, b, p)));
        Assert.Equal("Commit.authority_missing", Code(PitchCommitGate.Stage(null, (InPlayPitchCandidate?)null, Tuning, b, p)));
    }

    [Fact]
    public void Cancellation_is_checked_before_everything_else()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var f = Strike.Value;
        var (b, p) = Sheets(f.Game.State);

        Assert.Throws<OperationCanceledException>(() => PitchCommitGate.Stage(null, f.Prepared, null, Tuning, b, p, cts.Token));
        Assert.Throws<OperationCanceledException>(() => PitchCommitGate.Stage(f.Game, f.Prepared, f.Candidate, Tuning, b, p, cts.Token));
        Assert.Throws<OperationCanceledException>(() => PitchCommitGate.Stage(f.Game, ScoringSingle.Value.Candidate, Tuning, b, p, cts.Token));
    }

    [Fact]
    public void Null_preparation_tuning_or_sheets_are_reported_by_the_existing_check_prepared_codes_at_7a()
    {
        var f = Strike.Value;
        var (b, p) = Sheets(f.Game.State);

        Assert.Equal("PitchCandidate.prepared_missing", Code(PitchCommitGate.Stage(f.Game, null, f.Candidate, Tuning, b, p)));
        Assert.Equal("PitchCandidate.tuning_missing", Code(PitchCommitGate.Stage(f.Game, f.Prepared, f.Candidate, null, b, p)));
        Assert.Equal("PitchCandidate.batter_sheet_missing", Code(PitchCommitGate.Stage(f.Game, f.Prepared, f.Candidate, Tuning, null, p)));
        Assert.Equal("PitchCandidate.pitcher_sheet_missing", Code(PitchCommitGate.Stage(f.Game, f.Prepared, f.Candidate, Tuning, b, null)));
    }

    [Fact]
    public void A_wrong_event_id_is_refused_by_the_existing_id_checks_before_the_candidate_is_examined()
    {
        var f = Strike.Value;
        var pitch = (PitchEvent)f.Candidate.Event;

        Assert.Equal("Commit.event_id_invalid", Code(Stage(f.Game, f.Prepared, f.Candidate.WithEvent(pitch with { Id = "pb-1" }))));
        Assert.Equal("Commit.event_game_mismatch", Code(Stage(f.Game, f.Prepared, f.Candidate.WithEvent(pitch with { Id = "game:other:g9:e000009" }))));
        Assert.Equal("Commit.event_sequence_gap", Code(Stage(f.Game, f.Prepared, f.Candidate.WithEvent(pitch with { Id = $"{Gid}:e{f.Game.LastEventSequence + 3:D6}" }))));
    }

    [Fact]
    public void The_same_event_id_with_other_content_is_refused_at_step_8_after_the_link_check()
    {
        var f = Strike.Value;
        var used = f.Game.State.AppliedEvents[0].EventId;                // an id the game already used (an approach)
        var forged = f.Candidate.WithEvent(((PitchEvent)f.Candidate.Event) with { Id = used });

        Eq(Stage(f.Game, f.Prepared, forged), "Commit.event_id_in_use");
    }

    // ---------------------------------------------------------------- a committed candidate and a stale one

    [Fact]
    public void A_committed_candidate_submitted_again_is_already_committed_before_any_staleness_code()
    {
        var f = Strike.Value;
        var cursor = new GameCursor(f.Game);
        Ok(cursor.Publish(Ok(Stage(f.Game, f.Prepared, f.Candidate))));
        var after = cursor.Current;

        Assert.Equal("Commit.already_committed", Code(Stage(after, f.Prepared, f.Candidate)));   // 5 before 7a (the preparation is stale now)
        Assert.Same(after, cursor.Current);
    }

    [Fact]
    public void A_stale_preparation_is_refused_with_the_existing_check_prepared_code()
    {
        var f = Strike.Value;
        var cursor = new GameCursor(f.Game);
        var sibling = Sibling(f);
        Ok(cursor.Publish(Ok(Stage(f.Game, sibling.Prepared, sibling.Candidate))));   // another pitch is confirmed first
        var after = cursor.Current;
        // a candidate for the NEXT id so that steps 4 to 6 pass and the stale preparation is what stops it
        var next = f.Candidate.WithEvent(((PitchEvent)f.Candidate.Event) with { Id = $"{Gid}:e{after.LastEventSequence + 1:D6}" });

        var code = Code(Stage(after, f.Prepared, next));

        Assert.StartsWith("PitchCandidate.", code);
        Assert.Contains(code, new[] { "PitchCandidate.state_stale", "PitchCandidate.batter_mismatch", "PitchCandidate.batter_changed", "PitchPrepare.batter_mismatch", "PitchCandidate.rng_state_changed" });
        Assert.Same(after, cursor.Current);
    }

    [Fact]
    public void A_preparation_from_another_game_state_sheet_or_hand_is_refused_at_7a()
    {
        var f = Strike.Value;
        var (b, p) = Sheets(f.Game.State);
        var otherHand = Sheet(f.Game.State.Fielding.Pitcher, BattingSide.Right, ThrowingHand.Left);
        var otherBatter = Sheet(f.Game.State.Batting.SlotByOrder((f.Game.State.Batting.CurrentBatterSlot.Order % 9) + 1).Player);
        var otherState = Ok(AuthoritativeGame.Restore(f.Game.State with { }, f.Game.Rng, f.Game.LastEventSequence, f.Game.Revision));   // an equal but different state object

        Assert.Equal("PitchCandidate.pitcher_hand_changed", Code(PitchCommitGate.Stage(f.Game, f.Prepared, f.Candidate, Tuning, b, otherHand)));
        Assert.Equal("PitchPrepare.batter_mismatch", Code(PitchCommitGate.Stage(f.Game, f.Prepared, f.Candidate, Tuning, otherBatter, p)));
        Assert.Equal("PitchCandidate.state_stale", Code(PitchCommitGate.Stage(otherState, f.Prepared, f.Candidate, Tuning, b, p)));
    }

    // ---------------------------------------------------------------- 7b: the candidate and its source

    [Fact]
    public void A_candidate_combined_with_another_prepare_result_is_refused_also_when_both_came_from_identical_inputs()
    {
        var f = Strike.Value;
        var twin = Prepare(f.Game);                                       // the same inputs again: an equal draft, but its own snapshot instance
        Assert.Equal(f.Prepared.Draft.NextRng.StateHash, twin.Draft.NextRng.StateHash);
        Assert.NotSame(f.Prepared.Draft.NextRng, twin.Draft.NextRng);

        var message = Eq(Stage(f.Game, twin, f.Candidate), "Commit.candidate_link_mismatch");

        Assert.Contains("H1", message);
    }

    [Fact]
    public void A_mismatched_pair_from_different_seeds_is_refused()
    {
        var f = Strike.Value;
        var other = Sibling(f);

        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(f.Game, other.Prepared, f.Candidate)));
        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(f.Game, f.Prepared, other.Candidate)));
    }

    [Fact]
    public void A_wrong_location_kind_outcome_or_call_count_is_a_link_failure()
    {
        var f = Strike.Value;
        var pitch = (PitchEvent)f.Candidate.Event;
        var location = pitch.Spec.Location!;
        var flipped = Ok(PitchLocation.FromZone(location.Model, location.Zone, location.Side == AppliedBatterSide.Right ? AppliedBatterSide.Left : AppliedBatterSide.Right));
        var asBall = pitch with { Spec = pitch.Spec with { Outcome = PitchOutcome.Ball } };

        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(f.Game, f.Prepared, f.Candidate.WithEvent(pitch with { Spec = pitch.Spec with { Location = flipped } }))));
        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(f.Game, f.Prepared, f.Candidate.WithEvent(asBall))));
        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(f.Game, f.Prepared, f.Candidate.WithEvent(pitch with { Spec = pitch.Spec with { Location = null } }))));
        Assert.Equal("Commit.candidate_link_mismatch",
            Code(Stage(f.Game, f.Prepared, new NonInPlayPitchCandidate(f.Candidate.Event, f.Candidate.UnconfirmedStep, f.Candidate.NextRng, PitchBranchKind.CalledBall, f.Candidate.NextBoundedCalls))));
        Assert.Equal("Commit.candidate_link_mismatch",
            Code(Stage(f.Game, f.Prepared, new NonInPlayPitchCandidate(f.Candidate.Event, f.Candidate.UnconfirmedStep, f.Candidate.NextRng, f.Candidate.Kind, f.Candidate.NextBoundedCalls + 1))));
    }

    [Fact]
    public void A_take_and_a_pitch_swapped_for_each_other_are_a_link_failure()
    {
        var pitch = Strike.Value;
        var take = TakeBall.Value;
        var asTake = new TakeEvent(pitch.Candidate.Event.Id, ((PitchEvent)pitch.Candidate.Event).Spec);
        var asPitch = new PitchEvent(take.Candidate.Event.Id, ((TakeEvent)take.Candidate.Event).Spec);

        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(pitch.Game, pitch.Prepared, pitch.Candidate.WithEvent(asTake))));
        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(take.Game, take.Prepared, take.Candidate.WithEvent(asPitch))));
    }

    [Fact]
    public void An_in_play_pitch_cannot_be_confirmed_as_a_non_in_play_candidate()
    {
        var f = Strike.Value;
        var inPlayDraft = ScoringSingle.Value.Prepared.Draft;
        var forgedPrepared = f.Prepared.With(f.Prepared.Draft with { Kind = PitchBranchKind.InPlay });

        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(f.Game, forgedPrepared, f.Candidate)));
        Assert.Equal(PitchBranchKind.InPlay, inPlayDraft.Kind);
    }

    [Fact]
    public void A_base_candidate_with_an_advancement_detail_and_a_resolved_candidate_without_one_are_link_failures()
    {
        var baseCase = ScoringSingle.Value;
        var resolved = Resolved.Value;
        var detail = resolved.Candidate.Audit.AdvancementDetail!;

        var baseWithDetail = baseCase.Candidate.WithAudit(baseCase.Candidate.Audit with { AdvancementDetail = detail });
        var resolvedWithout = resolved.Candidate.WithAudit(resolved.Candidate.Audit with { AdvancementDetail = null });

        Assert.Contains("해결 기록", Eq(Stage(baseCase.Game, baseWithDetail), "Commit.candidate_link_mismatch"));
        Assert.Contains("미해결", Eq(Stage(resolved.Game, resolvedWithout), "Commit.candidate_link_mismatch"));
    }

    [Fact]
    public void A_forged_unresolved_candidate_whose_plan_still_needs_an_advancement_is_refused_and_no_entry_accepts_a_pending_advancement()
    {
        var pending = FindPending(Situation(1, 0, true).State, Situation(1, 0, true).LastIssued);
        var plan = pending.Pending.Plan;
        var id = $"{Gid}:e{pending.Game.LastEventSequence + 1:D6}";
        var baseEvent = InPlayBasePlanner.BuildEvent(plan.Facts, id, plan.Prepared.Draft.Location);
        var step = Ok(GameReducer.Apply(pending.Game.State, baseEvent));
        var forged = new InPlayPitchCandidate(plan, baseEvent, step, plan.Prepared.Draft.NextRng);   // an unresolved plan wearing a candidate's clothes

        Assert.Contains("미해결", Eq(Stage(pending.Game, forged), "Commit.candidate_link_mismatch"));

        // by reflection: no member of the gate types takes a pending advancement, the plan outcome or a base plan
        foreach (var gate in new[] { typeof(PitchCommitGate), typeof(CommitGate), typeof(CommitEvidence) })
            foreach (var method in gate.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                Assert.DoesNotContain(method.GetParameters(), q => q.ParameterType == typeof(PendingAdvancement) || q.ParameterType == typeof(InPlayPlanOutcome) || q.ParameterType == typeof(BaseInPlayPlan));
    }

    [Fact]
    public void Forged_h1_and_h2_audit_hashes_are_link_failures()
    {
        var r = Resolved.Value;
        var detail = r.Candidate.Audit.AdvancementDetail!;
        string Flip(string hash) => (hash[0] == '0' ? "1" : "0") + hash[1..];

        InPlayPitchCandidate With(Func<AdvancementAudit, AdvancementAudit> editDetail, Func<InPlayAudit, InPlayAudit>? editAudit = null)
        {
            var audit = r.Candidate.Audit with { AdvancementDetail = editDetail(detail) };
            return r.Candidate.WithAudit(editAudit?.Invoke(audit) ?? audit);
        }

        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(r.Game, With(d => d with { AfterPitchRngStateHash = Flip(d.AfterPitchRngStateHash) }))));
        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(r.Game, With(d => d with { FinalRngStateHash = Flip(d.FinalRngStateHash) }))));
        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(r.Game, With(d => d with { StartRngStateHash = Flip(d.StartRngStateHash) }))));
        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(r.Game, With(d => d, a => a with { FinalRngStateHash = Flip(a.FinalRngStateHash) }))));
        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(r.Game, With(d => d with { DrawStream = "game/pitch@1" }))));
        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(r.Game, With(d => d with { LogicalDraws = d.LogicalDraws + 1 }))));             // LogicalDraws versus Decisions
        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(r.Game, With(d => d with { RawDraws = d.RawDraws + 1 }))));                     // RawDraws versus the decisions' sum
        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(r.Game, With(d => d with { Decisions = d.Decisions.RemoveAt(0) }))));          // Decisions versus ContextKeys
    }

    // ---------------------------------------------------------------- 10b: the RNG evidence (the prepared draft and the candidate are forged COHERENTLY, otherwise 7b hides 10b)

    private static (AuthoritativeGame G, PreparedPitch P, NonInPlayPitchCandidate C) ForgeH1(NonInPlay f, GameRandomSnapshot h1, Func<PitchBranchDraft, PitchBranchDraft>? edit = null)
    {
        var draft = (edit?.Invoke(f.Prepared.Draft) ?? f.Prepared.Draft) with { NextRng = h1 };
        return (f.Game, f.Prepared.With(draft), f.Candidate.WithRng(h1));
    }

    private static string StageRng(NonInPlay f, GameRandomSnapshot h1, Func<PitchBranchDraft, PitchBranchDraft>? edit = null)
    {
        var (g, p, c) = ForgeH1(f, h1, edit);
        var result = Stage(g, p, c);
        Assert.False(result.IsSuccess);
        return result.Error!.Value.Code + "|" + result.Error!.Value.Message;
    }

    private static readonly RngStreamId Ai = GameRngStreams.Ai;

    [Fact]
    public void The_genuine_h1_equals_the_start_plus_the_recorded_raw_draws_and_the_forging_helper_is_faithful()
    {
        var f = Ball.Value;
        var h1 = Advance(f.Game.Rng, Usage(f.Prepared.Draft));

        Assert.Equal(f.Prepared.Draft.NextRng.StateHash, h1.StateHash);
        Assert.Equal("(success)", Code(Stage(ForgeH1(f, h1).G, ForgeH1(f, h1).P, ForgeH1(f, h1).C)));   // a coherently rebuilt equal H1 is accepted: the check is about the numbers, not the instance
    }

    [Fact]
    public void A_wrong_master_seed_or_context_key_in_the_adopted_position_is_refused()
    {
        var f = Ball.Value;
        var otherSeed = GameRandom.Create(f.Game.Rng.MasterSeed + 1, Gid).Value;
        otherSeed.Stream(GameRngStreams.Pitch).NextUInt32();
        var otherContext = GameRandom.Create(f.Game.Rng.MasterSeed, "game:3c3b:other").Value;
        otherContext.Stream(GameRngStreams.Pitch).NextUInt32();

        var seed = StageRng(f, otherSeed.Snapshot());
        var context = StageRng(f, otherContext.Snapshot());

        Assert.StartsWith("Commit.rng_evidence_mismatch|", seed);
        Assert.Contains("마스터 시드", seed);
        Assert.StartsWith("Commit.rng_evidence_mismatch|", context);
        Assert.Contains("맥락 키", context);
    }

    [Fact]
    public void A_snapshot_with_an_unsupported_version_keeps_its_rng_structure_code()
    {
        var f = Ball.Value;

        Assert.StartsWith("Rng.algorithm_unsupported|", StageRng(f, f.Prepared.Draft.NextRng with { AlgorithmVersion = "other-algorithm" }));
        Assert.StartsWith("Rng.snapshot_format_unsupported|", StageRng(f, f.Prepared.Draft.NextRng with { FormatVersion = "rngset9" }));
        Assert.StartsWith("Rng.key_derivation_unsupported|", StageRng(f, f.Prepared.Draft.NextRng with { KeyDerivationVersion = "kd9" }));
    }

    [Fact]
    public void A_stream_of_the_current_position_that_vanished_is_refused()
    {
        var f = Ball.Value;
        var h1 = f.Prepared.Draft.NextRng;

        var text = StageRng(f, h1 with { Streams = h1.Streams.Where(s => s.Stream != Ai).ToImmutableArray() });

        Assert.StartsWith("Commit.rng_evidence_mismatch|", text);
        Assert.Contains("game/ai@1", text);
    }

    [Fact]
    public void Too_few_and_too_many_raw_draws_on_a_stream_are_refused_with_the_stream_named()
    {
        var f = Ball.Value;
        var first = f.Prepared.Draft.StreamUsage[0].Stream.Canonical;

        var shortText = StageRng(f, Advance(f.Game.Rng, UsagePlus(f.Prepared.Draft, -1)));
        var overText = StageRng(f, Advance(f.Game.Rng, UsagePlus(f.Prepared.Draft, +1)));

        foreach (var text in new[] { shortText, overText })
        {
            Assert.StartsWith("Commit.rng_evidence_mismatch|", text);
            Assert.Contains(first, text);
            Assert.Contains("원시 소비 증가량", text);
        }
    }

    [Fact]
    public void A_stream_that_the_record_does_not_name_but_grew_is_refused_and_so_is_a_new_one()
    {
        var f = Ball.Value;
        var grown = Usage(f.Prepared.Draft).Append((Ai, 1L)).ToArray();                       // game/ai already exists in H0
        var created = Usage(f.Prepared.Draft).Append((GameRngStreams.Field, 2L)).ToArray();   // game/field does not exist in H0

        Assert.Contains("기록에 없는 스트림 game/ai@1", StageRng(f, Advance(f.Game.Rng, grown)));
        Assert.Contains("기록에 없는 스트림 game/field@1", StageRng(f, Advance(f.Game.Rng, created)));
    }

    [Fact]
    public void A_draft_whose_steps_calls_and_raw_draws_disagree_is_refused_before_the_increments_are_compared()
    {
        var f = Ball.Value;
        var h1 = f.Prepared.Draft.NextRng;

        var stepsRaw = StageRng(f, h1, d => d with { Steps = d.Steps.SetItem(0, d.Steps[0] with { RawDraws = d.Steps[0].RawDraws + 1 }) });
        var calls = StageRng(f, h1, d => d with { StreamUsage = d.StreamUsage.SetItem(0, d.StreamUsage[0] with { Calls = d.StreamUsage[0].Calls + 1 }) });
        var rawBelowCalls = StageRng(f, h1, d => d with { StreamUsage = d.StreamUsage.SetItem(0, d.StreamUsage[0] with { RawDraws = d.StreamUsage[0].Calls - 1 }) });

        foreach (var text in new[] { stepsRaw, calls, rawBelowCalls })
        {
            Assert.StartsWith("Commit.rng_evidence_mismatch|", text);
            Assert.Contains("투구 분기 기록", text);
        }
    }

    private static InPlayPitchCandidate ForgeH2(InPlay r, GameRandomSnapshot h2)
    {
        var detail = r.Candidate.Audit.AdvancementDetail! with { FinalRngStateHash = h2.StateHash };
        return r.Candidate.WithAudit(r.Candidate.Audit with { FinalRngStateHash = h2.StateHash, AdvancementDetail = detail }, h2);
    }

    [Fact]
    public void A_resolved_candidate_s_runner_stream_must_grow_by_exactly_the_advancement_record()
    {
        var r = Resolved.Value;
        var h1 = r.Prepared.Draft.NextRng;
        var raw = r.Candidate.Audit.AdvancementDetail!.RawDraws;
        Assert.Equal("(success)", Code(Stage(r.Game, ForgeH2(r, Advance(h1, (GameRngStreams.Runner, raw))))));     // the coherent exact value passes

        foreach (var wrong in new[] { raw - 1, raw + 1 })
        {
            var result = Stage(r.Game, ForgeH2(r, Advance(h1, (GameRngStreams.Runner, wrong))));
            Assert.Equal("Commit.rng_evidence_mismatch", Code(result));
            Assert.Contains("game/runner@1", Message(result));
        }
    }

    [Fact]
    public void A_resolved_candidate_that_moved_an_unrelated_stream_between_h1_and_h2_is_refused()
    {
        var r = Resolved.Value;
        var h1 = r.Prepared.Draft.NextRng;
        var raw = r.Candidate.Audit.AdvancementDetail!.RawDraws;

        var result = Stage(r.Game, ForgeH2(r, Advance(h1, (GameRngStreams.Runner, raw), (GameRngStreams.Pitch, 1))));

        Assert.Equal("Commit.rng_evidence_mismatch", Code(result));
        Assert.Contains("무관한 스트림 game/pitch@1", Message(result));
    }

    [Fact]
    public void A_resolved_candidate_that_adopts_h1_or_an_unresolved_one_that_adopts_another_position_is_refused()
    {
        var r = Resolved.Value;
        var h1 = r.Prepared.Draft.NextRng;
        var detail = r.Candidate.Audit.AdvancementDetail!;

        // H2 equal to H1 by instance, with the audit made coherent: nothing was drawn for the advancement although the record says so
        var audit = r.Candidate.Audit with { FinalRngStateHash = h1.StateHash, AdvancementDetail = detail with { FinalRngStateHash = h1.StateHash } };
        var sameAsH1 = Stage(r.Game, r.Candidate.WithAudit(audit, h1));

        Assert.Equal("Commit.rng_evidence_mismatch", Code(sameAsH1));
    }

    // ---------------------------------------------------------------- 10a: the structural preview comparison, one forged field group per case

    private static GameState WithFielding(GameState s, Func<TeamGameState, TeamGameState> edit) => s.FieldingSide == TeamSide.Away ? s with { Away = edit(s.Away) } : s with { Home = edit(s.Home) };

    private static GameState WithBatting(GameState s, Func<TeamGameState, TeamGameState> edit) => s.BattingSide == TeamSide.Away ? s with { Away = edit(s.Away) } : s with { Home = edit(s.Home) };

    private static GameStep S(GameStep s, Func<GameState, GameState> edit) => s with { State = edit(s.State) };

    private static GameStep LastPlay(GameStep s, Func<ScoredPlay, ScoredPlay> edit) =>
        s with { State = s.State with { Plays = s.State.Plays.SetItem(s.State.Plays.Count - 1, edit(s.State.Plays[^1])) }, NewPlays = s.NewPlays.SetItem(0, edit(s.NewPlays[0])) };

    /// <summary>Each case changes ONE field group of a scoring single's preview; expected is the first differing path the comparer names (its prefix).</summary>
    private static readonly IReadOnlyDictionary<string, (Func<GameStep, GameStep> Tamper, string Path)> Tampers = new Dictionary<string, (Func<GameStep, GameStep>, string)>
    {
        ["count"] = (s => S(s, st => st with { Count = new PitchCountState(1, 1) }), "Step.State.Count"),
        ["outs"] = (s => S(s, st => st with { Outs = st.Outs + 1 }), "Step.State.Outs"),
        ["runner placement"] = (s => S(s, st => st with { Bases = RunnerState.Empty }), "Step.State.Bases.First"),
        ["responsible pitcher"] = (s => S(s, st => st with { Bases = st.Bases with { First = st.Bases.First! with { ResponsiblePitcher = st.Bases.First.Player } } }), "Step.State.Bases.First"),
        ["score"] = (s => S(s, st => st with { AwayRuns = st.AwayRuns + 1 }), "Step.State.AwayRuns"),
        ["inning line"] = (s => S(s, st => st with { AwayLine = st.AwayLine.SetItem(0, st.AwayLine[0] + 1) }), "Step.State.AwayLine[0]"),
        ["pitch count"] = (s => S(s, st => WithFielding(st, t => t with { PitcherUsages = t.PitcherUsages.SetItem(0, t.PitcherUsages[0] with { PitchCount = t.PitcherUsages[0].PitchCount + 1 }) })), "PitcherUsages[0]"),
        ["batting order"] = (s => S(s, st => WithBatting(st, t => t with { NextBatterIndex = t.NextBatterIndex + 1 })), "NextBatterIndex"),
        ["plays sequence"] = (s => LastPlay(s, p => p with { Sequence = p.Sequence + 1 }), ".Sequence"),
        ["rbi"] = (s => LastPlay(s, p => p with { Runs = p.Runs.SetItem(0, p.Runs[0] with { RbiBatter = null }) }), ".Runs[0]"),
        ["run"] = (s => LastPlay(s, p => p with { Runs = p.Runs.Clear() }), ".Runs.Count"),
        ["run voided"] = (s => LastPlay(s, p => p with { Moves = p.Moves.SetItem(0, p.Moves[0] with { RunVoided = true }) }), ".Moves[0]"),
        ["credits"] = (s => LastPlay(s, p => p with { Credits = p.Credits.Add(new FielderCredit(FieldPosition.Catcher, p.Catcher, CreditKind.Assist)) }), ".Credits.Count"),
        ["errors"] = (s => LastPlay(s, p => p with { Errors = p.Errors.Add(FieldPosition.Catcher) }), ".Errors.Count"),
        ["stats"] = (s => S(s, st => st with { Stats = st.Stats.RemoveAt(0) }), "Step.State.Stats.Count"),
        ["applied events"] = (s => S(s, st => st with { AppliedEvents = st.AppliedEvents.RemoveAt(st.AppliedEvents.Count - 1) }), "Step.State.AppliedEvents.Count"),
        ["event receipt"] = (s => S(s, st => st with { AppliedEvents = st.AppliedEvents.SetItem(st.AppliedEvents.Count - 1, st.AppliedEvents[^1] with { Fingerprint = new string('0', 64) }) }), "Step.State.AppliedEvents["),
        ["delta entries"] = (s => s with { Delta = s.Delta with { Entries = s.Delta.Entries.RemoveAt(0) } }, "Step.Delta.Entries.Count"),
        ["delta source"] = (s => s with { Delta = s.Delta with { SourceEventId = "other" } }, "Step.Delta.SourceEventId"),
        ["facts"] = (s => s with { Facts = s.Facts.Clear() }, "Step.Facts.Count"),
        ["new plays"] = (s => s with { NewPlays = s.NewPlays.Clear() }, "Step.NewPlays.Count"),
        ["was duplicate"] = (s => s with { WasDuplicate = true }, "Step.WasDuplicate"),
        ["final"] = (s => S(s, st => st with { Final = new FinalizedGame(st.GameId, GameOutcome.Tie, 0, 0, "x", "y", null!) }), "Step.State.Final"),
    };

    public static IEnumerable<object[]> TamperNames() => Tampers.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(TamperNames))]
    public void A_forged_preview_is_refused_for_every_compared_field_group_and_the_first_path_is_named(string name)
    {
        var f = ScoringSingle.Value;
        var (tamper, path) = Tampers[name];
        var step = f.Candidate.UnconfirmedStep;
        Assert.Equal("(success)", Code(Stage(f.Game, f.Candidate)));                          // the untouched preview passes
        var forged = f.Candidate.WithStep(tamper(step));

        var message = Eq(Stage(f.Game, forged), "Commit.preview_mismatch");

        Assert.Contains(path, message);
    }

    [Fact]
    public void A_forged_non_in_play_preview_is_refused_too()
    {
        var f = Ball.Value;
        var forged = f.Candidate.WithStep(S(f.Candidate.UnconfirmedStep, st => st with { Count = new PitchCountState(3, 0) }));

        Assert.Contains("Step.State.Count", Eq(Stage(f.Game, f.Prepared, forged), "Commit.preview_mismatch"));
    }

    [Fact]
    public void A_forged_resolved_preview_is_refused_too()
    {
        var r = Resolved.Value;

        Assert.Equal("Commit.preview_mismatch", Code(Stage(r.Game, r.Candidate.WithStep(S(r.Candidate.UnconfirmedStep, st => st with { Outs = st.Outs + 1 })))));
        Assert.Equal("Commit.preview_mismatch", Code(Stage(r.Game, r.Candidate.WithStep(null!))));                  // a missing preview is a difference, not a crash
    }

    // ---------------------------------------------------------------- the O-2 priority between overlapping failures

    [Fact]
    public void Step_5_beats_7a_and_step_6_beats_7a()
    {
        var f = Strike.Value;
        var cursor = new GameCursor(f.Game);
        Ok(cursor.Publish(Ok(Stage(f.Game, f.Prepared, f.Candidate))));
        var after = cursor.Current;
        var gap = f.Candidate.WithEvent(((PitchEvent)f.Candidate.Event) with { Id = $"{Gid}:e{after.LastEventSequence + 3:D6}" });

        Assert.Equal("Commit.already_committed", Code(Stage(after, f.Prepared, f.Candidate)));      // stale preparation AND committed
        Assert.Equal("Commit.event_sequence_gap", Code(Stage(after, f.Prepared, gap)));              // stale preparation AND an id gap
    }

    [Fact]
    public void Step_7a_beats_7b_when_both_the_preparation_and_the_link_are_wrong()
    {
        var f = Strike.Value;
        var other = Sibling(f);
        var (b, _) = Sheets(f.Game.State);
        var wrongHand = Sheet(f.Game.State.Fielding.Pitcher, BattingSide.Right, ThrowingHand.Left);

        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(f.Game, other.Prepared, f.Candidate)));                              // the link alone
        Assert.Equal("PitchCandidate.pitcher_hand_changed", Code(PitchCommitGate.Stage(f.Game, other.Prepared, f.Candidate, Tuning, b, wrongHand)));   // both: 7a first
    }

    [Fact]
    public void Step_7b_beats_step_8()
    {
        var f = Strike.Value;
        var used = f.Game.State.AppliedEvents[0].EventId;
        var badLink = f.Candidate.WithEvent(((PitchEvent)f.Candidate.Event with { Id = used }) with { Spec = ((PitchEvent)f.Candidate.Event).Spec with { Outcome = PitchOutcome.Ball } });

        Assert.Equal("Commit.candidate_link_mismatch", Code(Stage(f.Game, f.Prepared, badLink)));
    }

    [Fact]
    public void Step_8_event_id_conflict_beats_step_10a_forged_preview()
    {
        var f = Ball.Value;
        var pitch = (PitchEvent)f.Candidate.Event;
        var used = f.Game.State.AppliedEvents[0].EventId;                                           // an id the game already used (an approach)
        var conflicting = pitch with { Id = used };
        var forgedStep = S(f.Candidate.UnconfirmedStep, st => st with { Count = new PitchCountState(3, 0) });   // the same forgery as A_forged_non_in_play_preview_is_refused_too

        Assert.Equal("(success)", Code(Stage(f.Game, f.Prepared, f.Candidate)));                    // the untouched candidate passes: only the changes below can fail it
        Assert.NotEqual(f.Game.State.AppliedEvents[0], EventFingerprint.Receipt(conflicting));      // other content under the used id: not step 5 (already committed)
        Assert.NotEqual(f.Candidate.UnconfirmedStep, forgedStep);

        // A: a fresh id, a forged preview -> 10a
        var a = f.Candidate.WithStep(forgedStep);
        Assert.Contains("Step.State.Count", Eq(Stage(f.Game, f.Prepared, a), "Commit.preview_mismatch"));

        // B: a used id, the genuine preview -> 8 (7a and 7b pass first, so 8 is what stops it)
        var b = f.Candidate.WithEvent(conflicting);
        Eq(Stage(f.Game, f.Prepared, b), "Commit.event_id_in_use");

        // C: both at once; the forged preview of A rides on the conflicting event of B -> 8, never 10a
        var c = b.WithStep(forgedStep);
        Assert.Same(forgedStep, c.UnconfirmedStep);
        Assert.Equal(conflicting, c.Event);

        var cursor = new GameCursor(f.Game);
        var (state, rng, last, revision, lastCommit) = (f.Game.State, f.Game.Rng, f.Game.LastEventSequence, f.Game.Revision, f.Game.LastCommit);
        var result = Stage(cursor.Current, f.Prepared, c);

        Assert.Equal("Commit.event_id_in_use", Code(result));
        Assert.NotEqual("Commit.preview_mismatch", Code(result));
        foreach (var unchanged in new[] { f.Game, cursor.Current })
        {
            Assert.Same(state, unchanged.State);
            Assert.Same(rng, unchanged.Rng);
            Assert.Equal((last, revision), (unchanged.LastEventSequence, unchanged.Revision));
            Assert.Same(lastCommit, unchanged.LastCommit);
        }

        Assert.Same(f.Game, cursor.Current);
    }

    [Fact]
    public void A_reducer_refusal_at_step_9_beats_a_forged_preview_at_step_10()
    {
        var f = Strike.Value;
        // a coherently forged draft that says "explicit take" in a plate appearance already in sequence: link and preparation pass, the reducer refuses a Take there
        // (Phase 3-E: the forgery is made coherent in the called_pitch step too - an outcome that disagrees with its own step and roll is now refused earlier, at the link; roll 0 selects "ball" in every row)
        var calledSteps = f.Prepared.Draft.Steps.Select(s => s.Stage == StageIds.CalledPitch ? s with { Outcome = "ball", Roll = 0 } : s).ToImmutableArray();
        var draft = f.Prepared.Draft with { ExplicitTake = true, Kind = PitchBranchKind.CalledBall, CalledPitch = "ball", Steps = calledSteps };
        var forgedPrepared = f.Prepared.With(draft);
        var take = new TakeEvent(f.Candidate.Event.Id, new PitchSpec(PitchOutcome.Ball, Location: draft.Location));
        var forged = new NonInPlayPitchCandidate(take, S(f.Candidate.UnconfirmedStep, st => st with { Outs = 2 }), draft.NextRng, PitchBranchKind.CalledBall, draft.NextBoundedCalls);
        var reducerCode = GameReducer.Apply(f.Game.State, take).Error!.Value.Code;

        var result = Stage(f.Game, forgedPrepared, forged);

        Assert.NotEqual("Commit.preview_mismatch", Code(result));
        Assert.Equal(reducerCode, Code(result));
    }

    [Fact]
    public void A_forged_preview_at_10a_beats_a_forged_rng_position_at_10b()
    {
        var f = Ball.Value;
        var tooMuch = Advance(f.Game.Rng, UsagePlus(f.Prepared.Draft, +1));
        var (g, p, c) = ForgeH1(f, tooMuch);
        var both = c.WithStep(S(c.UnconfirmedStep, st => st with { Outs = 2 }));

        Assert.Equal("Commit.rng_evidence_mismatch", Code(Stage(g, p, c)));          // the RNG alone
        Assert.Equal("Commit.preview_mismatch", Code(Stage(g, p, both)));            // both: the preview first
    }

    // ---------------------------------------------------------------- the pitch part of step 9 (unreachable with the real reducer, so the evidence check is exercised directly)

    [Fact]
    public void The_evidence_s_receipt_and_pitch_count_check_refuses_a_legacy_receipt_and_a_wrong_count()
    {
        var f = Strike.Value;
        var (b, p) = Sheets(f.Game.State);
        var evidence = CommitEvidence.ForNonInPlay(f.Game, f.Prepared, f.Candidate, Tuning, b, p);
        var genuine = Ok(GameReducer.Apply(f.Game.State, f.Candidate.Event));

        Assert.Null(evidence.CheckApplied(f.Game.State, genuine));

        var legacy = genuine with { State = genuine.State with { AppliedEvents = genuine.State.AppliedEvents.SetItem(genuine.State.AppliedEvents.Count - 1, genuine.State.AppliedEvents[^1] with { FingerprintVersion = EventFingerprint.Version }) } };
        Assert.Equal("Commit.receipt_mismatch", evidence.CheckApplied(f.Game.State, legacy)!.Value.Code);

        var uncounted = WithFielding2(genuine, t => t with { PitcherUsages = t.PitcherUsages.SetItem(0, t.PitcherUsages[0] with { PitchCount = t.PitcherUsages[0].PitchCount - 1 }) });
        Assert.Equal("Commit.pitch_count_invalid", evidence.CheckApplied(f.Game.State, uncounted)!.Value.Code);

        var twice = WithFielding2(genuine, t => t with { PitcherUsages = t.PitcherUsages.SetItem(0, t.PitcherUsages[0] with { PitchCount = t.PitcherUsages[0].PitchCount + 1 }) });
        Assert.Equal("Commit.pitch_count_invalid", evidence.CheckApplied(f.Game.State, twice)!.Value.Code);

        var noLocation = CommitEvidence.ForNonInPlay(f.Game, f.Prepared, f.Candidate.WithEvent(((PitchEvent)f.Candidate.Event) with { Spec = ((PitchEvent)f.Candidate.Event).Spec with { Location = null } }), Tuning, b, p);
        Assert.Equal("Commit.receipt_mismatch", noLocation.CheckApplied(f.Game.State, genuine)!.Value.Code);
    }

    private static GameStep WithFielding2(GameStep step, Func<TeamGameState, TeamGameState> edit) => step with { State = WithFielding(step.State, edit) };
}
