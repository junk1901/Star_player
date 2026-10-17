using System.Text.Json;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.SimHarness;

/// <summary>
/// The cohorts a run or a replay may name. Only implemented cohorts resolve; the ADR-008 cohorts F-C2-B will build are DECLARED here so that naming one is <c>Registry.cohort_not_implemented</c>
/// rather than unknown, and no id is ever answered with another cohort's inputs.
/// </summary>
public static class CohortRegistry
{
    private static readonly Dictionary<string, Func<CohortInputs>> Factories = new(StringComparer.Ordinal) { [NeutralCohort.Id] = NeutralCohort.Create };

    private static readonly HashSet<string> DeclaredNotImplemented = new(StringComparer.Ordinal) { "neutral-opp-v1", "neutral-mixed-v1", "virtual-league-v1" };

    public static IEnumerable<string> Implemented => Factories.Keys;

    public static Result<CohortInputs> Resolve(string? id) =>
        id is not null && Factories.TryGetValue(id, out var create) ? Result<CohortInputs>.Success(create())
        : id is not null && DeclaredNotImplemented.Contains(id) ? Result<CohortInputs>.Failure(new Error("Registry.cohort_not_implemented", $"코호트 '{id}'는 ADR-008에 선언되었지만 아직 구현되지 않았습니다(F-C2-B)."))
        : Result<CohortInputs>.Failure(new Error("Registry.cohort_unknown", $"등록되지 않은 코호트입니다: '{id}'."));
}

/// <summary>The game policies a run or a replay may name (the 12 strategy-matrix policies are F-C2-C).</summary>
public static class PolicyRegistry
{
    private static readonly Dictionary<string, Func<IGamePolicy>> Factories = new(StringComparer.Ordinal) { ["normal-balanced-v1"] = () => new NormalBalancedPolicy() };

    public static IEnumerable<string> Implemented => Factories.Keys;

    public static Result<IGamePolicy> Resolve(string? id) =>
        id is not null && Factories.TryGetValue(id, out var create) ? Result<IGamePolicy>.Success(create())
        : Result<IGamePolicy>.Failure(new Error("Registry.policy_unknown", $"등록되지 않은 정책입니다: '{id}'."));
}

/// <summary>What a validated manifest asks to replay: <see cref="Rules"/> is <c>automated</c> / <c>non-automated</c>, <see cref="Advancement"/> is <c>v1</c> / <c>v2</c>.</summary>
public sealed record ReplayPlan(
    string RunId, CohortInputs Cohort, IGamePolicy Policy, string Rules, string Advancement, int EventBudget, string CompetitionKey, IReadOnlyList<GameSpec> Games, string ConfigHash, string RunResultHash)
{
    /// <summary>
    /// The check that needs the rebuilt configuration, made BEFORE any game starts: every recorded game must be exactly what the configuration's own rule makes of its index and seed (the recorded
    /// id is never replaced by a regenerated one), and the configuration hash of those games must be the recorded one (so a changed seed is refused here, not only noticed in the result hash).
    /// </summary>
    public Error? CheckAgainst(RunConfig config)
    {
        foreach (var game in Games)
            if (config.Spec(game.GameIndex, game.Seed) != game)
                return new Error("Replay.game_id_mismatch", $"기록된 경기 {game.GameIndex}의 GameId '{game.GameId}'가 실행 설정이 만드는 값과 다릅니다.");
        return config.ConfigHash(Games) == ConfigHash ? null : new Error("Replay.config_hash_mismatch", "기록된 경기 목록(index·GameId·Seed)으로 만든 config_hash가 매니페스트의 config_hash와 다릅니다.");
    }
}

