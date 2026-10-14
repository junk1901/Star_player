using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;
using K = StarPlayer.Infrastructure.Tests.CommitChain.Kind;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-C3-C-2: one official event offered to <see cref="CommitChain"/>'s invariants WITHOUT being published, so two of them can be staged from the same aggregate first and
/// published afterwards (in a race or in a chosen order). The pitch-carrying ones are real candidates from a bounded seed scan over a fixed situation; nothing is forged.
/// </summary>
internal sealed record Contender(K Kind, GameEvent Event, Stager Stage, (RngStreamId Stream, long Raw)[] Usage, CommitChain.PitchSource? Pitch)
{
    public EventReceipt Receipt => EventFingerprint.Receipt(Event);

    /// <summary>The H1/H2 position this contender would adopt; the aggregate's own position for an RNG-free event.</summary>
    public GameRandomSnapshot? Adopts => Pitch?.Adopted;

    public CommitChain.Link Link(AuthoritativeGame before, StagedCommit staged, CommitRecord record) =>
        new(0, Kind, Event, before, staged.Next, staged, record, Usage, Pitch, Stage);

    public override string ToString() => Kind.ToString();
}

internal static class Contenders
{
    public static string NextId(AuthoritativeGame g) => ChainChecks.IdOf(g.LastEventSequence + 1);

    public static Contender Approach(AuthoritativeGame g) => Plain(K.Approach, new ApproachEvent(NextId(g), ApproachKind.Normal));

    public static Contender PitchingChange(AuthoritativeGame g, string player) =>
        Plain(K.PitchingChange, new SubstitutionEvent(NextId(g), SubstitutionKind.PitchingChange, g.State.FieldingSide, PlayerId.Create(player).Value));

    public static Contender Steal(AuthoritativeGame g, int from) =>
        Plain(K.Steal, new RunnerPlayEvent(NextId(g), RunnerPlayKind.Steal, ImmutableList.Create(new RunnerMove(from, from + 1, MoveCause.Steal))));

    private static Contender Plain(K kind, GameEvent e) => new(kind, e, (a, hook, ct) => CommitGate.Stage(a, e, hook, ct), [], null);

    public static Contender NonInPlay(AuthoritativeGame g, PreparedPitch prepared, NonInPlayPitchCandidate candidate)
    {
        var (b, p) = Sheets(g.State);
        return new(candidate.Event is TakeEvent ? K.Take : K.Pitch, candidate.Event, (a, hook, ct) => PitchCommitGate.Stage(a, prepared, candidate, Tuning, b, p, hook, ct),
            Usage(prepared.Draft), new CommitChain.PitchSource(prepared, candidate.NextRng, null));
    }

    public static Contender InPlay(AuthoritativeGame g, InPlayPitchCandidate candidate)
    {
        var (b, p) = Sheets(g.State);
        var prepared = candidate.Plan.Prepared;
        var usage = Usage(prepared.Draft).ToList();
        if (candidate.Audit.AdvancementDetail is { } detail)
            usage.Add((GameRngStreams.Runner, detail.RawDraws));
        return new(K.InPlay, candidate.Event, (a, hook, ct) => PitchCommitGate.Stage(a, candidate, Tuning, b, p, hook, ct), [.. usage],
            new CommitChain.PitchSource(prepared, candidate.NextRng, candidate));
    }

    // ---------------------------------------------------------------- fixed situations, bounded seed scans (the existing Pc.Find* pattern)

    /// <summary>The first seed in 1..<paramref name="maxSeeds"/> whose real non-in-play candidate (pitch or explicit take) the filter accepts; the aggregate is at <see cref="CommitChain.StartRevision"/>.</summary>
    public static (AuthoritativeGame Game, Contender Pitch) FindNonInPlay(Driver d, bool take, Func<NonInPlayPitchCandidate, bool> want, ulong maxSeeds = 20000)
    {
        for (ulong seed = 1; seed <= maxSeeds; seed++)
        {
            var g = Aggregate(d, seed, PriorDraws, CommitChain.StartRevision);
            var prepared = Prepare(g, take);
            if (prepared.Draft.Kind == PitchBranchKind.InPlay || (take && prepared.Draft.Kind is PitchBranchKind.SwingingStrike or PitchBranchKind.Foul))
                continue;
            if (BuildNonInPlay(g, prepared) is { IsSuccess: true } built && want(built.Value))
                return (g, NonInPlay(g, prepared, built.Value));
        }

        throw new InvalidOperationException($"no seed up to {maxSeeds} produced the wanted non-in-play candidate");
    }

