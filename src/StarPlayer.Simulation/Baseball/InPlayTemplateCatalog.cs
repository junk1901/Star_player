using System.Collections.Immutable;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Domain.Players;

namespace StarPlayer.Simulation.Baseball;

// Phase 3-C2B-2 (ADR-007 "Decisions (3-C2B-2 prototype policy)", docs/game-engine.md section 23): the owner-approved template catalogue `inplay-template-v1`. Fixed fielders are
// an approved prototype simplification (U-3): they bias defensive statistics and produce no double play, fielder's choice or sacrifice. A change here is a new catalogue version.

public static class InPlayPolicy
{
    /// <summary>Generation policy: template selection and base runner movement.</summary>
    public const string MappingVersion = "inplay-mapping-v1";

    /// <summary>Official record of a game-ending hit.</summary>
    public const string WalkOffCreditVersion = "walkoff-credit-v1";

    public const string TemplateCatalogVersion = "inplay-template-v1";

    public const string CandidateContractVersion = "inplay-candidate-v1";
}

/// <summary>
/// One virtual play template: the fielders and the batter's destination of one (batted-ball class, result) pair. <see cref="BatterDestination"/> is the credited base and the
/// batter-runner's real final base (0 = out, 4 = out-of-the-park home run); <see cref="Retriever"/> only describes the virtual play.
/// </summary>
internal sealed record InPlayTemplate(
    string Id, string Class, InPlayResult Result, BattedBallType Batted, int BatterDestination, ImmutableArray<PositionCredit> Credits, FieldPosition? Error,
    FieldPosition? Retriever, ImmutableArray<string> VirtualFacts);

/// <summary>A drawn (batted ball, defence result) pair resolved to its approved template.</summary>
internal sealed record DrawnCombination(string BattedBall, string BattedBallClass, string? Intensity, string DefenseResult, InPlayTemplate Template);

internal static class InPlayTemplateCatalog
{
    /// <summary>A pop-up hit or error is a ball beyond any infielder's ordinary effort (shallow centre field): the Infield Fly rule does not apply, and an error is never moved to an infielder.</summary>
    public const string PopBeyondInfielderEffort = "pop_beyond_infielder_ordinary_effort";

    private static PositionCredit Po(FieldPosition p) => new(p, CreditKind.Putout);

    private static PositionCredit A(FieldPosition p) => new(p, CreditKind.Assist);

    private static InPlayTemplate T(string cls, InPlayResult result, BattedBallType batted, int dest, PositionCredit[] credits, FieldPosition? error, FieldPosition? retriever, params string[] facts) =>
        new($"ipt1.{cls}.{Short(result)}", cls, result, batted, dest, [.. credits], error, retriever, [.. facts]);

    private static string Short(InPlayResult r) => r switch
    {
        InPlayResult.Out => "out", InPlayResult.Single => "single", InPlayResult.Double => "double", InPlayResult.Triple => "triple",
        InPlayResult.HomeRun => "homerun", _ => "roe",
    };

    /// <summary>The 19 templates: every (class, result) pair that has at least one positive-probability combination in <c>tuning-virtual-proto-v1</c>.</summary>
    public static ImmutableArray<InPlayTemplate> All { get; } =
    [
        T("ground", InPlayResult.Out, BattedBallType.Ground, 0, [A(FieldPosition.Shortstop), Po(FieldPosition.FirstBase)], null, null, "ground_out_ss_to_first"),
        T("ground", InPlayResult.Single, BattedBallType.Ground, 1, [], null, FieldPosition.LeftField),
        T("ground", InPlayResult.Double, BattedBallType.Ground, 2, [], null, FieldPosition.LeftField),
        T("ground", InPlayResult.ReachedOnError, BattedBallType.Ground, 1, [], FieldPosition.Shortstop, null, "single_error_never_fielders_choice"),
        T("line", InPlayResult.Out, BattedBallType.Line, 0, [Po(FieldPosition.Shortstop)], null, null),
        T("line", InPlayResult.Single, BattedBallType.Line, 1, [], null, FieldPosition.CenterField),
        T("line", InPlayResult.Double, BattedBallType.Line, 2, [], null, FieldPosition.RightField),
        T("line", InPlayResult.Triple, BattedBallType.Line, 3, [], null, FieldPosition.RightField),
        T("line", InPlayResult.HomeRun, BattedBallType.Line, 4, [], null, null, "out_of_the_park_home_run"),
        T("line", InPlayResult.ReachedOnError, BattedBallType.Line, 1, [], FieldPosition.Shortstop, null, "single_error_never_fielders_choice"),
        T("fly", InPlayResult.Out, BattedBallType.Fly, 0, [Po(FieldPosition.CenterField)], null, null),
        T("fly", InPlayResult.Single, BattedBallType.Fly, 1, [], null, FieldPosition.CenterField),
        T("fly", InPlayResult.Double, BattedBallType.Fly, 2, [], null, FieldPosition.CenterField),
        T("fly", InPlayResult.Triple, BattedBallType.Fly, 3, [], null, FieldPosition.RightField),
        T("fly", InPlayResult.HomeRun, BattedBallType.Fly, 4, [], null, null, "out_of_the_park_home_run"),
        T("fly", InPlayResult.ReachedOnError, BattedBallType.Fly, 1, [], FieldPosition.CenterField, null, "single_error_never_fielders_choice"),
        T("pop", InPlayResult.Out, BattedBallType.Pop, 0, [Po(FieldPosition.SecondBase)], null, null, "pop_infielder_ordinary_catch"),
        T("pop", InPlayResult.Single, BattedBallType.Pop, 1, [], null, FieldPosition.CenterField, PopBeyondInfielderEffort),
        T("pop", InPlayResult.ReachedOnError, BattedBallType.Pop, 1, [], FieldPosition.CenterField, null, PopBeyondInfielderEffort, "single_error_never_fielders_choice"),
    ];

