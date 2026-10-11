using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Domain.Common;

namespace StarPlayer.Domain.Baseball.Probability;

// Phase 3-B2 (ADR-006): the STRUCTURE of the conditional probability model - stages, their conditions and outcome sets, and the modifier slots.
// The numbers (baselines, slopes, caps) are tuning data (TuningProfile). Changing any structure below is a new ProbabilityModelVersion, never an edit.

/// <summary>The version of the probability model structure. Only versions this build knows can be created; an unknown one is an error, never a default.</summary>
public readonly record struct ProbabilityModelVersion : IStringId<ProbabilityModelVersion>
{
    public const string V1Text = "prob-model-v1";

    public static ProbabilityModelVersion V1 { get; } = new() { Value = V1Text };

    public string Value { get; private init; }

    public static Result<ProbabilityModelVersion> Create(string? value) =>
        string.Equals(value, V1Text, StringComparison.Ordinal)
            ? Result<ProbabilityModelVersion>.Success(V1)
            : Result<ProbabilityModelVersion>.Failure(new Error("ProbabilityModelVersion.unsupported", $"지원하지 않는 확률 모델 버전입니다: '{value}'"));

    public override string ToString() => Value ?? "";
}

/// <summary>The version of a tuning data set (baselines, slopes, caps). Only versions this build knows can be created.</summary>
public readonly record struct TuningVersion : IStringId<TuningVersion>
{
    public const string VirtualProtoV1Text = "tuning-virtual-proto-v1";

    public static TuningVersion VirtualProtoV1 { get; } = new() { Value = VirtualProtoV1Text };

    public string Value { get; private init; }

    public static Result<TuningVersion> Create(string? value) =>
        string.Equals(value, VirtualProtoV1Text, StringComparison.Ordinal)
            ? Result<TuningVersion>.Success(VirtualProtoV1)
            : Result<TuningVersion>.Failure(new Error("TuningVersion.unsupported", $"지원하지 않는 튜닝 버전입니다: '{value}'"));

    public override string ToString() => Value ?? "";
}

/// <summary>Stable stage ids of <c>prob-model-v1</c>, in ROADMAP section 4.1 order (the step numbers 1-6 are on <see cref="StageDef.RoadmapStep"/>).</summary>
public static class StageIds
{
    public const string TargetZone = "target_zone";
    public const string ActualLocation = "actual_location";
    public const string BatterDecision = "batter_decision";
    public const string CalledPitch = "called_pitch";
    public const string SwingResult = "swing_result";
    public const string BattedBall = "batted_ball";
    public const string DefenseResult = "defense_result";
    public const string Advancement = "advancement";
}

/// <summary>One condition of a stage: its stable key and the ordered, mutually exclusive outcome ids whose probabilities sum to the denominator.</summary>
public sealed record StageCondition(string Key, IReadOnlyList<string> Outcomes);

/// <summary>
/// One stage: a conditional distribution family. <see cref="ConditionedOn"/> says in words what must already be true (the denominator of every distribution of the
/// stage is "given this"), and <see cref="Conditions"/> lists every condition key with its outcome ids. Distributions of different stages are never combined into one.
/// </summary>
public sealed record StageDef(string Id, int Order, int RoadmapStep, string ConditionedOn, IReadOnlyList<StageCondition> Conditions)
{
    public StageCondition? Find(string condition) => Conditions.FirstOrDefault(c => string.Equals(c.Key, condition, StringComparison.Ordinal));
}

public enum ModifierKind { Linear, Table }

/// <summary>Where a slot may act when the stage is keyed by a zone: everywhere, only on inner (strike-zone) cells, or only on outer zones.</summary>
public enum ZoneScope { All, Inner, Outer }

