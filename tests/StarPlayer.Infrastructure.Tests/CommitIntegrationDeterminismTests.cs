using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C3-C-1: determinism of confirmed chains. TC-07 two independent cursors with the same inputs, and cancel-then-retry; TC-08 the fixture's own event bodies replayed
/// through the reducer; TC-10 the real published aggregate restored and continued on a new cursor. Values are compared structurally (<see cref="Structural"/>), never by
/// record <c>==</c> or a hash alone.
/// </summary>
public class CommitIntegrationDeterminismTests
{
    /// <summary>Final state, RNG, revision, sequence, receipts, plays, substitutions and every commit record agree by value and structure.</summary>
    private static void AssertSameOutcome(CommitChain expected, CommitChain actual)
    {
        var (a, b) = (expected.Cursor.Current, actual.Cursor.Current);
        Structural.AssertSame(a.State, b.State, "final State");
        Assert.Equal(a.Rng.Canonical(), b.Rng.Canonical());
        Structural.AssertSame(a.Rng.Streams, b.Rng.Streams, "final RNG streams");
        Assert.Equal((a.Revision, a.LastEventSequence), (b.Revision, b.LastEventSequence));
        Assert.Equal(a.State.AppliedEvents, b.State.AppliedEvents);
        Structural.AssertSame(a.State.Plays, b.State.Plays, "Plays");
        Structural.AssertSame(a.State.Substitutions, b.State.Substitutions, "Substitutions");
        Assert.Equal(expected.Links.Count, actual.Links.Count);
        for (var i = 0; i < expected.Links.Count; i++)
        {
            var (x, y) = (expected.Links[i].Record, actual.Links[i].Record);
            Assert.Equal((x.Receipt, x.EventSequence, x.RevisionBefore, x.RevisionAfter, x.RngStateHashBefore, x.RngStateHashAfter, x.Finalized),
                (y.Receipt, y.EventSequence, y.RevisionBefore, y.RevisionAfter, y.RngStateHashBefore, y.RngStateHashAfter, y.Finalized));
            Structural.AssertSame(x.Event, y.Event, $"step {i} event");
            Structural.AssertSame(x.Facts, y.Facts, $"step {i} facts");
        }
    }

    /// <summary>The structural oracle itself: equal content in distinct instances passes; one changed nested field is found by path.</summary>
    [Fact]
    public void The_structural_oracle_accepts_equal_content_and_names_a_nested_difference()
    {
        var chain = CommitChain.Find(ChainScenarios.HalfChange);
        var state = chain.Cursor.Current.State;
        var copy = state with { Plays = [.. state.Plays], Away = state.Away with { PitcherUsages = [.. state.Away.PitcherUsages] } };
        Assert.NotSame(state.Plays, copy.Plays);
        Assert.Null(Structural.FirstDifference(state, copy));

        var usage = copy.Away.PitcherUsages[0];
        var changed = copy with { Away = copy.Away with { PitcherUsages = copy.Away.PitcherUsages.SetItem(0, usage with { PitchCount = usage.PitchCount + 1 }) } };
        Assert.Equal("$.Away.PitcherUsages[0].PitchCount: " + usage.PitchCount + " vs " + (usage.PitchCount + 1), Structural.FirstDifference(state, changed));
    }

    // ---------------------------------------------------------------- TC-07

    [Theory]
    [InlineData("mixed-base")]
    [InlineData("mixed-resolved")]
    [InlineData("half-change")]
    [InlineData("walk-off")]
    public void TC07_two_independent_cursors_given_the_same_inputs_end_identical(string scenario)
    {
        var found = CommitChain.Find(ChainScenarios.ByName(scenario));
        var one = CommitChain.Again(found.Scenario, found.Seed);
        var two = CommitChain.Again(found.Scenario, found.Seed);

        Assert.NotSame(one.Start, two.Start);                                                          // independent start states and cursors
        Assert.NotSame(one.Cursor.Current, two.Cursor.Current);
        Structural.AssertSame(one.Start, two.Start, "start State");
        AssertSameOutcome(one, two);
        AssertSameOutcome(found, one);
    }

