using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.Pc;
using K = StarPlayer.Infrastructure.Tests.CommitChain.Kind;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C3-C-1 (game-engine 27.13 C3-C, 27.20 OD-2): C3-A and C3-B official events confirmed one after another on ONE <see cref="GameCursor"/> through the real gates,
/// each prepared from the then-current aggregate (<see cref="CommitChain"/>). TC-01 mixed chains, TC-02 RNG accumulation, TC-03 the shared invariants, TC-15 the pitch-count
/// premise of the provisional M7 NOTE. Every per-step invariant (<see cref="ChainChecks.AssertLink"/>) already ran while the chain was built; these tests add the chain-level facts.
/// </summary>
public class CommitIntegrationChainTests
{
    private static readonly K[] MixedKinds = [K.PitchingChange, K.Take, K.Steal, K.Approach, K.Pitch, K.InPlay, K.Approach, K.Pitch];

    // ---------------------------------------------------------------- TC-01

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TC01_mixed_C3A_and_C3B_events_are_published_in_turn_on_one_cursor(bool resolved)
    {
        var chain = CommitChain.Find(ChainScenarios.Mixed(resolved));

        Assert.Equal(MixedKinds, ChainChecks.Kinds(chain));
        Assert.Equal(resolved, chain.Links.Single(l => l.Kind == K.InPlay).Pitch!.Detail is not null);   // the base or the resolved in-play path
        ChainChecks.AssertChain(chain);
        var final = chain.Cursor.Current;
        Assert.Equal((CommitChain.StartRevision + 8, chain.StartLast + 8), (final.Revision, final.LastEventSequence));
        Assert.Equal(GameStatus.InProgress, final.State.Status);
    }

    [Fact]
    public void TC01_a_chain_across_a_half_inning_change_keeps_one_cursor_and_contiguous_ids()
    {
        var chain = CommitChain.Find(ChainScenarios.HalfChange);

        Assert.Equal([K.Approach, K.InPlay, K.Approach, K.Pitch], ChainChecks.Kinds(chain));
        ChainChecks.AssertChain(chain);
        Assert.Equal(InningHalf.Bottom, chain.Cursor.Current.State.Half);
    }

    [Fact]
    public void TC01_a_game_ending_chain_finalizes_in_the_last_publish_only()
    {
        var chain = CommitChain.Find(ChainScenarios.WalkOff);

        Assert.Equal([K.Approach, K.Pitch, K.InPlay], ChainChecks.Kinds(chain));
        ChainChecks.AssertChain(chain);
        Assert.Equal([false, false, true], chain.Links.Select(l => l.Record.Finalized));
        var final = chain.Cursor.Current;
        Assert.Equal((GameStatus.Final, (GameOutcome?)GameOutcome.HomeWin), (final.State.Status, final.State.Outcome));
        Assert.NotNull(final.State.Final);
        Assert.Equal(CommitChain.StartRevision + 3, final.Revision);                                  // Apply + Finalize were one publish
    }

    /// <summary>
    /// D-6 / O-2 step 5: each earlier submission sent again UNCHANGED (same event, same prepared pitch and candidate, same sheets) against the final aggregate is a legal
    /// identical re-submission: its id is used and its receipt equals the stored one, so <c>already_committed</c> wins over every freshness code. An old staged commit
    /// is <c>revision_stale</c> at Publish. Neither changes anything.
    /// </summary>
    [Theory]
    [InlineData("mixed-base")]
    [InlineData("mixed-resolved")]
    [InlineData("half-change")]
    [InlineData("walk-off")]
    public void TC01_every_earlier_submission_resent_after_the_chain_is_already_committed_and_changes_nothing(string scenario)
    {
        var chain = CommitChain.Find(ChainScenarios.ByName(scenario));
        var final = chain.Cursor.Current;

        foreach (var link in chain.Links)
        {
            var again = link.Restage(final, null, CancellationToken.None);
            Assert.True(Code(again) == "Commit.already_committed", $"step {link.Index} ({link.Kind}): {Code(again)} {Message(again)}");
            var stale = chain.Cursor.Publish(link.Staged);
            Assert.True(Code(stale) == "Commit.revision_stale", $"step {link.Index} ({link.Kind}): {Code(stale)}");
            Assert.Same(final, chain.Cursor.Current);
        }

        Assert.Same(chain.Links[^1].Record, final.LastCommit);
    }

