using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Simulation.Tests.Authority.Auth;
using static StarPlayer.Simulation.Tests.Script;

namespace StarPlayer.Simulation.Tests.Authority;

/// <summary>Phase 3-C3-A, game-engine 27.16 O-6/O-8: creation and restoration. The receipt checks cover id, game, continuity and form, never the match of a fingerprint with a body that was never stored.</summary>
public class AuthoritativeGameTests
{
    private const string Fp = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static EventReceipt Receipt(int sequence, string kind = "approach", string version = "ef1", string? fingerprint = null, string? id = null) =>
        new(id ?? Id(sequence), kind, version, fingerprint ?? Fp);

    private static GameState WithReceipts(IEnumerable<EventReceipt> receipts) => NewState() with { AppliedEvents = receipts.ToImmutableList() };

    private static string RestoreCode(GameState state, int last, long revision = 0, GameRandomSnapshot? rng = null) =>
        Code(AuthoritativeGame.Restore(state, rng ?? Rng(), last, revision));

    // ---------------------------------------------------------------- StartNew

    [Fact]
    public void A_new_game_has_an_empty_history_sequence_zero_revision_zero_and_no_last_commit_and_keeps_the_given_references()
    {
        var state = NewState();
        var rng = Rng();

        var game = Ok(AuthoritativeGame.StartNew(state, rng));

        Assert.Same(state, game.State);
        Assert.Same(rng, game.Rng);
        Assert.Equal((0, 0L), (game.LastEventSequence, game.Revision));
        Assert.Null(game.LastCommit);
        Assert.Empty(game.State.AppliedEvents);
    }

    [Fact]
    public void A_new_game_refuses_missing_inputs_a_non_empty_history_and_a_foreign_rng_context()
    {
        Assert.Equal("Authoritative.state_missing", Code(AuthoritativeGame.StartNew(null, Rng())));
        Assert.Equal("Authoritative.rng_missing", Code(AuthoritativeGame.StartNew(NewState(), null)));
        Assert.Equal("Authoritative.initial_state_not_empty", Code(AuthoritativeGame.StartNew(WithReceipts([Receipt(1)]), Rng())));
        Assert.Equal("Authoritative.rng_context_mismatch", Code(AuthoritativeGame.StartNew(NewState(), Rng("game:test:other"))));
        Assert.Equal("Id.game_invalid", Code(AuthoritativeGame.StartNew(NewState("bad game id!"), Rng())));
    }

    // ---------------------------------------------------------------- Restore: accepted

    [Fact]
    public void An_empty_history_restores_with_last_sequence_zero_and_keeps_a_stored_revision_that_differs_from_the_sequence()
    {
        var game = Ok(AuthoritativeGame.Restore(NewState(), Rng(), 0, 41));

        Assert.Equal((0, 41L), (game.LastEventSequence, game.Revision));
        Assert.Null(game.LastCommit);
    }

    [Fact]
    public void A_contiguous_real_history_restores_and_the_last_commit_is_null()
    {
        var script = CanonScript.ToBottomOfNinthTied();

        var game = script.ToAuthority(revision: 3);

        Assert.Equal((102, 3L), (game.LastEventSequence, game.Revision));
        Assert.Null(game.LastCommit);
        Assert.Equal(102, game.State.AppliedEvents.Count);
        Assert.Equal(Id(102), game.State.AppliedEvents[^1].EventId);
    }

    [Fact]
    public void Sequence_999999_is_the_accepted_boundary_and_the_next_official_id_does_not_exist()
    {
        var receipts = Enumerable.Range(1, 999_999).Select(i => Receipt(i)).ToImmutableList();

        var game = Ok(AuthoritativeGame.Restore(WithReceipts(receipts), Rng(), 999_999, 0));

        Assert.Equal(999_999, game.LastEventSequence);
        Assert.Equal("Commit.event_id_invalid", Code(CommitGate.Stage(game, new ApproachEvent($"{GameId}:e1000000", ApproachKind.Normal))));
    }

    // ---------------------------------------------------------------- Restore: refused

    [Fact]
    public void Missing_inputs_are_refused()
    {
        Assert.Equal("Authoritative.state_missing", Code(AuthoritativeGame.Restore(null, Rng(), 0, 0)));
        Assert.Equal("Authoritative.rng_missing", Code(AuthoritativeGame.Restore(NewState(), null, 0, 0)));
    }

