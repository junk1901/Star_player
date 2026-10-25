using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.SimHarness.Evidence;

/// <summary>A parsed, validated approval record (§13) together with the exact bytes it was parsed from and their SHA-256 (one read, §16 T2/L4).</summary>
public sealed record ApprovalRecordV1(JObject Record, byte[] Raw, string RawSha256)
{
    public string Id => Text(Record["approval_record_id"]);

    public string ValidationSetId => Text(Record["validation_set_id"]);

    public string FreezeId => Text(Record["freeze_id"]);

    public bool TestOnly => ((JBool)Record["test_only"]).Value;

    public string GitCommitSha => Text(Record["git_commit_sha"]);

    public JObject Ref(string name) => Obj(Record[name]);

    public IReadOnlyList<JObject> Families => Items(Record["families"]).Select(Obj).ToList();

    public JObject Family(string family) => Families.Single(f => Text(f["family"]) == family);

    public IReadOnlyList<JObject> Units(string family) => Items(Family(family)["units"]).Select(Obj).ToList();
}

/// <summary>
/// T06a: the closed approval-side formats (B1-T00 §13.1 approval record, §14.1 execution authorization, §18.2-§18.3 profiles, §12.1 test profile, §17 R03-1 trust anchor and R03-5-1 service config with
/// the NSC-5 field) and the checks that need no registry: catalogue (§13.2), rejection rules (§13.3), the independent sources that are local (§13.4), PATCH-04, and the verifier-only
/// <see cref="OfficialApprovals"/> mapping (§13.5) whose <c>ExecutionPathReady</c> is never true at this stage. <see cref="OfficialApprovals.Current"/> is not touched.
/// </summary>
public static class ApprovalFormats
{
    public const string ApprovalFormat = "simharness-approval-record-v1", AuthorizationFormat = "simharness-execution-authorization-v1", BuildFormat = "simharness-build-provenance-v1",
        EnvironmentFormat = "simharness-environment-profile-v1", TestProfileFormat = "simharness-pzl-test-profile-v1", AnchorFormat = "simharness-verifier-trust-anchor-v1",
        ServiceConfigFormat = "simharness-registry-service-config-v1";

    private static Shape IdRef => Obj(("id", Id), ("raw_sha256", H));

    private static Shape SortedUnique(Shape item, int min = 0) => (n, p) =>
    {
        Arr(item, min)(n, p);
        var v = Items(n).Select(Text).ToList();
        for (var i = 1; i < v.Count; i++)
            if (string.CompareOrdinal(v[i - 1], v[i]) >= 0)
                throw Bad($"{p}/{i}", "Schema.order", "ordinal 오름차순·중복 금지입니다.");
    };

    public static readonly Shape Approval = Obj(
        ("format", Const(ApprovalFormat)), ("approval_record_id", Id), ("validation_set_id", Pattern("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", "ID(≤64)")), ("freeze_id", Id), ("test_only", Bool),
        ("contracts", EvidenceShapes.Contracts), ("git_commit_sha", Git), ("build_provenance", IdRef), ("environment_profile", IdRef), ("pzl_test_profile", IdRef),
        ("execution_policy", Obj(("id", Const(OfficialProfile.Id)), ("hash", H))),
        ("evaluation_policies", Obj(("calibration", Obj(("id", Const(EvaluationPolicy.FcEvaluationV1.Id)), ("hash", H))), ("dominance", Obj(("id", Const(DominanceEvaluator.PolicyId)))))),
        ("dominance_games_per_cell", Int(2, int.MaxValue)), ("integration_cohort_ids", SortedUnique(Exact, 1)),
        ("cohort_inputs_hashes", Arr(Obj(("cohort_id", Exact), ("inputs_hash", H)))), ("combination_policy_clauses", Obj(("calibration", Exact), ("integration", Exact))),
        ("certification_allowances", Obj(("insufficient_sample", Bool))),
        ("families", ArrExact(Obj(("family", OneOf("fc3-cal", "fc3-int", "fc3-dom")), ("run_id", Run),
            ("units", Arr(Obj(("unit_id", Exact), ("cohort_id", Exact), ("rules_profile_id", Exact), ("planned_games", P), ("seed_manifest_path", Str(v => IsRelPath(v) && v.EndsWith(".json", StringComparison.Ordinal), "RELPATH(.json)")),
                ("seed_manifest_sha256", H))))), 3)),
        ("verifiers", Arr(Obj(("id", Id), ("version", Text1), ("source_git_sha", Git), ("binary_sha256", H)), 1)));

