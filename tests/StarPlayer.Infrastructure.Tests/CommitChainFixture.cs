using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;
using static StarPlayer.Infrastructure.Tests.Pc;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>Stages one submission against a given aggregate through the real gate entry (the internal overload with the existing test hook).</summary>
internal delegate Result<StagedCommit> Stager(AuthoritativeGame expected, Action<string>? checkpoint, CancellationToken cancellation);

/// <summary>
/// Phase 3-C3-C-1 (game-engine 27.20 OD-2): a bounded, FIXED, scripted chain of official events confirmed one after another on ONE <see cref="GameCursor"/>. Test assembly only.
/// <para>
/// It is not a plate-appearance driver (D-4, Phase 3-F): every scenario is a fixed list of at most <see cref="MaxEvents"/> steps written out in <see cref="ChainScenarios"/>;
/// nothing here chooses the next action or loops to the end of a game. Each step is prepared from the cursor's CURRENT aggregate, staged by the real
/// <see cref="CommitGate"/>/<see cref="PitchCommitGate"/> entry and published by the real cursor; a gate failure fails the test at once. The only freedom is the initial seed:
/// <see cref="Find"/> scans a bounded range for the first seed whose real draws give the step's wanted pitch kind (the existing <c>Pc.Find*</c> pattern). A step whose draw
/// is not the wanted kind returns false BEFORE staging; nothing is overwritten and no state or RNG is injected.
/// </para>
/// </summary>
internal sealed class CommitChain
{
    public const int MaxEvents = 10;

    /// <summary>Deliberately different from the event sequence, so a test can never confuse the two.</summary>
    public const long StartRevision = 40;

    public enum Kind { Approach, PitchingChange, Steal, Take, Pitch, InPlay }

    /// <summary>The source of a pitch-carrying commit: the prepared pitch (H0 to H1), the position the candidate adopts (H1 or H2) and the in-play candidate if any.</summary>
    public sealed record PitchSource(PreparedPitch Prepared, GameRandomSnapshot Adopted, InPlayPitchCandidate? InPlay)
    {
        public AdvancementAudit? Detail => InPlay?.Audit.AdvancementDetail;
    }

    /// <summary>One confirmed step. <see cref="Event"/> is the body the fixture itself submitted (TC-08 replays these, never a receipt).</summary>
    public sealed record Link(int Index, Kind Kind, GameEvent Event, AuthoritativeGame Before, AuthoritativeGame After, StagedCommit Staged, CommitRecord Record,
        (RngStreamId Stream, long Raw)[] Usage, PitchSource? Pitch, Stager Restage)
    {
        public bool CarriesPitch => Pitch is not null;
    }

    private readonly List<Link> _links = [];

    public CommitChain(ChainScenarios.Scenario scenario, ulong seed, int? cancelAt = null)
    {
        var start = scenario.Situation();
        Scenario = scenario;
        Seed = seed;
        Start = start.State;
        StartLast = start.LastIssued;
        CancelAt = cancelAt;
        Initial = Aggregate(start.State, start.LastIssued, seed, PriorDraws, StartRevision);
        Cursor = new GameCursor(Initial);
    }

    private CommitChain(ChainScenarios.Scenario scenario, GameState start, int startLast, ulong seed)
    {
        Scenario = scenario;
        Seed = seed;
        Start = start;
        StartLast = startLast;
        Initial = Aggregate(start, startLast, seed, PriorDraws, StartRevision);
        Cursor = new GameCursor(Initial);
    }

    public ChainScenarios.Scenario Scenario { get; }

    public ulong Seed { get; }

    /// <summary>The state the chain starts from (built by the reducer outside the gate, then restored); TC-08 replays from exactly this value.</summary>
    public GameState Start { get; }

    public int StartLast { get; }

    public AuthoritativeGame Initial { get; }

    public GameCursor Cursor { get; private set; }

    public IReadOnlyList<Link> Links => _links;

    /// <summary>The step index before which the cursor was replaced by a restored one (TC-10), and that restored aggregate.</summary>
    public int? ReopenedBefore { get; private set; }

    public AuthoritativeGame? Reopened { get; private set; }