    [Fact]
    public void A_last_sequence_outside_zero_to_999999_is_refused_before_the_history_is_looked_at()
    {
        Assert.Equal("Id.sequence_invalid", RestoreCode(NewState(), 1_000_000));
        Assert.Equal("Id.sequence_invalid", RestoreCode(NewState(), -1));
        Assert.Equal("Id.sequence_invalid", RestoreCode(NewState(), int.MinValue));
        Assert.Equal("Id.sequence_invalid", RestoreCode(NewState(), int.MaxValue));
    }

    [Fact]
    public void The_last_sequence_must_equal_the_number_of_receipts()
    {
        Assert.Equal("Authoritative.last_sequence_mismatch", RestoreCode(NewState(), 3));
        Assert.Equal("Authoritative.last_sequence_mismatch", RestoreCode(WithReceipts([Receipt(1), Receipt(2)]), 1));
        Assert.Equal("Authoritative.last_sequence_mismatch", RestoreCode(WithReceipts([Receipt(1)]), 2));
    }

    [Fact]
    public void A_skipped_a_repeated_and_an_out_of_order_sequence_are_each_refused_and_nothing_is_renumbered()
    {
        Assert.Equal("Authoritative.history_sequence_gap", RestoreCode(WithReceipts([Receipt(1), Receipt(3)]), 2));
        Assert.Equal("Authoritative.history_sequence_duplicate", RestoreCode(WithReceipts([Receipt(1), Receipt(1)]), 2));
        Assert.Equal("Authoritative.history_sequence_gap", RestoreCode(WithReceipts([Receipt(2), Receipt(1)]), 2));
        Assert.Equal("Authoritative.history_sequence_gap", RestoreCode(WithReceipts([Receipt(1), Receipt(3), Receipt(2)]), 3));
        Assert.Equal("Authoritative.history_sequence_duplicate", RestoreCode(WithReceipts([Receipt(1), Receipt(2), Receipt(2)]), 3));
    }

    [Fact]
    public void A_non_canonical_a_seven_digit_a_zero_and_a_foreign_receipt_id_are_refused()
    {
        foreach (var bad in new[] { "e1", $"{GameId}:e1", $"{GameId}:e0000001", $"{GameId}:e000000", $"{GameId}:E000001", $" {GameId}:e000001", Id(1, "game:test:other"), "" })
            Assert.Equal("Authoritative.history_event_id_invalid", RestoreCode(WithReceipts([Receipt(1, id: bad)]), 1));
        Assert.Equal("Authoritative.history_event_id_invalid", RestoreCode(WithReceipts([Receipt(1), Receipt(2, id: "e2")]), 2));
    }

    [Fact]
    public void A_bad_fingerprint_version_shape_or_kind_is_refused()
    {
        foreach (var bad in new[] { Receipt(1, version: "ef0"), Receipt(1, version: ""), Receipt(1, version: "EF1"), Receipt(1, fingerprint: Fp[..63]),
                     Receipt(1, fingerprint: Fp + "0"), Receipt(1, fingerprint: Fp.ToUpperInvariant()), Receipt(1, fingerprint: new string('g', 64)), Receipt(1, kind: "") })
            Assert.Equal("Authoritative.history_receipt_invalid", RestoreCode(WithReceipts([bad]), 1));
        Assert.True(AuthoritativeGame.Restore(WithReceipts([Receipt(1, version: "ef2")]), Rng(), 1, 0).IsSuccess);
    }

    [Fact]
    public void A_bad_rng_snapshot_and_a_rng_context_of_another_game_are_refused()
    {
        var good = Rng();
        Assert.Equal("Rng.snapshot_format_unsupported", RestoreCode(NewState(), 0, rng: good with { FormatVersion = "rngset0" }));
        Assert.Equal("Rng.state_mismatch", RestoreCode(NewState(), 0, rng: good with { Streams = [good.Streams[0] with { State = good.Streams[0].State + 2 }] }));
        Assert.Equal("Authoritative.rng_context_mismatch", RestoreCode(NewState(), 0, rng: Rng("game:test:other")));
        Assert.Equal("Id.game_invalid", RestoreCode(NewState("bad game id!"), 0));
    }

    [Fact]
    public void A_negative_revision_is_refused_and_zero_and_large_values_are_kept()
    {
        Assert.Equal("Authoritative.revision_invalid", RestoreCode(NewState(), 0, -1));
        Assert.Equal("Authoritative.revision_invalid", RestoreCode(NewState(), 0, long.MinValue));
        Assert.Equal(long.MaxValue, Ok(AuthoritativeGame.Restore(NewState(), Rng(), 0, long.MaxValue)).Revision);
    }

