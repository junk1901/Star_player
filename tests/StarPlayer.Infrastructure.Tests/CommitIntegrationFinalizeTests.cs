using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Simulation.Baseball;
using static StarPlayer.Infrastructure.Tests.Pc;
using K = StarPlayer.Infrastructure.Tests.CommitChain.Kind;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C3-C-2: game-ending publishes. TC-09 a regular third out that ends the game is finalized in the same publish (strikeout and in-play out; a first-inning third out is
/// only a half change); TC-11 cancellation of the in-play entry at every existing hook before the swap; TC-14 the premise of the provisional M25 NOTE on real ending paths.
/// </summary>
public class CommitIntegrationFinalizeTests
{
    // ---------------------------------------------------------------- fixed scenarios (literal step lists, CommitChain.MaxEvents bound)

    private static bool Strike(PitchBranchKind k) => k is PitchBranchKind.CalledStrike or PitchBranchKind.SwingingStrike or PitchBranchKind.Foul;

    /// <summary>
    /// Bottom of the 9th, away ahead 1-0, 2 out, bases empty: 1 approach; 2 and 3 a pitch that adds a strike (called, swinging or a foul before two strikes); 4 a called or
    /// swinging third strike: the batter is out on strikes, the third out, and the home team still trails, so the game ends.
    /// </summary>
    private static readonly ChainScenarios.Scenario Strikeout = new("third-out-strikeout", () => Contenders.AwayLeadsBottomNinth(2),
    [
        c => c.Approach(),
        c => c.Pitch(Strike),
        c => c.Pitch(Strike),
        c => c.Pitch(k => k is PitchBranchKind.CalledStrike or PitchBranchKind.SwingingStrike),
    ]);

    /// <summary>Bottom of the 9th, away ahead 1-0, 2 out, bases empty: 1 approach; 2 an in-play event that ends the game (with the bases empty only the third out can).</summary>
    private static readonly ChainScenarios.Scenario InPlayOut = new("third-out-in-play", () => Contenders.AwayLeadsBottomNinth(2),
    [
        c => c.Approach(),
        c => c.InPlay(x => x.UnconfirmedStep.State.Status == GameStatus.Final),
    ]);

    private static readonly Lazy<CommitChain> StrikeoutChain = new(() => CommitChain.Find(Strikeout));

    private static readonly Lazy<CommitChain> InPlayOutChain = new(() => CommitChain.Find(InPlayOut));

    private static CommitChain Ending(string name) => name switch
    {
        "strikeout" => StrikeoutChain.Value,
        "in-play-out" => InPlayOutChain.Value,
        "walk-off" => CommitChain.Find(ChainScenarios.WalkOff),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    // ---------------------------------------------------------------- TC-09

    [Theory]
    [InlineData("strikeout")]
    [InlineData("in-play-out")]
    public void TC09_a_regular_third_out_in_the_bottom_of_the_9th_ends_the_game_and_is_finalized_in_the_same_publish(string path)
    {
        var chain = Ending(path);
        ChainChecks.AssertChain(chain);                                                                // every step already passed AssertLink while the chain was built
        var last = chain.Links[^1];
        var (before, after, record) = (last.Before, last.After, last.Record);

        Assert.Equal(path == "strikeout" ? new[] { K.Approach, K.Pitch, K.Pitch, K.Pitch } : new[] { K.Approach, K.InPlay }, ChainChecks.Kinds(chain));
        Assert.All(chain.Links.SkipLast(1), l => Assert.Equal(GameStatus.InProgress, l.After.State.Status));
        Assert.Equal((GameStatus.InProgress, 2, 9, InningHalf.Bottom), (before.State.Status, before.State.Outs, before.State.Inning, before.State.Half));
        if (path == "strikeout")
            Assert.Equal(2, before.State.Count.Strikes);                                               // the deciding pitch is the third strike
        Assert.Null(before.State.Final);

        // one publish: the reducer's final state and its Finalize together
        Assert.True(record.Finalized);
        Assert.Equal((GameStatus.Final, (GameOutcome?)GameOutcome.AwayWin), (after.State.Status, after.State.Outcome));
        Assert.NotNull(after.State.Final);
        Assert.Equal((before.State.GameId, GameOutcome.AwayWin, 1, 0), (after.State.Final!.GameId, after.State.Final.Outcome, after.State.Final.AwayRuns, after.State.Final.HomeRuns));
        Assert.Equal((before.Revision + 1, before.LastEventSequence + 1, before.State.AppliedEvents.Count + 1),
            (after.Revision, after.LastEventSequence, after.State.AppliedEvents.Count));
        Assert.Same(record, after.LastCommit);
        Assert.Equal(record.Receipt, after.State.AppliedEvents[^1]);
        var completed = Assert.Single(record.Facts.OfType<GameCompleted>());
        Assert.Equal((after.State.GameId, GameOutcome.AwayWin, 1, 0), (completed.GameId, completed.Outcome, completed.AwayRuns, completed.HomeRuns));

        // no second publish is needed: Finalize on the published state is a no-op returning the very same final record
        var again = Ok(GameReducer.Finalize(after.State));
        Assert.False(again.WasNew);
        Assert.Same(after.State.Final, again.Game);
        Assert.Same(chain.Cursor.Current, after);

        // RNG: exactly the deciding pitch's verified H1 (or H2), and one pitch for the pitcher who threw it (AssertLink re-derived both; restated for this step)
        Assert.Same(last.Pitch!.Adopted, after.Rng);
        var thrower = (before.State.FieldingSide, before.State.Fielding.Pitcher);
        Assert.Equal(ChainChecks.PitchCounts(before.State)[thrower] + 1, ChainChecks.PitchCounts(after.State)[thrower]);
    }

    /// <summary>The contrast: a third out in the top of the 1st only changes the half (the reducer's own rule, no new policy); nothing is finalized.</summary>
    [Fact]
    public void TC09_a_first_inning_third_out_changes_the_half_and_is_not_a_game_end()
    {
        var chain = CommitChain.Find(ChainScenarios.HalfChange);
        var third = chain.Links[1];

        Assert.Equal((2, InningHalf.Top, 1), (third.Before.State.Outs, third.Before.State.Half, third.Before.State.Inning));
        Assert.Equal((InningHalf.Bottom, 0, GameStatus.InProgress), (third.After.State.Half, third.After.State.Outs, third.After.State.Status));
        Assert.False(third.Record.Finalized);
        Assert.Null(third.After.State.Final);
        Assert.Null(third.After.State.Outcome);
        Assert.Empty(third.Record.Facts.OfType<GameCompleted>());
    }

    // ---------------------------------------------------------------- TC-11

    private static readonly Lazy<(AuthoritativeGame Game, Contender InPlay)> BaseInPlay =
        new(() => Contenders.FindInPlay(Situation(0, 0, approach: true), resolved: false, c => !Contenders.Ends(c)));

    private static readonly Lazy<(AuthoritativeGame Game, Contender InPlay)> ResolvedInPlay =
        new(() => Contenders.FindInPlay(Situation(1, 0, approach: true), resolved: true, c => !Contenders.Ends(c)));

    private static readonly Lazy<(AuthoritativeGame Game, Contender InPlay)> EndingInPlay =
        new(() => Contenders.FindInPlay(Situation(4, 0, approach: true, tiedBottomNinth: true), resolved: false, Contenders.Ends));

    private static (AuthoritativeGame Game, Contender InPlay) InPlayCase(string name) => name switch
    {
        "base" => BaseInPlay.Value,
        "resolved" => ResolvedInPlay.Value,
        "ending" => EndingInPlay.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static void AssertNothingChanged(GameCursor cursor, AuthoritativeGame g)
    {
        Assert.Same(g, cursor.Current);                                                                // the shared reference itself, not only its (immutable) fields
        var now = cursor.Current;
        Assert.Equal((g.Revision, g.LastEventSequence, g.State.AppliedEvents.Count), (now.Revision, now.LastEventSequence, now.State.AppliedEvents.Count));
        Assert.Same(g.State, now.State);
        Assert.Same(g.Rng, now.Rng);
        Assert.Same(g.LastCommit, now.LastCommit);
        Assert.Null(now.State.Final);
        Assert.Equal(GameStatus.InProgress, now.State.Status);
    }

    /// <summary>After a cancellation the same candidate, with the same (unconsumed) next id, stages and publishes normally and passes every shared invariant.</summary>
    private static void AssertRetrySucceeds(GameCursor cursor, AuthoritativeGame g, Contender c)
    {
        Assert.Equal(Contenders.NextId(g), c.Event.Id);
        var staged = Ok(c.Stage(g, null, CancellationToken.None));
        var record = Ok(cursor.Publish(staged));
        ChainChecks.AssertLink(c.Link(g, staged, record));
    }

    [Theory]
    [InlineData("base")]
    [InlineData("resolved")]
    [InlineData("ending")]
    public void TC11_an_in_play_stage_with_an_already_cancelled_token_throws_at_start_and_changes_nothing(string name)
    {
        var (g, c) = InPlayCase(name);
        var cursor = new GameCursor(g);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var seen = new List<string>();

        Assert.Throws<OperationCanceledException>(() => c.Stage(g, seen.Add, cts.Token));
        Assert.Equal([CommitGate.StageName.Start], seen);
        AssertNothingChanged(cursor, g);
        AssertRetrySucceeds(cursor, g, c);
    }

    /// <summary>
    /// Cancelled at each existing stage hook of the in-play entry (the common core's <c>validated</c>, <c>applied</c>, and <c>finalized</c> for a game-ending candidate): the stage
    /// throws, nothing after the hook ran, the cursor still holds the very same aggregate, and a retry gives the normal result.
    /// </summary>
    [Theory]
    [InlineData("base", "validated")]
    [InlineData("base", "applied")]
    [InlineData("resolved", "validated")]
    [InlineData("resolved", "applied")]
    [InlineData("ending", "applied")]
    [InlineData("ending", "finalized")]
    public void TC11_an_in_play_stage_cancelled_at_an_inner_hook_throws_and_changes_nothing(string name, string stage)
    {
        var (g, c) = InPlayCase(name);
        var cursor = new GameCursor(g);
        using var cts = new CancellationTokenSource();
        var seen = new List<string>();

        void Hook(string at)
        {
            seen.Add(at);
            if (at == stage)
                cts.Cancel();
        }

        Assert.Throws<OperationCanceledException>(() => c.Stage(g, Hook, cts.Token));
        Assert.Equal(stage, seen[^1]);                                                                 // nothing ran after the cancelled stage
        if (stage == CommitGate.StageName.Finalized)
            Assert.Equal([CommitGate.StageName.Start, CommitGate.StageName.Validated, CommitGate.StageName.Applied, CommitGate.StageName.Finalized], seen);
        AssertNothingChanged(cursor, g);
        AssertRetrySucceeds(cursor, g, c);
    }

    [Theory]
    [InlineData("base")]
    [InlineData("resolved")]
    [InlineData("ending")]
    public void TC11_an_in_play_publish_cancelled_at_before_swap_changes_nothing_and_the_staged_commit_still_publishes(string name)
    {
        var (g, c) = InPlayCase(name);
        var cursor = new GameCursor(g);
        var staged = Ok(c.Stage(g, null, CancellationToken.None));
        Assert.Equal(name == "ending", staged.Record.Finalized);
        using var cts = new CancellationTokenSource();
        var seen = new List<string>();

        Assert.Throws<OperationCanceledException>(() => cursor.Publish(staged, at => { seen.Add(at); cts.Cancel(); }, cts.Token));
        Assert.Equal([GameCursor.Stage.BeforeSwap], seen);
        AssertNothingChanged(cursor, g);                                                               // a finalized staged aggregate was not exposed either

        var record = Ok(cursor.Publish(staged));                                                       // the staged commit was not consumed
        ChainChecks.AssertLink(c.Link(g, staged, record));
    }

    /// <summary>
    /// After the swap nothing consults the token (GameCursor.Publish: the token is checked once, right after the before_swap hook, and the compare-and-swap is the last
    /// statement). No hook exists after the swap, so a cancellation INSIDE that window cannot be injected without a new production hook; this pins only what is observable:
    /// a token cancelled after a successful publish does not turn it into a failure.
    /// </summary>
    [Fact]
    public void TC11_a_token_cancelled_after_a_successful_ending_publish_changes_nothing()
    {
        var (g, c) = InPlayCase("ending");
        var cursor = new GameCursor(g);
        using var cts = new CancellationTokenSource();
        var staged = Ok(c.Stage(g, null, cts.Token));

        var record = Ok(cursor.Publish(staged, cts.Token));
        cts.Cancel();
        Assert.Same(staged.Next, cursor.Current);
        Assert.Same(record, cursor.Current.LastCommit);
        Assert.NotNull(cursor.Current.State.Final);
        Assert.Equal("Commit.revision_stale", Code(cursor.Publish(staged, cts.Token)));               // the freshness check precedes the token check
    }

    // ---------------------------------------------------------------- TC-14 (pins the premise of the provisional M25 NOTE; not a mutation result)

    /// <summary>
    /// On each real ending path the gate's post-Finalize defence (<c>!WasNew || Final is null || Final not the returned game || !IsComplete</c>, CommitGate step 11) has every
    /// operand false: Finalize of the reducer's final state makes a NEW state carrying a NEW final record that is the returned game, of the same game, outcome and score, and complete.
    /// The published aggregate carries exactly that record. This does not kill M25 and does not prove the branch unreachable in general.
    /// </summary>
    [Theory]
    [InlineData("walk-off")]
    [InlineData("strikeout")]
    [InlineData("in-play-out")]
    public void TC14_on_a_real_ending_path_finalize_returns_a_new_complete_state_with_its_final_record(string path)
    {
        var last = Ending(path).Links[^1];
        Assert.True(last.Record.Finalized);

        var applied = Ok(GameReducer.Apply(last.Before.State, last.Event));                              // the gate's step 9 input to step 11, recomputed
        Assert.Equal(GameStatus.Final, applied.State.Status);
        Assert.Null(applied.State.Final);                                                               // the reducer alone does not finalize
        Assert.NotNull(applied.State.Outcome);

        var done = Ok(GameReducer.Finalize(applied.State));
        Assert.True(done.WasNew);
        Assert.NotSame(applied.State, done.State);
        Assert.NotNull(done.State.Final);
        Assert.Same(done.Game, done.State.Final);
        Assert.Equal((applied.State.GameId, applied.State.Outcome, applied.State.AwayRuns, applied.State.HomeRuns),
            (done.Game.GameId, (GameOutcome?)done.Game.Outcome, done.Game.AwayRuns, done.Game.HomeRuns));
        ChainChecks.AssertComplete(done.State, path);                                                    // restated rule
        Assert.True(AuthoritativeGame.IsComplete(done.State));                                          // the very predicate of the defence, pinned as a premise

        // the publish kept that record: structurally the same final state and record, and Finalize on it is idempotent
        Structural.AssertSame(done.State, last.After.State, "published final State");
        Structural.AssertSame(done.Game, last.After.State.Final, "published Final");
        var again = Ok(GameReducer.Finalize(last.After.State));
        Assert.False(again.WasNew);
        Assert.Same(last.After.State, again.State);
        Assert.Same(last.After.State.Final, again.Game);
    }
}