    /// <summary>The step at which a cancelled Stage and a cancelled Publish are tried before the real confirmation (TC-07).</summary>
    public int? CancelAt { get; }

    public int CancelDrills { get; private set; }

    // ---------------------------------------------------------------- finding and running a fixed scenario

    /// <summary>The first seed in 1..<paramref name="maxSeeds"/> for which every fixed step gets its wanted draw. Every attempted step is confirmed and checked for real.</summary>
    public static CommitChain Find(ChainScenarios.Scenario scenario, ulong maxSeeds = 3000)
    {
        var start = scenario.Situation();                             // immutable: shared by the attempts, never changed
        for (ulong seed = 1; seed <= maxSeeds; seed++)
        {
            var chain = new CommitChain(scenario, start.State, start.LastIssued, seed);
            if (chain.Run())
                return chain;
        }

        throw new InvalidOperationException($"no seed up to {maxSeeds} completes the fixed scenario '{scenario.Name}'");
    }

    /// <summary>The scenario again with a known seed and a fresh, independent start state; every step must succeed.</summary>
    public static CommitChain Again(ChainScenarios.Scenario scenario, ulong seed, int? cancelAt = null, int? reopenBefore = null)
    {
        var chain = new CommitChain(scenario, seed, cancelAt);
        Assert.True(chain.Run(reopenBefore), $"scenario '{scenario.Name}' seed {seed} did not repeat");
        return chain;
    }

    /// <summary>Runs the scenario's fixed steps in order; false as soon as a step's draw is not the wanted one (only a seed scan sees false).</summary>
    public bool Run(int? reopenBefore = null)
    {
        var steps = Scenario.Steps;
        Assert.True(steps.Count <= MaxEvents, "the scenario is longer than the chain bound");
        for (var i = 0; i < steps.Count; i++)
        {
            if (reopenBefore == i)
                Reopen();
            if (!steps[i](this))
                return false;
        }

        return true;
    }

    /// <summary>TC-10: the approved restoration inputs of the current published aggregate go through the internal <c>Restore</c>, and a NEW cursor continues from it.</summary>
    public AuthoritativeGame Reopen()
    {
        var published = Cursor.Current;
        var restored = Ok(AuthoritativeGame.Restore(published.State, published.Rng, published.LastEventSequence, published.Revision));
        Cursor = new GameCursor(restored);
        ReopenedBefore = _links.Count;
        Reopened = restored;
        return restored;
    }

    // ---------------------------------------------------------------- the step vocabulary (each is ONE official event)

    private string NextId => $"{Gid}:e{Cursor.Current.LastEventSequence + 1:D6}";

    public bool Approach() => Plain(Kind.Approach, new ApproachEvent(NextId, ApproachKind.Normal));

    public bool PitchingChange(string player) =>
        Plain(Kind.PitchingChange, new SubstitutionEvent(NextId, SubstitutionKind.PitchingChange, Cursor.Current.State.FieldingSide, PlayerId.Create(player).Value));

    public bool Steal(int from) =>
        Plain(Kind.Steal, new RunnerPlayEvent(NextId, RunnerPlayKind.Steal, ImmutableList.Create(new RunnerMove(from, from + 1, MoveCause.Steal))));

    public bool Take(Func<PitchBranchKind, bool> want) => NonInPlay(Kind.Take, take: true, want);

    public bool Pitch(Func<PitchBranchKind, bool> want) => NonInPlay(Kind.Pitch, take: false, want);

    public bool InPlay(Func<InPlayPitchCandidate, bool> want)
    {
        var g = Cursor.Current;                                       // H0 is the cursor's current position, never a remembered one
        var prepared = Prepare(g);
        if (prepared.Draft.Kind != PitchBranchKind.InPlay)
            return false;
        var built = BuildInPlay(g, prepared);
        if (!built.IsSuccess)
            return false;
        InPlayPitchCandidate candidate;
        if (built.Value is PendingAdvancement pending)
        {
            var resolved = Resolve(g, pending);
            if (!resolved.IsSuccess)
                return false;
            candidate = resolved.Value;
        }
        else
            candidate = (InPlayPitchCandidate)built.Value;
        if (!want(candidate))
            return false;

        var usage = Usage(prepared.Draft).ToList();
        if (candidate.Audit.AdvancementDetail is { } detail)
            usage.Add((GameRngStreams.Runner, detail.RawDraws));
        var (b, p) = Sheets(g.State);
        return Confirm(Kind.InPlay, candidate.Event, (a, hook, ct) => PitchCommitGate.Stage(a, candidate, Tuning, b, p, hook, ct), [.. usage],
            new PitchSource(prepared, candidate.NextRng, candidate));
    }

