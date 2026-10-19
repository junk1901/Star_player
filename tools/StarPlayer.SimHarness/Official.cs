using System.Text;
using System.Text.Json;
using StarPlayer.Domain.Baseball;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.Probability;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.SimHarness;

/// <summary>
/// The fixed official run profile of ADR-008 FC-D7.5: <c>prob-model-v4</c> / <c>tuning-virtual-proto-v4</c> (ContentHash pinned), the two <c>gamerules-v2</c> fictional-test profiles (their committed
/// content hashes), <c>advancement-policy-v2</c>, park <c>pf-sim-neutral</c> v1, event budget 3,000; plus the cohorts ADR-008 assigns per official family. <see cref="Id"/> / <see cref="Hash"/> name this
/// profile as an execution policy; it becomes approved only when <see cref="OfficialApprovals"/> names the same id and hash (not in F-C2-A).
/// </summary>
public static class OfficialProfile
{
    public const string Id = "fc3-official-execution-v1";
    public const string ModelVersion = ProbabilityModelVersion.V4Text;
    public const string TuningVersion = "tuning-virtual-proto-v4";
    public const string TuningHash = "5011f8acaae4848481db6bc60f636d7594b2a30fcdf594aeab375f0aa98b8fd8";
    public const string AdvancementPolicyVersion = AdvancementPolicy.VersionV2;
    public const int EventBudget = 3000;
    public const string ParkText = "stadium-sim-neutral|pf-sim-neutral|1|1000|1000|1000";

    public static IReadOnlyDictionary<string, string> RulesHashes { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [HarnessData.AutomatedProfile] = "7ad9776729b4a71d60af352d620df561d02f699e036fd6f4a4870b82635f4fa7",
        [HarnessData.NonAutomatedProfile] = "2d3f7557750c65d920ee79ec0ff57121ae8b8990f13368ae4daa7520492dce2d",
    };

    /// <summary>FC-D7 (calibration strata) and FC-D4 (strategy matrix). ADR-008 names no cohort for <c>fc3-int</c>, so an integration manifest is NOT_READY until one is declared.</summary>
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> FamilyCohorts { get; } = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
    {
        ["fc3-cal"] = new HashSet<string>(StringComparer.Ordinal) { "neutral-mixed-v1", "virtual-league-v1" },
        ["fc3-dom"] = new HashSet<string>(StringComparer.Ordinal) { "neutral-v1", "neutral-opp-v1" },
    };

    /// <summary>FC-D3: calibration and integration measure with <c>normal-balanced-v1</c> only.</summary>
    public const string CalibrationPolicy = "normal-balanced-v1";

    /// <summary>
    /// The approved cohorts of an official family: <see cref="FamilyCohorts"/>, or for <c>fc3-int</c> (which ADR-008 does not name) the freeze slot <see cref="OfficialApprovals.IntegrationCohortIds"/>;
    /// null (undeclared, NOT_READY) for every other case. The slot is null in <see cref="OfficialApprovals.Current"/>, so production integration stays undeclared.
    /// </summary>
    public static IReadOnlySet<string>? CohortsOf(string family, OfficialApprovals a) =>
        FamilyCohorts.TryGetValue(family, out var cohorts) ? cohorts : family == "fc3-int" ? a.IntegrationCohortIds : null;

    public static string ParkTextOf(ParkFactorSnapshot p) => $"{p.Stadium.Value}|{p.Profile.Value}|{p.Version}|{p.HomeRuns.Permille}|{p.Hits.Permille}|{p.ExtraBaseHits.Permille}";

    public static string Hash()
    {
        var t = new StringBuilder("official-execution-profile-v1\n").Append(Id).Append('\n').Append(ModelVersion).Append('|').Append(TuningVersion).Append('|').Append(TuningHash).Append('\n');
        foreach (var r in RulesHashes.OrderBy(r => r.Key, StringComparer.Ordinal))
            t.Append("rules=").Append(r.Key).Append('|').Append(r.Value).Append('\n');
        t.Append(AdvancementPolicyVersion).Append('\n').Append(ParkText).Append('\n').Append(EventBudget).Append('\n').Append(SeedManifestReader.GameIdRuleV1).Append('\n');
        foreach (var f in FamilyCohorts.OrderBy(f => f.Key, StringComparer.Ordinal))
            t.Append("family=").Append(f.Key).Append('|').Append(string.Join(',', f.Value.Order(StringComparer.Ordinal))).Append('\n');
        return Hashing.Sha256(t.Append("calibration_policy=").Append(CalibrationPolicy).Append('\n').ToString());
    }
}

