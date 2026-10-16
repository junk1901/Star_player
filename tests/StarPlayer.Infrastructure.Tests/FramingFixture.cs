using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;
using static StarPlayer.Infrastructure.Tests.PitchBranchFixture;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-E Package B inputs: sheets with CATCHER ratings, content-version-2 player input sets, a <c>prob-model-v3</c> game that pins one, and real pitch preparations on it. Every pitch is a REAL one
/// (real seeded streams, real resolver); forged objects are made only through the internal constructors the test assemblies see.
/// </summary>
internal static class Fr
{
    public const string Gid = Pc.Gid;

    public static readonly TuningProfile T3 = Gx.Tuning3;

    /// <summary>A sheet with chosen catcher rating (null = no catcher ratings at all); the hitter ratings and pitcher ratings are 50.</summary>
    public static PlayerSheet Sheet(PlayerId id, int? framing, int fielding = 50, int speed = 50, int arm = 50, bool pitcherOnly = false)
    {
        SkillValue S(int v) => Pc.Ok(SkillValue.Create(v));
        var hitter = pitcherOnly ? null : new HitterRatings(S(50), S(50), S(50), S(speed), S(fielding), S(arm));
        var pitcher = new PitcherRatings(S(50), S(50), S(50), S(50), PitchRepertoire.Create([new PitchRating(PitchType.Fastball, S(50))]).Value);
        var catcher = framing is { } f ? new CatcherRatings(S(f), S(50), S(50), S(50)) : null;
        var player = Player.Create(id, "시험선수", new DateOnly(2000, 1, 1), ThrowingHand.Right, BattingSide.Right, BodyMeasurements.Create(180, 80).Value, [], "").Value;
        var status = PlayerStatus.Create([new OriginRecord(OriginType.HighSchool, null, null, null)], EntryPath.Draft, RegistrationStatus.Registered,
            LeagueAssignment.FirstTeam, [new ClubAffiliation(ClubId.Create("club-a").Value, new DateOnly(2020, 1, 1), null)], CareerStatus.Active).Value;
        return Pc.Ok(PlayerSheet.Create(player, Ratings.Create(hitter, pitcher, catcher).Value, new PlayerState(S(50), S(50), S(50), S(50), S(50), S(50)),
            ProficiencyProfile.Create(pitcherOnly ? FieldPosition.Pitcher : FieldPosition.CenterField, Array.Empty<(FieldPosition, SkillValue)>(), []).Value, status));
    }

    /// <summary>The sheets of every base player; <paramref name="framing"/> chooses each player's ReceivingFraming (null = no catcher ratings).</summary>
    public static IEnumerable<PlayerSheet> Sheets(Func<PlayerId, int?> framing) => BasePlayers.Select(id => Sheet(id, framing(id)));

    /// <summary>A content-version-2 set of the base game.</summary>
    public static PlayerInputSet SetV2(Func<PlayerId, int?> framing) => Pc.Ok(PlayerInputSet.CreateForFraming(Sheets(framing)));

    /// <summary>The set in which the given players have the given ReceivingFraming; every other player has the rating <paramref name="others"/> (null = none).</summary>
    public static PlayerInputSet SetV2(IReadOnlyDictionary<PlayerId, int> framing, int? others = null) => SetV2(id => framing.TryGetValue(id, out var f) ? f : others);

    /// <summary>The set in which the fielding team's CURRENT catcher has <paramref name="catcherFraming"/> and everyone else has 50.</summary>
    public static PlayerInputSet SetFor(GameState state, int catcherFraming, int? others = 50) =>
        SetV2(new Dictionary<PlayerId, int> { [CatcherOf(state)] = catcherFraming }, others);

    /// <summary>The same sheets as a content-version-1 set (the pre-3-E format).</summary>
    public static PlayerInputSet SetV1(Func<PlayerId, int?> framing) => Pc.Ok(PlayerInputSet.Create(Sheets(framing)));

    public static PlayerId CatcherOf(GameState state) => Gx.Defender(state, FieldPosition.Catcher);

    /// <summary>A model-v3 execution contract pinning <paramref name="set"/> (null = none pinned).</summary>
    public static GameExecution Exec(PlayerInputSet? set, TuningProfile? tuning = null, string policy = AdvancementPolicy.Version, ParkFactorSnapshot? park = null) =>
        Gx.Exec(park, policy, set?.Id, tuning ?? T3);

    /// <summary>Distinct, deterministic base Fielding / Speed / Arm per player (index based), so the Phase 3-D v2 slots really move.</summary>
    public static (int Fielding, int Speed, int Arm) Varied(PlayerId id)
    {
        var i = BasePlayers.ToList().IndexOf(id);
        return (30 + i * 7 % 60, 20 + i * 11 % 70, 25 + i * 13 % 65);
    }