    /// <summary>The first seed whose real in-play candidate, on the base path or (with <paramref name="resolved"/>) after the real resolver, the filter accepts.</summary>
    public static (AuthoritativeGame Game, Contender InPlay) FindInPlay(Driver d, bool resolved, Func<InPlayPitchCandidate, bool> want, ulong maxSeeds = 60000)
    {
        for (ulong seed = 1; seed <= maxSeeds; seed++)
        {
            var g = Aggregate(d, seed, PriorDraws, CommitChain.StartRevision);
            var prepared = Prepare(g);
            if (prepared.Draft.Kind != PitchBranchKind.InPlay || BuildInPlay(g, prepared) is not { IsSuccess: true } built)
                continue;
            InPlayPitchCandidate? candidate = built.Value switch
            {
                InPlayPitchCandidate c when !resolved => c,
                PendingAdvancement pending when resolved && Resolve(g, pending) is { IsSuccess: true } r => r.Value,
                _ => null,
            };
            if (candidate is not null && want(candidate))
                return (g, InPlay(g, candidate));
        }

        throw new InvalidOperationException($"no seed up to {maxSeeds} produced the wanted in-play candidate");
    }

    public static bool Ends(NonInPlayPitchCandidate c) => c.UnconfirmedStep.State.Status == GameStatus.Final;

    public static bool Ends(InPlayPitchCandidate c) => c.UnconfirmedStep.State.Status == GameStatus.Final;

    /// <summary>Bottom of the 9th, 0-0, bases loaded, 0 out, new plate appearance: a hit by pitch forces the winning run home; a pitching change (by the away side) is legal.</summary>
    public static Driver LoadedTiedBottomNinth() => Situation(7, 0, approach: false, tiedBottomNinth: true);

    /// <summary>Bottom of the 9th, the away team ahead 1-0 (a top-of-the-1st home run), bases empty, <paramref name="outs"/> out, new plate appearance.</summary>
    public static Driver AwayLeadsBottomNinth(int outs)
    {
        var d = new Driver(StartState(NonAutomated, Gid), Gid).HomeRun();
        for (var i = 0; i < 17; i++)
            d.Outs(3);
        d.Outs(outs);
        Assert.Equal((9, InningHalf.Bottom, 1, 0, outs, PaStage.Start), (d.State.Inning, d.State.Half, d.State.AwayRuns, d.State.HomeRuns, d.State.Outs, d.State.Stage));
        return d;
    }

    // ---------------------------------------------------------------- the outcome of two staged commits of the same base

    public sealed record Entry(Contender Contender, StagedCommit Staged, Result<CommitRecord> Result);

    /// <summary>Both contenders staged from the very same aggregate BEFORE either is published; both stages succeed and change nothing.</summary>
    public static (StagedCommit A, StagedCommit B) StageBoth(GameCursor cursor, Contender a, Contender b)
    {
        var expected = cursor.Current;
        Assert.Equal(a.Event.Id, b.Event.Id);                                                          // the same next official id
        Assert.Equal(NextId(expected), a.Event.Id);
        Assert.NotEqual(a.Receipt, b.Receipt);                                                         // two different events
        var stagedA = Ok(a.Stage(expected, null, CancellationToken.None));
        var stagedB = Ok(b.Stage(expected, null, CancellationToken.None));
        Assert.Same(expected, cursor.Current);
        Assert.Same(expected, stagedA.Expected);
        Assert.Same(expected, stagedB.Expected);
        Assert.Equal((expected.Revision + 1, expected.Revision + 1), (stagedA.Next.Revision, stagedB.Next.Revision));
        Assert.NotSame(stagedA.Next, stagedB.Next);
        return (stagedA, stagedB);
    }