/// <summary>
/// F-C2-A replay validation: the manifest and input schema versions must be the supported ones; the cohort and policy are looked up in the registries and the rebuilt cohort must have the recorded
/// input hash; the rules profile and advancement policy must be exactly one of the known values (nothing falls back to a default); a run on a test tuning file is refused. Checked first: a manifest
/// whose mode is <c>official</c> or whose competition key is in an official family's namespace (<see cref="SeedFamilies.IsOfficialCompetitionKey"/>) must carry the official envelope, and an envelope
/// requires mode <c>official</c>, so no official-family manifest reaches the legacy path. The envelope is replayed only after <see cref="OfficialReplay.Verify"/> and only when its attestation agrees
/// with the manifest. The game hashes are then compared by the caller.
/// </summary>
public static class ReplayCheck
{
    /// <summary>
    /// The RECORDED games, checked before anything else uses them: at least one; no repeated index or GameId; indices 1..n in file order; every GameId equal to the rule's value for the recorded
    /// competition key, cohort and index (<see cref="RunConfig.Spec"/>). The recomputed id is a check only: a mismatch is refused, never repaired.
    /// </summary>
    private static Result<IReadOnlyList<GameSpec>> RecordedGames(JsonElement element, string competitionKey, string cohortId)
    {
        static Result<IReadOnlyList<GameSpec>> Bad(string code, string message) => Result<IReadOnlyList<GameSpec>>.Failure(new Error(code, message));
        var games = element.EnumerateArray().Select(g => new GameSpec(g.GetProperty("index").GetInt32(), g.GetProperty("game_id").GetString() ?? "", g.GetProperty("seed").GetUInt64())).ToList();
        if (games.Count == 0)
            return Bad("Replay.game_list_mismatch", "매니페스트의 경기 목록이 비어 있습니다.");
        if (games.GroupBy(g => g.GameIndex).FirstOrDefault(g => g.Count() > 1) is { } index)
            return Bad("Replay.game_identity_duplicate", $"기록된 경기 index {index.Key}가 중복됩니다.");
        if (games.GroupBy(g => g.GameId, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1) is { } id)
            return Bad("Replay.game_identity_duplicate", $"기록된 GameId '{id.Key}'가 중복됩니다.");
        for (var i = 0; i < games.Count; i++)
        {
            if (games[i].GameIndex != i + 1)
                return Bad("Replay.game_index_mismatch", $"{i + 1}번째 경기의 기록된 index가 {games[i].GameIndex}입니다(1부터 연속된 기록 순서여야 함).");
            var expected = GameIds.Create(competitionKey, $"{cohortId}-{games[i].GameIndex.ToString("D6", System.Globalization.CultureInfo.InvariantCulture)}");
            if (!expected.IsSuccess || games[i].GameId != expected.Value)
                return Bad("Replay.game_id_mismatch", $"경기 {games[i].GameIndex}의 기록된 GameId '{games[i].GameId}'가 규칙 값 '{(expected.IsSuccess ? expected.Value : "(생성 불가)")}'와 다릅니다.");
        }

        return Result<IReadOnlyList<GameSpec>>.Success(games);
    }

    /// <summary>A required string field: a JSON <c>null</c> (which <c>GetString</c> returns as null) is a malformed manifest, never a null reference later on.</summary>
    private static string Required(JsonElement parent, string name) =>
        parent.GetProperty(name).GetString() ?? throw new FormatException($"필수 문자열 필드 '{name}'가 null입니다.");