    /// <summary>A content-version-2 set with the varied Fielding / Speed / Arm and the given ReceivingFraming for every player.</summary>
    public static PlayerInputSet VariedSetV2(Func<PlayerId, int?> framing) =>
        Pc.Ok(PlayerInputSet.CreateForFraming(BasePlayers.Select(id => { var v = Varied(id); return Sheet(id, framing(id), v.Fielding, v.Speed, v.Arm); })));

    /// <summary>The content-version-1 set with exactly the same Fielding / Speed / Arm (the model-v2 side of a comparison).</summary>
    public static PlayerInputSet VariedSetV1() => Gx.Set(id => Varied(id));

    /// <summary>A new plate appearance with an approach chosen (an approach-driven pitch is legal) under <paramref name="rules"/> and <paramref name="execution"/>.</summary>
    public static Pc.Driver InSequence(GameRuleSnapshot rules, GameExecution? execution, string gameId = Gid) =>
        new Pc.Driver(StartState(rules, gameId, execution), gameId).Approach();

    /// <summary>A new plate appearance with NO approach yet: an explicit Take is legal and a substitution is legal.</summary>
    public static Pc.Driver AtStart(GameRuleSnapshot rules, GameExecution? execution, string gameId = Gid) =>
        new Pc.Driver(StartState(rules, gameId, execution), gameId);

    /// <summary>The mutable-game convenience: a v3 non_automated game whose catcher is <paramref name="catcherFraming"/> and everybody else 50, with the set it pins.</summary>
    public static (Pc.Driver Driver, PlayerInputSet Set) NonAutomatedGame(int catcherFraming, bool approach = true, int? others = 50)
    {
        // the catcher of the fielding team is the same player whatever the set says, so the set is built from a probe start state of the same game
        var probe = AtStart(NonAutomated, null).State;
        var set = SetFor(probe, catcherFraming, others);
        var exec = Exec(set);
        return (approach ? InSequence(NonAutomated, exec) : AtStart(NonAutomated, exec), set);
    }

    /// <summary>The same for the AUTOMATED rules of the same model.</summary>
    public static (Pc.Driver Driver, PlayerInputSet Set) AutomatedGame(PlayerInputSet? set = null, bool approach = true)
    {
        var probe = AtStart(Automated, null).State;
        set ??= SetFor(probe, 50);
        var exec = Exec(set);
        return (approach ? InSequence(Automated, exec) : AtStart(Automated, exec), set);
    }

    public static PreparedPitch Prepare(AuthoritativeGame g, PlayerInputSet set, TuningProfile? tuning = null, bool take = false, PitchIntent intent = PitchIntent.Balanced)
    {
        var (b, p) = Pc.Sheets(g.State);
        return Pc.Ok(PitchPreparation.Prepare(g.State, g.Rng, tuning ?? T3, intent, take, b, p, default, null, set));
    }

    public static Result<PreparedPitch> TryPrepare(AuthoritativeGame g, PlayerInputSet? set, TuningProfile? tuning = null, bool take = false, GameState? state = null)
    {
        var s = state ?? g.State;
        var (b, p) = Pc.Sheets(s);
        return PitchPreparation.Prepare(s, g.Rng, tuning ?? T3, PitchIntent.Balanced, take, b, p, default, null, set);
    }

    /// <summary>The called-pitch step of a draft (null when the pitch had none).</summary>
    public static PitchBranchStep? Called(PitchBranchDraft d) => d.Steps.FirstOrDefault(s => s.Stage == StageIds.CalledPitch);

    public static bool IsBorderline(PitchBranchDraft d) => d.Location.Boundary == StarPlayer.Domain.Baseball.PitchZone.BoundaryClass.Borderline;

    /// <summary>The first fixed seed (from 1) whose real prepared pitch satisfies <paramref name="want"/>; deterministic.</summary>
    public static (AuthoritativeGame Game, PreparedPitch Prepared) Find(Pc.Driver d, PlayerInputSet set, Func<PitchBranchDraft, bool> want, bool take = false, ulong maxSeeds = 20000, ulong first = 1)
    {
        for (var seed = first; seed < first + maxSeeds; seed++)
        {
            var g = Pc.Aggregate(d, seed);
            var prepared = Prepare(g, set, take: take);
            if (want(prepared.Draft))
                return (g, prepared);
        }

        throw new InvalidOperationException("no seed produced the wanted pitch");
    }
}
