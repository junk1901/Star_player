using System.Collections.Immutable;
using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E (Game Calling v4) inputs: sheets with chosen pitcher Control and catcher Framing / GameCalling, player input sets of content version 3, a <c>prob-model-v4</c> game that pins one, and real pitch
/// preparations, candidates and staged commits on it. Every pitch is a REAL one (real seeded streams, real resolver); forged objects are made only through the internal constructors the test assemblies see.
/// </summary>
internal static class Gc
{
    public const string Gid = Pc.Gid;

    public static readonly TuningProfile T4 = Gx.Tuning4;

    /// <summary>The ratings of one player: <c>Control</c> (null = no pitching ratings), <c>Catcher</c> (null = NO catcher ratings, the default; give (50, 50) for a neutral catcher) and the base Fielding / Speed / Arm.</summary>
    public sealed record Spec(int? Control = 50, (int Framing, int Calling)? Catcher = null, int Fielding = 50, int Speed = 50, int Arm = 50, bool Hitter = true);

    public static PlayerSheet Sheet(PlayerId id, Spec spec, ThrowingHand throws = ThrowingHand.Right, BattingSide bats = BattingSide.Right)
    {
        SkillValue S(int v) => Pc.Ok(SkillValue.Create(v));
        var hitter = spec.Hitter ? new HitterRatings(S(50), S(50), S(50), S(spec.Speed), S(spec.Fielding), S(spec.Arm)) : null;
        var pitcher = spec.Control is { } c ? new PitcherRatings(S(50), S(c), S(50), S(50), PitchRepertoire.Create([new PitchRating(PitchType.Fastball, S(50))]).Value) : null;
        var catcher = spec.Catcher is { } k ? new CatcherRatings(S(k.Framing), S(50), S(50), S(k.Calling)) : null;
        var player = Player.Create(id, "시험선수", new DateOnly(2000, 1, 1), throws, bats, BodyMeasurements.Create(180, 80).Value, [], "").Value;
        var status = PlayerStatus.Create([new OriginRecord(OriginType.HighSchool, null, null, null)], EntryPath.Draft, RegistrationStatus.Registered,
            LeagueAssignment.FirstTeam, [new ClubAffiliation(ClubId.Create("club-a").Value, new DateOnly(2020, 1, 1), null)], CareerStatus.Active).Value;
        return Pc.Ok(PlayerSheet.Create(player, Ratings.Create(hitter, pitcher, catcher).Value, new PlayerState(S(50), S(50), S(50), S(50), S(50), S(50)),
            ProficiencyProfile.Create(spec.Hitter ? FieldPosition.CenterField : FieldPosition.Pitcher, Array.Empty<(FieldPosition, SkillValue)>(), []).Value, status));
    }

    public static IEnumerable<PlayerSheet> Sheets(Func<PlayerId, Spec> spec) => BasePlayers.Select(id => Sheet(id, spec(id)));

    /// <summary>A content-version-3 set of the base game.</summary>
    public static PlayerInputSet SetV3(Func<PlayerId, Spec> spec) => Pc.Ok(PlayerInputSet.CreateForGameCalling(Sheets(spec)));

    public static PlayerId PitcherOf(GameState state) => state.Fielding.Pitcher;

    public static PlayerId CatcherOf(GameState state) => Gx.Defender(state, FieldPosition.Catcher);

    /// <summary>Distinct, deterministic Fielding / Speed / Arm per player, so the Phase 3-D v2 slots really move.</summary>
    public static Spec Varied(PlayerId id, int? control, (int, int)? catcher)
    {
        var (f, s, a) = Fr.Varied(id);
        return new Spec(control, catcher, f, s, a);
    }