    [Fact]
    public void A_finished_game_without_its_final_record_is_refused_and_is_never_finalized_implicitly()
    {
        var script = CanonScript.ToBottomOfNinthTied().Triple();
        var ended = Ok(GameReducer.Apply(script.State, new RunnerPlayEvent(Id(script.Count + 1), RunnerPlayKind.Balk, [M(3, 4, MoveCause.Balk)]))).State;
        Assert.Equal((GameStatus.Final, true), (ended.Status, ended.Final is null));

        Assert.Equal("Authoritative.final_state_invalid", RestoreCode(ended, script.Count + 1));
        Assert.Null(ended.Final);

        var finalized = Ok(GameReducer.Finalize(ended)).State;
        Assert.True(AuthoritativeGame.Restore(finalized, Rng(), script.Count + 1, 0).IsSuccess);
    }

    [Fact]
    public void A_final_record_of_another_game_outcome_or_score_and_a_record_on_a_game_in_progress_are_refused()
    {
        var script = CanonScript.ToBottomOfNinthTied().Triple();
        var ended = Ok(GameReducer.Apply(script.State, new RunnerPlayEvent(Id(script.Count + 1), RunnerPlayKind.Balk, [M(3, 4, MoveCause.Balk)]))).State;
        var done = Ok(GameReducer.Finalize(ended)).State;
        var last = script.Count + 1;

        Assert.Equal("Authoritative.final_state_invalid", RestoreCode(done with { Final = done.Final! with { GameId = "game:test:other" } }, last));
        Assert.Equal("Authoritative.final_state_invalid", RestoreCode(done with { Final = done.Final! with { Outcome = GameOutcome.AwayWin } }, last));
        Assert.Equal("Authoritative.final_state_invalid", RestoreCode(done with { Final = done.Final! with { HomeRuns = 5 } }, last));
        Assert.Equal("Authoritative.final_state_invalid", RestoreCode(done with { Outcome = null }, last));
        Assert.Equal("Authoritative.final_state_invalid", RestoreCode(script.State with { Final = done.Final }, script.Count));
        Assert.Equal("Authoritative.final_state_invalid", RestoreCode(script.State with { Outcome = GameOutcome.Tie }, script.Count));
    }

    [Fact]
    public void Only_the_explicit_event_references_are_checked_against_the_receipts()
    {
        var script = CanonScript.ToBottomOfNinthTied().Triple();
        var withPlays = script.State;
        Assert.NotEmpty(withPlays.Plays);

        // A scored play that names an event with no receipt (explicit reference ScoredPlay.EventId)
        var strayPlay = withPlays with { Plays = withPlays.Plays.SetItem(0, withPlays.Plays[0] with { EventId = Id(900) }) };
        Assert.Equal("Authoritative.evidence_without_receipt", RestoreCode(strayPlay, script.Count));

        // A substitution record that names an event with no receipt (explicit reference SubstitutionRecord.EventId)
        var sub = new SubstitutionRecord(Id(900), 1, InningHalf.Top, TeamSide.Away, SubstitutionKind.PinchHit, default, default, 1, FieldPosition.CenterField);
        Assert.Equal("Authoritative.evidence_without_receipt", RestoreCode(withPlays with { Substitutions = withPlays.Substitutions.Add(sub) }, script.Count));

        // StatEntry.SourceId and ScoredPlay.Id are NOT event references and are not checked
        var stat = new StatEntry(TeamSide.Away, default, StatKey.BatPA, 1, "not-an-event-id");
        var odd = withPlays with { Stats = withPlays.Stats.Add(stat), Plays = withPlays.Plays.SetItem(0, withPlays.Plays[0] with { Id = "any-play-id" }) };
        Assert.True(AuthoritativeGame.Restore(odd, Rng(), script.Count, 0).IsSuccess);
    }

    [Fact]
    public void Every_refusal_creates_no_aggregate_and_changes_none_of_its_inputs()
    {
        var state = WithReceipts([Receipt(1), Receipt(3)]);
        var rng = Rng();
        var hash = rng.StateHash;

        Assert.False(AuthoritativeGame.Restore(state, rng, 2, 0).IsSuccess);

        Assert.Equal(2, state.AppliedEvents.Count);
        Assert.Equal(Id(3), state.AppliedEvents[1].EventId);
        Assert.Equal(hash, rng.StateHash);
    }
}