/// <summary>
/// One modifier slot of the model: which stage it acts in, which outcomes' weights it multiplies, its input kind and range, and its documented monotone direction.
/// <para>
/// <b>Linear</b> slots take a whole-number input (a 0-100 skill, or a 1-3000 permille park factor); the factor is <c>1,000,000 + (input - neutral) * slope</c>.
/// <see cref="HigherInputRaisesFactor"/> fixes the sign of the slope, so "more of this ability" moves the probability in one explainable direction.
/// <b>Table</b> slots take a named choice whose factors must be non-decreasing in the order of <see cref="TableOrder"/> (lowest effect first).
/// A <see cref="Reserved"/> slot belongs to a later package: its only legal input is the neutral one, its tuning carries no numeric authority, and the target
/// token <c>@condition</c> means "the outcome whose id equals the condition key".
/// </para>
/// </summary>
public sealed record ModifierSlot(
    string Id, string Stage, IReadOnlyList<string> Targets, ModifierKind Kind, ZoneScope Scope, int InputMin, int InputMax,
    bool HigherInputRaisesFactor, IReadOnlyList<string> TableOrder, bool Reserved, string OwnerNote);

/// <summary>A stage whose distribution the batter's hidden zone tendency may shift: the batter-favourable outcomes and the cap that +100 stands for.</summary>
public sealed record TendencyStage(string Stage, IReadOnlyList<string> Favoured, bool CapIsContact);

/// <summary>Helpers that build the stable condition keys. They are the only place the key text is produced, so a stage's condition is never assembled by hand.</summary>
public static class ConditionKeys
{
    public const string NonAutomated = "non_automated";
    public const string Automated = "automated";

    public static string Zone(PitchZoneId zone) => zone.Value;

    /// <summary>Key of the <c>called_pitch</c> stage. The mode must be declared by the rule profile; an undeclared mode is an error, never a default.</summary>
    public static Result<string> CalledPitch(CalledPitchMode? mode, PitchZoneId zone)
    {
        if (mode is null)
            return Result<string>.Failure(new Error("CalledPitchMode.undeclared", "규칙 프로필이 판정 방식(CalledPitchMode)을 선언하지 않아 콜 확률을 만들 수 없습니다."));
        if (!Enum.IsDefined(mode.Value))
            return Result<string>.Failure(new Error("CalledPitchMode.invalid", "판정 방식 값이 올바르지 않습니다."));
        var info = PitchZoneModel.V1.Info(zone);
        if (!info.IsSuccess)
            return Result<string>.Failure(info.Error!.Value);
        return Result<string>.Success($"{(mode == CalledPitchMode.Automated ? Automated : NonAutomated)}:{zone.Value}");
    }

    /// <summary>Key of the <c>batted_ball</c> stage: the centre cell is <c>heart</c>, the other eight inner cells <c>edge</c>, the eight outer zones <c>outside</c>.</summary>
    public static Result<string> BattedBallGroup(PitchZoneId zone)
    {
        var info = PitchZoneModel.V1.Info(zone);
        if (!info.IsSuccess)
            return Result<string>.Failure(info.Error!.Value);
        return Result<string>.Success(info.Value.Region == PitchZoneRegion.Outer ? "outside" : info.Value.InnerIndex == 4 ? "heart" : "edge");
    }
}

/// <summary>
/// The structure of <c>prob-model-v1</c>. Order of the stages (ROADMAP section 4.1): target zone, actual location, batter response (decision, called pitch, swing
/// result), batted ball, defence result, advancement. Each stage is conditioned on a named outcome of an earlier stage; a joint path probability is the product of its
/// stage probabilities, each over its OWN denominator, and a stage is never mixed with another stage's totals.
/// <para><b>Modifier application order</b> is the order of <see cref="Slots"/>: by stage, then pitcher, battery/catcher, batter, strategy, situation, matchup, defence, park, runner.</para>
/// </summary>
public sealed class ProbabilityModel
{
    public const string CondTarget = "@condition";

    public static ProbabilityModel V1 { get; } = BuildV1();

    public ProbabilityModelVersion Version { get; }
    public IReadOnlyList<StageDef> Stages { get; }
    public IReadOnlyList<ModifierSlot> Slots { get; }
    public IReadOnlyList<TendencyStage> TendencyStages { get; }

    // The shared static model is deeply frozen here: every nested list is copied into an ImmutableArray, so no consumer can reach an array or a List through any
    // public property - not even by casting - and one consumer can never change what another sees (ADR-006 item 13). The records stay plain positional records;
    // only the instances this class publishes are guaranteed frozen.
    private ProbabilityModel(ProbabilityModelVersion version, IEnumerable<StageDef> stages, IEnumerable<ModifierSlot> slots, IEnumerable<TendencyStage> tendencyStages)
    {
        Version = version;
        Stages = stages.Select(s => s with { Conditions = s.Conditions.Select(c => c with { Outcomes = c.Outcomes.ToImmutableArray() }).ToImmutableArray() }).ToImmutableArray();
        Slots = slots.Select(s => s with { Targets = s.Targets.ToImmutableArray(), TableOrder = s.TableOrder.ToImmutableArray() }).ToImmutableArray();
        TendencyStages = tendencyStages.Select(t => t with { Favoured = t.Favoured.ToImmutableArray() }).ToImmutableArray();
    }

