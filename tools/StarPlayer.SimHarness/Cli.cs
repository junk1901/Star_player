using System.Globalization;
using StarPlayer.Domain.Common;

namespace StarPlayer.SimHarness;

public enum OptionKind { Seeds, Choice, Count, Text }

/// <summary>One option of one command: what its value must look like (<see cref="Choices"/> only for <see cref="OptionKind.Choice"/>).</summary>
public sealed record OptionSpec(OptionKind Kind, IReadOnlyList<string>? Choices = null);

/// <summary>A command's whitelist: the options it accepts, the ones it requires, and options that are refused with a specific reason (rather than as merely unknown).</summary>
public sealed record CommandSpec(IReadOnlyDictionary<string, OptionSpec> Options, IReadOnlySet<string> Required, IReadOnlyDictionary<string, string> Forbidden);

/// <summary>A parsed command line: the command and each given option's value exactly as typed (defaults are the command's business, never the parser's).</summary>
public sealed record CliCommand(string Name, IReadOnlyDictionary<string, string> Options);

/// <summary>
/// F-C2-A strict command line (FC-D10). <see cref="Commands"/> is the single authority for what each command accepts. Unknown commands and options, a repeated option (even with the same value),
/// an option without a value, a value of the wrong form, a positional argument and a forbidden option are all refused with a typed <c>Cli.*</c> code naming the argument; nothing is ignored, merged
/// or replaced by a default. Only <c>--name value</c> is accepted (no <c>--name=value</c>, no abbreviations, case-sensitive).
/// </summary>
public static class Cli
{
    private static readonly OptionSpec PathText = new(OptionKind.Text);

    private static readonly string[] OfficialFixed = ["seeds", "tuning", "rules", "advancement", "event-budget"];

    public static IReadOnlyDictionary<string, CommandSpec> Commands { get; } = new Dictionary<string, CommandSpec>(StringComparer.Ordinal)
    {
        ["run"] = new(new Dictionary<string, OptionSpec>(StringComparer.Ordinal)
        {
            ["seeds"] = new(OptionKind.Seeds),
            ["rules"] = new(OptionKind.Choice, ["non-automated", "automated"]),
            ["advancement"] = new(OptionKind.Choice, ["v1", "v2"]),
            ["event-budget"] = new(OptionKind.Count),
            ["tuning"] = PathText,
            ["run-id"] = PathText,
            ["out"] = PathText,
        }, new HashSet<string>(), new Dictionary<string, string>()),
        // --seed-manifest / --seed-sha256: the operator's pin of a fc2-dry seed file, the trusted stop parameters of a stop-aware dry replay (ADR-009 SR-D8.6); refused for every other replay
        ["replay"] = new(new Dictionary<string, OptionSpec>(StringComparer.Ordinal) { ["manifest"] = PathText, ["seed-manifest"] = PathText, ["seed-sha256"] = PathText },
            new HashSet<string> { "manifest" }, new Dictionary<string, string>()),
        // the non-official stop-aware dry run (ADR-009 SR-D4): its seeds, cohort, policy, rules, block size and small PA target come only from the pinned fc2-dry seed file
        ["dry"] = new(new Dictionary<string, OptionSpec>(StringComparer.Ordinal) { ["seed-manifest"] = PathText, ["seed-sha256"] = PathText, ["run-id"] = PathText, ["out"] = PathText },
            new HashSet<string> { "seed-manifest", "seed-sha256" },
            OfficialFixed.ToDictionary(o => o, o => o == "tuning"
                ? "--tuning은 시험용 입력이므로 dry run에서도 사용할 수 없습니다."
                : $"--{o}는 dry run에서 지정할 수 없습니다(고정된 dry Seed 파일과 고정 실행 프로파일이 결정합니다).", StringComparer.Ordinal)),
        // an official run takes its seeds, cohort, policy and rules profile only from the pinned seed manifest and the approved execution profile, never from the command line
        ["official"] = new(new Dictionary<string, OptionSpec>(StringComparer.Ordinal) { ["seed-manifest"] = PathText, ["run-id"] = PathText, ["out"] = PathText },
            new HashSet<string> { "seed-manifest", "run-id" },
            OfficialFixed.ToDictionary(o => o, o => o == "tuning"
                ? "--tuning은 시험용 입력이므로 공식 실행에서 사용할 수 없습니다(FC-D10)."
                : $"--{o}는 공식 실행에서 지정할 수 없습니다(승인된 Seed Manifest와 실행 프로파일이 결정합니다).", StringComparer.Ordinal)),
    };