    private bool NonInPlay(Kind kind, bool take, Func<PitchBranchKind, bool> want)
    {
        var g = Cursor.Current;
        var prepared = Prepare(g, take);
        if (prepared.Draft.Kind == PitchBranchKind.InPlay || !want(prepared.Draft.Kind))
            return false;
        var built = BuildNonInPlay(g, prepared);
        if (!built.IsSuccess)
            return false;
        var candidate = built.Value;
        var (b, p) = Sheets(g.State);
        return Confirm(kind, candidate.Event, (a, hook, ct) => PitchCommitGate.Stage(a, prepared, candidate, Tuning, b, p, hook, ct), Usage(prepared.Draft),
            new PitchSource(prepared, candidate.NextRng, null));
    }

    private bool Plain(Kind kind, GameEvent gameEvent) => Confirm(kind, gameEvent, (a, hook, ct) => CommitGate.Stage(a, gameEvent, hook, ct), [], null);

    private bool Confirm(Kind kind, GameEvent gameEvent, Stager stage, (RngStreamId Stream, long Raw)[] usage, PitchSource? pitch)
    {
        Assert.True(_links.Count < MaxEvents, "chain bound exceeded");
        var before = Cursor.Current;
        if (CancelAt == _links.Count)
            CancelThenRetry(before, stage);

        var staged = Ok(stage(before, null, CancellationToken.None));
        Assert.Same(before, Cursor.Current);                           // staging alone changes nothing
        var record = Ok(Cursor.Publish(staged));
        var link = new Link(_links.Count, kind, gameEvent, before, Cursor.Current, staged, record, usage, pitch, stage);
        _links.Add(link);
        ChainChecks.AssertLink(link);
        return true;
    }

    /// <summary>TC-07: cancel inside Stage (after the reducer re-run) and once more just before the swap; neither may change the cursor. The real confirmation follows.</summary>
    private void CancelThenRetry(AuthoritativeGame before, Stager stage)
    {
        using var inStage = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => stage(before, name => { if (name == CommitGate.StageName.Applied) inStage.Cancel(); }, inStage.Token));
        Assert.Same(before, Cursor.Current);

        var staged = Ok(stage(before, null, CancellationToken.None));
        using var atSwap = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => Cursor.Publish(staged, name => { if (name == GameCursor.Stage.BeforeSwap) atSwap.Cancel(); }, atSwap.Token));
        Assert.Same(before, Cursor.Current);
        CancelDrills++;
    }
}

/// <summary>The fixed scenarios. Each is a literal list of steps; the comment says why every step is legal in the state the previous step leaves.</summary>
internal static class ChainScenarios
{
    public sealed record Scenario(string Name, Func<Driver> Situation, IReadOnlyList<Func<CommitChain, bool>> Steps)
    {
        public override string ToString() => Name;
    }

    private static bool InProgress(InPlayPitchCandidate c) => c.UnconfirmedStep.State.Status == GameStatus.InProgress;

    /// <summary>
    /// Top of the 1st, 0 out, runner on first (a walk), new plate appearance (<see cref="PaStage.Start"/>), home pitcher <c>hp</c>:
    /// 1 pitching change to <c>hb1</c> (C3-A; legal at Start, by the fielding team, bench player not yet used);
    /// 2 explicit Take, ball or called strike (C3-B non-in-play; Take is legal at Start, the plate appearance continues at <see cref="PaStage.Decision"/>);
    /// 3 steal of second (C3-A; a runner play has no stage restriction and second is empty);
    /// 4 approach (C3-A; legal at Decision);
    /// 5 pitch that is not in play and not a hit-by-pitch (C3-B non-in-play; at most the second ball/strike, so the plate appearance continues);
    /// 6 in-play, the base path or the resolved-advancement path as asked, game not over (C3-B in-play; the plate appearance ends);
    /// 7 approach for the next batter (C3-A; a new plate appearance);
    /// 8 any non-in-play pitch (C3-B non-in-play).
    /// </summary>
    public static Scenario Mixed(bool resolved) => new(resolved ? "mixed-resolved" : "mixed-base", () => Situation(1, 0, approach: false),
    [
        c => c.PitchingChange("hb1"),
        c => c.Take(k => k is PitchBranchKind.CalledBall or PitchBranchKind.CalledStrike),
        c => c.Steal(1),
        c => c.Approach(),
        c => c.Pitch(k => k is not PitchBranchKind.HitByPitch),
        c => c.InPlay(x => (x.Audit.AdvancementDetail is not null) == resolved && InProgress(x)),
        c => c.Approach(),
        c => c.Pitch(_ => true),
    ]);