    /// <summary>
    /// Exactly one publish succeeded and the cursor holds exactly its staged aggregate: the shared per-commit invariants of C3-C-1 hold for the winner, and nothing of the loser
    /// (state, receipt, RNG position, record) is visible. The loser's later publish is stale and changes nothing. Returns the winner.
    /// </summary>
    public static Entry AssertOneWinner(AuthoritativeGame before, GameCursor cursor, string loserCode, params Entry[] entries)
    {
        Assert.Single(entries, e => e.Result.IsSuccess);
        var winner = entries.Single(e => e.Result.IsSuccess);
        var loser = entries.Single(e => !e.Result.IsSuccess);
        Assert.True(Code(loser.Result) == loserCode, $"loser {loser.Contender}: {Code(loser.Result)} {Message(loser.Result)}");

        var now = cursor.Current;
        Assert.Same(winner.Staged.Next, now);
        Assert.Same(winner.Result.Value, now.LastCommit);                                               // O-9: the returned record IS LastCommit
        ChainChecks.AssertLink(winner.Contender.Link(before, winner.Staged, winner.Result.Value));     // Revision/sequence/receipt +1, RNG, count, completeness, Finalize
        ChainChecks.AssertAggregate(now, "winner");

        // the winner's content and only it: the published state equals the reducer's own result for the winner's event (and its Finalize), computed independently
        var replay = Ok(GameReducer.Apply(before.State, winner.Contender.Event)).State;
        if (replay.Status == GameStatus.Final)
            replay = Ok(GameReducer.Finalize(replay)).State;
        Structural.AssertSame(replay, now.State, "published State vs the winner's own reducer result");
        Assert.NotNull(Structural.FirstDifference(loser.Staged.Next.State, now.State));
        Assert.Equal(winner.Contender.Receipt, now.State.AppliedEvents[^1]);
        Assert.DoesNotContain(loser.Contender.Receipt, now.State.AppliedEvents);
        Assert.NotSame(loser.Staged.Record, now.LastCommit);
        Assert.NotSame(loser.Staged.Next, now);

        // the RNG is the winner's: the same reference for an RNG-free winner, its verified H1/H2 otherwise; never the loser's draws
        Assert.Same(winner.Contender.Adopts ?? before.Rng, now.Rng);
        if (loser.Contender.Adopts is { } lost)
            Assert.NotEqual(lost.StateHash, now.Rng.StateHash);

        // never a second revision: the loser, published again, is stale
        Assert.Equal("Commit.revision_stale", Code(cursor.Publish(loser.Staged)));
        Assert.Same(now, cursor.Current);
        Assert.Equal((before.Revision + 1, before.LastEventSequence + 1, before.State.AppliedEvents.Count + 1),
            (cursor.Current.Revision, cursor.Current.LastEventSequence, cursor.Current.State.AppliedEvents.Count));
        return winner;
    }
}