    public static Result<CliCommand> Parse(IReadOnlyList<string> args)
    {
        static Result<CliCommand> Fail(string code, string message) => Result<CliCommand>.Failure(new Error(code, message));
        if (args.Count == 0)
            return Fail("Cli.command_missing", "명령이 없습니다.");
        if (!Commands.TryGetValue(args[0], out var spec))
            return Fail("Cli.command_unknown", $"알 수 없는 명령입니다: '{args[0]}' (허용: {string.Join(", ", Commands.Keys)}).");
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Count; i += 2)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                return Fail("Cli.argument_unexpected", $"예상하지 못한 위치 인수입니다: '{token}'.");
            var name = token[2..];
            if (spec.Forbidden.TryGetValue(name, out var reason))
                return Fail("Cli.option_forbidden", $"{token}: {reason}");
            if (!spec.Options.TryGetValue(name, out var option))
                return Fail("Cli.option_unknown", $"'{args[0]}' 명령에 없는 옵션입니다: '{token}'.");
            if (options.ContainsKey(name))
                return Fail("Cli.option_duplicate", $"옵션이 두 번 이상 지정되었습니다: '{token}'.");
            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                return Fail("Cli.value_missing", $"옵션 '{token}'에 값이 없습니다.");
            var value = args[i + 1];
            if (Validate(option, value) is { } problem)
                return Fail("Cli.value_invalid", $"옵션 '{token}'의 값 '{value}'이(가) 올바르지 않습니다: {problem}");
            options[name] = value;
        }

        var missing = spec.Required.Where(r => !options.ContainsKey(r)).Order(StringComparer.Ordinal).FirstOrDefault();
        return missing is not null
            ? Fail("Cli.option_required", $"'{args[0]}' 명령에는 '--{missing}'가 필요합니다.")
            : Result<CliCommand>.Success(new CliCommand(args[0], options));
    }

    /// <summary>A comma-separated list of canonical unsigned 64-bit integers: digits only, no sign, space, empty item or leading zero. A repeated seed is legal (each game has its own GameId).</summary>
    public static Result<IReadOnlyList<ulong>> ParseSeeds(string text)
    {
        var seeds = new List<ulong>();
        foreach (var item in text.Split(','))
        {
            if (!IsCanonicalDigits(item) || !ulong.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
                return Result<IReadOnlyList<ulong>>.Failure(new Error("Cli.value_invalid", $"Seed는 부호·공백 없는 0~{ulong.MaxValue} 정수를 쉼표로 나열해야 합니다(받은 항목: '{item}')."));
            seeds.Add(seed);
        }

        return Result<IReadOnlyList<ulong>>.Success(seeds);
    }

    private static string? Validate(OptionSpec option, string value) => option.Kind switch
    {
        OptionKind.Seeds => ParseSeeds(value).Error?.Message,
        OptionKind.Choice => option.Choices!.Contains(value, StringComparer.Ordinal) ? null : $"허용 값은 {string.Join(" | ", option.Choices!)}입니다.",
        OptionKind.Count => IsCanonicalDigits(value) && value.Length <= 9 ? null : "부호 없는 9자리 이하 정수여야 합니다.",
        _ => value.Length == 0 ? "빈 값은 허용되지 않습니다." : null,
    };

    private static bool IsCanonicalDigits(string s) => s.Length > 0 && s.All(char.IsAsciiDigit) && (s.Length == 1 || s[0] != '0');
}