    /// <summary>
    /// Top of the 1st, 2 out, bases empty, new plate appearance: 1 approach; 2 an in-play out that ends the half (the fielding side flips in the same event);
    /// 3 approach for the home team's first batter (bottom of the 1st, new plate appearance); 4 any non-in-play pitch, now thrown by the away pitcher <c>ap</c>.
    /// </summary>
    public static Scenario HalfChange { get; } = new("half-change", () => Situation(0, 2, approach: false),
    [
        c => c.Approach(),
        c => c.InPlay(x => x.UnconfirmedStep.State.Half == InningHalf.Bottom && InProgress(x)),
        c => c.Approach(),
        c => c.Pitch(_ => true),
    ]);

    /// <summary>
    /// Bottom of the 9th, 0-0, 0 out, runner on third, new plate appearance: 1 approach; 2 any non-in-play pitch (with first empty even a hit-by-pitch forces no run);
    /// 3 an in-play event that ends the game: the reducer's final state and its <c>Finalize</c> go out in ONE publish.
    /// </summary>
    public static Scenario WalkOff { get; } = new("walk-off", () => Situation(4, 0, approach: false, tiedBottomNinth: true),
    [
        c => c.Approach(),
        c => c.Pitch(_ => true),
        c => c.InPlay(x => x.UnconfirmedStep.State.Status == GameStatus.Final),
    ]);

    public static Scenario ByName(string name) => name switch
    {
        "mixed-base" => Mixed(false),
        "mixed-resolved" => Mixed(true),
        "half-change" => HalfChange,
        "walk-off" => WalkOff,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };
}

/// <summary>
/// TC-03: the shared commit invariants, checked on the PUBLISHED values only and computed independently here (canonical id text, receipt prefixes, pitch counts from
/// <see cref="PitcherUsage"/>, RNG positions re-derived with raw draws). Nothing calls a production check (<c>IsConsistent</c>, <c>IsComplete</c>, <c>PitchesOf</c>,
/// <c>PreviewComparison</c>) to decide a pass.
/// </summary>
internal static class ChainChecks
{
    public static string IdOf(int sequence) => $"{Gid}:e{sequence:D6}";

    // ---------------------------------------------------------------- one successful publish