/// <summary>
/// Everything that must be frozen before an official run (Final Execution Freeze). <see cref="Current"/> is F-C2-A's truth: nothing is approved, so no official run or official replay can start.
/// Any other instance exists only in tests. <see cref="DominanceGamesPerCell"/> is the frozen per-cell sample size of the 48-cell matrix (ADR-008 FC-D4.7); null until that freeze.
/// <see cref="IntegrationCohortIds"/> is the owner's declaration of the <c>fc3-int</c> cohort(s), which ADR-008 FC-D7.2 does not name (F-C2 B-6; an open owner decision, no value is chosen here):
/// null until declared, so every integration preflight and replay stays NOT_READY / refused.
/// </summary>
public sealed record OfficialApprovals(
    string? GitCommitSha, string? ExecutionPolicyId, string? ExecutionPolicyHash, IReadOnlyDictionary<string, string> SeedManifestPins, IReadOnlyDictionary<string, string> CohortInputsHashes,
    bool ExecutionPathReady, int? DominanceGamesPerCell = null, IReadOnlySet<string>? IntegrationCohortIds = null)
{
    public static OfficialApprovals Current { get; } = new(null, null, null, StarPlayer.SimHarness.SeedManifestPins.Official, new Dictionary<string, string>(StringComparer.Ordinal), false);
}

