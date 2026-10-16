using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Inputs of the Phase 3-C3-B tests. Every candidate is a REAL one: the state is driven through the public reducer with canonical event ids (so <c>AuthoritativeGame.Restore</c>
/// accepts it; the older helpers use the non-canonical id <c>pb-approach</c>), the pitch comes from the real <see cref="PitchPreparation.Prepare"/> on the real seeded streams,
/// and the candidate from the real builders. Forged objects are made only through the internal constructors the two test assemblies see, and each test says so.
/// </summary>
internal static class Pc
{
    public const string Gid = "game:3c3b:probe";

    public static PlayerSheet Sheet(PlayerId id, BattingSide bats = BattingSide.Right, ThrowingHand throws = ThrowingHand.Right)
    {
        SkillValue S(int v) => SkillValue.Create(v).Value;
        var hitter = new HitterRatings(S(50), S(50), S(50), S(50), S(50), S(50));
        var pitcher = new PitcherRatings(S(50), S(50), S(50), S(50), PitchRepertoire.Create([new PitchRating(PitchType.Fastball, S(50))]).Value);
        var player = Player.Create(id, "시험선수", new DateOnly(2000, 1, 1), throws, bats, BodyMeasurements.Create(180, 80).Value, [], "").Value;
        var status = PlayerStatus.Create([new OriginRecord(OriginType.HighSchool, null, null, null)], EntryPath.Draft, RegistrationStatus.Registered,
            LeagueAssignment.FirstTeam, [new ClubAffiliation(ClubId.Create("club-a").Value, new DateOnly(2020, 1, 1), null)], CareerStatus.Active).Value;
        return PlayerSheet.Create(player, Ratings.Create(hitter, pitcher, null).Value, new PlayerState(S(50), S(50), S(50), S(50), S(50), S(50)),
            ProficiencyProfile.Create(FieldPosition.Pitcher, Array.Empty<(FieldPosition, SkillValue)>(), []).Value, status).Value;
    }

    public static (PlayerSheet Batter, PlayerSheet Pitcher) Sheets(GameState s, ThrowingHand throws = ThrowingHand.Right) =>
        (Sheet(s.Batting.CurrentBatterSlot.Player), Sheet(s.Fielding.Pitcher, BattingSide.Right, throws));

    public static T Ok<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, $"{result.Error?.Code}: {result.Error?.Message}");
        return result.Value;
    }

    public static string Code<T>(Result<T> result) => result.IsSuccess ? "(success)" : result.Error!.Value.Code;

    public static string Message<T>(Result<T> result) => result.IsSuccess ? "" : result.Error!.Value.Message;

    // ---------------------------------------------------------------- states

    /// <summary>Plays real events through the public reducer with canonical event ids, so the receipts, the id cursor and the aggregate's last sequence agree.</summary>
    public sealed class Driver
    {
        private readonly GameEventIds _ids;

        public Driver(GameState start, string gameId)
        {
            State = start;
            _ids = GameEventIds.Create(gameId).Value;
        }

        public GameState State { get; private set; }

        public int LastIssued => _ids.LastIssued;

        private string Id() => _ids.Next().Value;

        private Driver Do(Func<string, GameEvent> make)
        {
            var applied = GameReducer.Apply(State, make(Id()));
            Assert.True(applied.IsSuccess, applied.Error?.Code + " " + applied.Error?.Message);
            State = applied.Value.State;
            return this;
        }

        public Driver Approach() => Do(id => new ApproachEvent(id, ApproachKind.Normal));

        /// <summary>A real substitution between plate appearances (a pinch runner needs <paramref name="base"/>, a defensive substitution <paramref name="slot"/>).</summary>
        public Driver Substitute(SubstitutionKind kind, TeamSide team, PlayerId player, int? @base = null, int? slot = null) =>
            Do(id => new SubstitutionEvent(id, kind, team, player, @base, slot));

        public Driver Walk()
        {
            Approach();
            for (var i = 0; i < 4; i++)
                Do(id => new PitchEvent(id, new PitchSpec(PitchOutcome.Ball)));
            return this;
        }

        public Driver Steal(int from) => Do(id => new RunnerPlayEvent(id, RunnerPlayKind.Steal, ImmutableList.Create(new RunnerMove(from, from + 1, MoveCause.Steal))));

        public Driver FlyOut() =>
            Approach().Do(id => new InPlayEvent(id, BattedBallType.Fly, InPlayResult.Out, false, null, ImmutableList.Create(new PositionCredit(FieldPosition.LeftField, CreditKind.Putout))));

        public Driver HomeRun() => Approach().Do(id => new InPlayEvent(id, BattedBallType.Fly, InPlayResult.HomeRun));

        public Driver Outs(int count)
        {
            for (var i = 0; i < count; i++)
                FlyOut();
            return this;
        }

        public Driver Runners(int occupancy)
        {
            switch (occupancy)
            {
                case 1: Walk(); break;
                case 2: Walk().Steal(1); break;
                case 3: Walk().Steal(1).Walk(); break;
                case 4: Walk().Steal(1).Steal(2); break;
                case 7: Walk().Walk().Walk(); break;
            }

            return this;
        }
    }

    /// <summary>
    /// A new plate appearance (no approach yet: an explicit Take is legal) or, with <paramref name="approach"/>, one in progress, with the given runners (bit mask 1 first, 2 second,
    /// 4 third) and outs in the 1st inning, or, with <paramref name="tiedBottomNinth"/>, the bottom of the 9th at 0-0.
    /// </summary>
    public static Driver Situation(int occupancy, int outs, bool approach, bool tiedBottomNinth = false)
    {
        var d = new Driver(StartState(NonAutomated, Gid), Gid);
        if (tiedBottomNinth)
        {
            for (var i = 0; i < 17; i++)
                d.Outs(3);
            Assert.Equal((9, InningHalf.Bottom), (d.State.Inning, d.State.Half));
        }

        d.Outs(outs).Runners(occupancy);
        if (approach)
            d.Approach();
        Assert.Equal(approach ? PaStage.InSequence : PaStage.Start, d.State.Stage);
        return d;
    }

    public static AuthoritativeGame Aggregate(Driver d, ulong seed, IReadOnlyDictionary<string, int>? prior = null, long revision = 0) =>
        Aggregate(d.State, d.LastIssued, seed, prior, revision);

    public static AuthoritativeGame Aggregate(GameState state, int last, ulong seed, IReadOnlyDictionary<string, int>? prior = null, long revision = 0) =>
        Ok(AuthoritativeGame.Restore(state, Snapshot(seed, Gid, prior), last, revision));

    // ---------------------------------------------------------------- real candidates

    public static PreparedPitch Prepare(AuthoritativeGame g, bool take = false, PitchIntent intent = PitchIntent.Balanced, ThrowingHand throws = ThrowingHand.Right)
    {
        var (b, p) = Sheets(g.State, throws);
        return Ok(PitchPreparation.Prepare(g.State, g.Rng, Tuning, intent, take, b, p));
    }

    public static Result<NonInPlayPitchCandidate> BuildNonInPlay(AuthoritativeGame g, PreparedPitch prepared)
    {
        var (b, p) = Sheets(g.State);
        return PitchPreparation.BuildNonInPlayCandidate(prepared, g.State, g.Rng, Tuning, b, p, g.LastEventSequence);
    }

    public static Result<InPlayPlanOutcome> BuildInPlay(AuthoritativeGame g, PreparedPitch prepared)
    {
        var (b, p) = Sheets(g.State);
        return InPlayPitchPlanning.Build(prepared, g.State, g.Rng, Tuning, b, p, g.LastEventSequence);
    }

    public static Result<InPlayPitchCandidate> Resolve(AuthoritativeGame g, PendingAdvancement pending)
    {
        var (b, p) = Sheets(g.State);
        return AdvancementPlanning.Resolve(pending, g.State, g.Rng, Tuning, b, p, g.LastEventSequence);
    }

    public static Result<StagedCommit> Stage(AuthoritativeGame g, PreparedPitch prepared, NonInPlayPitchCandidate candidate, CancellationToken ct = default)
    {
        var (b, p) = Sheets(g.State);
        return PitchCommitGate.Stage(g, prepared, candidate, Tuning, b, p, ct);
    }

    public static Result<StagedCommit> Stage(AuthoritativeGame g, InPlayPitchCandidate candidate, CancellationToken ct = default)
    {
        var (b, p) = Sheets(g.State);
        return PitchCommitGate.Stage(g, candidate, Tuning, b, p, ct);
    }

    public sealed record NonInPlay(AuthoritativeGame Game, PreparedPitch Prepared, NonInPlayPitchCandidate Candidate);

    public sealed record InPlay(AuthoritativeGame Game, PreparedPitch Prepared, InPlayPitchCandidate Candidate);

    public sealed record PendingCase(AuthoritativeGame Game, PreparedPitch Prepared, PendingAdvancement Pending);

    /// <summary>The first fixed seed whose real, built non-in-play candidate the filter accepts (deterministic scan).</summary>
    public static NonInPlay FindNonInPlay(GameState state, int last, bool take, Func<PitchBranchDraft, GameState, bool> want, IReadOnlyDictionary<string, int>? prior = null,
        ulong skip = 0, ulong maxSeeds = 20000)
    {
        for (ulong seed = 1; seed <= maxSeeds; seed++)
        {
            var g = Aggregate(state, last, seed, prior);
            var prepared = Prepare(g, take);
            if (prepared.Draft.Kind == PitchBranchKind.InPlay || (take && prepared.Draft.Kind is PitchBranchKind.SwingingStrike or PitchBranchKind.Foul) || !want(prepared.Draft, state))
                continue;
            var built = BuildNonInPlay(g, prepared);
            if (!built.IsSuccess)
                continue;
            if (skip-- > 0)
                continue;
            return new NonInPlay(g, prepared, built.Value);
        }

        throw new InvalidOperationException("no seed produced the wanted non-in-play candidate");
    }

    /// <summary>The first fixed seed whose real in-play pitch leaves NO advancement decision open and builds a base candidate the filter accepts.</summary>
    public static InPlay FindBaseInPlay(GameState state, int last, Func<InPlayPitchCandidate, bool> want, IReadOnlyDictionary<string, int>? prior = null, ulong maxSeeds = 60000)
    {
        for (ulong seed = 1; seed <= maxSeeds; seed++)
        {
            var g = Aggregate(state, last, seed, prior);
            var prepared = Prepare(g);
            if (prepared.Draft.Kind != PitchBranchKind.InPlay)
                continue;
            var built = BuildInPlay(g, prepared);
            if (built.IsSuccess && built.Value is InPlayPitchCandidate candidate && want(candidate))
                return new InPlay(g, prepared, candidate);
        }

        throw new InvalidOperationException("no seed produced the wanted base in-play candidate");
    }

    /// <summary>The first fixed seed whose real in-play pitch leaves an advancement decision open, resolved by the real resolver into a candidate the filter accepts.</summary>
    public static InPlay FindResolved(GameState state, int last, Func<InPlayPitchCandidate, bool> want, IReadOnlyDictionary<string, int>? prior = null, ulong maxSeeds = 60000)
    {
        for (ulong seed = 1; seed <= maxSeeds; seed++)
        {
            var g = Aggregate(state, last, seed, prior);
            var prepared = Prepare(g);
            if (prepared.Draft.Kind != PitchBranchKind.InPlay)
                continue;
            var built = BuildInPlay(g, prepared);
            if (!built.IsSuccess || built.Value is not PendingAdvancement pending)
                continue;
            var resolved = Resolve(g, pending);
            if (resolved.IsSuccess && want(resolved.Value))
                return new InPlay(g, prepared, resolved.Value);
        }

        throw new InvalidOperationException("no seed produced the wanted resolved candidate");
    }

    public static PendingCase FindPending(GameState state, int last, ulong maxSeeds = 60000)
    {
        for (ulong seed = 1; seed <= maxSeeds; seed++)
        {
            var g = Aggregate(state, last, seed);
            var prepared = Prepare(g);
            if (prepared.Draft.Kind == PitchBranchKind.InPlay && BuildInPlay(g, prepared) is { IsSuccess: true, Value: PendingAdvancement pending })
                return new PendingCase(g, prepared, pending);
        }

        throw new InvalidOperationException("no seed produced a pending advancement");
    }

    // ---------------------------------------------------------------- forging (internal constructors only; no production bypass exists)

    public static PreparedPitch With(this PreparedPitch p, PitchBranchDraft draft) =>
        new(p.State, p.GameId, p.StartRngStateHash, p.BatterId, p.PitcherId, p.BatterDeclaredSide, p.PitcherThrows, draft, p.RulesVersion, p.RulesHash, p.ProbabilityModelVersion,
            p.TuningVersion, p.TuningHash, p.Fielding, p.Catcher);

    /// <summary>Phase 3-E: the same preparation with its frozen catcher input replaced (null removes it). Forging goes through the internal constructor only.</summary>
    public static PreparedPitch WithCatcher(this PreparedPitch p, FrozenCatcherInput? catcher) =>
        new(p.State, p.GameId, p.StartRngStateHash, p.BatterId, p.PitcherId, p.BatterDeclaredSide, p.PitcherThrows, p.Draft, p.RulesVersion, p.RulesHash, p.ProbabilityModelVersion,
            p.TuningVersion, p.TuningHash, p.Fielding, catcher);

    public static NonInPlayPitchCandidate WithStep(this NonInPlayPitchCandidate c, GameStep step) => new(c.Event, step, c.NextRng, c.Kind, c.NextBoundedCalls);

    public static NonInPlayPitchCandidate WithEvent(this NonInPlayPitchCandidate c, GameEvent gameEvent) => new(gameEvent, c.UnconfirmedStep, c.NextRng, c.Kind, c.NextBoundedCalls);

    public static NonInPlayPitchCandidate WithRng(this NonInPlayPitchCandidate c, GameRandomSnapshot rng) => new(c.Event, c.UnconfirmedStep, rng, c.Kind, c.NextBoundedCalls);

    public static InPlayPitchCandidate WithStep(this InPlayPitchCandidate c, GameStep step) => new(c.Plan, c.Event, step, c.NextRng, c.Audit);

    /// <summary>A candidate with its own audit replaced and its RNG position: used to forge resolved audits.</summary>
    public static InPlayPitchCandidate WithAudit(this InPlayPitchCandidate c, InPlayAudit audit, GameRandomSnapshot? rng = null) => new(c.Plan, c.Event, c.UnconfirmedStep, rng ?? c.NextRng, audit);

    /// <summary>
    /// The snapshot reached from <paramref name="start"/> by drawing the given number of RAW 32-bit outputs on each stream. The position after N raw outputs does not depend on how they
    /// were consumed, so this builds the exact snapshot a draw sequence of that size produces (and ones a short or long sequence would).
    /// </summary>
    public static GameRandomSnapshot Advance(GameRandomSnapshot start, params (RngStreamId Stream, long Raw)[] draws)
    {
        var random = GameRandom.Restore(start).Value;
        foreach (var (id, raw) in draws)
        {
            var stream = random.Stream(id);
            for (long i = 0; i < raw; i++)
                stream.NextUInt32();
        }

        return random.Snapshot();
    }

    public static (RngStreamId Stream, long Raw)[] Usage(PitchBranchDraft draft) => draft.StreamUsage.Select(u => (u.Stream, u.RawDraws)).ToArray();

    /// <summary>The usage with <paramref name="delta"/> raw draws added to (or taken from) the first stream.</summary>
    public static (RngStreamId Stream, long Raw)[] UsagePlus(PitchBranchDraft draft, long delta)
    {
        var usage = Usage(draft);
        usage[0] = (usage[0].Stream, usage[0].Raw + delta);
        return usage;
    }

    public static readonly IReadOnlyDictionary<string, int> PriorDraws = new Dictionary<string, int> { ["game/ai@1"] = 3, ["game/pitch@1"] = 2, ["game/runner@1"] = 1 };
}