    /// <summary>The set in which the fielding team's CURRENT pitcher has <paramref name="control"/>, the CURRENT catcher has (<paramref name="framing"/>, <paramref name="calling"/>) and everybody else is neutral (50).</summary>
    public static PlayerInputSet SetFor(GameState probe, int control, int framing, int calling, bool varied = false)
    {
        var (pitcher, catcher) = (PitcherOf(probe), CatcherOf(probe));
        Spec For(PlayerId id)
        {
            var c = id == pitcher ? control : 50;
            var k = id == catcher ? (framing, calling) : (50, 50);
            return varied ? Varied(id, c, k) : new Spec(c, k);
        }

        return SetV3(For);
    }

    /// <summary>A model-v4 execution contract pinning <paramref name="set"/> (null = none pinned).</summary>
    public static GameExecution Exec(PlayerInputSet? set, string policy = AdvancementPolicy.Version, ParkFactorSnapshot? park = null, TuningProfile? tuning = null) =>
        Gx.Exec(park, policy, set?.Id, tuning ?? T4);

    public sealed record World(Pc.Driver Driver, PlayerInputSet Set, PlayerId Pitcher, PlayerId Catcher, int Control);

    /// <summary>
    /// A new v4 game under <paramref name="rules"/>: the current pitcher has <paramref name="control"/>, the current catcher (<paramref name="framing"/>, <paramref name="calling"/>), the rest are neutral. With
    /// <paramref name="approach"/> an approach is already chosen (a pitch is legal), otherwise an explicit Take is legal.
    /// </summary>
    public static World Game(GameRuleSnapshot rules, int control = 50, int calling = 50, int framing = 50, bool approach = true, string policy = AdvancementPolicy.Version,
        ParkFactorSnapshot? park = null, bool varied = false, int occupancy = 0, int outs = 0)
    {
        var probe = Fr.AtStart(rules, null).State;                         // the pitcher and the catcher of the fielding team do not depend on the execution
        var set = SetFor(probe, control, framing, calling, varied);
        var exec = Exec(set, policy, park);
        var driver = Fr.AtStart(rules, exec);
        driver.Outs(outs).Runners(occupancy);                             // at most two outs: the fielding team (and so the pitcher and catcher) stays the same
        if (approach)
            driver.Approach();
        return new World(driver, set, PitcherOf(probe), CatcherOf(probe), control);
    }

    /// <summary>The batter and pitcher sheets of the state. The pitcher sheet shows <paramref name="pitcherControl"/> as his Control (the set pins the value the game uses).</summary>
    public static (PlayerSheet Batter, PlayerSheet Pitcher) SheetsOf(GameState s, int pitcherControl = 50) =>
        (Sheet(s.Batting.CurrentBatterSlot.Player, new Spec(50, null)), Sheet(s.Fielding.Pitcher, new Spec(pitcherControl, null)));

    public static PreparedPitch Prepare(AuthoritativeGame g, PlayerInputSet set, int control = 50, TuningProfile? tuning = null, bool take = false, PitchIntent intent = PitchIntent.Balanced)
    {
        var (b, p) = SheetsOf(g.State, control);
        return Pc.Ok(PitchPreparation.Prepare(g.State, g.Rng, tuning ?? T4, intent, take, b, p, default, null, set));
    }

    public static Result<PreparedPitch> TryPrepare(AuthoritativeGame g, PlayerInputSet? set, int control = 50, TuningProfile? tuning = null, bool take = false, GameState? state = null,
        PlayerSheet? pitcherSheet = null)
    {
        var s = state ?? g.State;
        var (b, p) = SheetsOf(s, control);
        return PitchPreparation.Prepare(s, g.Rng, tuning ?? T4, PitchIntent.Balanced, take, b, pitcherSheet ?? p, default, null, set);
    }

    public static Result<NonInPlayPitchCandidate> BuildNonInPlay(AuthoritativeGame g, PreparedPitch prepared, PlayerInputSet? set, int control = 50, TuningProfile? tuning = null)
    {
        var (b, p) = SheetsOf(g.State, control);
        return PitchPreparation.BuildNonInPlayCandidate(prepared, g.State, g.Rng, tuning ?? T4, b, p, g.LastEventSequence, default, set);
    }

