using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.SimHarness;

/// <summary>A run family of FC-D5. Only the <c>fc3-*</c> families are official; <c>fc2-dry</c> is the F-C2 dry run on non-official seeds.</summary>
public sealed record SeedFamilyInfo(string Id, bool Official, string Purpose);

public static class SeedFamilies
{
    public static IReadOnlyDictionary<string, SeedFamilyInfo> All { get; } = new[]
    {
        new SeedFamilyInfo("fc2-dry", false, "F-C2 독립 dry run(비공식 Seed, 파일럿·σ_g·DEFF 진단)"),
        new SeedFamilyInfo("fc3-cal", true, "F-C3 공식 calibration 층(L1~L4, 층당 100,000 PA 이상, 블록 단위)"),
        new SeedFamilyInfo("fc3-int", true, "F-C3 integration(호출 모드당 정확히 100경기, 지표는 진단)"),
        new SeedFamilyInfo("fc3-dom", true, "F-C3 지배 전략 48셀 행렬"),
    }.ToDictionary(f => f.Id, StringComparer.Ordinal);

    /// <summary>
    /// Whether <paramref name="key"/> is in an official family's competition-key namespace: the family id, or the id followed by <c>-</c> (ignoring case, so a case variant is no way out). Exactly
    /// these keys are ones an official seed file may declare; GameIds and the config hash embed the key, so a manifest cannot leave the namespace without becoming another run.
    /// </summary>
    public static bool IsOfficialCompetitionKey(string key) =>
        All.Values.Any(f => f.Official && (key.Equals(f.Id, StringComparison.OrdinalIgnoreCase) || key.StartsWith(f.Id + "-", StringComparison.OrdinalIgnoreCase)));
}

/// <summary>Pinned SHA-256 of official seed files (repository-relative path → raw-file hash). Empty: no official seed file is frozen before the Final Execution Freeze.</summary>
public static class SeedManifestPins
{
    public static IReadOnlyDictionary<string, string> Official { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record SeedEntry(int Index, ulong Seed, string GameId);

public enum BlockDecision { Continue, StopTargetReached, StopSeedsExhausted }

/// <summary>A validated seed manifest. <see cref="FileSha256"/> is the hash of the raw bytes that were read, which equalled the externally pinned hash.</summary>
public sealed record SeedManifest(
    string Family, string CompetitionKey, string CohortId, string PolicyId, string RulesProfileId, int BlockSizeGames, int MaxGames, long? PaTarget, IReadOnlyList<SeedEntry> Seeds, string FileSha256)
{
    /// <summary>
    /// FC-D5.3 stop rule, checked after <paramref name="gamesPlayed"/> games with <paramref name="cumulativePa"/> plate appearances: only at a block boundary may a run stop; it stops when the PA target
    /// (calibration only) is reached or when every declared seed has run. No seed is ever added beyond the manifest.
    /// </summary>
    public BlockDecision AfterGames(int gamesPlayed, long cumulativePa)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cumulativePa);
        if (gamesPlayed < 0 || gamesPlayed > Seeds.Count)
            throw new ArgumentOutOfRangeException(nameof(gamesPlayed), gamesPlayed, $"0~{Seeds.Count}이어야 합니다.");
        if (gamesPlayed % BlockSizeGames != 0)
            return BlockDecision.Continue;
        if (PaTarget is { } target && cumulativePa >= target)
            return BlockDecision.StopTargetReached;
        return gamesPlayed == Seeds.Count ? BlockDecision.StopSeedsExhausted : BlockDecision.Continue;
    }
}

/// <summary>
/// F-C2-A seed-manifest reader (FC-D5). The file is read only from a repository-relative <c>.json</c> path inside the given root, and only against a SHA-256 of its RAW bytes pinned outside the file;
/// then it is parsed strictly (no unknown, missing or repeated member) and checked: schema and GameId-rule versions, a known family, a competition key of that family, contiguous indices 1..n in
/// file order, unique indices and GameIds, every GameId equal to <see cref="RunConfig.Spec"/>'s <c>game:{key}:{cohort}-{index:D6}</c>, whole blocks within the declared cap, a calibration PA target
/// of at least 100,000 (FC-D7.1) and exactly 100 integration games (FC-D7.2). Block size and caps themselves are the manifest's declaration (Final Execution Freeze), not chosen here.
/// </summary>
public static class SeedManifestReader
{
    public const string SchemaVersion = "simharness-seed-manifest-v1";

    /// <summary><c>game:{competition_key}:{cohort_id}-{index:D6}</c>, the rule <see cref="RunConfig.Spec"/> implements.</summary>
    public const string GameIdRuleV1 = "game-id-v1";

    public const long CalibrationMinPa = 100_000;

    public const int IntegrationGames = 100;

    public const int MaxIndex = 999_999;

    private const int MaxFileBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    private static Result<SeedManifest> Fail(string code, string message) => Result<SeedManifest>.Failure(new Error(code, message));

