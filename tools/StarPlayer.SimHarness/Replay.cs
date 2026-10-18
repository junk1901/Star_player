using System.Text.Json;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.SimHarness;

/// <summary>
/// The cohorts a run or a replay may name. Only implemented cohorts resolve; an ADR-008 cohort not built yet (<c>neutral-opp-v1</c>, the strategy matrix, F-C2-C) is DECLARED here so that naming it is
/// <c>Registry.cohort_not_implemented</c> rather than unknown, and no id is ever answered with another cohort's inputs.
/// </summary>
public static class CohortRegistry
{
    private static readonly Dictionary<string, Func<CohortInputs>> Factories = new(StringComparer.Ordinal)
    {
        [NeutralCohort.Id] = NeutralCohort.Create,
        [CalibrationCohorts.NeutralMixedId] = CalibrationCohorts.NeutralMixed,
        [CalibrationCohorts.VirtualLeagueId] = CalibrationCohorts.VirtualLeague,
    };

    private static readonly HashSet<string> DeclaredNotImplemented = new(StringComparer.Ordinal) { "neutral-opp-v1" };

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

/// <summary>
/// An operator-supplied pin of a <c>fc2-dry</c> seed file (<c>replay --seed-manifest REL --seed-sha256 HEX</c>): the trusted source of a dry replay's stop parameters (ADR-009 SR-D8.6, F-C2-B decision).
/// It comes from outside the manifest, is accepted only for a <c>fc2-dry</c> competition key, and never for an official replay, which trusts <see cref="OfficialApprovals"/> alone.
/// </summary>
public sealed record DryStopPin(string Path, string Sha256);

/// <summary>What a validated manifest asks to replay: <see cref="Rules"/> is <c>automated</c> / <c>non-automated</c>, <see cref="Advancement"/> is <c>v1</c> / <c>v2</c>.</summary>
public sealed record ReplayPlan(
    string RunId, CohortInputs Cohort, IGamePolicy Policy, string Rules, string Advancement, int EventBudget, string CompetitionKey, IReadOnlyList<GameSpec> Games, string ConfigHash, string RunResultHash)
{
    /// <summary>The trusted stop parameters (from the pinned seed file) of a stop-aware replay; null for a legacy / full-execution replay.</summary>
    public StopPlan? Stop { get; init; }

    /// <summary>What the stop-aware manifest claims; compared after the re-derivation, never trusted (SR-D8.3).</summary>
    public RecordedStopResult? Recorded { get; init; }

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

    /// <summary>A key of the non-official dry-run namespace (<c>fc2-dry</c> or <c>fc2-dry-…</c>, exact case as the seed reader requires).</summary>
    public static bool IsDryCompetitionKey(string key) => key == "fc2-dry" || key.StartsWith("fc2-dry-", StringComparison.Ordinal);

    /// <summary>The manifest's result version: absent is <c>run-result-v1</c> (every manifest written before F-C2-B); otherwise exactly <c>run-result-v1</c> or <c>run-result-v2</c>.</summary>
    private static Result<string> ResultVersion(JsonElement results)
    {
        if (!results.TryGetProperty("run_result_version", out var v))
            return Result<string>.Success(RunResultText.V1);
        return v.ValueKind == JsonValueKind.String && v.GetString() is RunResultText.V1 or RunResultText.V2
            ? Result<string>.Success(v.GetString()!)
            : Result<string>.Failure(new Error("Replay.result_version_unknown", $"알 수 없는 results.run_result_version입니다(지원: {RunResultText.V1}, {RunResultText.V2})."));
    }

    /// <summary>
    /// ADR-009 SR-D7.4 / SR-D8: the required result version follows from the TRUSTED plan (the pinned seed file's family), never from the manifest. With a trusted stop plan the manifest must be v2
    /// and carry a well-formed stop record over exactly the leading games of the whole plan; without one, a v2 result or a stop record is refused.
    /// </summary>
    private static Result<ReplayPlan> WithStop(ReplayPlan plan, JsonElement results, string version, StopPlan? trusted, string competitionKey)
    {
        static Result<ReplayPlan> Bad(string code, string message) => Result<ReplayPlan>.Failure(new Error(code, message));
        var hasStop = results.TryGetProperty("stop", out var stop);
        if (trusted is null)
        {
            if (version == RunResultText.V1 && !hasStop)
                return Result<ReplayPlan>.Success(plan);
            return IsDryCompetitionKey(competitionKey)
                ? Bad("Replay.stop_parameters_untrusted", "정지 인식(v2) dry 결과의 정지 파라미터는 고정된 dry Seed 파일에서만 얻습니다(--seed-manifest/--seed-sha256 필요; 기록값은 신뢰하지 않음).")
                : Bad("Replay.stop_not_allowed", $"Competition Key '{competitionKey}'의 실행에는 정지 기록·run-result-v2가 허용되지 않습니다(정지 규칙은 fc3-cal과 fc2-dry에만, SR-D5).");
        }

        if (version != RunResultText.V2)
            return Bad("Replay.result_version_downgrade", $"고정된 Seed 파일(family '{trusted.Family}')은 {RunResultText.V2}와 정지 기록을 요구합니다(매니페스트 {version}; 자기 선언으로 낮출 수 없음).");
        if (!hasStop)
            return Bad("Replay.stop_record_missing", "정지 인식 결과에 results.stop이 없습니다(기본값으로 채우지 않음).");
        var record = StopRecord.FromJson(stop);
        if (!record.IsSuccess)
            return Result<ReplayPlan>.Failure(record.Error!.Value);
        var games = results.GetProperty("games").EnumerateArray()
            .Select(g => new RecordedGame(Required(g, "game_id"), g.GetProperty("seed").GetUInt64(), Required(g, "status"), Required(g, "result_hash"), Required(g, "event_hash"), Required(g, "rng_hash"),
                g.GetProperty("error_code").GetString())).ToList();
        if (games.Any(g => g.Status == nameof(GameRunStatus.Cancelled)))
            return Bad("Replay.run_not_terminated", "취소된 경기가 있는 실행을 정지 규칙으로 끝난 실행으로 제시할 수 없습니다(SR-D3.5).");
        var recorded = new RecordedStopResult(record.Value, results.GetProperty("plate_appearances").GetInt64(), games, results.GetProperty("games_requested").GetInt32(),
            results.GetProperty("games_completed").GetInt32(), results.GetProperty("games_failed").GetInt32(), results.GetProperty("pitches").GetInt64(), Required(results, "run_status"));
        return StopReplay.Precheck(trusted, recorded) is { } refused
            ? Result<ReplayPlan>.Failure(refused)
            : Result<ReplayPlan>.Success(plan with { Stop = trusted, Recorded = recorded });
    }

    /// <summary>A dry replay's trusted plan: the operator-pinned <c>fc2-dry</c> seed file, bound to the manifest's key, cohort, policy, rules, whole game list and recorded seed-file pin.</summary>
    private static Result<StopPlan> DryPlan(DryStopPin pin, string? repositoryRoot, JsonElement root, string competitionKey, string cohortId, string policyId, string rulesProfileId,
        IReadOnlyList<GameSpec> games)
    {
        static Result<StopPlan> Bad(string code, string message) => Result<StopPlan>.Failure(new Error(code, message));
        if (repositoryRoot is null)
            return Bad("Replay.dry_seed_manifest_unavailable", "dry Seed 파일을 읽을 저장소 루트를 알 수 없습니다.");
        var read = SeedManifestReader.Read(repositoryRoot, pin.Path, pin.Sha256);
        if (!read.IsSuccess)
            return Result<StopPlan>.Failure(read.Error!.Value);
        var sm = read.Value;
        if (sm.Family != "fc2-dry")
            return Bad("Replay.dry_seed_family_invalid", $"dry replay의 Seed 파일 family는 fc2-dry여야 합니다(받은 값 '{sm.Family}').");
        if (sm.CompetitionKey != competitionKey)
            return Bad("Replay.competition_key_mismatch", $"Competition Key가 고정된 dry Seed 파일의 '{sm.CompetitionKey}'와 다릅니다(매니페스트 '{competitionKey}').");
        if (sm.CohortId != cohortId || sm.PolicyId != policyId || sm.RulesProfileId != rulesProfileId)
            return Bad("Replay.seed_manifest_binding_mismatch", "코호트·정책·규칙 프로파일이 고정된 dry Seed 파일의 선언과 다릅니다.");
        if (!games.Select(g => new SeedEntry(g.GameIndex, g.Seed, g.GameId)).SequenceEqual(sm.Seeds))
            return Bad("Replay.seed_games_mismatch", "매니페스트의 경기 목록이 고정된 dry Seed 파일의 전체 계획과 정확히 같지 않습니다(SR-D1.1).");
        if (!root.TryGetProperty("seed_manifest", out var recorded) || recorded.ValueKind != JsonValueKind.Object
            || recorded.GetProperty("path").GetString() != pin.Path || recorded.GetProperty("sha256").GetString() != pin.Sha256)
            return Bad("Replay.seed_manifest_hash_mismatch", "매니페스트가 기록한 Seed 파일 경로·SHA-256이 지정한 dry Seed 파일 고정값과 다릅니다.");
        return StopPlan.FromSeedManifest(sm, pin.Path);
    }

    public static Result<ReplayPlan> Validate(JsonElement root, OfficialApprovals approvals, string? repositoryRoot = null, DryStopPin? dryPin = null)
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
            if (dryPin is not null && (hasEnvelope || !IsDryCompetitionKey(competitionKey)))
                return Fail("Replay.dry_seed_manifest_unexpected", $"--seed-manifest/--seed-sha256는 fc2-dry 실행의 replay에만 쓸 수 있습니다(Competition Key '{competitionKey}'; 공식 replay는 승인된 고정값만 사용).");
            var results = root.GetProperty("results");
            if (results.TryGetProperty("run_status", out var status) && status.GetString() is RunReport.StatusCancelled or RunReport.StatusAborted)
                return Fail("Replay.run_not_terminated", $"run_status '{status.GetString()}'인 실행은 정지 규칙으로 끝나지 않았으므로 replay 대상 결과가 아닙니다(SR-D3.4·SR-D3.5).");
            var version = ResultVersion(results);
            if (!version.IsSuccess)
                return Result<ReplayPlan>.Failure(version.Error!.Value);
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
            StopPlan? trusted = null;
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
                    approvals, repositoryRoot, out var verified) is { } unbound)
                    return Result<ReplayPlan>.Failure(unbound);
                if (StopPlan.FamilyIsStopAware(verified!.Family))                                  // fc3-cal: the pinned file's family requires v2 and a stop record (SR-D7.4)
                    trusted = StopPlan.FromSeedManifest(verified, a.SeedManifestPath).Value;
            }
            else if (dryPin is not null)
            {
                var dry = DryPlan(dryPin, repositoryRoot, root, competitionKey, cohort.Value.CohortId, policy.Value.Id, Required(engine, "rules_profile_id"), games.Value);
                if (!dry.IsSuccess)
                    return Result<ReplayPlan>.Failure(dry.Error!.Value);
                trusted = dry.Value;
            }

            var plan = new ReplayPlan(Required(run, "run_id"), cohort.Value, policy.Value, rules, advancement, engine.GetProperty("event_budget").GetInt32(),
                competitionKey, games.Value, configHash, Required(results, "run_result_hash"));
            return WithStop(plan, results, version.Value, trusted, competitionKey);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return Fail("Replay.manifest_invalid", $"매니페스트 형식이 올바르지 않습니다: {e.Message}");
        }
    }
}