    /// <summary>
    /// The 45 combinations of the drawn batted ball and defence result that have a positive probability in <c>tuning-virtual-proto-v1</c>. A combination outside this set is not
    /// approved even when the class has a template for the result (a tuning change that makes one positive needs a template decision and a re-run of the matrix; a test
    /// reads the tuning and fails then).
    /// </summary>
    private static readonly Dictionary<string, string[]> Approved = new(StringComparer.Ordinal)
    {
        ["ground_weak"] = ["out", "single", "reached_on_error"],
        ["ground_medium"] = ["out", "single", "double", "reached_on_error"],
        ["ground_hard"] = ["out", "single", "double", "reached_on_error"],
        ["line_weak"] = ["out", "single", "double", "reached_on_error"],
        ["line_medium"] = ["out", "single", "double", "triple", "reached_on_error"],
        ["line_hard"] = ["out", "single", "double", "triple", "home_run", "reached_on_error"],
        ["fly_weak"] = ["out", "single", "double", "reached_on_error"],
        ["fly_medium"] = ["out", "single", "double", "triple", "home_run", "reached_on_error"],
        ["fly_hard"] = ["out", "single", "double", "triple", "home_run", "reached_on_error"],
        ["popup"] = ["out", "single", "reached_on_error"],
    };

    private static InPlayResult? ResultOf(string? text) => text switch
    {
        "out" => InPlayResult.Out, "single" => InPlayResult.Single, "double" => InPlayResult.Double, "triple" => InPlayResult.Triple,
        "home_run" => InPlayResult.HomeRun, "reached_on_error" => InPlayResult.ReachedOnError, _ => null,
    };

    public static InPlayTemplate? Find(string cls, InPlayResult result) => All.FirstOrDefault(t => t.Class == cls && t.Result == result);

    /// <summary>Resolves the 3-C2A draft's <c>batted_ball</c> and <c>defense_result</c> texts to the approved template; nothing is guessed, defaulted or mapped to a neighbour.</summary>
    public static Result<DrawnCombination> Resolve(string? battedBall, string? defenseResult)
    {
        if (battedBall is null || !Approved.TryGetValue(battedBall, out var results))
            return Fail("InPlayPlan.batted_ball_unknown", $"알 수 없는 타구 종류입니다: '{battedBall}'");
        if (ResultOf(defenseResult) is not { } result)
            return Fail("InPlayPlan.defense_result_unknown", $"알 수 없는 수비 결과입니다: '{defenseResult}'");
        var cls = battedBall == "popup" ? "pop" : battedBall.Split('_')[0];
        if (Find(cls, result) is not { } template)
            return Fail("InPlayPlan.template_unsupported", $"{cls} 타구의 '{defenseResult}' 결과에 승인된 템플릿이 없습니다.");
        if (!results.Contains(defenseResult!, StringComparer.Ordinal))
            return Fail("InPlayPlan.combination_not_approved", $"'{battedBall}' 타구와 '{defenseResult}' 결과 조합은 승인된 양의 확률 조합이 아닙니다.");
        return Result<DrawnCombination>.Success(new DrawnCombination(battedBall, cls, battedBall == "popup" ? null : battedBall.Split('_')[1], defenseResult!, template));
    }

    private static Result<DrawnCombination> Fail(string code, string message) => Result<DrawnCombination>.Failure(new Error(code, message));
}