    public static Result<InPlayPlanOutcome> BuildInPlay(AuthoritativeGame g, PreparedPitch prepared, PlayerInputSet? set, int control = 50)
    {
        var (b, p) = SheetsOf(g.State, control);
        return InPlayPitchPlanning.Build(prepared, g.State, g.Rng, T4, b, p, g.LastEventSequence, default, set);
    }

    public static Result<InPlayPitchCandidate> Resolve(AuthoritativeGame g, PendingAdvancement pending, PlayerInputSet? set, int control = 50)
    {
        var (b, p) = SheetsOf(g.State, control);
        return AdvancementPlanning.Resolve(pending, g.State, g.Rng, T4, b, p, g.LastEventSequence, default, set);
    }

    public static Result<StagedCommit> Stage(AuthoritativeGame g, PreparedPitch prepared, NonInPlayPitchCandidate candidate, PlayerInputSet? set, int control = 50, TuningProfile? tuning = null,
        PlayerSheet? pitcherSheet = null)
    {
        var (b, p) = SheetsOf(g.State, control);
        return PitchCommitGate.Stage(g, prepared, candidate, tuning ?? T4, b, pitcherSheet ?? p, default, set);
    }

    public static Result<StagedCommit> Stage(AuthoritativeGame g, InPlayPitchCandidate candidate, PlayerInputSet? set, int control = 50)
    {
        var (b, p) = SheetsOf(g.State, control);
        return PitchCommitGate.Stage(g, candidate, T4, b, p, default, set);
    }

    /// <summary>The actual_location step of a draft.</summary>
    public static PitchBranchStep Location(PitchBranchDraft d) => d.Steps.Single(s => s.Stage == StageIds.ActualLocation);

    public static PitchBranchStep Target(PitchBranchDraft d) => d.Steps.Single(s => s.Stage == StageIds.TargetZone);

    public static BoundaryClass AimedClass(PitchBranchDraft d) => PitchZoneModel.V1.Info(d.Target.Zone).Value.Boundary;

    /// <summary>The first fixed seed (from <paramref name="first"/>) whose real prepared pitch satisfies <paramref name="want"/>; deterministic.</summary>
    public static (AuthoritativeGame Game, PreparedPitch Prepared) Find(World w, Func<PitchBranchDraft, bool> want, int control = 50, bool take = false, ulong maxSeeds = 20000, ulong first = 1)
    {
        for (var seed = first; seed < first + maxSeeds; seed++)
        {
            var g = Pc.Aggregate(w.Driver, seed);
            var prepared = Prepare(g, w.Set, control, take: take);
            if (want(prepared.Draft))
                return (g, prepared);
        }

        throw new InvalidOperationException("no seed produced the wanted pitch");
    }

    /// <summary>A real, built non-in-play candidate (hit by pitch, ball, strike, swinging strike, foul) the filter accepts.</summary>
    public static (AuthoritativeGame Game, PreparedPitch Prepared, NonInPlayPitchCandidate Candidate) FindNonInPlay(World w, Func<PitchBranchDraft, bool> want, int control = 50, ulong first = 1,
        ulong maxSeeds = 20000, bool take = false)
    {
        for (var seed = first; seed < first + maxSeeds; seed++)
        {
            var g = Pc.Aggregate(w.Driver, seed);
            var prepared = Prepare(g, w.Set, control, take: take);
            if (prepared.Draft.Kind == PitchBranchKind.InPlay || !want(prepared.Draft))
                continue;
            if (BuildNonInPlay(g, prepared, w.Set, control) is { IsSuccess: true } built)
                return (g, prepared, built.Value);
        }

        throw new InvalidOperationException("no seed produced the wanted non-in-play candidate");
    }