    public static Result<ProbabilityModel> Resolve(ProbabilityModelVersion version) =>
        version.Value == ProbabilityModelVersion.V1Text
            ? Result<ProbabilityModel>.Success(V1)
            : Result<ProbabilityModel>.Failure(new Error("ProbabilityModelVersion.unsupported", $"지원하지 않는 확률 모델 버전입니다: '{version}'"));

    public StageDef? Stage(string id) => Stages.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    public ModifierSlot? Slot(string id) => Slots.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    public TendencyStage? TendencyFor(string stage) => TendencyStages.FirstOrDefault(t => string.Equals(t.Stage, stage, StringComparison.Ordinal));

    private static ProbabilityModel BuildV1()
    {
        var zoneIds = PitchZoneModel.V1.Zones.Select(z => z.Id.Value).ToList();

        static StageCondition Cond(string key, params string[] outcomes) => new(key, outcomes);
        StageCondition[] PerZone(params string[] outcomes) => zoneIds.Select(z => Cond(z, outcomes)).ToArray();

        string[] batted = ["ground_weak", "ground_medium", "ground_hard", "line_weak", "line_medium", "line_hard", "fly_weak", "fly_medium", "fly_hard", "popup"];
        string[] defence = ["out", "single", "double", "triple", "home_run", "reached_on_error"];
        string[] advance = ["advance_extra", "hold", "out_on_bases"];

        var stages = new List<StageDef>
        {
            new(StageIds.TargetZone, 1, 1, "the pitcher's chosen intent", [Cond("balanced", zoneIds.ToArray()), Cond("attack", zoneIds.ToArray()), Cond("careful", zoneIds.ToArray())]),
            new(StageIds.ActualLocation, 2, 2, "the target zone the pitcher aimed at",
                PerZone(zoneIds.ToArray())),
            new(StageIds.BatterDecision, 3, 3, "the actual pitch location (one of 17 zones)", PerZone("hit_by_pitch", "take", "swing")),
            new(StageIds.CalledPitch, 4, 3, "the batter took the pitch (take), per called-pitch mode and actual zone",
                new[] { ConditionKeys.Automated, ConditionKeys.NonAutomated }.SelectMany(mode => zoneIds.Select(z => Cond($"{mode}:{z}", "ball", "strike"))).ToArray()),
            new(StageIds.SwingResult, 5, 3, "the batter swung (swing), per actual zone", PerZone("miss", "foul", "in_play")),
            new(StageIds.BattedBall, 6, 4, "the ball was put in play (in_play), per location group", [Cond("heart", batted), Cond("edge", batted), Cond("outside", batted)]),
            new(StageIds.DefenseResult, 7, 5, "the kind and quality of the batted ball", batted.Select(b => Cond(b, defence)).ToArray()),
            new(StageIds.Advancement, 8, 6, "the runner situation after the defence result",
                [Cond("single_runner_on_second", advance), Cond("single_runner_on_first", advance), Cond("double_runner_on_first", advance), Cond("fly_out_runner_on_third", advance)]),
        };

        string[] hard = ["ground_hard", "line_hard", "fly_hard"];
        string[] noTable = [];
        ModifierSlot Skill(string id, string stage, string[] targets, bool higherRaises, ZoneScope scope, string note) =>
            new(id, stage, targets, ModifierKind.Linear, scope, SkillValue0, SkillValue100, higherRaises, noTable, false, note);
        ModifierSlot Reserved(string id, string stage, string[] targets, int min, int max, string note) =>
            new(id, stage, targets, ModifierKind.Linear, ZoneScope.All, min, max, true, noTable, true, note);
        ModifierSlot Table(string id, string stage, string[] targets, string[] order, string note) =>
            new(id, stage, targets, ModifierKind.Table, ZoneScope.All, 0, 0, true, order, false, note);

        var slots = new List<ModifierSlot>
        {
            Skill("pitcher.control", StageIds.ActualLocation, [CondTarget], true, ZoneScope.All, "Control raises the chance the pitch lands on the aimed zone. One slot only: Control feeds nothing else."),
            Reserved("catcher.game_calling", StageIds.ActualLocation, [CondTarget], SkillValue0, SkillValue100, "Reserved: Game Calling improves bounded execution (ROADMAP 4.1). Owner: Phase 3-D/Phase 7. Neutral input only."),
            Reserved("battery.chemistry", StageIds.ActualLocation, [CondTarget], SkillValue0, SkillValue100, "Reserved: pair BatteryChemistry affects effective performance only (C41), never the pitcher's base. Owner: Phase 7. Neutral input only."),
            Skill("batter.eye", StageIds.BatterDecision, ["swing"], false, ZoneScope.Outer, "Eye lowers the chance of swinging at a pitch outside the strike zone (chase). Outer zones only."),
            Table("strategy.approach", StageIds.BatterDecision, ["swing"], ["patient", "neutral", "aggressive"], "The selected plate-appearance approach changes the swing/take split only."),
            Table("situation.count_pressure", StageIds.BatterDecision, ["swing"], ["three_balls", "neutral", "two_strikes"], "Count pressure (a real, authoritative count) changes the swing/take split only."),
            Reserved("catcher.framing", StageIds.CalledPitch, ["strike"], SkillValue0, SkillValue100, "Reserved: Framing acts only in non_automated mode, only on Borderline zones, within a cap fixed by Phase 3-E; exactly zero in automated mode."),
            Skill("pitcher.stuff", StageIds.SwingResult, ["miss"], true, ZoneScope.All, "Stuff raises the whiff chance."),
            Skill("batter.contact", StageIds.SwingResult, ["miss"], false, ZoneScope.All, "Contact lowers the whiff chance."),
            Table("matchup.handedness", StageIds.SwingResult, ["miss"], ["opposite_hand", "neutral", "same_hand"], "Platoon: a same-handed matchup favours the pitcher, an opposite-handed one the batter. Uses the applied batter side."),
            Skill("pitcher.velocity", StageIds.BattedBall, hard, false, ZoneScope.All, "Velocity lowers the share of hard contact."),
            Skill("batter.power", StageIds.BattedBall, hard, true, ZoneScope.All, "Power raises the share of hard contact. Power acts ONLY here: home runs are not boosted a second time in defense_result."),
            Skill("defense.fielding", StageIds.DefenseResult, ["out"], true, ZoneScope.All, "ONE effective fielding value supplied by the caller (rating combined with position proficiency ONCE); it must not be applied again elsewhere."),
            Reserved("park.home_runs", StageIds.DefenseResult, ["home_run"], 1, 3000, "Reserved: C46 ParkFactorSnapshot.HomeRuns (permille). Owns home_run only. Owner: Phase 3-D. Neutral (1000) only."),
            Reserved("park.hits", StageIds.DefenseResult, ["single"], 1, 3000, "Reserved: C46 ParkFactorSnapshot.Hits (permille). Owns single only (disjoint from HR and XBH). Owner: Phase 3-D. Neutral (1000) only."),
            Reserved("park.extra_base_hits", StageIds.DefenseResult, ["double", "triple"], 1, 3000, "Reserved: C46 ParkFactorSnapshot.ExtraBaseHits (permille). Owns double and triple only. Owner: Phase 3-D. Neutral (1000) only."),
            Skill("runner.speed", StageIds.Advancement, ["advance_extra"], true, ZoneScope.All, "Runner speed raises the chance of taking the extra base."),
            Skill("defense.arm", StageIds.Advancement, ["out_on_bases"], true, ZoneScope.All, "Fielder arm raises the chance the runner is thrown out."),
        };

        var tendency = new List<TendencyStage>
        {
            new(StageIds.SwingResult, ["foul", "in_play"], true),
            new(StageIds.BattedBall, hard, false),
        };

        return new ProbabilityModel(ProbabilityModelVersion.V1, stages, slots, tendency);

    }

    private const int SkillValue0 = Players.SkillValue.Min;
    private const int SkillValue100 = Players.SkillValue.Max;
}
