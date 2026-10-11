using System.Globalization;
using System.Text.Json;
using StarPlayer.Application.Ports;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Players;
using StarPlayer.Infrastructure.Fixtures;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Shared inputs of the Phase 3-C2A tests. The expected values come from tests/Fixtures/phase3c2a_pitch_branch_golden.json, written by the INDEPENDENT Python oracle
/// tests/Fixtures/generator/generate_pitch_branch_golden.py (own PCG32 + kd1, reads the baseline rows of content/tuning/virtual_tuning_v1.json). Nothing here
/// computes an expected value with the production resolver.
/// </summary>
internal static class PitchBranchFixture
{
    public static readonly string Root = FindRoot();

    private static readonly Lazy<JsonElement> Doc = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tests", "Fixtures", "phase3c2a_pitch_branch_golden.json"))).RootElement);

    public static JsonElement Golden => Doc.Value;

    public static readonly TuningProfile Tuning = TuningProfileTests.Profile();

    private static readonly Lazy<ScriptedGame> BaseGame = new(() =>
    {
        var loaded = new ScriptedGameSource(File.ReadAllText(Path.Combine(Root, "content", "rules", "game_rules_profiles.json")),
            File.ReadAllText(Path.Combine(Root, "tests", "Fixtures", "phase2_scripted_games_100.json"))).Load();
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        return loaded.Value.First(g => g.Rules.ProfileId == "test-dh-cap11");
    });

    private static readonly Lazy<IReadOnlyList<GameRuleSnapshot>> V2Rules = new(() => GameRulesJson.Read(ScriptedGameSource.ReadResource("game_rules_profiles_v2.json")).Value);

    public static GameRuleSnapshot Automated => V2Rules.Value.Single(r => r.CalledPitchMode == CalledPitchMode.Automated);

    public static GameRuleSnapshot NonAutomated => V2Rules.Value.Single(r => r.CalledPitchMode == CalledPitchMode.NonAutomated);

    /// <summary>A gamerules-v1 profile: it declares no called-pitch mode.</summary>
    public static GameRuleSnapshot Undeclared => BaseGame.Value.Rules;

    public static GameRuleSnapshot RulesFor(string mode) => mode == "automated" ? Automated : NonAutomated;

    /// <summary>New plate appearance, 0-0: an explicit Take is legal, an approach-driven pitch is not.</summary>
    /// <remarks>The game id is always explicit: the resolver requires it to equal the RNG snapshot's context key, so a positive test passes the same text to both.</remarks>
    public static GameState StartState(GameRuleSnapshot rules, string gameId) =>
        GameReducer.Start(new GameStart(gameId, rules, BaseGame.Value.Away, BaseGame.Value.Home)).Value;

    /// <summary>An approach is chosen: the pitch sequence is running (an approach-driven pitch is legal, an explicit Take is not).</summary>
    public static GameState InSequenceState(GameRuleSnapshot rules, string gameId) =>
        GameReducer.Apply(StartState(rules, gameId), new ApproachEvent("pb-approach", ApproachKind.Normal)).Value.State;

    /// <summary>After one explicit Take (a ball): the plate appearance continues and control is back at a decision point.</summary>
    public static GameState DecisionState(GameRuleSnapshot rules, string gameId) =>
        GameReducer.Apply(StartState(rules, gameId), new TakeEvent("pb-take", new PitchSpec(PitchOutcome.Ball))).Value.State;

    public static GameState StateFor(GameRuleSnapshot rules, bool explicitTake, string gameId) =>
        explicitTake ? StartState(rules, gameId) : InSequenceState(rules, gameId);

    /// <summary>The RNG position of a fresh context after the given number of whole <c>NextBounded(1,000,000)</c> calls on the given streams (the caller's earlier use).</summary>
    public static GameRandomSnapshot Snapshot(ulong seed, string context, IReadOnlyDictionary<string, int>? priorCalls = null)
    {
        var random = GameRandom.Create(seed, context).Value;
        foreach (var (stream, count) in priorCalls ?? new Dictionary<string, int>())
        {
            var parts = stream.Split('@');
            var s = random.Stream(RngStreamId.Create(parts[0], int.Parse(parts[1], CultureInfo.InvariantCulture)).Value);
            for (var i = 0; i < count; i++)
                s.NextBounded(ProbabilityScale.RollBound);
        }

        return random.Snapshot();
    }

    public static ulong Hex64(JsonElement e) => ulong.Parse(e.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    public static BattingSide Bats(string text) => text switch { "right" => BattingSide.Right, "left" => BattingSide.Left, _ => BattingSide.Switch };

    public static ThrowingHand Throws(string text) => text == "right" ? ThrowingHand.Right : ThrowingHand.Left;

    public static PitchIntent Intent(string text) => text switch { "balanced" => PitchIntent.Balanced, "attack" => PitchIntent.Attack, _ => PitchIntent.Careful };

    public static PitchBranchKind Kind(string text) => text switch
    {
        "hit_by_pitch" => PitchBranchKind.HitByPitch,
        "called_ball" => PitchBranchKind.CalledBall,
        "called_strike" => PitchBranchKind.CalledStrike,
        "swinging_strike" => PitchBranchKind.SwingingStrike,
        "foul" => PitchBranchKind.Foul,
        _ => PitchBranchKind.InPlay,
    };

    public static PitchBranchRequest Request(GameState state, GameRandomSnapshot rng, PitchIntent intent = PitchIntent.Balanced, bool explicitTake = false,
        BattingSide? bats = BattingSide.Right, ThrowingHand? throws = ThrowingHand.Right, TuningProfile? tuning = null) =>
        new(state, tuning ?? Tuning, intent, explicitTake, bats, throws, rng);

    public static PitchBranchDraft Resolved(PitchBranchRequest request)
    {
        var made = PitchBranchResolver.Resolve(request);
        Assert.True(made.IsSuccess, made.Error?.Code + " " + made.Error?.Message);
        return made.Value;
    }

    /// <summary>One text line per fact of a draft (for equality checks that the record's array members would defeat).</summary>
    public static string Summary(PitchBranchDraft d) => string.Join("\n",
        d.ContractVersion, d.Intent, d.ExplicitTake, d.Target.Zone, d.Location.Zone, d.Location.Side, d.Location.Boundary, d.Kind,
        d.BatterDecision, d.CalledPitch, d.SwingResult, d.BattedBall, d.DefenseResult, d.NextBoundedCalls,
        string.Join(";", d.Steps.Select(s => $"{s.Stage}|{s.Condition}|{s.Outcome}|{s.Stream.Canonical}|{s.Roll}|{s.RawDraws}")),
        string.Join(";", d.StreamUsage.Select(u => $"{u.Stream.Canonical}|{u.Calls}|{u.RawDraws}")),
        d.NextRng.Canonical(), d.ProbabilityModelVersion, d.TuningVersion, d.TuningHash, d.RulesProfileId, d.RulesVersion, d.RulesHash);

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                return d.FullName;
        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }
}