/// <summary>
/// The versioned binding of every input that gives an official run its meaning, kept separate from the unchanged <see cref="RunConfig.ConfigHash"/> (which it includes as <see cref="ConfigHash"/>).
/// <see cref="Hash"/> covers every field; time, machine and log facts are not in it.
/// </summary>
public sealed record OfficialAttestation(
    string SchemaVersion, string GitCommitSha, string ExecutionPolicyId, string ExecutionPolicyHash, string SeedManifestPath, string SeedManifestSha256, string SeedFamily, string CompetitionKey,
    string GameIdRule, string CohortId, string CohortInputsHash, string PolicyId, string ModelVersion, string TuningVersion, string TuningHash, string RulesProfileId, string RulesHash,
    string AdvancementPolicyVersion, string ParkSnapshot, int EventBudget, string ConfigHash)
{
    public const string Version = "simharness-official-attestation-v1";

    /// <summary>The <c>official</c> envelope of a manifest: <c>{ schema_version, attestation, attestation_hash }</c>.</summary>
    public const string EnvelopeVersion = "simharness-official-v1";

    private static readonly JsonSerializerOptions Strict = new(RunReport.Json) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    private (string Name, string Value)[] Fields() =>
    [
        ("schema_version", SchemaVersion), ("git_commit_sha", GitCommitSha), ("execution_policy_id", ExecutionPolicyId), ("execution_policy_hash", ExecutionPolicyHash),
        ("seed_manifest_path", SeedManifestPath), ("seed_manifest_sha256", SeedManifestSha256), ("seed_family", SeedFamily), ("competition_key", CompetitionKey), ("game_id_rule", GameIdRule),
        ("cohort_id", CohortId), ("cohort_inputs_hash", CohortInputsHash), ("policy_id", PolicyId), ("model_version", ModelVersion), ("tuning_version", TuningVersion), ("tuning_hash", TuningHash),
        ("rules_profile_id", RulesProfileId), ("rules_hash", RulesHash), ("advancement_policy_version", AdvancementPolicyVersion), ("park_snapshot", ParkSnapshot),
        ("event_budget", EventBudget.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("config_hash", ConfigHash),
    ];

    public string Hash()
    {
        var t = new StringBuilder("official-attestation-hash-v1\n");
        foreach (var (name, value) in Fields())
            t.Append(name).Append('=').Append(value).Append('\n');
        return Hashing.Sha256(t.ToString());
    }

    /// <summary>Strict read of an attestation object: no unknown member; every field present, non-empty and free of control characters; a positive event budget. The schema version is the caller's check.</summary>
    public static Result<OfficialAttestation> FromJson(JsonElement element)
    {
        static Result<OfficialAttestation> Bad(string why) => Result<OfficialAttestation>.Failure(new Error("Official.attestation_invalid", $"공식 Attestation이 올바르지 않습니다: {why}"));
        OfficialAttestation? a;
        try
        {
            a = element.Deserialize<OfficialAttestation>(Strict);
        }
        catch (JsonException e)
        {
            return Bad(e.Message);
        }

        if (a is null)
            return Bad("비어 있습니다.");
        if (a.Fields().FirstOrDefault(f => string.IsNullOrEmpty(f.Value) || f.Value.Any(char.IsControl)) is { Name: { } empty })
            return Bad($"'{empty}'가 없거나 비어 있습니다.");
        return a.EventBudget < 1 ? Bad("event_budget는 1 이상이어야 합니다.") : Result<OfficialAttestation>.Success(a);
    }
}

public enum CheckStatus { Pass, Fail, NotReady }

public sealed record PreflightCheck(string Id, CheckStatus Status, string? Code, string Message);

public sealed record OfficialRequest(string RepositoryRoot, string SeedManifestPath, string RunId, string? OutDirectory);

/// <summary>The outcome of a preflight. <see cref="Ready"/> only when every check passed; <see cref="Attestation"/> exists only then.</summary>
public sealed record PreflightReport(
    IReadOnlyList<PreflightCheck> Checks, string? HeadSha, TuningProfile? Tuning, SeedManifest? Manifest, GameRuleSnapshot? Rules, string? OutputDirectory, OfficialAttestation? Attestation)
{
    public bool Ready => Checks.All(c => c.Status == CheckStatus.Pass);

    /// <summary>The first failed check's code; a preflight that failed nothing but is not ready is <c>Official.not_ready</c>.</summary>
    public string? ErrorCode => Ready ? null : Checks.FirstOrDefault(c => c.Status == CheckStatus.Fail)?.Code ?? "Official.not_ready";
}

/// <summary>
/// F-C2-A official preflight (FC-D10): every check runs and is reported (fail-closed; nothing short-circuits to a pass). It reads only a seed file whose raw hash is pinned by the approvals, writes
/// nothing and starts no game. With <see cref="OfficialApprovals.Current"/> the Git SHA, seed manifests, cohort input hashes and execution policy are unfrozen and the F-C2-B/C evaluation path does
/// not exist, so the answer is always NOT_READY.
/// </summary>
public static class OfficialPreflight
{
    public static PreflightReport Run(OfficialRequest r, OfficialApprovals a, GitIntegrityReport git)
    {
        var checks = new List<PreflightCheck>();
        void Add(string id, CheckStatus status, string? code, string message) => checks.Add(new PreflightCheck(id, status, code, message));
        void Unavailable(string id) => Add(id, CheckStatus.NotReady, "Official.input_unavailable", "앞선 검사가 통과하지 않아 확인할 입력이 없습니다.");

        Add("cli", CheckStatus.Pass, null, "명령줄이 엄격 파서를 통과했습니다.");
        var tuning = EnvironmentChecks(checks, a, git);

        SeedManifest? manifest = null;
        var pinned = a.SeedManifestPins.TryGetValue(r.SeedManifestPath, out var pin);
        var read = SeedManifestReader.Read(r.RepositoryRoot, r.SeedManifestPath, pinned ? pin : null);
        if (read.IsSuccess)
        {
            manifest = read.Value;
            Add("seed_manifest", CheckStatus.Pass, null, $"{r.SeedManifestPath} SHA-256 {manifest.FileSha256} = 고정값");
        }
        else if (!pinned && read.Error!.Value.Code == "Seed.pin_invalid")
            Add("seed_manifest", CheckStatus.NotReady, "Official.seed_manifest_not_frozen", $"'{r.SeedManifestPath}'에 고정된 공식 Seed Manifest 해시가 없습니다(Final Execution Freeze 전).");
        else
            Add("seed_manifest", CheckStatus.Fail, read.Error!.Value.Code, read.Error.Value.Message);

        if (manifest is null)
            Unavailable("family");
        else if (!SeedFamilies.All[manifest.Family].Official)
            Add("family", CheckStatus.Fail, "Official.family_not_official", $"family '{manifest.Family}'는 공식 계열이 아닙니다.");
        else
            Add("family", CheckStatus.Pass, null, manifest.Family);

        GameRuleSnapshot? rules = null;
        if (manifest is null)
            Unavailable("rules");
        else if (!OfficialProfile.RulesHashes.TryGetValue(manifest.RulesProfileId, out var rulesHash))
            Add("rules", CheckStatus.Fail, "Official.rules_not_approved", $"규칙 프로파일 '{manifest.RulesProfileId}'는 공식 실행 프로파일에 없습니다.");
        else if (HarnessData.LoadRules(manifest.RulesProfileId) is not { IsSuccess: true } loaded || loaded.Value.ContentHash != rulesHash)
            Add("rules", CheckStatus.Fail, "Official.rules_hash_mismatch", $"규칙 프로파일 '{manifest.RulesProfileId}'의 내용 해시가 승인값과 다릅니다.");
        else
        {
            rules = loaded.Value;
            Add("rules", CheckStatus.Pass, null, $"{rules.ProfileId} {rules.ContentHash}");
        }

        IGamePolicy? policy = null;
        if (manifest is null)
            Unavailable("policy");
        else if (PolicyRegistry.Resolve(manifest.PolicyId) is { IsSuccess: false } unknownPolicy)
            Add("policy", CheckStatus.Fail, unknownPolicy.Error!.Value.Code, unknownPolicy.Error.Value.Message);
        else if (manifest.Family is "fc3-cal" or "fc3-int" && manifest.PolicyId != OfficialProfile.CalibrationPolicy)
            Add("policy", CheckStatus.Fail, "Official.policy_not_approved", $"{manifest.Family}는 '{OfficialProfile.CalibrationPolicy}'만 사용합니다(FC-D3).");
        else
        {
            policy = PolicyRegistry.Resolve(manifest.PolicyId).Value;
            Add("policy", CheckStatus.Pass, null, policy.Id);
        }

        CohortInputs? cohort = null;
        if (manifest is null)
            Unavailable("cohort");
        else if (OfficialProfile.CohortsOf(manifest.Family, a) is not { } allowed)
            Add("cohort", CheckStatus.NotReady, "Official.family_cohorts_not_declared", $"ADR-008은 family '{manifest.Family}'의 공식 코호트를 정하지 않았습니다.");
        else if (!allowed.Contains(manifest.CohortId))
            Add("cohort", CheckStatus.Fail, "Official.cohort_not_approved_for_family", $"코호트 '{manifest.CohortId}'는 {manifest.Family}의 승인 코호트({string.Join(", ", allowed)})가 아닙니다.");
        else if (CohortRegistry.Resolve(manifest.CohortId) is { IsSuccess: false } missing)
            Add("cohort", missing.Error!.Value.Code == "Registry.cohort_not_implemented" ? CheckStatus.NotReady : CheckStatus.Fail, missing.Error.Value.Code, missing.Error.Value.Message);
        else
        {
            cohort = CohortRegistry.Resolve(manifest.CohortId).Value;
            Add("cohort", CheckStatus.Pass, null, cohort.CohortId);
        }

        if (cohort is null)
            Unavailable("cohort_hash");
        else if (!a.CohortInputsHashes.TryGetValue(cohort.CohortId, out var cohortHash))
            Add("cohort_hash", CheckStatus.NotReady, "Official.cohort_hash_not_frozen", $"코호트 '{cohort.CohortId}'의 InputsHash가 아직 동결되지 않았습니다.");
        else if (cohortHash != cohort.InputsHash)
            Add("cohort_hash", CheckStatus.Fail, "Official.cohort_hash_mismatch", $"코호트 '{cohort.CohortId}'의 InputsHash {cohort.InputsHash}이(가) 동결값 {cohortHash}과 다릅니다.");
        else
            Add("cohort_hash", CheckStatus.Pass, null, cohort.InputsHash);

        if (manifest is null)
            Unavailable("game_ids");
        else
            Add("game_ids", CheckStatus.Pass, null, $"{manifest.Seeds.Count}경기, {SeedManifestReader.GameIdRuleV1}, Competition Key '{manifest.CompetitionKey}'");

        var output = OfficialOutput.Resolve(r.RepositoryRoot, r.OutDirectory, r.RunId);
        Add("output_directory", output.IsSuccess ? CheckStatus.Pass : CheckStatus.Fail, output.IsSuccess ? null : output.Error!.Value.Code, output.IsSuccess ? output.Value : output.Error!.Value.Message);
        ExecutionChecks(checks, a);

        OfficialAttestation? attestation = null;
        if (checks.All(c => c.Status == CheckStatus.Pass))
        {
            var park = HarnessData.NeutralPark();
            var config = park.IsSuccess
                ? RunConfig.Create(r.RunId, "official", cohort, rules, tuning.Value, park.Value, OfficialProfile.AdvancementPolicyVersion, OfficialProfile.EventBudget, policy, manifest!.CompetitionKey)
                : Result<RunConfig>.Failure(park.Error!.Value);
            if (!config.IsSuccess || OfficialProfile.ParkTextOf(config.Value.Park) != OfficialProfile.ParkText)
                Add("attestation", CheckStatus.Fail, config.IsSuccess ? "Official.park_not_approved" : config.Error!.Value.Code, config.IsSuccess ? "구장 스냅샷이 승인값과 다릅니다." : config.Error!.Value.Message);
            else
            {
                var c = config.Value;
                attestation = new OfficialAttestation(OfficialAttestation.Version, a.GitCommitSha!, a.ExecutionPolicyId!, a.ExecutionPolicyHash!, r.SeedManifestPath, manifest!.FileSha256, manifest.Family,
                    manifest.CompetitionKey, SeedManifestReader.GameIdRuleV1, cohort!.CohortId, cohort.InputsHash, policy!.Id, c.Execution.ProbabilityModelVersion, c.Execution.TuningVersion,
                    c.Execution.TuningHash, rules!.ProfileId, rules.ContentHash, c.AdvancementPolicyVersion, OfficialProfile.ParkTextOf(c.Park), c.EventBudget,
                    c.ConfigHash(manifest.Seeds.Select(s => c.Spec(s.Index, s.Seed))));
                Add("attestation", CheckStatus.Pass, null, attestation.Hash());
            }
        }
        else
            Unavailable("attestation");

        return new PreflightReport(checks, git.HeadSha, tuning.IsSuccess ? tuning.Value : null, manifest, rules, output.IsSuccess ? output.Value : null, attestation);
    }

    /// <summary>The run-independent checks <c>git_integrity</c>, <c>git_sha</c> and <c>model_tuning</c> (shared with the 48-cell preflight); returns the embedded tuning read.</summary>
    internal static Result<TuningProfile> EnvironmentChecks(List<PreflightCheck> checks, OfficialApprovals a, GitIntegrityReport git)
    {
        void Add(string id, CheckStatus status, string? code, string message) => checks.Add(new PreflightCheck(id, status, code, message));
        Add("git_integrity", git.State switch { GitIntegrityState.Clean => CheckStatus.Pass, GitIntegrityState.BuildOutputUnverified => CheckStatus.NotReady, _ => CheckStatus.Fail }, git.Code, git.Message);
        if (a.GitCommitSha is null)
            Add("git_sha", CheckStatus.NotReady, "Official.git_sha_not_approved", "공식 실행 Git SHA가 아직 동결되지 않았습니다.");
        else if (git.HeadSha != a.GitCommitSha)
            Add("git_sha", CheckStatus.Fail, "Official.git_sha_mismatch", $"HEAD {git.HeadSha ?? "(없음)"}이(가) 승인 SHA {a.GitCommitSha}와 다릅니다.");
        else
            Add("git_sha", CheckStatus.Pass, null, $"HEAD = 승인 SHA {a.GitCommitSha}");

        var tuning = HarnessData.LoadTuningV4();
        var tuningOk = tuning.IsSuccess && tuning.Value.Model.Version.Value == OfficialProfile.ModelVersion && tuning.Value.Tuning.Value == OfficialProfile.TuningVersion && tuning.Value.ContentHash == OfficialProfile.TuningHash;
        Add("model_tuning", tuningOk ? CheckStatus.Pass : CheckStatus.Fail, tuningOk ? null : "Official.model_tuning_not_approved", tuningOk ? $"{OfficialProfile.ModelVersion}/{OfficialProfile.TuningVersion} {OfficialProfile.TuningHash}" : "내장 모델·튜닝이 승인값과 다릅니다.");
        return tuning;
    }

    /// <summary>The checks <c>execution_policy</c> and <c>execution_path</c> (shared with the 48-cell preflight).</summary>
    internal static void ExecutionChecks(List<PreflightCheck> checks, OfficialApprovals a)
    {
        void Add(string id, CheckStatus status, string? code, string message) => checks.Add(new PreflightCheck(id, status, code, message));
        if (a.ExecutionPolicyId is null || a.ExecutionPolicyHash is null)
            Add("execution_policy", CheckStatus.NotReady, "Official.execution_policy_not_frozen", "공식 실행 정책(ID·해시)이 아직 동결되지 않았습니다.");
        else if (a.ExecutionPolicyId != OfficialProfile.Id || a.ExecutionPolicyHash != OfficialProfile.Hash())
            Add("execution_policy", CheckStatus.Fail, "Official.execution_policy_mismatch", "승인된 실행 정책이 이 하네스의 공식 실행 프로파일과 다릅니다.");
        else
            Add("execution_policy", CheckStatus.Pass, null, $"{OfficialProfile.Id} {OfficialProfile.Hash()}");

        Add("execution_path", a.ExecutionPathReady ? CheckStatus.Pass : CheckStatus.NotReady, a.ExecutionPathReady ? null : "Official.not_ready",
            a.ExecutionPathReady ? "공식 실행 경로 준비됨" : "Final Execution Freeze가 없어 공식 실행을 시작할 수 없습니다(OfficialApprovals.ExecutionPathReady = false).");
    }
}

/// <summary>The official output directory rules and the official result document.</summary>
public static class OfficialOutput
{
    public const string RootRelative = "build/simharness/official";

    public const string ResultVersion = "simharness-official-result-v1";

    /// <summary>The only statistical gate states (separate from the process exit code). Nothing in F-C2-A produces anything but NOT_EVALUATED.</summary>
    public static IReadOnlyList<string> GateStatuses { get; } = ["NOT_EVALUATED", "INSUFFICIENT_SAMPLE", "PASS", "FAIL"];

    /// <summary>
    /// A NEW directory strictly inside <c>{root}/build/simharness/official/</c> (git-ignored, separate from smoke output under <c>build/simharness/</c>): the default is <c>…/official/{run id}</c>; an
    /// explicit one must be a repository-relative <c>/</c> path below that root with no empty, <c>.</c> or <c>..</c> segment. An existing file or directory is never reused or overwritten.
    /// </summary>
    public static Result<string> Resolve(string repositoryRoot, string? requested, string runId)
    {
        static Result<string> Invalid(string why) => Result<string>.Failure(new Error("Official.output_invalid", $"공식 출력 경로가 올바르지 않습니다: {why}"));
        if (runId.Length is 0 or > 64 || runId is "." or ".." || runId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')))
            return Invalid($"실행 ID '{runId}'는 디렉터리 이름으로 쓸 수 없습니다.");
        var rel = requested ?? $"{RootRelative}/{runId}";
        if (rel.Length == 0 || Path.IsPathRooted(rel) || rel.Contains(':') || rel.Contains('\\'))
            return Invalid($"'{rel}'는 '/'로 구분한 저장소 기준 상대 경로가 아닙니다.");
        var segments = rel.Split('/');
        if (segments.Any(s => s is "" or "." or ".."))
            return Invalid($"'{rel}'에 빈 구간, '.', '..'가 있습니다.");
        if (!rel.StartsWith(RootRelative + "/", StringComparison.Ordinal))
            return Invalid($"'{rel}'는 '{RootRelative}/' 아래가 아닙니다.");
        var officialRoot = Path.GetFullPath(Path.Combine(repositoryRoot, RootRelative.Replace('/', Path.DirectorySeparatorChar)));
        var full = Path.GetFullPath(Path.Combine([repositoryRoot, .. segments]));
        if (!full.StartsWith(officialRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return Invalid($"'{rel}'가 공식 출력 루트 밖으로 해석됩니다.");
        if (PathSafety.FirstReparsePoint(repositoryRoot, segments) is { } link)
            return Result<string>.Failure(new Error("Official.output_reparse_point", $"공식 출력 경로에 링크/Junction(reparse point)이 있어 거부합니다: '{link}'."));
        return Directory.Exists(full) || File.Exists(full)
            ? Result<string>.Failure(new Error("Official.output_exists", $"공식 출력 경로가 이미 있습니다(덮어쓰지 않음): '{rel}'."))
            : Result<string>.Success(full);
    }

    public static object Result(PreflightReport report) => new
    {
        schema_version = ResultVersion,
        run_type = "official",
        official = true,
        git_commit_sha = report.HeadSha,
        execution_policy_id = report.Attestation?.ExecutionPolicyId,
        execution_policy_hash = report.Attestation?.ExecutionPolicyHash,
        cohort_id = report.Manifest?.CohortId,
        cohort_hash = report.Attestation?.CohortInputsHash,
        seed_manifest_hash = report.Attestation?.SeedManifestSha256,
        model_version = report.Tuning?.Model.Version.Value,
        tuning_version = report.Tuning?.Tuning.Value,
        tuning_hash = report.Tuning?.ContentHash,
        rule_snapshot_hash = report.Rules?.ContentHash,
        config_hash = report.Attestation?.ConfigHash,
        attestation_version = report.Attestation is null ? null : OfficialAttestation.Version,
        attestation_hash = report.Attestation?.Hash(),
        run_status = report.Ready ? "NOT_STARTED" : "PREFLIGHT_REFUSED",
        gate_status = GateStatuses[0],
        error_code = report.Ready ? "Official.execution_not_implemented" : report.ErrorCode,
        checks = report.Checks.Select(c => new { id = c.Id, status = c.Status switch { CheckStatus.Pass => "PASS", CheckStatus.Fail => "FAIL", _ => "NOT_READY" }, code = c.Code, message = c.Message }).ToList(),
    };
}

/// <summary>What a manifest records about the run that an official replay must reconcile with the verified seed file: <see cref="Park"/> is the canonical park text of the park it ran with.</summary>
public sealed record RecordedRun(string CompetitionKey, string CohortId, string PolicyId, string RulesProfileId, string Park, IReadOnlyList<GameSpec> Games);

/// <summary>
/// Replay of a manifest that carries an official envelope: the envelope and attestation versions, the recomputed attestation hash, the attestation's binding to the manifest's (unchanged)
/// config hash, then every frozen value — seed-file hash, execution policy, cohort input hash, Git SHA — and the readiness of the execution path. Unfrozen is a refusal, never a pass.
/// </summary>
public static class OfficialReplay
{
    /// <summary>
    /// The second half of an official replay (the first, <see cref="Verify"/>, checks the envelope against the approvals): the trusted pin makes the REAL seed file readable, and everything the
    /// attestation and the manifest claim is checked against that file's parse, the fixed <see cref="OfficialProfile"/> and each other — family, game-id rule, competition key, cohort / policy /
    /// rules, park, the profile values, and finally the exact game list. An attestation whose own hash is right but whose content is not what was approved is refused here.
    /// Field by field (attestation → verified against): schema_version (Verify) · git_commit_sha, execution_policy_id / hash, seed_manifest_path / sha256, cohort_inputs_hash (Verify: the approvals) ·
    /// seed_family, game_id_rule, competition_key, cohort_id, policy_id, rules_profile_id (the parsed seed file) · park_snapshot, model_version, tuning_version, tuning_hash, rules_hash,
    /// advancement_policy_version, event_budget, execution policy (the fixed official profile; park / budget also against the manifest) · config_hash (Verify: the manifest, then
    /// <see cref="ReplayPlan.CheckAgainst"/>: recomputed from the recorded games) · the games (the parsed seed file).
    /// </summary>
    public static Error? VerifyAgainstSeedFile(OfficialAttestation att, RecordedRun run, OfficialApprovals a, string? repositoryRoot) => VerifyAgainstSeedFile(att, run, a, repositoryRoot, out _);

    /// <summary>The same check, also handing back the verified seed file (null unless it passed), whose family and stop parameters an official replay then trusts (ADR-009 SR-D7.4, SR-D8.2).</summary>
    public static Error? VerifyAgainstSeedFile(OfficialAttestation att, RecordedRun run, OfficialApprovals a, string? repositoryRoot, out SeedManifest? verified)
    {
        static Error E(string code, string message) => new(code, message);
        verified = null;
        if (repositoryRoot is null)
            return E("Replay.official_seed_manifest_unavailable", "공식 replay에는 승인·고정된 Seed Manifest 파일이 필요하지만 저장소 루트를 알 수 없어 읽을 수 없습니다(Hash 문자열만으로는 검증되지 않음).");
        var read = SeedManifestReader.Read(repositoryRoot, att.SeedManifestPath, a.SeedManifestPins.GetValueOrDefault(att.SeedManifestPath));
        if (!read.IsSuccess)
            return read.Error;
        var sm = read.Value;
        if (sm.FileSha256 != att.SeedManifestSha256)
            return E("Replay.seed_manifest_hash_mismatch", "읽은 Seed Manifest의 원본 해시가 Attestation의 값과 다릅니다.");
        if (att.SeedFamily != sm.Family)
            return E("Replay.attestation_seed_family_mismatch", $"Attestation의 seed_family '{att.SeedFamily}'가 승인된 Seed 파일의 family '{sm.Family}'와 다릅니다.");
        if (!SeedFamilies.All[sm.Family].Official)
            return E("Official.family_not_official", $"family '{sm.Family}'는 공식 계열이 아닙니다.");
        if (att.GameIdRule != SeedManifestReader.GameIdRuleV1)
            return E("Replay.game_id_rule_mismatch", $"Attestation의 game_id_rule '{att.GameIdRule}'가 지원·승인된 규칙 '{SeedManifestReader.GameIdRuleV1}'이 아닙니다.");
        if (att.CompetitionKey != sm.CompetitionKey || run.CompetitionKey != sm.CompetitionKey)
            return E("Replay.competition_key_mismatch", $"Competition Key가 승인된 Seed 파일의 '{sm.CompetitionKey}'와 다릅니다(Attestation '{att.CompetitionKey}', 매니페스트 '{run.CompetitionKey}').");
        if (new[] { att.CohortId, run.CohortId }.Any(c => c != sm.CohortId) || new[] { att.PolicyId, run.PolicyId }.Any(p => p != sm.PolicyId) || new[] { att.RulesProfileId, run.RulesProfileId }.Any(r => r != sm.RulesProfileId))
            return E("Replay.seed_manifest_binding_mismatch", "코호트·정책·규칙 프로파일이 승인된 Seed 파일의 선언과 다릅니다.");
        if (OfficialProfile.CohortsOf(sm.Family, a) is not { } cohorts || !cohorts.Contains(sm.CohortId)
            || (sm.Family is "fc3-cal" or "fc3-int" && sm.PolicyId != OfficialProfile.CalibrationPolicy))
            return E("Replay.official_profile_mismatch", $"family '{sm.Family}'에 승인되지 않은 코호트 '{sm.CohortId}' 또는 정책 '{sm.PolicyId}'입니다.");
        if (att.ParkSnapshot != OfficialProfile.ParkText || run.Park != att.ParkSnapshot)
            return E("Replay.park_snapshot_mismatch", "구장 스냅샷이 승인된 값 또는 매니페스트의 실제 구장 기록과 다릅니다.");
        if ((att.ExecutionPolicyId, att.ExecutionPolicyHash, att.ModelVersion, att.TuningVersion, att.TuningHash, att.AdvancementPolicyVersion, att.EventBudget)
            != (OfficialProfile.Id, OfficialProfile.Hash(), OfficialProfile.ModelVersion, OfficialProfile.TuningVersion, OfficialProfile.TuningHash, OfficialProfile.AdvancementPolicyVersion, OfficialProfile.EventBudget)
            || !OfficialProfile.RulesHashes.TryGetValue(att.RulesProfileId, out var rulesHash) || rulesHash != att.RulesHash)
            return E("Replay.official_profile_mismatch", "Attestation의 모델·튜닝·규칙·진루 정책·이벤트 예산·실행 정책이 고정된 공식 실행 프로파일과 다릅니다.");
        if (!run.Games.Select(g => new SeedEntry(g.GameIndex, g.Seed, g.GameId)).SequenceEqual(sm.Seeds))
            return E("Replay.seed_games_mismatch", "replay가 실행할 경기 목록(index·Seed·GameId)이 승인된 Seed 파일의 경기 목록과 정확히 같지 않습니다.");
        verified = sm;
        return null;
    }

    public static Error? Verify(JsonElement official, string manifestConfigHash, OfficialApprovals a)
    {
        try
        {
            if (official.GetProperty("schema_version").GetString() != OfficialAttestation.EnvelopeVersion)
                return new Error("Replay.official_schema_unsupported", $"지원하지 않는 공식 envelope 버전입니다(지원: {OfficialAttestation.EnvelopeVersion}).");
            var parsed = OfficialAttestation.FromJson(official.GetProperty("attestation"));
            if (!parsed.IsSuccess)
                return parsed.Error!.Value;
            var att = parsed.Value;
            if (att.SchemaVersion != OfficialAttestation.Version)
                return new Error("Replay.official_schema_unsupported", $"지원하지 않는 Attestation 버전입니다(지원: {OfficialAttestation.Version}).");
            if (official.GetProperty("attestation_hash").GetString() != att.Hash())
                return new Error("Replay.attestation_mismatch", "기록된 attestation_hash가 Attestation 내용의 해시와 다릅니다.");
            if (att.ConfigHash != manifestConfigHash)
                return new Error("Replay.attestation_config_mismatch", "Attestation의 config_hash가 매니페스트의 config_hash와 다릅니다.");
            if (!a.SeedManifestPins.TryGetValue(att.SeedManifestPath, out var pin))
                return new Error("Official.seed_manifest_not_frozen", $"'{att.SeedManifestPath}'에 고정된 공식 Seed Manifest 해시가 없습니다.");
            if (pin != att.SeedManifestSha256)
                return new Error("Replay.seed_manifest_hash_mismatch", "Attestation의 Seed Manifest 해시가 고정값과 다릅니다.");
            if (a.ExecutionPolicyId is null || a.ExecutionPolicyHash is null)
                return new Error("Official.execution_policy_not_frozen", "공식 실행 정책이 동결되지 않았습니다.");
            if (att.ExecutionPolicyId != a.ExecutionPolicyId || att.ExecutionPolicyHash != a.ExecutionPolicyHash)
                return new Error("Replay.execution_policy_mismatch", "Attestation의 실행 정책이 승인값과 다릅니다.");
            if (!a.CohortInputsHashes.TryGetValue(att.CohortId, out var cohortHash))
                return new Error("Official.cohort_hash_not_frozen", $"코호트 '{att.CohortId}'의 InputsHash가 동결되지 않았습니다.");
            if (cohortHash != att.CohortInputsHash)
                return new Error("Replay.cohort_hash_mismatch", "Attestation의 코호트 InputsHash가 동결값과 다릅니다.");
            if (a.GitCommitSha is null)
                return new Error("Official.git_sha_not_approved", "공식 Git SHA가 동결되지 않았습니다.");
            if (att.GitCommitSha != a.GitCommitSha)
                return new Error("Replay.git_sha_mismatch", "Attestation의 Git SHA가 승인값과 다릅니다.");
            return a.ExecutionPathReady ? null : new Error("Official.not_ready", "공식 실행 경로가 준비되지 않아 공식 replay를 인정할 수 없습니다.");
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException)
        {
            return new Error("Replay.manifest_invalid", $"공식 envelope 형식이 올바르지 않습니다: {e.Message}");
        }
    }
}