    // ---------------------------------------------------------------- TC-02

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TC02_the_rng_moves_only_by_each_step_s_recorded_raw_draws_and_the_end_is_h0_plus_their_sum(bool resolved)
    {
        var chain = CommitChain.Find(ChainScenarios.Mixed(resolved));
        var links = chain.Links;

        for (var i = 0; i < links.Count; i++)
        {
            var link = links[i];
            var h0 = i == 0 ? chain.Initial.Rng : links[i - 1].After.Rng;
            Assert.Same(h0, link.Before.Rng);                                                          // each step's H0 is the previous publish's position, never a stale one
            if (link.Pitch is null)
            {
                Assert.Same(h0, link.After.Rng);                                                       // RNG-free: no draw, same reference
                Assert.Equal(link.Record.RngStateHashBefore, link.Record.RngStateHashAfter);
                continue;
            }

            var h1 = link.Pitch.Prepared.Draft.NextRng;
            Assert.Same(link.Pitch.Detail is null ? h1 : link.Pitch.InPlay!.NextRng, link.After.Rng);  // H1 adopted, or H2 for the resolved in-play
            Assert.True(link.Usage.All(u => u.Raw >= 1));
        }

        // stream by stream: every raw draw of the chain is accounted for exactly once (no double application, no omission)
        var start = chain.Initial.Rng.Streams.ToDictionary(s => s.Stream.Canonical, s => s.Draws);
        var end = chain.Cursor.Current.Rng.Streams.ToDictionary(s => s.Stream.Canonical, s => s.Draws);
        var recorded = links.SelectMany(l => l.Usage).GroupBy(u => u.Stream.Canonical).ToDictionary(g => g.Key, g => g.Sum(u => u.Raw));
        Assert.Equal(recorded.Keys.Union(start.Keys).Order(StringComparer.Ordinal), end.Keys.Order(StringComparer.Ordinal));
        foreach (var (stream, draws) in end)
            Assert.Equal(start.GetValueOrDefault(stream) + recorded.GetValueOrDefault(stream), draws);
        if (resolved)
            Assert.True(recorded[GameRngStreams.Runner.Canonical] > 0);

        var independent = Advance(chain.Initial.Rng, links.SelectMany(l => l.Usage).ToArray());
        Assert.Equal(independent.Canonical(), chain.Cursor.Current.Rng.Canonical());
        Assert.Equal(independent.StateHash, chain.Cursor.Current.Rng.StateHash);
        Assert.Equal((4, 4), (links.Count(l => l.Pitch is null), links.Count(l => l.Pitch is not null)));  // RNG-free, pitch-carrying
    }

    // ---------------------------------------------------------------- TC-03

    [Theory]
    [InlineData("mixed-base")]
    [InlineData("mixed-resolved")]
    [InlineData("half-change")]
    [InlineData("walk-off")]
    public void TC03_every_published_aggregate_satisfies_the_shared_invariants_and_restores_to_itself(string scenario)
    {
        var chain = CommitChain.Find(ChainScenarios.ByName(scenario));

        ChainChecks.AssertAggregate(chain.Initial, "initial");
        foreach (var link in chain.Links)
        {
            ChainChecks.AssertAggregate(link.After, $"step {link.Index}");
            var restored = ChainChecks.AssertRestoreRoundTrip(link.After, $"step {link.Index}");
            Assert.Equal(link.After.State.AppliedEvents, restored.State.AppliedEvents);
            Assert.Equal(link.After.Rng.Canonical(), restored.Rng.Canonical());
        }

        ChainChecks.AssertChain(chain);
    }

    // ---------------------------------------------------------------- TC-15 (pins the premise of the provisional M7 NOTE; not a mutation result)

    [Fact]
    public void TC15_after_a_pitching_change_every_take_pitch_and_in_play_counts_one_pitch_for_the_new_pitcher_only()
    {
        var chain = CommitChain.Find(ChainScenarios.Mixed(false));
        var hp = (TeamSide.Home, PlayerId.Create("hp").Value);
        var hb1 = (TeamSide.Home, PlayerId.Create("hb1").Value);
        var counts = chain.Links.Select(l => ChainChecks.PitchCounts(l.After.State)).ToArray();
        var hpBefore = ChainChecks.PitchCounts(chain.Initial.State)[hp];

        Assert.Equal(0, counts[0][hb1]);                                                               // the change itself counts nothing
        Assert.Equal([0, 1, 1, 1, 2, 3, 3, 4], counts.Select(c => c[hb1]));                            // Take, Pitch, InPlay, Pitch: +1 each; RNG-free steps: 0
        Assert.All(counts, c => Assert.Equal(hpBefore, c[hp]));                                        // the replaced pitcher's count is frozen
    }

    [Fact]
    public void TC15_an_in_play_out_that_flips_the_half_counts_its_pitch_for_the_pitcher_who_threw_it()
    {
        var chain = CommitChain.Find(ChainScenarios.HalfChange);
        var inPlay = chain.Links[1];
        var hp = (TeamSide.Home, PlayerId.Create("hp").Value);
        var ap = (TeamSide.Away, PlayerId.Create("ap").Value);

        Assert.Equal((InningHalf.Top, InningHalf.Bottom), (inPlay.Before.State.Half, inPlay.After.State.Half));
        Assert.NotEqual(inPlay.Before.State.FieldingSide, inPlay.After.State.FieldingSide);
        var before = ChainChecks.PitchCounts(inPlay.Before.State);
        var after = ChainChecks.PitchCounts(inPlay.After.State);
        Assert.Equal(before[hp] + 1, after[hp]);                                                       // anchored to the fielding pitcher BEFORE the event
        Assert.Equal(before.GetValueOrDefault(ap), after.GetValueOrDefault(ap));

        var pitch = chain.Links[3];                                                                    // next half: the away pitcher throws
        Assert.Equal(ap.Item2, pitch.Before.State.Fielding.Pitcher);
        Assert.Equal(ChainChecks.PitchCounts(pitch.Before.State).GetValueOrDefault(ap) + 1, ChainChecks.PitchCounts(pitch.After.State)[ap]);
        Assert.Equal(after[hp], ChainChecks.PitchCounts(pitch.After.State)[hp]);
    }

    [Fact]
    public void TC15_the_game_ending_pitch_is_counted_once_in_the_finalized_state()
    {
        var chain = CommitChain.Find(ChainScenarios.WalkOff);
        var last = chain.Links[^1];
        var thrower = (last.Before.State.FieldingSide, last.Before.State.Fielding.Pitcher);

        Assert.True(last.Record.Finalized);
        Assert.Equal(ChainChecks.PitchCounts(last.Before.State)[thrower] + 1, ChainChecks.PitchCounts(last.After.State)[thrower]);
    }
}