    public static void AssertLink(CommitChain.Link link)
    {
        var (before, after, record, e) = (link.Before, link.After, link.Record, link.Event);
        var at = $"step {link.Index} ({link.Kind})";

        // identity, revision, sequence and the record
        Assert.Equal(before.State.GameId, after.State.GameId);
        Assert.True(before.Revision + 1 == after.Revision, $"{at}: Revision {before.Revision} -> {after.Revision}");
        Assert.True(before.LastEventSequence + 1 == after.LastEventSequence, $"{at}: LastEventSequence {before.LastEventSequence} -> {after.LastEventSequence}");
        Assert.Equal((before.Revision, after.Revision, after.LastEventSequence), (record.RevisionBefore, record.RevisionAfter, record.EventSequence));
        Assert.Equal(IdOf(after.LastEventSequence), e.Id);                                         // EventId: exactly the next canonical id
        Assert.Same(e, record.Event);                                                              // the fixture's own body was confirmed
        Assert.Same(record, after.LastCommit);                                                     // Publish's record IS the new LastCommit
        Assert.Same(link.Staged.Record, record);
        Assert.Same(link.Staged.Next, after);
        Assert.Same(link.Staged.Expected, before);

        // receipts: the old ones untouched, exactly one new one, the event's own
        var old = before.State.AppliedEvents;
        var now = after.State.AppliedEvents;
        Assert.True(old.Count + 1 == now.Count, $"{at}: receipts {old.Count} -> {now.Count}");
        for (var i = 0; i < old.Count; i++)
            Assert.Equal(old[i], now[i]);
        Assert.Equal(record.Receipt, now[^1]);
        Assert.Equal(e.Id, record.Receipt.EventId);
        Assert.Equal(link.CarriesPitch ? EventFingerprint.VersionV2 : EventFingerprint.Version, record.Receipt.FingerprintVersion);

        AssertAppendOnly(before.State.Plays, after.State.Plays, p => p.EventId, e.Id, at);
        AssertAppendOnly(before.State.Substitutions, after.State.Substitutions, s => s.EventId, e.Id, at);

        // completeness and finalization in the same publish
        AssertComplete(after.State, at);
        var finalized = before.State.Status == GameStatus.InProgress && after.State.Status == GameStatus.Final;
        Assert.Equal(finalized, record.Finalized);
        Assert.Equal(finalized ? 1 : 0, record.Facts.Count(f => f is GameCompleted));

        AssertRng(link, at);
        AssertPitchCount(link, at);
        AssertTransition(link, at);
        AssertRestoreRoundTrip(after, at);
    }

    private static void AssertAppendOnly<T>(ImmutableList<T> before, ImmutableList<T> after, Func<T, string> eventId, string id, string at) where T : class
    {
        Assert.True(after.Count >= before.Count, $"{at}: an append-only list shrank");
        for (var i = 0; i < before.Count; i++)
            Assert.Same(before[i], after[i]);
        for (var i = before.Count; i < after.Count; i++)
            Assert.True(eventId(after[i]) == id, $"{at}: a new {typeof(T).Name} names {eventId(after[i])}, not {id}");
    }

    /// <summary>InProgress has no outcome and no final record; Final has a final record of the same game, outcome and score (re-stated here, not the production rule).</summary>
    public static void AssertComplete(GameState s, string at)
    {
        if (s.Status == GameStatus.InProgress)
        {
            Assert.True(s.Outcome is null && s.Final is null, $"{at}: in-progress state carries an outcome or final record");
            return;
        }

        Assert.Equal(GameStatus.Final, s.Status);
        Assert.True(s.Final is not null, $"{at}: Status == Final && Final == null");
        Assert.Equal((s.GameId, s.Outcome, s.AwayRuns, s.HomeRuns), (s.Final!.GameId, (GameOutcome?)s.Final.Outcome, s.Final.AwayRuns, s.Final.HomeRuns));
    }

    // ---------------------------------------------------------------- RNG (TC-02)

    private static void AssertRng(CommitChain.Link link, string at)
    {
        var (h0, adopted, record) = (link.Before.Rng, link.After.Rng, link.Record);
        Assert.Equal(h0.StateHash, record.RngStateHashBefore);
        Assert.Equal(adopted.StateHash, record.RngStateHashAfter);
        if (link.Pitch is not { } pitch)
        {
            Assert.Empty(link.Usage);
            Assert.Same(h0, adopted);                                                              // RNG-free: the very reference is kept
            return;
        }

        var prepared = pitch.Prepared;
        var h1 = prepared.Draft.NextRng;
        Assert.Same(link.Before.State, prepared.State);                                            // prepared from the cursor's current aggregate
        Assert.Equal(h0.StateHash, prepared.StartRngStateHash);
        Assert.Same(pitch.Adopted, adopted);                                                       // the candidate's verified position, not a new draw
        AssertStreamDelta(h0, h1, Usage(prepared.Draft), $"{at} H0->H1");
        if (pitch.Detail is { } detail)
        {
            Assert.NotSame(h1, adopted);                                                           // resolved: H2 adopted
            Assert.Equal((h0.StateHash, h1.StateHash, adopted.StateHash), (detail.StartRngStateHash, detail.AfterPitchRngStateHash, detail.FinalRngStateHash));
            AssertStreamDelta(h1, adopted, [(GameRngStreams.Runner, detail.RawDraws)], $"{at} H1->H2");
        }
        else
            Assert.Same(h1, adopted);                                                              // non-in-play and base in-play: H1 adopted

        // the whole step re-derived from H0 with raw 32-bit outputs, compared structurally and not only by hash
        Assert.Equal(Advance(h0, link.Usage).Canonical(), adopted.Canonical());
    }