    /// <summary>
    /// A cancelled Stage (cancelled inside the core after the reducer re-run) and a cancelled Publish (just before the swap) leave the cursor on the very same aggregate;
    /// staging and publishing again without cancellation continues the chain to exactly the uncancelled result. Covers an RNG-free C3-A step and the three C3-B kinds.
    /// </summary>
    [Theory]
    [InlineData(0)]   // pitching change (C3-A)
    [InlineData(1)]   // take (C3-B non-in-play)
    [InlineData(2)]   // steal (C3-A)
    [InlineData(4)]   // pitch (C3-B non-in-play)
    [InlineData(5)]   // in play, resolved (C3-B)
    public void TC07_cancelling_at_a_step_then_retrying_gives_the_uncancelled_result(int cancelAt)
    {
        var plain = CommitChain.Find(ChainScenarios.Mixed(true));

        var retried = CommitChain.Again(plain.Scenario, plain.Seed, cancelAt: cancelAt);

        Assert.Equal(1, retried.CancelDrills);
        ChainChecks.AssertChain(retried);
        AssertSameOutcome(plain, retried);
    }

    // ---------------------------------------------------------------- TC-08

    [Theory]
    [InlineData("mixed-base")]
    [InlineData("mixed-resolved")]
    [InlineData("half-change")]
    [InlineData("walk-off")]
    public void TC08_replaying_the_preserved_event_bodies_through_the_reducer_rebuilds_the_authoritative_state(string scenario)
    {
        var chain = CommitChain.Find(ChainScenarios.ByName(scenario));
        Assert.Same(chain.Start, chain.Initial.State);

        // only the reducer: no RNG is an input of the replay, nothing is drawn again
        var state = chain.Start;
        foreach (var link in chain.Links)
        {
            Assert.Same(link.Event, link.Record.Event);                                               // the body the fixture kept is the one the gate confirmed
            var step = Ok(GameReducer.Apply(state, link.Event));
            Assert.False(step.WasDuplicate);
            state = step.State;
            if (state.Status == GameStatus.Final)
            {
                var done = Ok(GameReducer.Finalize(state));                                           // only for a game-ending event, as the gate does
                Assert.True(done.WasNew);
                state = done.State;
            }

            Structural.AssertSame(link.After.State, state, $"replayed state after step {link.Index}");
        }

        var final = chain.Cursor.Current.State;
        Structural.AssertSame(final, state, "replayed final State");
        Assert.Equal(final.AppliedEvents.Select(r => r.EventId), state.AppliedEvents.Select(r => r.EventId));
        Assert.All(state.Plays, p => Assert.Contains(state.AppliedEvents, r => r.EventId == p.EventId));
        Assert.All(state.Substitutions, s => Assert.Contains(state.AppliedEvents, r => r.EventId == s.EventId));
        Assert.Equal(final.Final is not null, state.Final is not null);
    }

    // ---------------------------------------------------------------- TC-10

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TC10_the_real_published_aggregate_restores_and_a_new_cursor_continues_to_the_uninterrupted_result(bool resolved)
    {
        var plain = CommitChain.Find(ChainScenarios.Mixed(resolved));

        // four events (C3-A change, C3-B take, C3-A steal, C3-A approach) on the first cursor, then Restore and a new cursor for the other four
        var reopened = CommitChain.Again(plain.Scenario, plain.Seed, reopenBefore: 4);

        var published = reopened.Links[3].After;
        var restored = reopened.Reopened!;
        Assert.NotSame(published, restored);
        Assert.Equal((published.LastEventSequence, published.Revision), (restored.LastEventSequence, restored.Revision));
        Assert.Same(published.State, restored.State);
        Assert.Equal(published.Rng.Canonical(), restored.Rng.Canonical());
        Assert.Equal(published.State.AppliedEvents, restored.State.AppliedEvents);
        Assert.Null(restored.LastCommit);                                                              // O-6: a restored game has no last commit

        var first = reopened.Links[4];
        Assert.Same(restored, first.Before);                                                           // the continuation starts from the restored aggregate
        Assert.Equal((restored.Revision + 1, restored.LastEventSequence + 1), (first.After.Revision, first.After.LastEventSequence));
        Assert.Same(first.Record, first.After.LastCommit);
        Assert.Equal(restored.Rng.StateHash, first.Record.RngStateHashBefore);

        ChainChecks.AssertChain(reopened);
        AssertSameOutcome(plain, reopened);
    }
}