    /// <summary>A real in-play candidate with no advancement decision open (<c>Resolved: false</c>) or with it resolved by the real resolver (<c>Resolved: true</c>).</summary>
    public static (AuthoritativeGame Game, PreparedPitch Prepared, InPlayPitchCandidate Candidate) FindInPlay(World w, bool resolved, Func<PitchBranchDraft, bool>? want = null, int control = 50,
        ulong first = 1, ulong maxSeeds = 60000)
    {
        for (var seed = first; seed < first + maxSeeds; seed++)
        {
            var g = Pc.Aggregate(w.Driver, seed);
            var prepared = Prepare(g, w.Set, control);
            if (prepared.Draft.Kind != PitchBranchKind.InPlay || (want is not null && !want(prepared.Draft)))
                continue;
            var plan = BuildInPlay(g, prepared, w.Set, control);
            if (!plan.IsSuccess)
                continue;
            if (!resolved && plan.Value is InPlayPitchCandidate baseCandidate)
                return (g, prepared, baseCandidate);
            if (resolved && plan.Value is PendingAdvancement pending && Resolve(g, pending, w.Set, control) is { IsSuccess: true } done)
                return (g, prepared, done.Value);
        }

        throw new InvalidOperationException("no seed produced the wanted in-play candidate");
    }

    /// <summary>A roll that selects the SAME outcome from <paramref name="step"/>'s recorded distribution but is a different number (null when the outcome's interval has one value).</summary>
    public static uint? SameIntervalRoll(PitchBranchStep step)
    {
        long low = 0;
        for (var i = 0; i < step.Trace.Final.Count; i++)
        {
            var high = low + step.Trace.Final[i];
            if (step.Roll >= low && step.Roll < high)
            {
                if (step.Roll + 1 < high) return step.Roll + 1;
                if (step.Roll - 1 >= low) return step.Roll - 1;
                return null;
            }

            low = high;
        }

        throw new InvalidOperationException("the recorded roll lies outside its recorded distribution");
    }

    /// <summary>A roll that selects a DIFFERENT outcome from <paramref name="step"/>'s distribution.</summary>
    public static uint OtherIntervalRoll(PitchBranchStep step)
    {
        long low = 0;
        for (var i = 0; i < step.Trace.Final.Count; i++)
        {
            var high = low + step.Trace.Final[i];
            if (step.Trace.Final[i] > 0 && !(step.Roll >= low && step.Roll < high))
                return (uint)low;
            low = high;
        }

        throw new InvalidOperationException("the distribution has a single possible outcome");
    }

    /// <summary>The draft with the step at <paramref name="index"/> replaced (nothing else changes: not the other steps, the usage, the position H1, the kind or the event).</summary>
    public static PitchBranchDraft WithStep(this PitchBranchDraft d, int index, Func<PitchBranchStep, PitchBranchStep> edit) =>
        d with { Steps = d.Steps.SetItem(index, edit(d.Steps[index])) };

    /// <summary>The state of everything a refused commit must leave alone, for a before/after comparison.</summary>
    public sealed record Basis(AuthoritativeGame Game, long Revision, int LastSequence, string RngHash, CommitRecord? LastCommit, GameRandomSnapshot Rng, int Receipts)
    {
        public static Basis Of(GameCursor c) => Of(c.Current);

        public static Basis Of(AuthoritativeGame g) => new(g, g.Revision, g.LastEventSequence, g.Rng.StateHash, g.LastCommit, g.Rng, g.State.AppliedEvents.Count);
    }

    public static void AssertUnchanged(Basis before, GameCursor cursor)
    {
        var now = Basis.Of(cursor);
        Assert.Same(before.Game, now.Game);
        Assert.Equal(before.Revision, now.Revision);
        Assert.Equal(before.LastSequence, now.LastSequence);
        Assert.Equal(before.RngHash, now.RngHash);
        Assert.Same(before.Rng, now.Rng);
        Assert.Same(before.LastCommit, now.LastCommit);
        Assert.Equal(before.Receipts, now.Receipts);
    }

    public static string Code<T>(Result<T> r) => r.IsSuccess ? "(success)" : r.Error!.Value.Code;
}