    /// <summary>Every stream of <paramref name="from"/> is still there in the same order and never goes backwards; each stream grew by exactly the expected raw draws; no other stream appeared or moved.</summary>
    public static void AssertStreamDelta(GameRandomSnapshot from, GameRandomSnapshot to, IEnumerable<(RngStreamId Stream, long Raw)> expected, string at)
    {
        Assert.Equal((from.FormatVersion, from.AlgorithmVersion, from.KeyDerivationVersion, from.MasterSeed, from.ContextKey),
            (to.FormatVersion, to.AlgorithmVersion, to.KeyDerivationVersion, to.MasterSeed, to.ContextKey));
        var want = expected.GroupBy(u => u.Stream.Canonical, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Sum(u => u.Raw), StringComparer.Ordinal);
        var a = from.Streams.ToDictionary(s => s.Stream.Canonical, s => s.Draws, StringComparer.Ordinal);
        var b = to.Streams.ToDictionary(s => s.Stream.Canonical, s => s.Draws, StringComparer.Ordinal);
        Assert.True(a.Keys.All(b.ContainsKey), $"{at}: a stream disappeared");
        foreach (var stream in a.Keys.Union(b.Keys).Union(want.Keys))
            Assert.True(b.GetValueOrDefault(stream) - a.GetValueOrDefault(stream) == want.GetValueOrDefault(stream),
                $"{at}: stream {stream} moved {b.GetValueOrDefault(stream) - a.GetValueOrDefault(stream)} raw draws, expected {want.GetValueOrDefault(stream)}");
        AssertCanonicalOrder(to, at);
    }

    public static void AssertCanonicalOrder(GameRandomSnapshot rng, string at)
    {
        var names = rng.Streams.Select(s => s.Stream.Canonical).ToArray();
        Assert.True(names.SequenceEqual(names.Order(StringComparer.Ordinal)) && names.Distinct(StringComparer.Ordinal).Count() == names.Length, $"{at}: streams not in canonical order or repeated");
    }

    // ---------------------------------------------------------------- pitch count (TC-15)

    /// <summary>Pitch count per (team, pitcher) from the published usages, summed over stints.</summary>
    public static Dictionary<(TeamSide, PlayerId), int> PitchCounts(GameState s)
    {
        var counts = new Dictionary<(TeamSide, PlayerId), int>();
        foreach (var side in new[] { TeamSide.Away, TeamSide.Home })
            foreach (var u in s.Team(side).PitcherUsages)
                counts[(side, u.Pitcher)] = counts.GetValueOrDefault((side, u.Pitcher)) + u.PitchCount;
        return counts;
    }

    /// <summary>A pitch-carrying event adds exactly 1 to the pitcher who was FIELDING BEFORE it (even when the event flips the half); nothing else moves any count.</summary>
    private static void AssertPitchCount(CommitChain.Link link, string at)
    {
        var before = PitchCounts(link.Before.State);
        var after = PitchCounts(link.After.State);
        var thrower = (link.Before.State.FieldingSide, link.Before.State.Fielding.Pitcher);
        foreach (var key in before.Keys.Union(after.Keys))
        {
            var expected = link.CarriesPitch && key == thrower ? 1 : 0;
            Assert.True(after.GetValueOrDefault(key) - before.GetValueOrDefault(key) == expected,
                $"{at}: pitch count of {key} moved {after.GetValueOrDefault(key) - before.GetValueOrDefault(key)}, expected {expected}");
        }

        Assert.True(!link.CarriesPitch || after.ContainsKey(thrower), $"{at}: the thrower has no usage");
    }

    // ---------------------------------------------------------------- the event kind matches the state change