/// <summary>
/// Phase 3-C3-C-2 (game-engine 27.16 O-3/O-5/O-9, 27.18 B-04, D-5): a C3-A event and a C3-B candidate staged from the SAME aggregate race to the one compare-and-swap of a
/// <see cref="GameCursor"/>. TC-04 mixed CAS race, TC-05 a game-ending candidate against an ordinary event, TC-06 a publish staged before the end and tried after it.
/// <para>
/// Races are held at the existing <c>before_swap</c> hook with a <see cref="Barrier"/> (both publishers pass the freshness check, then are released together), never with Sleep.
/// Which one wins is not fixed: every assertion depends on the observed winner. The ordered tests cover each winner deterministically.
/// </para>
/// </summary>
public class CommitIntegrationRaceTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

    private const int Rounds = 8;

    /// <summary>
    /// A: new plate appearance (<see cref="PaStage.Start"/>), bases empty: approach (C3-A, legal at Start) vs explicit Take (C3-B, legal at Start).
    /// B: approach chosen (<see cref="PaStage.InSequence"/>), runner on first: steal of second (C3-A, a runner play has no stage restriction) vs pitch (C3-B, legal InSequence).
    /// </summary>
    private static readonly Lazy<(AuthoritativeGame Game, Contender A, Contender B)> ApproachVsTake = new(() =>
    {
        var (g, take) = Contenders.FindNonInPlay(Situation(0, 0, approach: false), take: true, c => !Contenders.Ends(c));
        return (g, Contenders.Approach(g), take);
    });

    private static readonly Lazy<(AuthoritativeGame Game, Contender A, Contender B)> StealVsPitch = new(() =>
    {
        var (g, pitch) = Contenders.FindNonInPlay(Situation(1, 0, approach: true), take: false, c => !Contenders.Ends(c));
        return (g, Contenders.Steal(g, 1), pitch);
    });

    /// <summary>TC-05/06: bases loaded, tied, bottom 9th, Start: a game-ending explicit-Take hit by pitch (C3-B) vs a pitching change by the away side (C3-A).</summary>
    private static readonly Lazy<(AuthoritativeGame Game, Contender Ending, Contender Normal)> EndingVsChange = new(() =>
    {
        // only the kind is filtered: that this hit by pitch ends the game (a forced run) is asserted by each test from the staged commit, not assumed
        var (g, hbp) = Contenders.FindNonInPlay(Contenders.LoadedTiedBottomNinth(), take: true, c => c.Kind == PitchBranchKind.HitByPitch);
        return (g, hbp, Contenders.PitchingChange(g, "ab1"));
    });

    private static (AuthoritativeGame Game, Contender A, Contender B) Pair(string scenario) => scenario switch
    {
        "approach-vs-take" => ApproachVsTake.Value,
        "steal-vs-pitch" => StealVsPitch.Value,
        "ending-vs-change" => EndingVsChange.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
    };

    private static async Task<Contenders.Entry[]> Race(GameCursor cursor, Contender a, StagedCommit stagedA, Contender b, StagedCommit stagedB)
    {
        using var barrier = new Barrier(2);
        void Hold(string stage)
        {
            Assert.Equal(GameCursor.Stage.BeforeSwap, stage);
            Assert.True(barrier.SignalAndWait(Wait), "both publishers must reach the swap together");
        }

        var results = await Task.WhenAll(Task.Run(() => cursor.Publish(stagedA, Hold, default)), Task.Run(() => cursor.Publish(stagedB, Hold, default))).WaitAsync(Wait * 2);
        return [new(a, stagedA, results[0]), new(b, stagedB, results[1])];
    }

    // ---------------------------------------------------------------- TC-04

    [Theory]
    [InlineData("approach-vs-take")]
    [InlineData("steal-vs-pitch")]
    public async Task TC04_a_C3A_event_and_a_C3B_candidate_staged_from_one_aggregate_race_and_exactly_one_is_published(string scenario)
    {
        var (g, a, b) = Pair(scenario);
        Assert.Null(a.Pitch);                                                                          // A is the RNG-free C3-A event, B the C3-B candidate
        Assert.NotNull(b.Pitch);
        var winners = new List<K>();

        for (var round = 0; round < Rounds; round++)
        {
            var cursor = new GameCursor(g);
            var (stagedA, stagedB) = Contenders.StageBoth(cursor, a, b);
            var entries = await Race(cursor, a, stagedA, b, stagedB);

            var winner = Contenders.AssertOneWinner(g, cursor, "Commit.cas_lost", entries);           // both passed the freshness check, so the loser lost the swap itself
            Assert.Equal(GameStatus.InProgress, cursor.Current.State.Status);
            winners.Add(winner.Contender.Kind);
        }

        Assert.Equal(Rounds, winners.Count);                                                           // which one won is reported, not fixed
    }

    /// <summary>The same pair published one after the other in each order: the first is published, the second is <c>revision_stale</c>; covers both winners deterministically.</summary>
    [Theory]
    [InlineData("approach-vs-take", true)]
    [InlineData("approach-vs-take", false)]
    [InlineData("steal-vs-pitch", true)]
    [InlineData("steal-vs-pitch", false)]
    public void TC04_in_either_publish_order_the_first_is_published_and_the_second_is_stale(string scenario, bool c3aFirst)
    {
        var (g, a, b) = Pair(scenario);
        var cursor = new GameCursor(g);
        var (stagedA, stagedB) = Contenders.StageBoth(cursor, a, b);

        var first = c3aFirst ? new Contenders.Entry(a, stagedA, cursor.Publish(stagedA)) : new Contenders.Entry(b, stagedB, cursor.Publish(stagedB));
        var second = c3aFirst ? new Contenders.Entry(b, stagedB, cursor.Publish(stagedB)) : new Contenders.Entry(a, stagedA, cursor.Publish(stagedA));

        var winner = Contenders.AssertOneWinner(g, cursor, "Commit.revision_stale", first, second);
        Assert.Same(first.Contender, winner.Contender);
        Assert.Same(c3aFirst ? g.Rng : b.Adopts, cursor.Current.Rng);                                  // RNG-free winner keeps the reference; the C3-B winner adopts its H1
    }

    // ---------------------------------------------------------------- TC-05

    private static void AssertEndingOrNormal(AuthoritativeGame g, Contenders.Entry winner, Contender ending, Contender normal, GameCursor cursor)
    {
        var now = cursor.Current;
        if (ReferenceEquals(winner.Contender, ending))
        {
            Assert.True(winner.Result.Value.Finalized);
            Assert.Equal((GameStatus.Final, (GameOutcome?)GameOutcome.HomeWin), (now.State.Status, now.State.Outcome));
            Assert.NotNull(now.State.Final);
            Assert.Equal((g.State.AwayRuns, g.State.HomeRuns + 1), (now.State.Final!.AwayRuns, now.State.Final.HomeRuns));
            Assert.Single(winner.Result.Value.Facts.OfType<GameCompleted>());
            Assert.Equal(g.State.Substitutions.Count, now.State.Substitutions.Count);                  // the pitching change is nowhere
            Assert.Equal(g.State.Fielding.Pitcher, now.State.Fielding.Pitcher);
        }
        else
        {
            Assert.Same(normal, winner.Contender);
            Assert.False(winner.Result.Value.Finalized);
            Assert.Equal(GameStatus.InProgress, now.State.Status);
            Assert.Null(now.State.Final);
            Assert.Null(now.State.Outcome);
            Assert.Empty(winner.Result.Value.Facts.OfType<GameCompleted>());
            Assert.Equal((g.State.AwayRuns, g.State.HomeRuns, g.State.Bases), (now.State.AwayRuns, now.State.HomeRuns, now.State.Bases));   // no forced run
            Assert.Same(g.Rng, now.Rng);                                                               // the ending candidate's H1 is not adopted
        }
    }

    [Fact]
    public async Task TC05_a_game_ending_candidate_and_an_ordinary_event_race_and_the_end_is_published_whole_or_not_at_all()
    {
        var (g, ending, normal) = Pair("ending-vs-change");
        Assert.Equal((GameStatus.InProgress, 9, InningHalf.Bottom, 0, 0, 3), (g.State.Status, g.State.Inning, g.State.Half, g.State.AwayRuns, g.State.HomeRuns, g.State.Bases.Count));

        for (var round = 0; round < Rounds; round++)
        {
            var cursor = new GameCursor(g);
            var (stagedEnd, stagedNormal) = Contenders.StageBoth(cursor, ending, normal);

            // the staged ending is already the whole finalized aggregate (never a half-finalized one); the ordinary one is not finalized
            Assert.True(stagedEnd.Record.Finalized);
            ChainChecks.AssertComplete(stagedEnd.Next.State, "staged ending");
            Assert.NotNull(stagedEnd.Next.State.Final);
            Assert.False(stagedNormal.Record.Finalized);
            Assert.Null(stagedNormal.Next.State.Final);

            var entries = await Race(cursor, ending, stagedEnd, normal, stagedNormal);
            var winner = Contenders.AssertOneWinner(g, cursor, "Commit.cas_lost", entries);
            AssertEndingOrNormal(g, winner, ending, normal, cursor);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TC05_in_either_publish_order_only_the_first_is_published_and_finalization_follows_it_exactly(bool endingFirst)
    {
        var (g, ending, normal) = Pair("ending-vs-change");
        var cursor = new GameCursor(g);
        var (stagedEnd, stagedNormal) = Contenders.StageBoth(cursor, ending, normal);

        var first = endingFirst ? new Contenders.Entry(ending, stagedEnd, cursor.Publish(stagedEnd)) : new Contenders.Entry(normal, stagedNormal, cursor.Publish(stagedNormal));
        var second = endingFirst ? new Contenders.Entry(normal, stagedNormal, cursor.Publish(stagedNormal)) : new Contenders.Entry(ending, stagedEnd, cursor.Publish(stagedEnd));

        var winner = Contenders.AssertOneWinner(g, cursor, "Commit.revision_stale", first, second);
        Assert.Same(first.Contender, winner.Contender);
        AssertEndingOrNormal(g, winner, ending, normal, cursor);
    }

    // ---------------------------------------------------------------- TC-06

    [Fact]
    public void TC06_a_commit_staged_before_the_game_ended_and_published_after_it_is_stale_and_changes_nothing()
    {
        var (g, ending, normal) = Pair("ending-vs-change");
        var cursor = new GameCursor(g);
        var stagedNormal = Ok(normal.Stage(g, null, CancellationToken.None));                         // 2 the ordinary event first
        var stagedEnd = Ok(ending.Stage(g, null, CancellationToken.None));                            // 3 then the ending one, from the same expected aggregate
        Assert.Same(g, cursor.Current);

        var record = Ok(cursor.Publish(stagedEnd));                                                    // 4 the ending publish
        ChainChecks.AssertLink(ending.Link(g, stagedEnd, record));
        var final = cursor.Current;
        Assert.Equal(GameStatus.Final, final.State.Status);                                            // 5 finalized in that one publish
        Assert.NotNull(final.State.Final);
        var frozen = (final.State, final.Rng, final.LastEventSequence, final.Revision, final.LastCommit, final.State.AppliedEvents, final.State.Final, final.State.Plays, final.State.Substitutions);

        var late = cursor.Publish(stagedNormal);                                                       // 6 the old staged commit, late
        Assert.Equal("Commit.revision_stale", Code(late));                                             // 7 the cursor's own contract for an old staged commit

        AssertUnchanged(cursor, final, frozen);                                                        // 8
        Assert.Equal((g.Revision + 1, g.LastEventSequence + 1), (final.Revision, final.LastEventSequence));
    }

    /// <summary>
    /// After the end, a new Stage through the existing APIs is refused by the codes of the approved order (O-2): the same ending event again is <c>already_committed</c> (step 5);
    /// the ordinary event, whose id the ending event used with other content, is <c>event_id_in_use</c> (step 8); a fresh ordinary event with the next id reaches the reducer, which
    /// refuses a finished game (<c>Game.already_over</c>). None changes the cursor.
    /// </summary>
    [Fact]
    public void TC06_after_the_end_no_new_stage_is_accepted_and_the_cursor_is_unchanged()
    {
        var (g, ending, normal) = Pair("ending-vs-change");
        var cursor = new GameCursor(g);
        Ok(cursor.Publish(Ok(ending.Stage(g, null, CancellationToken.None))));
        var final = cursor.Current;
        var frozen = (final.State, final.Rng, final.LastEventSequence, final.Revision, final.LastCommit, final.State.AppliedEvents, final.State.Final, final.State.Plays, final.State.Substitutions);

        Assert.Equal("Commit.already_committed", Code(ending.Stage(final, null, CancellationToken.None)));
        Assert.Equal("Commit.event_id_in_use", Code(normal.Stage(final, null, CancellationToken.None)));
        var fresh = Contenders.PitchingChange(final, "ab1");
        Assert.Equal(ChainChecks.IdOf(final.LastEventSequence + 1), fresh.Event.Id);
        Assert.Equal("Game.already_over", Code(fresh.Stage(final, null, CancellationToken.None)));

        AssertUnchanged(cursor, final, frozen);
    }

    private static void AssertUnchanged(GameCursor cursor, AuthoritativeGame final,
        (GameState State, GameRandomSnapshot Rng, int Last, long Revision, CommitRecord? Commit, ImmutableList<EventReceipt> Receipts, FinalizedGame? Final,
            ImmutableList<ScoredPlay> Plays, ImmutableList<SubstitutionRecord> Subs) frozen)
    {
        var now = cursor.Current;
        Assert.Same(final, now);
        Assert.Same(frozen.State, now.State);
        Assert.Same(frozen.Rng, now.Rng);
        Assert.Equal((frozen.Last, frozen.Revision), (now.LastEventSequence, now.Revision));
        Assert.Same(frozen.Commit, now.LastCommit);
        Assert.Same(frozen.Receipts, now.State.AppliedEvents);
        Assert.Same(frozen.Final, now.State.Final);
        Assert.Same(frozen.Plays, now.State.Plays);
        Assert.Same(frozen.Subs, now.State.Substitutions);
    }
}
