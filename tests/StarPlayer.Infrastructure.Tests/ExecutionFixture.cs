using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Phase 3-D v2 inputs shared by the tests: the embedded <c>tuning-virtual-proto-v2</c>, park snapshots built through the real C46 factory (domain bounds up to 3000 permille, so a value the
/// ACTIVE model does not support can be built on purpose) and the game execution contract that pins them.
/// </summary>
internal static class Gx
{
    public static readonly TuningProfile Tuning2 = Pc.Ok(TuningProfileJson.LoadVirtualV2());

    public static readonly TuningProfile Tuning1 = TuningProfileTests.Profile();

    /// <summary>Phase 3-E: the embedded <c>tuning-virtual-proto-v3</c> of <c>prob-model-v3</c> (catcher.framing active).</summary>
    public static readonly TuningProfile Tuning3 = Pc.Ok(TuningProfileJson.LoadVirtualV3());

    /// <summary>A snapshot built by the real <see cref="ParkFactorProfile"/> factory under the widest domain bounds.</summary>
    public static ParkFactorSnapshot Park(int homeRuns = 1000, int hits = 1000, int extraBaseHits = 1000, string stadium = "stadium-gx", string profile = "pf-gx", int version = 1) =>
        Pc.Ok(ParkFactorProfile.Create(ParkFactorProfileId.Create(profile).Value, StadiumId.Create(stadium).Value, version, new DateOnly(2030, 1, 1),
            homeRuns, hits, extraBaseHits, ParkFactorBounds.Create(1, ParkFactorBounds.AbsoluteMaxPermille).Value)).ToSnapshot();

    private static readonly Lazy<System.Text.Json.JsonElement> OracleDoc = new(() =>
        System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(PitchBranchFixture.Root, "tests", "Fixtures", "phase3d_v2_oracle.json"))).RootElement);

    /// <summary>The independent Python oracle <c>generate_phase3d_v2_oracle.py</c> (reads only content/tuning/virtual_tuning_v2.json; shares no code with production).</summary>
    public static System.Text.Json.JsonElement Oracle => OracleDoc.Value;

    // ---------------------------------------------------------------- real pipeline helpers for a v2 game (the Pc helpers are bound to the v1 tuning)

    /// <summary>A new plate appearance with an approach chosen and the bases empty: an approach-driven pitch is legal and an in-play result needs no advancement decision.</summary>
    public static Pc.Driver InSequence(GameExecution? execution, string gameId = Pc.Gid) =>
        new Pc.Driver(PitchBranchFixture.StartState(PitchBranchFixture.NonAutomated, gameId, execution), gameId).Approach();

    public static AuthoritativeGame Aggregate(Pc.Driver d, ulong seed) => Pc.Aggregate(d, seed);

    public static PreparedPitch Prepare(AuthoritativeGame g, TuningProfile tuning, bool take = false, PitchIntent intent = PitchIntent.Balanced, PlayerInputSet? inputs = null)
    {
        var (b, p) = Pc.Sheets(g.State);
        return Pc.Ok(PitchPreparation.Prepare(g.State, g.Rng, tuning, intent, take, b, p, default, null, inputs));
    }

    public static Result<PreparedPitch> TryPrepare(GameState state, GameRandomSnapshot rng, TuningProfile tuning, PlayerInputSet? inputs = null)
    {
        var (b, p) = Pc.Sheets(state);
        return PitchPreparation.Prepare(state, rng, tuning, PitchIntent.Balanced, false, b, p, default, null, inputs);
    }

    public static Result<InPlayPlanOutcome> BuildInPlay(AuthoritativeGame g, PreparedPitch prepared, TuningProfile tuning, PlayerInputSet? inputs = null)
    {
        var (b, p) = Pc.Sheets(g.State);
        return InPlayPitchPlanning.Build(prepared, g.State, g.Rng, tuning, b, p, g.LastEventSequence, default, inputs);
    }

    public static Result<NonInPlayPitchCandidate> BuildNonInPlay(AuthoritativeGame g, PreparedPitch prepared, TuningProfile tuning, PlayerInputSet? inputs = null)
    {
        var (b, p) = Pc.Sheets(g.State);
        return PitchPreparation.BuildNonInPlayCandidate(prepared, g.State, g.Rng, tuning, b, p, g.LastEventSequence, default, inputs);
    }

    public static Result<StagedCommit> Stage(AuthoritativeGame g, InPlayPitchCandidate candidate, TuningProfile tuning, PlayerInputSet? inputs = null)
    {
        var (b, p) = Pc.Sheets(g.State);
        return PitchCommitGate.Stage(g, candidate, tuning, b, p, default, inputs);
    }

    public static Result<StagedCommit> Stage(AuthoritativeGame g, PreparedPitch prepared, NonInPlayPitchCandidate candidate, TuningProfile tuning, PlayerInputSet? inputs = null)
    {
        var (b, p) = Pc.Sheets(g.State);
        return PitchCommitGate.Stage(g, prepared, candidate, tuning, b, p, default, inputs);
    }

    /// <summary>The first fixed seed whose real pitch is in play and builds a base candidate (deterministic scan); <paramref name="want"/> filters on the draft.</summary>
    public static (AuthoritativeGame Game, PreparedPitch Prepared, InPlayPitchCandidate Candidate) FindInPlay(Pc.Driver d, TuningProfile tuning, Func<PitchBranchDraft, bool>? want = null,
        ulong maxSeeds = 20000, PlayerInputSet? inputs = null)
    {
        for (ulong seed = 1; seed <= maxSeeds; seed++)
        {
            var g = Aggregate(d, seed);
            var prepared = Prepare(g, tuning, inputs: inputs);
            if (prepared.Draft.Kind != PitchBranchKind.InPlay || (want is not null && !want(prepared.Draft)))
                continue;
            if (BuildInPlay(g, prepared, tuning, inputs) is { IsSuccess: true, Value: InPlayPitchCandidate candidate })
                return (g, prepared, candidate);
        }

        throw new InvalidOperationException("no seed produced the wanted in-play pitch");
    }

    // ---------------------------------------------------------------- player inputs

    /// <summary>A sheet with chosen base fielding / speed / arm (the other hitting ratings 50); <paramref name="pitcherOnly"/> gives a player with pitching ratings and no hitter ratings.</summary>
    public static PlayerSheet Sheet(PlayerId id, int fielding = 50, int speed = 50, int arm = 50, bool pitcherOnly = false, string name = "시험선수")
    {
        SkillValue S(int v) => Pc.Ok(SkillValue.Create(v));
        var hitter = pitcherOnly ? null : new HitterRatings(S(50), S(50), S(50), S(speed), S(fielding), S(arm));
        var pitcher = new PitcherRatings(S(50), S(50), S(50), S(50), PitchRepertoire.Create([new PitchRating(PitchType.Fastball, S(50))]).Value);
        var player = Player.Create(id, name, new DateOnly(2000, 1, 1), ThrowingHand.Right, BattingSide.Right, BodyMeasurements.Create(180, 80).Value, [], "").Value;
        var status = PlayerStatus.Create([new OriginRecord(OriginType.HighSchool, null, null, null)], EntryPath.Draft, RegistrationStatus.Registered,
            LeagueAssignment.FirstTeam, [new ClubAffiliation(ClubId.Create("club-a").Value, new DateOnly(2020, 1, 1), null)], CareerStatus.Active).Value;
        return Pc.Ok(PlayerSheet.Create(player, Ratings.Create(hitter, pitcher, null).Value, new PlayerState(S(50), S(50), S(50), S(50), S(50), S(50)),
            ProficiencyProfile.Create(pitcherOnly ? FieldPosition.Pitcher : FieldPosition.CenterField, Array.Empty<(FieldPosition, SkillValue)>(), []).Value, status));
    }

    /// <summary>The player input set of the base game: every player gets <paramref name="custom"/> (fielding, speed, arm) or 50/50/50.</summary>
    public static PlayerInputSet Set(Func<PlayerId, (int Fielding, int Speed, int Arm)?>? custom = null, Func<PlayerId, bool>? pitcherOnly = null) =>
        Pc.Ok(PlayerInputSet.Create(PitchBranchFixture.BasePlayers.Select(id =>
        {
            var c = custom?.Invoke(id) ?? (50, 50, 50);
            return Sheet(id, c.Item1, c.Item2, c.Item3, pitcherOnly?.Invoke(id) ?? false);
        })));

    // ---------------------------------------------------------------- advancement (Part C)

    /// <summary>Runners (bit codes of <see cref="Pc.Driver.Runners"/>) and outs in the 1st inning; <paramref name="approach"/> chooses an approach (a pitch is legal) or leaves the plate appearance new.</summary>
    public static Pc.Driver Situation(GameExecution? execution, int occupancy, int outs, bool approach)
    {
        var d = new Pc.Driver(PitchBranchFixture.StartState(PitchBranchFixture.NonAutomated, Pc.Gid, execution), Pc.Gid);
        d.Outs(outs).Runners(occupancy);
        return approach ? d.Approach() : d;
    }

    public static Result<InPlayPitchCandidate> Resolve(AuthoritativeGame g, PendingAdvancement pending, TuningProfile tuning, PlayerInputSet? inputs = null)
    {
        var (b, p) = Pc.Sheets(g.State);
        return AdvancementPlanning.Resolve(pending, g.State, g.Rng, tuning, b, p, g.LastEventSequence, default, inputs);
    }

    /// <summary>The first fixed seed whose real in-play pitch leaves an extra-base decision open (and that <paramref name="want"/> accepts), with the base state that produced it.</summary>
    public static (AuthoritativeGame Game, PreparedPitch Prepared, PendingAdvancement Pending) FindPending(Pc.Driver d, TuningProfile tuning, PlayerInputSet? inputs,
        Func<PendingAdvancement, bool>? want = null, ulong firstSeed = 1, ulong maxSeeds = 40000)
    {
        for (var seed = firstSeed; seed < firstSeed + maxSeeds; seed++)
        {
            var g = Aggregate(d, seed);
            var prepared = Prepare(g, tuning, inputs: inputs);
            if (prepared.Draft.Kind != PitchBranchKind.InPlay)
                continue;
            if (BuildInPlay(g, prepared, tuning, inputs) is { IsSuccess: true, Value: PendingAdvancement pending } && (want is null || want(pending)))
                return (g, prepared, pending);
        }

        throw new InvalidOperationException("no seed produced the wanted pending advancement");
    }

    /// <summary>Every real pending advancement of the situation in seed order (lazy): each seed is prepared, planned and, when an extra-base decision is open, yielded.</summary>
    public static IEnumerable<(AuthoritativeGame Game, PreparedPitch Prepared, PendingAdvancement Pending)> Pendings(Pc.Driver d, TuningProfile tuning, PlayerInputSet? inputs,
        ulong firstSeed = 1, ulong maxSeeds = 200_000)
    {
        for (var seed = firstSeed; seed < firstSeed + maxSeeds; seed++)
        {
            var g = Aggregate(d, seed);
            var prepared = Prepare(g, tuning, inputs: inputs);
            if (prepared.Draft.Kind == PitchBranchKind.InPlay && BuildInPlay(g, prepared, tuning, inputs) is { IsSuccess: true, Value: PendingAdvancement pending })
                yield return (g, prepared, pending);
        }
    }

    // ---------------------------------------------------------------- a game played through the REAL pipeline

    /// <summary>What a harness game did, and every invariant breach it saw (an empty <see cref="Violations"/> is the pass condition).</summary>
    public sealed record GameRun(AuthoritativeGame Last, int Events, int Pitches, int InPlay, int Pending, int Resolved, int Void, IReadOnlyList<string> Receipts, IReadOnlyList<string> Violations,
        IReadOnlyDictionary<string, int> SlotsSeen);

    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [StageIds.TargetZone] = [],
        [StageIds.ActualLocation] = ["pitcher.control"],
        [StageIds.BatterDecision] = ["batter.eye", "strategy.approach", "situation.count_pressure"],
        [StageIds.CalledPitch] = ["catcher.framing"],       // Phase 3-E: only a model-v3 non_automated game ever lists it (the v2 tests below assert it is never seen)
        [StageIds.SwingResult] = ["pitcher.stuff", "batter.contact", "matchup.handedness"],
        [StageIds.BattedBall] = ["pitcher.velocity", "batter.power"],
        [StageIds.DefenseResult] = ["defense.fielding", "park.home_runs", "park.hits", "park.extra_base_hits"],
    };

    /// <summary>
    /// Plays one game with the real preparation, planning, advancement resolution, staging (<see cref="PitchCommitGate"/>) and publication (<see cref="GameCursor"/>) until it ends or
    /// <paramref name="maxEvents"/> events were confirmed. No production runner exists; this loop only chooses an approach for every new plate appearance and confirms what the pipeline
    /// built. Invariants are checked on every pitch: sums, caps, which slots reach which stage, the streams used, the retained execution contract.
    /// </summary>
    public static GameRun Play(GameExecution? execution, TuningProfile tuning, PlayerInputSet? inputs, ulong seed, int maxEvents = 6000, GameRuleSnapshot? rules = null)
    {
        var state0 = PitchBranchFixture.StartState(rules ?? PitchBranchFixture.NonAutomated, Pc.Gid, execution);
        var cursor = new GameCursor(Pc.Ok(AuthoritativeGame.Restore(state0, PitchBranchFixture.Snapshot(seed, Pc.Gid), 0, 0)));
        var violations = new List<string>();
        var receipts = new List<string>();
        var slots = new Dictionary<string, int>();
        int pitches = 0, inPlay = 0, pending = 0, resolved = 0, voids = 0;
        var streams = new[] { "game/target@1", "game/location@1", "game/pitch@1", "game/contact@1", "game/field@1" };

        void Publish(Result<StagedCommit> staged, string what)
        {
            if (!staged.IsSuccess)
            {
                violations.Add($"{what}: {staged.Error!.Value.Code} {staged.Error.Value.Message}");
                return;
            }

            var published = cursor.Publish(staged.Value);
            if (!published.IsSuccess)
                violations.Add($"{what}: publish {published.Error!.Value.Code}");
            else
                receipts.Add(published.Value.Receipt.Fingerprint);
        }

        while (cursor.Current.State.Status == GameStatus.InProgress && receipts.Count < maxEvents && violations.Count == 0)
        {
            var g = cursor.Current;
            var s = g.State;
            if (!ReferenceEquals(s.Execution, state0.Execution) || !AuthoritativeGame.IsComplete(s))
                violations.Add("the execution contract or the aggregate changed identity");
            if (s.Stage != PaStage.InSequence)
            {
                var id = GameEventIds.Create(s.GameId, g.LastEventSequence).Value.Next().Value;
                Publish(CommitGate.Stage(g, new ApproachEvent(id, ApproachKind.Normal)), "approach");
                continue;
            }

            var (b, p) = Pc.Sheets(s);
            var made = PitchPreparation.Prepare(s, g.Rng, tuning, PitchIntent.Balanced, false, b, p, default, null, inputs);
            if (!made.IsSuccess)
            {
                violations.Add($"prepare: {made.Error!.Value.Code} {made.Error.Value.Message}");
                break;
            }

            var prepared = made.Value;
            pitches++;
            foreach (var step in prepared.Draft.Steps)
            {
                if (step.Trace.Final.Sum() != 1_000_000 || step.Trace.Final.Any(v => v < 0))
                    violations.Add($"{step.Stage}/{step.Condition}: distribution does not sum to 1,000,000");
                if (step.Trace.CombinedFactorPpm.Any(f => f < tuning.CombinedMinPpm || f > tuning.CombinedMaxPpm))
                    violations.Add($"{step.Stage}: combined factor outside the cap");
                for (var i = 0; i < step.Trace.Baseline.Count; i++)
                    if ((step.Trace.Baseline[i] > 0) != (step.Trace.Final[i] > 0))
                        violations.Add($"{step.Stage}/{step.Condition}: a zero changed");
                foreach (var m in step.Trace.Modifiers)
                {
                    slots[m.Slot] = slots.GetValueOrDefault(m.Slot) + 1;
                    if (!Allowed[step.Stage].Contains(m.Slot))
                        violations.Add($"{step.Stage}: unexpected slot {m.Slot}");
                }

                if (step.Trace.Modifiers.Select(m => m.Slot).Distinct().Count() != step.Trace.Modifiers.Count)
                    violations.Add($"{step.Stage}: a slot was applied twice");
                if (!streams.Contains(step.Stream.Canonical))
                    violations.Add($"{step.Stage}: unexpected stream {step.Stream.Canonical}");
            }

            if (prepared.Draft.Kind != PitchBranchKind.InPlay)
            {
                var built = PitchPreparation.BuildNonInPlayCandidate(prepared, s, g.Rng, tuning, b, p, g.LastEventSequence, default, inputs);
                if (!built.IsSuccess) { violations.Add($"build: {built.Error!.Value.Code}"); break; }
                Publish(PitchCommitGate.Stage(g, prepared, built.Value, tuning, b, p, default, inputs), "pitch");
                continue;
            }

            inPlay++;
            var plan = InPlayPitchPlanning.Build(prepared, s, g.Rng, tuning, b, p, g.LastEventSequence, default, inputs);
            if (!plan.IsSuccess) { violations.Add($"plan: {plan.Error!.Value.Code}"); break; }
            InPlayPitchCandidate candidate;
            if (plan.Value is PendingAdvancement open)
            {
                pending++;
                var made2 = AdvancementPlanning.Resolve(open, s, g.Rng, tuning, b, p, g.LastEventSequence, default, inputs);
                if (!made2.IsSuccess) { violations.Add($"resolve: {made2.Error!.Value.Code} {made2.Error.Value.Message}"); break; }
                candidate = made2.Value;
                resolved++;
                var detail = candidate.Audit.AdvancementDetail!;
                voids += detail.Decisions.Count(x => x.Applied == AdvancementOutcome.Void);
                var expectedPolicy = s.Execution?.AdvancementPolicyVersion ?? AdvancementPolicy.Version;
                if (detail.PolicyVersion != expectedPolicy || detail.Decisions.Any(x => x.Ppm.Sum() != 1_000_000 || (x.Abilities is null) != (expectedPolicy != AdvancementPolicy.VersionV2)))
                    violations.Add("advancement policy or row invariant");
                if (detail.LogicalDraws != open.ContextKeys.Length || detail.DrawStream != "game/runner@1")
                    violations.Add("advancement draw contract");
            }
            else
                candidate = (InPlayPitchCandidate)plan.Value;
            Publish(PitchCommitGate.Stage(g, candidate, tuning, b, p, default, inputs), "in play");
        }

        var last = cursor.Current;
        if (last.State.Status == GameStatus.Final && !ReferenceEquals(last.State.Final!.Execution, state0.Execution))
            violations.Add("the finalized game does not carry the start execution");
        return new GameRun(last, receipts.Count, pitches, inPlay, pending, resolved, voids, receipts, violations, slots);
    }

    /// <summary>The independent oracle's advancement distribution (null speed / arm = that slot not supplied).</summary>
    public static int[] OracleAdvancement(string context, int? speed, int? arm, bool sequential = false)
    {
        foreach (var c in Oracle.GetProperty("advancement").EnumerateArray())
        {
            if (c.GetProperty("context").GetString() != context)
                continue;
            var s = c.GetProperty("speed");
            var a = c.GetProperty("arm");
            if ((speed is null) != (s.ValueKind != System.Text.Json.JsonValueKind.Number) || (speed is not null && s.GetInt32() != speed))
                continue;
            if ((arm is null) != (a.ValueKind != System.Text.Json.JsonValueKind.Number) || (arm is not null && a.GetInt32() != arm))
                continue;
            return c.GetProperty(sequential ? "sequential_ppm" : "ppm").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        }

        throw new InvalidOperationException($"the oracle has no case {context} speed={speed} arm={arm} sequential={sequential}");
    }

    /// <summary>The player of <paramref name="position"/> for the FIELDING team of a state.</summary>
    public static PlayerId Defender(GameState state, FieldPosition position) => state.DefenderAt(state.FieldingSide, position)!.Value;

    /// <summary>The player at <paramref name="position"/> gets (fielding, 50, 50); everyone else 50/50/50. Several positions can be given.</summary>
    public static PlayerInputSet SetWithFielders(GameState state, params (FieldPosition Position, int Fielding)[] fielders) =>
        Set(id => fielders.Where(f => Defender(state, f.Position) == id).Select(f => ((int, int, int)?)(f.Fielding, 50, 50)).FirstOrDefault());

    /// <summary>The independent oracle's distribution for the given batted ball, park and fielding case (null park / fielding = not supplied).</summary>
    public static int[] OracleDefense(string batted, int[]? park, int? fielding)
    {
        foreach (var c in Oracle.GetProperty("defense").EnumerateArray())
        {
            if (c.GetProperty("batted").GetString() != batted)
                continue;
            var fieldingText = c.GetProperty("fielding");
            if ((fielding is null) != (fieldingText.ValueKind != System.Text.Json.JsonValueKind.Number) || (fielding is not null && fieldingText.GetInt32() != fielding))
                continue;
            var parkText = c.GetProperty("park");
            var same = park is null ? parkText.ValueKind == System.Text.Json.JsonValueKind.Null : parkText.ValueKind == System.Text.Json.JsonValueKind.Array && parkText.EnumerateArray().Select(x => x.GetInt32()).SequenceEqual(park);
            if (same)
                return c.GetProperty("ppm").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        }

        throw new InvalidOperationException($"the oracle has no case {batted} park={(park is null ? "-" : string.Join("/", park))} fielding={fielding}");
    }

    /// <summary>The outcome a roll selects from a distribution, written here (cumulative thresholds) and not taken from <c>Distribution.Select</c>.</summary>
    public static string Pick(IReadOnlyList<string> outcomes, IReadOnlyList<int> ppm, uint roll)
    {
        long running = 0;
        for (var i = 0; i < ppm.Count; i++)
        {
            running += ppm[i];
            if (roll < running)
                return outcomes[i];
        }

        throw new InvalidOperationException("roll outside the distribution");
    }

    /// <summary>The execution contract of a game on <c>tuning-virtual-proto-v2</c> (or the given tuning's pins).</summary>
    public static GameExecution Exec(ParkFactorSnapshot? park = null, string policy = AdvancementPolicy.Version, string? inputsId = null, TuningProfile? tuning = null)
    {
        var t = tuning ?? Tuning2;
        return new GameExecution(t.Model.Version.Value, t.Tuning.Value, t.ContentHash, policy, park ?? Park(), inputsId);
    }
}