    private static void AssertTransition(CommitChain.Link link, string at)
    {
        var (s0, s1) = (link.Before.State, link.After.State);
        switch (link.Kind)
        {
            case CommitChain.Kind.Approach:
                Assert.Equal((PaStage.InSequence, (ApproachKind?)ApproachKind.Normal), (s1.Stage, s1.Approach));
                Assert.Equal(s0.Count, s1.Count);
                break;
            case CommitChain.Kind.PitchingChange:
                var change = (SubstitutionEvent)link.Event;
                Assert.Equal(change.Player, s1.Team(change.Team).Pitcher);
                Assert.Equal(s0.Substitutions.Count + 1, s1.Substitutions.Count);
                Assert.Equal((SubstitutionKind.PitchingChange, change.Player), (s1.Substitutions[^1].Kind, s1.Substitutions[^1].In));
                break;
            case CommitChain.Kind.Steal:
                var move = ((RunnerPlayEvent)link.Event).MoveList.Single();
                Assert.Null(s1.Bases.At(move.From));
                Assert.Equal(s0.Bases.At(move.From)!.RunnerKey, s1.Bases.At(move.To)!.RunnerKey);
                Assert.Equal((s0.Stage, s0.Count), (s1.Stage, s1.Count));
                break;
            case CommitChain.Kind.Take:
                Assert.IsType<TakeEvent>(link.Event);
                if (s1.Batting.CurrentBatterSlot.Player == s0.Batting.CurrentBatterSlot.Player && s1.Half == s0.Half)
                    Assert.Equal(PaStage.Decision, s1.Stage);                                      // the plate appearance continues: control back at a decision point
                break;
            case CommitChain.Kind.Pitch:
                Assert.IsType<PitchEvent>(link.Event);
                break;
            case CommitChain.Kind.InPlay:
                Assert.IsType<InPlayEvent>(link.Event);
                if (s1.Status == GameStatus.InProgress)
                    Assert.Equal((PaStage.Start, PitchCountState.Zero), (s1.Stage, s1.Count));     // the plate appearance ended
                break;
        }
    }

    // ---------------------------------------------------------------- restoration and the whole aggregate

    /// <summary>The published aggregate's approved restoration inputs restore to the same logical aggregate (a restored game has no last commit, O-6).</summary>
    public static AuthoritativeGame AssertRestoreRoundTrip(AuthoritativeGame published, string at)
    {
        var restored = AuthoritativeGame.Restore(published.State, published.Rng, published.LastEventSequence, published.Revision);
        Assert.True(restored.IsSuccess, $"{at}: Restore refused a published aggregate: {restored.Error?.Code}");
        var r = restored.Value;
        Assert.Same(published.State, r.State);
        Assert.Same(published.Rng, r.Rng);
        Assert.Equal((published.LastEventSequence, published.Revision), (r.LastEventSequence, r.Revision));
        Assert.Null(r.LastCommit);
        return r;
    }

    /// <summary>Whole-aggregate check: canonical continuous receipts 1..n, last sequence = n, plays/substitutions name receipts, completeness, RNG context and order.</summary>
    public static void AssertAggregate(AuthoritativeGame g, string at)
    {
        var receipts = g.State.AppliedEvents;
        Assert.Equal(receipts.Count, g.LastEventSequence);
        for (var i = 0; i < receipts.Count; i++)
            Assert.True(receipts[i].EventId == IdOf(i + 1), $"{at}: receipt {i + 1} is {receipts[i].EventId}");
        var ids = receipts.Select(r => r.EventId).ToHashSet(StringComparer.Ordinal);
        Assert.All(g.State.Plays, p => Assert.Contains(p.EventId, ids));
        Assert.All(g.State.Substitutions, s => Assert.Contains(s.EventId, ids));
        AssertComplete(g.State, at);
        Assert.Equal(g.State.GameId, g.Rng.ContextKey);
        AssertCanonicalOrder(g.Rng, at);
        if (g.LastCommit is { } last)
        {
            Assert.Equal(last.Receipt, receipts[^1]);
            Assert.Equal((g.LastEventSequence, g.Revision, g.Rng.StateHash), (last.EventSequence, last.RevisionAfter, last.RngStateHashAfter));
        }
    }

    // ---------------------------------------------------------------- the whole chain