    public static readonly Shape Authorization = Obj(("format", Const(AuthorizationFormat)), ("authorization_id", Id), ("approval_record_id", Id), ("approval_record_sha256", H), ("test_only", Bool),
        ("families", Arr(OneOf("fc3-cal", "fc3-int", "fc3-dom"), 1)));

    public static readonly Shape BuildProvenance = Obj(("format", Const(BuildFormat)), ("build_provenance_id", Id), ("source_git_sha", Git), ("environment_profile", IdRef),
        ("dotnet_sdk", Const("8.0.425")), ("configuration", Const("Release")), ("build_procedure_clause_id", Exact),
        ("artifacts", Arr(Obj(("role", OneOf("SIMHARNESS_BINARY", "VERIFIER_BINARY", "TEST_ASSEMBLY", "DEPENDENCY", "RUNTIME_CONFIG", "TRUST_CONFIG")), ("relative_path", RelPath), ("raw_sha256", H),
            ("byte_length", N)))));

    /// <summary>§18.2: the three pinned runtime files, by name in ordinal order — exactly these, each once.</summary>
    public static readonly string[] RuntimeFileNames = ["System.Private.CoreLib.dll", "clrjit.dll", "coreclr.dll"];

    public static readonly Shape EnvironmentProfile = Obj(("format", Const(EnvironmentFormat)), ("environment_profile_id", Id),
        ("os", Obj(("platform", Const("Windows")), ("version", Exact), ("architecture", Const("X64")))), ("process_architecture", Const("X64")),
        ("runtime", Obj(("framework_description", Exact), ("netcore_app_version", Exact), ("runtime_identifier", Const("win-x64")))),
        ("native_math", Obj(("library", Const("ucrtbase.dll")), ("file_version", Exact), ("raw_sha256", H))),
        ("runtime_files", Exactly(ArrExact(Obj(("name", OneOf(RuntimeFileNames)), ("raw_sha256", H)), 3), "name", RuntimeFileNames)),
        ("dotnet_variables", Obj(("forbidden_prefixes", (n, p) => { ArrExact(Text1, 2)(n, p); if (!Items(n).Select(Text).SequenceEqual(["COMPlus_", "DOTNET_"])) throw Bad(p, "Schema.format", "[\"COMPlus_\",\"DOTNET_\"]입니다."); }))),
        ("math_golden", Obj(("upper_quantile", Exactly(ArrExact(Obj(("divisor", Int(1, 36)), ("bits", Bits64)), 36), "divisor", Enumerable.Range(1, 36).Select(j => j.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray())),
            ("witness_fixture", Const("dom-witness-fixture-v1")), ("witness_digest", AH))));

    /// <summary>An array whose <paramref name="key"/> members are exactly <paramref name="expected"/> in that order (no duplicate, gap or reordering; B1-T11-05).</summary>
    private static Shape Exactly(Shape array, string key, string[] expected) => (n, p) =>
    {
        array(n, p);
        var got = Items(n).Select(Obj).Select(e => e[key] is JNumber num ? num.Raw : Text(e[key])).ToList();
        if (!got.SequenceEqual(expected, StringComparer.Ordinal))
            throw Bad(p, "Schema.exact_set", $"{key}는 정확히 [{string.Join(", ", expected)}] 순서여야 합니다(중복·누락·재정렬 금지, §18.2).");
    };

    public static readonly string[] TestProfileChecks = ["pzl-4", "pzl-6", "pzl-7", "pzl-8", "pzl-9", "pzl-10", "pzl-11", "pzl-12a"];

    public static readonly Shape TestProfile = Obj(("format", Const(TestProfileFormat)), ("profile_id", Id), ("source_git_sha", Git), ("build_provenance_id", Id),
        ("runner", Obj(("dotnet_sdk", Const("8.0.425")), ("test_sdk", Const("17.14.1")), ("xunit", Const("2.9.3")), ("xunit_runner", Const("2.8.2")))),
        ("fixtures", Arr(Obj(("path", RelPath), ("git_blob_sha256", H), ("byte_length", N)))),
        ("checks", ArrExact(Obj(("check_id", OneOf(TestProfileChecks)), ("cases", Arr(Str(v => IsText1(v) && v.Contains('|'), "CASE_ID")))), 8)));

    public static readonly Shape AnchorPipeName = Pattern("^[A-Za-z0-9._-]{1,128}$", "pipe name");

    public static readonly Shape Anchor = Obj(("format", Const(AnchorFormat)), ("registry_instance_id", Id), ("registry_pipe_name", AnchorPipeName), ("registry_account_sid", SidUser),
        ("registry_service_sid", Opt(SidService)), ("owner_principal_sids", SortedUnique(SidUser, 1)), ("verifier_principal_sid", SidUser), ("evidence_reader_sids", SortedUnique(SidUser)));

    /// <summary>R03-5-1 + NSC-5: the anchor's identity and role SIDs plus <c>registry_database_path</c> (<see cref="WindowsLocalAbsPath"/>).</summary>
    public static readonly Shape ServiceConfig = Obj(("format", Const(ServiceConfigFormat)), ("registry_instance_id", Id), ("registry_pipe_name", AnchorPipeName), ("registry_account_sid", SidUser),
        ("registry_service_sid", Opt(SidService)), ("owner_principal_sids", SortedUnique(SidUser, 1)), ("verifier_principal_sid", SidUser), ("evidence_reader_sids", SortedUnique(SidUser)),
        ("registry_database_path", Str(v => WindowsLocalAbsPath.Syntax(v) is null, "WINDOWS_LOCAL_ABS_PATH")));

    // ---------------------------------------------------------------- reads (one byte snapshot → shape → rules)

    private static EvidenceException Mismatch(string cause, string pointer, string message) => EvidenceCodes.Fail(EvidenceCodes.ApprovalMismatch, cause, pointer, "approval", message);

    public static JObject Read(Shape shape, ReadOnlySpan<byte> raw)
    {
        var n = StrictJson.Parse(raw, Stage);
        shape(n, "");
        return (JObject)n;
    }

    /// <summary>§13.1-§13.3 and the local part of §13.4. <paramref name="repositoryRoot"/> lets every seed pin be read through the existing reader and matched against the unit and the catalogue.</summary>
    public static ApprovalRecordV1 ReadApproval(byte[] raw, string? repositoryRoot)
    {
        var r = Read(Approval, raw);
        var record = new ApprovalRecordV1(r, raw, Archive.Sha(raw));
        if (Text(Obj(r["contracts"])["hash"]) != "1" || EvidenceShapes.ContractKeys.Any(k => Text(Obj(r["contracts"])[k]) != EvidenceShapes.SupportedContracts[k]))
            throw EvidenceCodes.Fail(EvidenceCodes.ContractNotApproved, "Approval.contracts", "/contracts", "approval", "이 검증기가 지원하지 않는 계약 조합입니다.");
        string[] order = ["fc3-cal", "fc3-int", "fc3-dom"];
        if (!record.Families.Select(f => Text(f["family"])).SequenceEqual(order))
            throw Mismatch("Approval.family_order", "/families", "families는 fc3-cal, fc3-int, fc3-dom 순서의 정확히 3개입니다.");
        if (record.Families.Select(f => Text(f["run_id"])).Distinct(StringComparer.Ordinal).Count() != 3)
            throw Mismatch("Approval.run_id_duplicate", "/families", "run_id가 Family 사이에서 중복됩니다(§13.3).");
        var integrationCohorts = Items(r["integration_cohort_ids"]).Select(Text).ToList();
        foreach (var id in integrationCohorts)
            if (!CohortRegistry.Resolve(id).IsSuccess)
                throw Mismatch("Approval.integration_cohort_unknown", "/integration_cohort_ids", $"integration_cohort_ids의 '{id}'는 CohortRegistry에 없습니다.");
        Catalogue(record, integrationCohorts);

        var all = order.SelectMany(f => record.Units(f).Select(u => (Family: f, Unit: u))).ToList();
        if (all.Select(x => Text(x.Unit["seed_manifest_path"])).Distinct(StringComparer.Ordinal).Count() != all.Count
            || all.Select(x => Text(x.Unit["seed_manifest_sha256"])).Distinct(StringComparer.Ordinal).Count() != all.Count)
            throw Mismatch("Approval.seed_pin_duplicate", "/families", "seed_manifest_path·seed_manifest_sha256이 Unit 사이에서 중복됩니다(§13.3).");

        var hashes = Items(r["cohort_inputs_hashes"]).Select(Obj).ToList();
        for (var i = 1; i < hashes.Count; i++)
            if (string.CompareOrdinal(Text(hashes[i - 1]["cohort_id"]), Text(hashes[i]["cohort_id"])) >= 0)
                throw EvidenceCodes.Fail(EvidenceCodes.SchemaInvalid, "Schema.order", $"/cohort_inputs_hashes/{i}", Stage, "cohort_id ordinal 오름차순·중복 금지입니다.");
        var used = all.Select(x => Text(x.Unit["cohort_id"])).ToHashSet(StringComparer.Ordinal);
        if (!used.SetEquals(hashes.Select(h => Text(h["cohort_id"]))))
            throw Mismatch("Approval.cohort_set", "/cohort_inputs_hashes", "cohort_inputs_hashes의 Cohort 집합이 Unit들이 쓰는 Cohort의 합집합과 다릅니다(§13.3).");
        foreach (var h in hashes)
            if (CohortRegistry.Resolve(Text(h["cohort_id"])) is not { IsSuccess: true } c || c.Value.InputsHash != Text(h["inputs_hash"]))
                throw Mismatch("Approval.inputs_hash", "/cohort_inputs_hashes", $"Cohort '{Text(h["cohort_id"])}'의 InputsHash가 CohortRegistry 재계산값과 다릅니다(§13.4).");
        if (Text(Obj(r["execution_policy"])["hash"]) != OfficialProfile.Hash())
            throw Mismatch("Approval.execution_policy_hash", "/execution_policy/hash", "execution_policy.hash가 OfficialProfile.Hash() 재계산값과 다릅니다(§13.4).");
        if (Text(Obj(Obj(r["evaluation_policies"])["calibration"])["hash"]) != EvaluationPolicy.FcEvaluationV1.Hash())
            throw Mismatch("Approval.evaluation_policy_hash", "/evaluation_policies/calibration/hash", "평가 정책 Hash가 FcEvaluationV1.Hash() 재계산값과 다릅니다(§13.4).");

        var verifiers = Items(r["verifiers"]).Select(Obj).ToList();
        for (var i = 1; i < verifiers.Count; i++)
            if (string.CompareOrdinal(Text(verifiers[i - 1]["id"]) + "\0" + Text(verifiers[i - 1]["version"]), Text(verifiers[i]["id"]) + "\0" + Text(verifiers[i]["version"])) >= 0)
                throw Mismatch("Approval.verifier_order", $"/verifiers/{i}", "verifiers는 (id, version) ordinal 오름차순·중복 금지입니다.");
        if (verifiers.Select(v => Text(v["binary_sha256"])).Distinct(StringComparer.Ordinal).Count() != verifiers.Count)
            throw Mismatch("Approval.verifier_duplicate", "/verifiers", "verifier binary_sha256이 중복됩니다(§13.3).");

        if (repositoryRoot is not null)
            foreach (var (family, unit) in all)
            {
                var path = Text(unit["seed_manifest_path"]);
                var read = SeedManifestReader.Read(repositoryRoot, path, Text(unit["seed_manifest_sha256"]));
                if (!read.IsSuccess)
                    throw Mismatch(read.Error!.Value.Code, "/families", $"{family}/{Text(unit["unit_id"])}: {read.Error.Value.Message}");
                var m = read.Value;
                if (m.Family != family || m.CohortId != Text(unit["cohort_id"]) || m.RulesProfileId != Text(unit["rules_profile_id"]) || m.Seeds.Count != Long(unit["planned_games"]))
                    throw Mismatch("Approval.seed_manifest_binding", "/families", $"{family}/{Text(unit["unit_id"])}: Seed 파일의 family·cohort·rules·Seed 수가 UnitApproval과 다릅니다(§13.4).");
                if (family == DominanceMatrix.Family && DominanceMatrix.CellOf(m) is { IsSuccess: false } cell)
                    throw Mismatch(cell.Error!.Value.Code, "/families", cell.Error.Value.Message);
            }

        return record;
    }

    /// <summary>§13.2: the exact unit set, order, cohort, rules profile and plan size of each family.</summary>
    private static void Catalogue(ApprovalRecordV1 record, IReadOnlyList<string> integrationCohorts)
    {
        var perCell = Long(record.Record["dominance_games_per_cell"]);
        var expected = new Dictionary<string, List<(string Unit, string? Cohort, string Rules, long? Planned)>>(StringComparer.Ordinal)
        {
            ["fc3-cal"] = CalibrationStrata.All.Select(s => (s.Id, (string?)s.CohortId, s.RulesProfileId, (long?)null)).ToList(),
            ["fc3-int"] = [("automated", null, HarnessData.AutomatedProfile, 100L), ("non-automated", null, HarnessData.NonAutomatedProfile, 100L)],
            ["fc3-dom"] = DominanceMatrix.Cells.Select(c => (c.Id, (string?)c.CohortId, c.RulesProfileId, (long?)perCell)).ToList(),
        };
        foreach (var (family, units) in expected)
        {
            var got = record.Units(family);
            if (got.Count != units.Count)
                throw Mismatch("Approval.catalogue", $"/families/{family}", $"{family}의 Unit 수는 {units.Count}개입니다.");
            for (var i = 0; i < units.Count; i++)
            {
                var (unit, cohort, rules, planned) = units[i];
                var g = got[i];
                var cohortOk = cohort is null ? integrationCohorts.Contains(Text(g["cohort_id"]), StringComparer.Ordinal) : Text(g["cohort_id"]) == cohort;
                if (Text(g["unit_id"]) != unit || !cohortOk || Text(g["rules_profile_id"]) != rules || (planned is { } p && Long(g["planned_games"]) != p))
                    throw Mismatch("Approval.catalogue", $"/families/{family}/units/{i}", $"{family}의 {i + 1}번째 Unit은 카탈로그의 {unit}(cohort·rules·planned_games)이어야 합니다(§13.2).");
            }
        }
    }

    /// <summary>§13.5: the verifier-only approvals of a record. <see cref="OfficialApprovals.ExecutionPathReady"/> is the given R01-1 result (false for the AMPV candidate, §14.6).</summary>
    public static OfficialApprovals ToOfficialApprovals(ApprovalRecordV1 a, bool executionPathReady = false) => new(
        a.GitCommitSha, OfficialProfile.Id, Text(a.Ref("execution_policy")["hash"]),
        a.Families.SelectMany(f => Items(f["units"]).Select(Obj)).ToDictionary(u => Text(u["seed_manifest_path"]), u => Text(u["seed_manifest_sha256"]), StringComparer.Ordinal),
        Items(a.Record["cohort_inputs_hashes"]).Select(Obj).ToDictionary(h => Text(h["cohort_id"]), h => Text(h["inputs_hash"]), StringComparer.Ordinal),
        executionPathReady, (int)Long(a.Record["dominance_games_per_cell"]), Items(a.Record["integration_cohort_ids"]).Select(Text).ToHashSet(StringComparer.Ordinal));

    /// <summary>PATCH-04 (§13.4): build provenance, environment profile and PZL test profile against the approval record (each already read from its own registry entry).</summary>
    public static void CheckProfiles(ApprovalRecordV1 a, (JObject Doc, string Sha) build, (JObject Doc, string Sha) environment, (JObject Doc, string Sha) testProfile)
    {
        bool Ref(string name, string id, string sha) => Text(a.Ref(name)["id"]) == id && Text(a.Ref(name)["raw_sha256"]) == sha;
        if (!Ref("build_provenance", Text(build.Doc["build_provenance_id"]), build.Sha) || Text(build.Doc["source_git_sha"]) != a.GitCommitSha)
            throw Mismatch("Approval.build_provenance", "/build_provenance", "Build Provenance의 ID·Raw SHA·source_git_sha가 승인 기록과 다릅니다(PATCH-04).");
        if (!Ref("environment_profile", Text(environment.Doc["environment_profile_id"]), environment.Sha)
            || Text(Obj(build.Doc["environment_profile"])["id"]) != Text(a.Ref("environment_profile")["id"])
            || Text(Obj(build.Doc["environment_profile"])["raw_sha256"]) != Text(a.Ref("environment_profile")["raw_sha256"]))
            throw Mismatch("Approval.environment_profile", "/environment_profile", "Environment Profile {id, raw_sha256}가 승인 기록·Build Provenance와 다릅니다(PATCH-04).");
        if (!Ref("pzl_test_profile", Text(testProfile.Doc["profile_id"]), testProfile.Sha) || Text(testProfile.Doc["source_git_sha"]) != a.GitCommitSha
            || Text(testProfile.Doc["build_provenance_id"]) != Text(a.Ref("build_provenance")["id"]))
            throw Mismatch("Approval.pzl_test_profile", "/pzl_test_profile", "PZL Test Profile의 ID·SHA·source_git_sha·build_provenance_id가 승인 기록과 다릅니다(PATCH-04).");
        var verifierBinaries = Items(build.Doc["artifacts"]).Select(Obj).Where(x => Text(x["role"]) == "VERIFIER_BINARY").Select(x => Text(x["raw_sha256"])).ToHashSet(StringComparer.Ordinal);
        foreach (var v in Items(a.Record["verifiers"]).Select(Obj))
            if (!verifierBinaries.Contains(Text(v["binary_sha256"])))
                throw Mismatch("Approval.verifier_binary", "/verifiers", $"verifier {Text(v["id"])}의 binary_sha256이 Build Provenance의 VERIFIER_BINARY에 없습니다.");
    }

    /// <summary>§14.1 plus its binding to the approval record (id, SHA, test_only, families a subsequence of the record order).</summary>
    public static JObject ReadAuthorization(byte[] raw, ApprovalRecordV1 a)
    {
        var x = Read(Authorization, raw);
        if (Text(x["approval_record_id"]) != a.Id || Text(x["approval_record_sha256"]) != a.RawSha256 || ((JBool)x["test_only"]).Value != a.TestOnly)
            throw EvidenceCodes.Fail(EvidenceCodes.AuthorizationMissing, "Authorization.binding", "", "authorization", "실행 허가 기록이 이 승인 기록(ID·SHA·test_only)을 가리키지 않습니다.");
        string[] order = ["fc3-cal", "fc3-int", "fc3-dom"];
        var fams = Items(x["families"]).Select(Text).ToList();
        if (fams.Distinct(StringComparer.Ordinal).Count() != fams.Count || !fams.SequenceEqual(order.Where(fams.Contains)))
            throw Bad("/families", "Schema.order", "families는 승인 기록 순서의 부분열이며 중복 금지입니다.");
        return x;
    }

    /// <summary>R03-1: the anchor's role SIDs are mutually exclusive (a role overlap invalidates it).</summary>
    public static JObject ReadAnchor(byte[] raw) => Roles(Read(Anchor, raw), "Trust.anchor_untrusted");

    public static JObject ReadServiceConfig(byte[] raw) => Roles(Read(ServiceConfig, raw), "Trust.registry_config_mismatch");

    private static JObject Roles(JObject a, string cause)
    {
        var sids = new List<string> { Text(a["registry_account_sid"]), Text(a["verifier_principal_sid"]) };
        if (a["registry_service_sid"] is JString s)
            sids.Add(s.Value);
        sids.AddRange(Items(a["owner_principal_sids"]).Select(Text));
        sids.AddRange(Items(a["evidence_reader_sids"]).Select(Text));
        if (sids.Distinct(StringComparer.Ordinal).Count() != sids.Count)
            throw Mismatch(cause, "", "역할 SID가 서로 겹칩니다(R03-1 상호 배타).");
        return a;
    }
}

/// <summary>
/// NSC-5 <c>WINDOWS_LOCAL_ABS_PATH</c> (OWNER decision 2026-10-24): a local drive absolute file path. <see cref="Syntax"/> is the string rule; <see cref="Resolve"/> adds the real checks (canonical form,
/// reparse points, outside the repository and evidence roots). Syntax alone never passes a path.
/// </summary>
public static class WindowsLocalAbsPath
{
    private static readonly string[] Reserved = ["CON", "PRN", "AUX", "NUL", .. Enumerable.Range(0, 10).Select(i => $"COM{i}"), .. Enumerable.Range(0, 10).Select(i => $"LPT{i}")];