    public static Result<ReplayPlan> Validate(JsonElement root, OfficialApprovals approvals, string? repositoryRoot = null)
    {
        static Result<ReplayPlan> Fail(string code, string message) => Result<ReplayPlan>.Failure(new Error(code, message));
        try
        {
            if (root.GetProperty("manifest_version").GetString() != RunReport.ManifestVersion)
                return Fail("Replay.schema_unsupported", $"지원하지 않는 manifest_version입니다(지원: {RunReport.ManifestVersion}).");
            var run = root.GetProperty("run");
            if (run.GetProperty("input_schema_version").GetString() != RunConfig.InputSchemaVersion)
                return Fail("Replay.schema_unsupported", $"지원하지 않는 input_schema_version입니다(지원: {RunConfig.InputSchemaVersion}).");
            var engine = root.GetProperty("engine");
            var competitionKey = Required(engine, "competition_key");
            var mode = run.GetProperty("mode").GetString();
            var hasEnvelope = root.TryGetProperty("official", out var official);
            // the competition key is the official marker a manifest cannot drop (GameIds and the config hash embed it); the envelope and the mode label can be removed or edited, so all three must agree
            if (!hasEnvelope && (mode == "official" || SeedFamilies.IsOfficialCompetitionKey(competitionKey)))
                return Fail("Replay.official_envelope_missing", $"공식 계열 경기(mode '{mode}', Competition Key '{competitionKey}')의 매니페스트에 official envelope가 없습니다(공식 replay를 일반 replay로 낮출 수 없음).");
            if (hasEnvelope && mode != "official")
                return Fail("Replay.official_mode_mismatch", $"official envelope가 있는 매니페스트의 mode가 'official'이 아닙니다(받은 값 '{mode}').");
            if (engine.TryGetProperty("tuning_file_sha256", out var customTuning) && customTuning.ValueKind == JsonValueKind.String)
                return Fail("Replay.test_tuning_not_replayable", "replay는 --tuning으로 읽은 시험용 튜닝 파일의 실행을 다시 만들 수 없습니다(매니페스트에 파일 해시만 있음).");
            var cohort = CohortRegistry.Resolve(run.GetProperty("cohort_id").GetString());
            if (!cohort.IsSuccess)
                return Result<ReplayPlan>.Failure(cohort.Error!.Value);
            if (cohort.Value.InputsHash != engine.GetProperty("cohort_inputs_hash").GetString())
                return Fail("Replay.cohort_hash_mismatch", $"코호트 '{cohort.Value.CohortId}'의 입력 해시가 매니페스트 기록과 다릅니다.");
            var policy = PolicyRegistry.Resolve(engine.GetProperty("policy_id").GetString());
            if (!policy.IsSuccess)
                return Result<ReplayPlan>.Failure(policy.Error!.Value);
            var rules = engine.GetProperty("rules_profile_id").GetString() switch
            {
                HarnessData.AutomatedProfile => "automated",
                HarnessData.NonAutomatedProfile => "non-automated",
                _ => null,
            };
            if (rules is null)
                return Fail("Replay.rules_unknown", $"알 수 없는 규칙 프로파일입니다: '{engine.GetProperty("rules_profile_id").GetString()}'.");
            var advancement = engine.GetProperty("advancement_policy_version").GetString() switch
            {
                AdvancementPolicy.Version => "v1",
                AdvancementPolicy.VersionV2 => "v2",
                _ => null,
            };
            if (advancement is null)
                return Fail("Replay.advancement_unknown", $"알 수 없는 진루 정책입니다: '{engine.GetProperty("advancement_policy_version").GetString()}'.");
            var configHash = Required(run, "config_hash");
            var games = RecordedGames(root.GetProperty("games"), competitionKey, cohort.Value.CohortId);
            if (!games.IsSuccess)
                return Result<ReplayPlan>.Failure(games.Error!.Value);
            if (hasEnvelope)
            {
                if (OfficialReplay.Verify(official, configHash, approvals) is { } refused)
                    return Result<ReplayPlan>.Failure(refused);
                var a = OfficialAttestation.FromJson(official.GetProperty("attestation")).Value;
                var recorded = (engine.GetProperty("competition_key").GetString(), run.GetProperty("cohort_id").GetString(), engine.GetProperty("cohort_inputs_hash").GetString(),
                    engine.GetProperty("policy_id").GetString(), engine.GetProperty("rules_profile_id").GetString(), engine.GetProperty("rules_hash").GetString(),
                    engine.GetProperty("probability_model_version").GetString(), engine.GetProperty("tuning_version").GetString(), engine.GetProperty("tuning_hash").GetString(),
                    engine.GetProperty("advancement_policy_version").GetString(), engine.GetProperty("event_budget").GetInt32());
                if (recorded != (a.CompetitionKey, a.CohortId, a.CohortInputsHash, a.PolicyId, a.RulesProfileId, a.RulesHash, a.ModelVersion, a.TuningVersion, a.TuningHash, a.AdvancementPolicyVersion, a.EventBudget))
                    return Fail("Replay.attestation_manifest_mismatch", "공식 Attestation의 입력이 매니페스트의 실행 기록과 다릅니다.");
                var park = engine.GetProperty("park");
                var parkText = $"{park.GetProperty("stadium").GetString()}|{park.GetProperty("profile").GetString()}|{park.GetProperty("version").GetInt32()}|{park.GetProperty("home_runs_permille").GetInt32()}|"
                    + $"{park.GetProperty("hits_permille").GetInt32()}|{park.GetProperty("extra_base_hits_permille").GetInt32()}";
                if (OfficialReplay.VerifyAgainstSeedFile(a, new RecordedRun(competitionKey, cohort.Value.CohortId, policy.Value.Id, engine.GetProperty("rules_profile_id").GetString()!, parkText, games.Value),
                    approvals, repositoryRoot) is { } unbound)
                    return Result<ReplayPlan>.Failure(unbound);
            }

            return Result<ReplayPlan>.Success(new ReplayPlan(Required(run, "run_id"), cohort.Value, policy.Value, rules, advancement, engine.GetProperty("event_budget").GetInt32(),
                competitionKey, games.Value, configHash, Required(root.GetProperty("results"), "run_result_hash")));
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return Fail("Replay.manifest_invalid", $"매니페스트 형식이 올바르지 않습니다: {e.Message}");
        }
    }
}