    public static void AssertChain(CommitChain chain)
    {
        var links = chain.Links;
        var final = chain.Cursor.Current;
        Assert.InRange(links.Count, 1, CommitChain.MaxEvents);
        for (var i = 0; i < links.Count; i++)
        {
            var link = links[i];
            Assert.Equal(i, link.Index);
            var expectedBase = i == 0 ? chain.Initial : chain.ReopenedBefore == i ? chain.Reopened! : links[i - 1].After;
            Assert.Same(expectedBase, link.Before);                                               // every step started from the then-current aggregate
            Assert.Equal(CommitChain.StartRevision + i + 1, link.After.Revision);
            Assert.Equal(chain.StartLast + i + 1, link.After.LastEventSequence);
            Assert.Equal(link.Record.Receipt, final.State.AppliedEvents[chain.StartLast + i]);
            AssertAggregate(link.After, $"step {i}");
        }

        Assert.Same(links[^1].After, final);
        Assert.Same(links[^1].Record, final.LastCommit);

        // cumulative RNG: H0 of the chain plus the sum of every step's recorded raw draws, re-derived independently
        var total = links.SelectMany(l => l.Usage).ToArray();
        AssertStreamDelta(chain.Initial.Rng, final.Rng, total, "chain");
        Assert.Equal(Advance(chain.Initial.Rng, total).Canonical(), final.Rng.Canonical());

        // pitch totals against the receipts: each pitcher gained exactly the number of pitch-carrying events thrown while he was fielding
        var expected = PitchCounts(chain.Initial.State);
        foreach (var link in links.Where(l => l.CarriesPitch))
        {
            var key = (link.Before.State.FieldingSide, link.Before.State.Fielding.Pitcher);
            expected[key] = expected.GetValueOrDefault(key) + 1;
            Assert.Equal(EventFingerprint.VersionV2, final.State.AppliedEvents[link.Record.EventSequence - 1].FingerprintVersion);
        }

        var actual = PitchCounts(final.State);
        foreach (var key in expected.Keys.Union(actual.Keys))
            Assert.True(expected.GetValueOrDefault(key) == actual.GetValueOrDefault(key), $"pitch total of {key}: {actual.GetValueOrDefault(key)}, expected {expected.GetValueOrDefault(key)}");
    }

    /// <summary>The kinds the chain confirmed, in order.</summary>
    public static CommitChain.Kind[] Kinds(CommitChain chain) => chain.Links.Select(l => l.Kind).ToArray();
}

/// <summary>
/// Value-and-structure comparison by reflection over public instance properties, collections element by element in order. It never uses record <c>==</c> on a type with
/// collection members (those compare by reference) and is independent of the production <c>PreviewComparison</c>. Returns the first differing path or null.
/// </summary>
internal static class Structural
{
    public static string? FirstDifference(object? a, object? b, string path = "$", int depth = 0)
    {
        if (ReferenceEquals(a, b))
            return null;
        if (a is null || b is null)
            return $"{path}: {a ?? "null"} vs {b ?? "null"}";
        var type = a.GetType();
        if (type != b.GetType())
            return $"{path}: {type.Name} vs {b.GetType().Name}";
        if (depth > 40)
            return $"{path}: nested too deep";
        if (type.IsPrimitive || type.IsEnum || a is string or decimal or DateOnly or DateTime)
            return a.Equals(b) ? null : $"{path}: {a} vs {b}";
        if (a is IEnumerable left && b is IEnumerable right)
        {
            var x = left.Cast<object?>().ToList();
            var y = right.Cast<object?>().ToList();
            if (x.Count != y.Count)
                return $"{path}: count {x.Count} vs {y.Count}";
            for (var i = 0; i < x.Count; i++)
                if (FirstDifference(x[i], y[i], $"{path}[{i}]", depth + 1) is { } d)
                    return d;
            return null;
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0).ToArray();
        if (properties.Length == 0)
            return a.Equals(b) ? null : $"{path}: {a} vs {b}";
        foreach (var p in properties)
            if (FirstDifference(p.GetValue(a), p.GetValue(b), $"{path}.{p.Name}", depth + 1) is { } d)
                return d;
        return null;
    }

    public static void AssertSame(object? expected, object? actual, string what)
    {
        var difference = FirstDifference(expected, actual);
        Assert.True(difference is null, $"{what} differs: {difference}");
    }
}