    /// <summary>The reason the text is not a local drive absolute path, or null.</summary>
    public static string? Syntax(string v)
    {
        if (v.Length < 4 || !char.IsAsciiLetter(v[0]) || v[1] != ':' || v[2] != '\\')
            return "드라이브 문자로 시작하는 절대 경로(X:\\…)가 아닙니다(UNC·장치·확장·상대·드라이브 상대·루트 상대 거부).";
        if (v.Any(c => c < 0x20) || v[2..].IndexOfAny(['<', '>', '"', '|', '?', '*', ':', '/']) >= 0)
            return "제어 문자, 잘못된 경로 문자, 대체 데이터 스트림(':')이 있습니다.";
        foreach (var segment in v[3..].Split('\\'))
        {
            if (segment.Length == 0 || segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' '))
                return "빈 구간, '.'·'..' 구간, 끝이 '.'·공백인 구간이 있습니다.";
            var stem = segment.Split('.')[0].TrimEnd();
            if (Reserved.Contains(stem, StringComparer.OrdinalIgnoreCase))
                return $"예약 장치 이름 '{segment}'입니다.";
        }

        return null;
    }

    /// <summary>The path after the real checks: canonical (GetFullPath equals the text), no reparse point on the way, not inside any of <paramref name="forbiddenRoots"/>.</summary>
    public static string Resolve(string v, params string[] forbiddenRoots)
    {
        static EvidenceException Bad(string why) => EvidenceCodes.Fail(EvidenceCodes.SchemaInvalid, "Schema.windows_local_abs_path", "/registry_database_path", "service_config", why);
        if (Syntax(v) is { } why)
            throw Bad(why);
        if (!string.Equals(Path.GetFullPath(v), v, StringComparison.Ordinal))
            throw Bad("정규화한 경로가 원문과 다릅니다.");
        var root = Path.GetPathRoot(v)!;
        if (PathSafety.FirstReparsePoint(root, v[root.Length..].Split('\\')) is { } link)
            throw Bad($"경로에 Reparse Point가 있습니다: '{link}'.");
        foreach (var f in forbiddenRoots)
        {
            var full = Path.GetFullPath(f).TrimEnd('\\') + "\\";
            if (v.StartsWith(full, StringComparison.OrdinalIgnoreCase) || string.Equals(v, full.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                throw Bad($"저장소 루트 또는 증거 루트 안의 경로입니다: '{f}'.");
        }

        return v;
    }
}