    public static Result<SeedManifest> Read(string root, string relativePath, string? pinnedSha256)
    {
        if (PathProblem(root, relativePath) is { } problem)
            return Fail("Seed.path_invalid", $"Seed Manifest 경로 '{relativePath}': {problem}");
        if (LinkProblem(root, relativePath) is { } early)
            return Fail("Seed.path_reparse_point", early);
        if (pinnedSha256 is null || pinnedSha256.Length != 64 || !pinnedSha256.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f'))
            return Fail("Seed.pin_invalid", "Seed Manifest에 고정된(pin) 소문자 SHA-256 64자가 없습니다. 파일 자신의 선언은 무결성 근거가 아닙니다.");
        var full = Path.Combine(root, relativePath);
        if (!File.Exists(full))
            return Fail("Seed.file_missing", $"Seed Manifest 파일이 없습니다: '{relativePath}'.");
        if (new FileInfo(full).Length > MaxFileBytes)
            return Fail("Seed.file_too_large", $"Seed Manifest 파일이 {MaxFileBytes}바이트를 넘습니다.");
        if (LinkProblem(root, relativePath) is { } link)                                       // checked again right before the bytes are read
            return Fail("Seed.path_reparse_point", link);
        var raw = File.ReadAllBytes(full);
        var sha = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
        if (sha != pinnedSha256)
            return Fail("Seed.file_hash_mismatch", $"Seed Manifest 원본 SHA-256 {sha}이(가) 고정값 {pinnedSha256}과 다릅니다.");
        return Parse(raw, sha);
    }

    private static string? LinkProblem(string root, string rel) =>
        PathSafety.FirstReparsePoint(root, rel.Split('/')) is { } link ? $"Seed Manifest 경로에 링크/Junction(reparse point)이 있어 거부합니다: '{link}'." : null;

    private static string? PathProblem(string root, string rel)
    {
        if (string.IsNullOrEmpty(rel) || Path.IsPathRooted(rel) || rel.Contains(':') || rel.Contains('\\'))
            return "저장소 기준 상대 경로('/' 구분)여야 합니다.";
        if (rel.Split('/').Any(s => s is "" or "." or ".."))
            return "빈 구간, '.', '..'는 허용되지 않습니다.";
        if (!rel.EndsWith(".json", StringComparison.Ordinal))
            return ".json 파일이어야 합니다.";
        var rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(Path.Combine(root, rel)).StartsWith(rootFull, StringComparison.Ordinal) ? null : "루트 밖을 가리킵니다.";
    }

    private static Result<SeedManifest> Parse(byte[] raw, string sha)
    {
        Dto? d;
        try
        {
            if (DuplicateMember(raw) is { } name)
                return Fail("Seed.json_duplicate_member", $"같은 JSON 항목이 두 번 나옵니다: '{name}'.");
            d = JsonSerializer.Deserialize<Dto>(raw, Options);
        }
        catch (JsonException e)
        {
            return Fail("Seed.json_invalid", $"Seed Manifest JSON이 올바르지 않습니다: {e.Message}");
        }

        if (d is null)
            return Fail("Seed.json_invalid", "Seed Manifest가 비어 있습니다.");
        if (d.SchemaVersion != SchemaVersion)
            return Fail("Seed.schema_unsupported", $"지원하지 않는 schema_version입니다: '{d.SchemaVersion}' (지원: {SchemaVersion}).");
        if (string.IsNullOrEmpty(d.Family) || string.IsNullOrEmpty(d.CompetitionKey) || string.IsNullOrEmpty(d.CohortId) || string.IsNullOrEmpty(d.PolicyId) || string.IsNullOrEmpty(d.RulesProfileId)
            || d.Seeds is null || d.Seeds.Any(s => s is null || string.IsNullOrEmpty(s.GameId)))
            return Fail("Seed.shape_invalid", "필수 문자열·목록 항목이 null이거나 비어 있습니다.");
        if (!SeedFamilies.All.ContainsKey(d.Family))
            return Fail("Seed.family_unknown", $"알 수 없는 family입니다: '{d.Family}' (허용: {string.Join(", ", SeedFamilies.All.Keys)}).");
        if (d.GameIdRule != GameIdRuleV1)
            return Fail("Seed.game_id_rule_unknown", $"알 수 없는 game_id_rule입니다: '{d.GameIdRule}' (지원: {GameIdRuleV1}).");
        if (!GameIds.Create(d.CompetitionKey, "x").IsSuccess)
            return Fail("Seed.competition_key_invalid", $"Competition Key '{d.CompetitionKey}'는 GameId 구성 요소 규칙(영문·숫자·'_'·'-'·'.')을 따르지 않습니다.");
        if (d.CompetitionKey != d.Family && !d.CompetitionKey.StartsWith(d.Family + "-", StringComparison.Ordinal))
            return Fail("Seed.competition_key_family_mismatch", $"Competition Key '{d.CompetitionKey}'는 family '{d.Family}'의 키('{d.Family}' 또는 '{d.Family}-…')가 아닙니다.");
        if (!GameIds.Create(d.CompetitionKey, $"{d.CohortId}-{MaxIndex.ToString("D6", CultureInfo.InvariantCulture)}").IsSuccess)
            return Fail("Seed.cohort_id_invalid", $"cohort_id '{d.CohortId}'로 GameId를 만들 수 없습니다.");
        var seeds = d.Seeds.Select(s => new SeedEntry(s!.Index, s.Seed, s.GameId!)).ToList();
        if (seeds.Count == 0)
            return Fail("Seed.seeds_empty", "Seed 목록이 비어 있습니다.");
        if (d.BlockSizeGames < 1 || d.MaxGames < d.BlockSizeGames || d.MaxGames % d.BlockSizeGames != 0)
            return Fail("Seed.block_boundary_invalid", $"블록 크기 {d.BlockSizeGames}가 상한 {d.MaxGames}을(를) 정확히 나누는 1 이상의 값이 아닙니다.");
        if (d.MaxGames > MaxIndex || seeds.Count > d.MaxGames)
            return Fail("Seed.cap_exceeded", $"Seed {seeds.Count}개 / 상한 {d.MaxGames}이(가) 선언 상한(또는 {MaxIndex})을 넘습니다.");
        if (seeds.Count % d.BlockSizeGames != 0)
            return Fail("Seed.block_incomplete", $"Seed {seeds.Count}개는 블록 {d.BlockSizeGames}경기의 배수가 아닙니다(불완전한 블록).");
        if (d.Family == "fc3-cal" ? d.PaTarget is not >= CalibrationMinPa : d.PaTarget is not null)
            return Fail("Seed.pa_target_invalid", d.Family == "fc3-cal" ? $"calibration은 pa_target {CalibrationMinPa} 이상이 필요합니다." : "pa_target은 calibration(fc3-cal)에만 둡니다.");
        if (d.Family == "fc3-int" && seeds.Count != IntegrationGames)
            return Fail("Seed.integration_size_invalid", $"integration은 정확히 {IntegrationGames}경기여야 합니다(받은 값 {seeds.Count}).");
        if (seeds.GroupBy(s => s.Index).FirstOrDefault(g => g.Count() > 1) is { } index)
            return Fail("Seed.index_duplicate", $"index {index.Key}가 중복됩니다.");
        if (seeds.GroupBy(s => s.GameId, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1) is { } id)
            return Fail("Seed.game_id_duplicate", $"GameId '{id.Key}'가 중복됩니다.");
        for (var i = 0; i < seeds.Count; i++)
        {
            if (seeds[i].Index != i + 1)
                return Fail("Seed.order_invalid", $"{i + 1}번째 항목의 index가 {seeds[i].Index}입니다(1부터 연속된 파일 순서여야 함).");
            var expected = GameIds.Create(d.CompetitionKey, $"{d.CohortId}-{seeds[i].Index.ToString("D6", CultureInfo.InvariantCulture)}").Value;
            if (seeds[i].GameId != expected)
                return Fail("Seed.game_id_rule_mismatch", $"index {seeds[i].Index}의 GameId '{seeds[i].GameId}'가 규칙 값 '{expected}'와 다릅니다.");
        }

        return Result<SeedManifest>.Success(new SeedManifest(d.Family, d.CompetitionKey, d.CohortId, d.PolicyId, d.RulesProfileId, d.BlockSizeGames, d.MaxGames, d.PaTarget, seeds, sha));
    }

    /// <summary>A competition key is used by one manifest only (FC-D5.2). GameIds embed the key injectively, so unique keys also make every GameId of the set unique.</summary>
    public static Error? CheckUnique(IEnumerable<SeedManifest> manifests) =>
        manifests.GroupBy(m => m.CompetitionKey, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1) is { } reused
            ? new Error("Seed.competition_key_reused", $"Competition Key '{reused.Key}'가 Manifest {reused.Count()}개에서 쓰입니다.")
            : null;

    // System.Text.Json keeps the LAST of repeated members silently; a repeated member is refused instead.
    internal static string? DuplicateMember(byte[] raw)
    {
        var reader = new Utf8JsonReader(raw);
        var scopes = new Stack<HashSet<string>?>();
        while (reader.Read())
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject: scopes.Push(new HashSet<string>(StringComparer.Ordinal)); break;
                case JsonTokenType.StartArray: scopes.Push(null); break;
                case JsonTokenType.EndObject or JsonTokenType.EndArray: scopes.Pop(); break;
                case JsonTokenType.PropertyName when !scopes.Peek()!.Add(reader.GetString()!): return reader.GetString();
            }

        return null;
    }

    private sealed class Dto
    {
        public required string? SchemaVersion { get; set; }
        public required string? Family { get; set; }
        public required string? CompetitionKey { get; set; }
        public required string? CohortId { get; set; }
        public required string? PolicyId { get; set; }
        public required string? RulesProfileId { get; set; }
        public required string? GameIdRule { get; set; }
        public required int BlockSizeGames { get; set; }
        public required int MaxGames { get; set; }
        public required long? PaTarget { get; set; }
        public required List<EntryDto?>? Seeds { get; set; }
    }

    private sealed class EntryDto
    {
        public required int Index { get; set; }
        public required ulong Seed { get; set; }
        public required string? GameId { get; set; }
    }
}
