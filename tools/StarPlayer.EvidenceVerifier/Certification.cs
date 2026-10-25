using System.Collections;
using System.Runtime.CompilerServices;
using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.EvidenceVerifier;

/// <summary>
/// One certification session: the trust bootstrap's result (§16), the registry it came from, the environment snapshot C0 taken before it (§18 E2-1..E2-5 passed) and the roots. Only
/// <see cref="Certification.Open"/> makes one.
/// </summary>
public sealed class CertificationSession
{
    internal CertificationSession(ResolvedApproval trust, IEvidenceRegistry registry, EnvironmentSnapshot c0, string repositoryRoot, string archiveRoot) =>
        (Trust, Registry, C0, RepositoryRoot, ArchiveRoot) = (trust, registry, c0, repositoryRoot, archiveRoot);

    public ResolvedApproval Trust { get; }

    public IEvidenceRegistry Registry { get; }

    internal EnvironmentSnapshot C0 { get; }

    public string RepositoryRoot { get; }

    public string ArchiveRoot { get; }

    /// <summary>TEST_ONLY when the record or the authorization says so (§19): such a session issues only <c>test_only=true</c> receipts.</summary>
    public bool TestOnly => Trust.Resolution == ApprovalResolution.ResolvedTestOnly || ((JBool)Trust.Authorization["test_only"]).Value;
}

/// <summary>The verifier's own Test Profile run of a session (§12.3): the per-check evidence made from its child <c>dotnet test</c> TRX files. Only <see cref="Certification.RunTestProfile"/> makes one.</summary>
public sealed class TestProfileRun
{
    internal TestProfileRun(CertificationSession session, IReadOnlyDictionary<string, JObject> evidence) =>
        (Session, Evidence) = (session, new System.Collections.ObjectModel.ReadOnlyDictionary<string, JObject>(evidence.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal)));   // B1 T11 R1

    internal CertificationSession Session { get; }

    public IReadOnlyDictionary<string, JObject> Evidence { get; }
}

/// <summary>
/// B1 T11 R2: a submitted REGRESSION_PROOF (§21, one set for the approved check profile) found byte-identical to the session's own Test Profile run (§12.3 2), with its archived reference. Only
/// <see cref="Certification.VerifyRegressionProof"/> makes one.
/// </summary>
public sealed class VerifiedRegressionProof
{
    internal VerifiedRegressionProof(TestProfileRun run, JObject source) => (Run, Source) = (run, source);

    internal TestProfileRun Run { get; }

    /// <summary>The ORIGINAL source file of the proof (its archived reference, origin ORIGINAL).</summary>
    internal JObject Source { get; }
}

/// <summary>
/// B1-T11-01 / T11-02: the ONLY production path from verification to a receipt (§18.4 E2-1..E2-7, §19, §20.2). A session exists only after E2-1 (no DOTNET_ / COMPlus_ variable), the trust bootstrap
/// (E2-2/E2-3), E2-4 (C0 against the approved profile, C1 == C0) and E2-5 (bit Golden); results are verified INSIDE the session; a receipt is built here from that result alone — targets, A+, approvals,
/// source files, PZL state and <c>test_only</c> are never taken from the caller — after the §19 table (technical, statistical, PZL) and with the E2-7 re-capture done here. A Phase receipt additionally
/// binds its exact three child receipts (registered bytes, order, roles, set, record, run, evidence id, A+, TEST_ONLY) and the session's own Test Profile run. Nothing here enables official execution:
/// a local or TEST_ONLY registry never certifies (A17).
/// </summary>
public static class Certification
{
    private static readonly ConditionalWeakTable<VerifiedFamily, CertificationSession> Sessions = new();

    /// <summary>Tests only: the process environment variables for E2-1 (a <c>dotnet test</c> host itself carries DOTNET_ variables).</summary>
    internal static Func<IDictionary>? VariablesSeam { get; set; }

    /// <summary>Tests only: the environment capture for C0 / C1 / C2 (to make E2-4 / E2-7 fail).</summary>
    internal static Func<EnvironmentSnapshot>? CaptureSeam { get; set; }

    private static EvidenceException Environment(string cause, string message) => EvidenceCodes.Fail(EvidenceCodes.EnvironmentMismatch, cause, "", "environment", message);

    private static EvidenceException NotCertifiable(string cause, string message) => EvidenceCodes.Fail(EvidenceCodes.NotCertifiable, cause, "", "certification", message);

    private static EnvironmentSnapshot Capture() => CaptureSeam?.Invoke() ?? ExecutionEnvironment.Capture();

    /// <summary>E2-1 → E2-2/E2-3 (trust bootstrap) → E2-4 → E2-5; any failure leaves no session.</summary>
    public static CertificationSession Open(TrustAnchorV1 anchor, IEvidenceRegistry registry, string approvalRecordId, string approvalRecordSha256, string authorizationId, string repositoryRoot,
        string archiveRoot)
    {
        if (ExecutionEnvironment.ForbiddenVariables(VariablesSeam?.Invoke() ?? System.Environment.GetEnvironmentVariables()) is { Count: > 0 } forbidden)
            throw Environment("Environment.forbidden_variable", $"금지 접두사 환경 변수가 있습니다: {string.Join(", ", forbidden)} (E2-1).");
        var c0 = Capture();
        var trust = TrustBootstrap.Resolve(anchor, registry, approvalRecordId, approvalRecordSha256, authorizationId, repositoryRoot);
        if (ExecutionEnvironment.Mismatch(c0, trust.Environment.Doc) is { } bad)
            throw Environment("Environment.profile_mismatch", $"C0가 승인 Environment Profile과 다릅니다(E2-4): {bad}");
        if (Capture().Text() != c0.Text())
            throw Environment("Environment.c1_differs", "C1이 C0와 다릅니다(E2-4).");
        if (ExecutionEnvironment.GoldenMismatch(trust.Environment.Doc) is { } golden)
            throw Environment("Environment.bit_golden", $"비트 Golden이 승인 값과 다릅니다(E2-5): {golden}");
        return new CertificationSession(trust, registry, c0, repositoryRoot, archiveRoot);
    }

    /// <summary>E2-6: the family verified under the session's approval, authorization and registry trust; the result stays bound to this session.</summary>
    public static VerifiedFamily VerifyFamily(CertificationSession session, string family, JObject documentReference, EventJsonCodec? codec = null)
    {
        var v = Verifier.VerifyFamily(new FamilyVerificationRequest(session.Trust.Approval, session.Trust.Authorization, family, session.RepositoryRoot, session.ArchiveRoot, documentReference, codec),
            registryTrusted: session.Registry.Trusted && session.Trust.Resolution == ApprovalResolution.ResolvedOfficial);
        Sessions.Add(v, session);
        return v;
    }

    /// <summary>
    /// §12.3: the session's Test Profile checked (required cases, fixture blobs of the approved commit, every TEST_ASSEMBLY's bytes against the Build Provenance) and run by the verifier itself.
    /// <paramref name="buildRoot"/> is the checkout holding the built tree; nothing from it is trusted that the provenance does not pin.
    /// </summary>
    public static TestProfileRun RunTestProfile(CertificationSession session, string buildRoot, string workDirectory)
    {
        var profile = session.Trust.TestProfile.Doc;
        TestProfiles.CheckRequired(profile);
        TestProfiles.CheckFixtures(profile, buildRoot);
        var cases = Items(profile["checks"]).Select(Obj).SelectMany(c => Items(c["cases"]).Select(Text)).Distinct(StringComparer.Ordinal).ToList();
        var results = new List<TestCaseResult>();
        foreach (var rel in Items(session.Trust.Build.Doc["artifacts"]).Select(Obj).Where(x => Text(x["role"]) == "TEST_ASSEMBLY").Select(x => Text(x["relative_path"])))
        {
            var full = Path.Combine([buildRoot, .. rel.Split('/')]);
            TestProfiles.CheckAssembly(session.Trust.Build.Doc, rel, File.ReadAllBytes(full));
            results.AddRange(TestProfiles.Run(Path.GetFileNameWithoutExtension(rel), full, cases, workDirectory));
        }

        return new TestProfileRun(session, TestProfiles.Evidence(profile, session.Trust.TestProfile.Sha, results));
    }

    /// <summary>The session's own result in its role, as its verification snapshot (B1 T11 R1): everything issued is derived from the returned copy, never from the caller's object.</summary>
    private static VerifiedFamily Bound(CertificationSession session, VerifiedFamily v, string family)
    {
        if (!Verifier.IsVerified(v) || !Sessions.TryGetValue(v, out var owner) || !ReferenceEquals(owner, session))
            throw NotCertifiable("Certification.unverified_result", "이 세션이 검증한 결과가 아닙니다(직접 만들거나 바꾼 결과는 발급 대상이 아님).");
        if (v.Family != family)
            throw NotCertifiable("Certification.family_role", $"{family} 자리에 {v.Family} 결과가 왔습니다.");
        return Verifier.Snapshot(v);
    }

    private static string Kind(string family) => family switch { "fc3-cal" => "CALIBRATION", "fc3-int" => "INTEGRATION", _ => "DOMINANCE" };

    private static JObject Target(CertificationSession s, VerifiedFamily v) => J.O(
        ("kind", J.S(Kind(v.Family))), ("run_id", s.Trust.Approval.Family(v.Family)["run_id"]), ("evidence_id", Obj(v.Document["execution"])["evidence_id"]), ("evidence_hash", J.S(v.Payload.Hash)),
        ("technical_status", J.S(v.TechnicalStatus!)), ("statistical_status", J.S(v.StatisticalStatus!)));

    /// <summary>
    /// B1 T11 R2: the ORIGINAL source files of a receipt hold exactly the originals this session itself used — the approval record and build provenance it resolved, every input the run consumed
    /// (INPUT_BUNDLE, R2-A: the two profiles of the session's trust and every embedded data file), the preflight records of every family it covers (PREFLIGHT, R2-B: one Family record and one per
    /// approved unit) and, for a Phase, exactly the verified REGRESSION_PROOF — by role and raw SHA from the session's trust and the run's own loading path, never from the submitted list. A list that
    /// merely agrees with its own RAW_INVENTORY is not enough.
    /// </summary>
    internal static void RequireOriginals(CertificationSession s, IReadOnlyList<JObject> originals, string? regressionProofSha, IReadOnlyList<string> families)
    {
        string Shas(string role) => string.Join(",", originals.Where(o => Text(o["role"]) == role).Select(o => Text(o["raw_sha256"])).Order(StringComparer.Ordinal));
        (string Role, string Expected)[] required =
        [
            ("APPROVAL_RECORD", s.Trust.Approval.RawSha256), ("BUILD_PROVENANCE", s.Trust.Build.Sha), ("INPUT_BUNDLE", ConsumedInputs.Expected(s.Trust.Environment.Sha, s.Trust.TestProfile.Sha)),
            ("REGRESSION_PROOF", regressionProofSha ?? ""),
        ];
        foreach (var (role, expected) in required)
            if (Shas(role) != expected)
                throw NotCertifiable("Certification.required_original", $"영수증 ORIGINAL의 {role}가 이 세션이 실제로 쓴 원본과 다릅니다(§21, 기대 [{expected}], 받은 값 [{Shas(role)}]).");
        static IEnumerable<string> Units(string family) => family switch
        {
            "fc3-cal" => OfficialFamilyPlan.Calibration.Units.Select(u => u.Id),
            "fc3-int" => OfficialFamilyPlan.Integration.Units.Select(u => u.Id),
            _ => DominanceMatrix.Cells.Select(c => c.Id),
        };
        var expectedPreflights = string.Join(",", families.SelectMany(f => Units(f).Prepend("")).Order(StringComparer.Ordinal));
        var preflights = string.Join(",", originals.Where(o => Text(o["role"]) == "PREFLIGHT").Select(o => o["unit_id"] is JString u ? u.Value : "").Order(StringComparer.Ordinal));
        if (preflights != expectedPreflights)
            throw NotCertifiable("Certification.required_original", $"영수증 ORIGINAL의 PREFLIGHT가 Family 기록 {families.Count}개 + 승인 Unit별 1개가 아닙니다(§21).");
    }

    /// <summary>§20.2 4: the receipt from verified values only; the canonical payload and the RAW_INVENTORY of the snapshot are archived as VERIFIER_DERIVED source files.</summary>
    private static IssuedReceipt Issue(CertificationSession s, EvidencePayload payload, IReadOnlyList<JObject> originals, JArray targets, IReadOnlyList<JObject> checks, IReadOnlyList<string> children,
        IReadOnlyList<string> families, string? regressionProofSha = null)
    {
        RequireOriginals(s, originals, regressionProofSha, families);
        var a = s.Trust.Approval;
        var id = Guid.NewGuid().ToString("D");
        JObject Derived(string rel, byte[] bytes, string role) => (JObject)JEditMembers.With(Archive.WriteNew(s.ArchiveRoot, rel, bytes, $"{role.ToLowerInvariant().Replace('_', '-')}-{id}", role), "origin",
            J.S("VERIFIER_DERIVED"));
        var inventory = Archive.Inventory(originals.Select(o => new JObject(o.Members.Where(m => m.Key != "origin").ToList())));
        var sources = originals.Concat([Derived($"verifier/{id}/canonical-payload.json", payload.Canonical, "CANONICAL_PAYLOAD"), Derived($"verifier/{id}/raw-inventory.json", inventory, "RAW_INVENTORY")])
            .OrderBy(x => Text(x["artifact_id"]), StringComparer.Ordinal).ToList();
        var verifier = Obj(Items(a.Record["verifiers"])[0]);
        var receipt = J.O(
            ("format", J.S(Receipts.Format)), ("receipt_id", J.S(id)), ("validation_set_id", J.S(a.ValidationSetId)),
            ("approvals", J.O(("approval_record_id", J.S(a.Id)), ("approval_record_sha256", J.S(a.RawSha256)), ("freeze_id", J.S(a.FreezeId)))), ("contracts", a.Record["contracts"]),
            ("verifier", J.O(("id", verifier["id"]), ("version", verifier["version"]), ("source_git_sha", verifier["source_git_sha"]), ("binary_sha256", verifier["binary_sha256"]))),
            ("build_provenance", a.Ref("build_provenance")), ("environment_profile", a.Ref("environment_profile")), ("pzl_test_profile", a.Ref("pzl_test_profile")),
            ("execution_authorization_id", s.Trust.Authorization["authorization_id"]), ("test_only", J.B(s.TestOnly)), ("verification_decision", J.S("ELIGIBLE")),
            ("pzl_status", J.S("REQUIRED_CHECKS_SATISFIED")), ("targets", targets), ("checks", J.A(checks)), ("source_files", J.A(sources)), ("child_receipt_ids", J.A(children.Select(c => (JNode)J.S(c)))));
        Receipts.CheckInventory(receipt, s.ArchiveRoot);                                                  // OWNER decision 3 §2: the inventory is this receipt's ORIGINAL list
        return Receipts.Issue(receipt, s.ArchiveRoot, s.Registry, () => Recheck(s));
    }

    /// <summary>E2-7: C2 captured here, equal to C0 and conformant to the profile.</summary>
    private static string? Recheck(CertificationSession s)
    {
        var c2 = Capture();
        return c2.Text() != s.C0.Text() ? "C2 ≠ C0" : ExecutionEnvironment.Mismatch(c2, s.Trust.Environment.Doc);
    }

    /// <summary>
    /// B1 T11 R2: the submitted REGRESSION_PROOF of the session's own Test Profile run — one archived ORIGINAL (role REGRESSION_PROOF, no unit or game) read through the archive snapshot, whose bytes
    /// are exactly <see cref="TestProfiles.RegressionProof"/> of that run (§12.3 2: the submitted evidence equals the verifier's own result). Else not certifiable / <c>test_profile_mismatch</c>.
    /// </summary>
    public static VerifiedRegressionProof VerifyRegressionProof(CertificationSession session, TestProfileRun run, JObject reference)
    {
        if (!ReferenceEquals(run.Session, session))
            throw NotCertifiable("Certification.test_profile", "이 세션이 직접 실행한 Test Profile 결과가 아닙니다(§12.3).");
        if (Text(reference["role"]) != "REGRESSION_PROOF" || reference["unit_id"] is not JNull || reference["game_id"] is not JNull)
            throw EvidenceCodes.Fail(EvidenceCodes.ArchiveInvalid, "Archive.required_original", "/role", "regression_proof", "REGRESSION_PROOF는 Unit·경기에 속하지 않는 그 역할의 참조여야 합니다(§21).");
        var file = Archive.Snapshot(session.ArchiveRoot, [reference]).Values.Single();
        if (!file.Bytes.AsSpan().SequenceEqual(TestProfiles.RegressionProof(run)))
            throw EvidenceCodes.Fail(EvidenceCodes.TestProfileMismatch, "TestProfile.regression_proof_differs", "", "regression_proof", "제출된 REGRESSION_PROOF가 검증기 자신의 Test Profile 실행 결과와 다릅니다(§12.3 2).");
        return new VerifiedRegressionProof(run, JEditMembers.With(file.Reference, "origin", J.S("ORIGINAL")));
    }

    /// <summary>A Family / Dominance receipt for a result this session verified (§19: technical COMPLETED, statistical PASS / FAIL / allowed INSUFFICIENT_SAMPLE, PZL required checks satisfied).</summary>
    public static IssuedReceipt IssueFamily(CertificationSession session, VerifiedFamily result)
    {
        var v = Bound(session, result, result.Family);
        if (v.Ineligible is { } no)
            throw new EvidenceException(no);
        return Issue(session, v.Payload, v.SourceFiles, J.A(Target(session, v)), [], [], [v.Family]);
    }

    /// <summary>
    /// A Phase receipt (§9.2, §20.1, §20.3): the session's three verified families in their roles, its own Test Profile run with its verified REGRESSION_PROOF, all 13 Phase checks satisfied (12b DEFERRED), and exactly three distinct child
    /// receipts — each registered with exactly its bytes, a single target of its role (CALIBRATION → INTEGRATION → DOMINANCE), of this validation set and approval record, this run id, evidence id and A+,
    /// with this session's TEST_ONLY state and statuses. Anything else is <c>not_certifiable</c>.
    /// </summary>
    public static IssuedReceipt IssuePhase(CertificationSession session, VerifiedFamily calibration, VerifiedFamily integration, VerifiedFamily dominance, IReadOnlyList<IssuedReceipt> children,
        VerifiedRegressionProof regressionProof)
    {
        string[] roles = ["fc3-cal", "fc3-int", "fc3-dom"];
        VerifiedFamily[] families = [calibration, integration, dominance];
        for (var i = 0; i < 3; i++)
            families[i] = Bound(session, families[i], roles[i]);                                           // B1 T11 R1: the snapshots from here on
        (calibration, integration, dominance) = (families[0], families[1], families[2]);
        var childTargets = BindChildren(session, families, children);                                      // first, always: the exact three children (B1-T11-02)
        var testProfile = regressionProof.Run;
        if (!ReferenceEquals(testProfile.Session, session))
            throw NotCertifiable("Certification.test_profile", "이 세션이 직접 실행한 Test Profile 결과가 아닙니다(§12.3).");
        var checks = PhaseVerifier.Checks(calibration, integration, dominance, testProfile.Evidence);
        if (checks.FirstOrDefault(c => Text(c["status"]) != (Text(c["id"]) == "pzl-12b" ? "DEFERRED" : "PASS")) is { } failing)
            throw NotCertifiable("Certification.phase_checks", $"Phase Check {Text(failing["id"])}가 {Text(failing["status"])}입니다(§19).");
        var payload = EvidenceHash.Phase(calibration.Payload, integration.Payload, dominance.Payload, checks);
        var phase = J.O(("kind", J.S("PHASE")), ("run_id", JNode.Null), ("evidence_id", J.S(EvidenceIds.Phase(session.Trust.Approval.ValidationSetId))), ("evidence_hash", J.S(payload.Hash)),
            ("technical_status", J.S("COMPLETED")), ("statistical_status", JNode.Null));
        var originals = PhaseOriginals([.. families.Select(f => f.SourceFiles), [regressionProof.Source]]);
        return Issue(session, payload, originals, J.A([.. childTargets, phase]), checks, children.Select(c => c.ReceiptId).ToList(), roles, Text(regressionProof.Source["raw_sha256"]));
    }

    /// <summary>The roles of the validation set itself (§21: one approval version, one build set, the consumed inputs) — the only originals two families may name with one artifact id.</summary>
    private static readonly string[] SetScoped = ["APPROVAL_RECORD", "BUILD_PROVENANCE", "INPUT_BUNDLE"];

    /// <summary>
    /// B1 T11 R3: the union of the families' ORIGINAL source files (and the regression proof) for the Phase receipt and its RAW_INVENTORY — nothing chosen silently. One artifact id is merged only when
    /// both references are the same original (all seven fields and the origin) of a validation-set role; the same id naming another path, SHA, length or role, or a family's own file named twice, and two
    /// ids for one path are <c>archive_invalid</c>.
    /// </summary>
    internal static IReadOnlyList<JObject> PhaseOriginals(IEnumerable<IReadOnlyList<JObject>> sources)
    {
        static EvidenceException Conflict(string cause, string message) => EvidenceCodes.Fail(EvidenceCodes.ArchiveInvalid, cause, "/source_files", "phase", message);
        var byId = new Dictionary<string, JObject>(StringComparer.Ordinal);
        var byPath = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var list in sources)
            foreach (var x in list)
            {
                var (id, path) = (Text(x["artifact_id"]), Text(x["relative_path"]));
                if (byId.TryGetValue(id, out var known))
                {
                    if (Jcs.Text(known) != Jcs.Text(x) || !SetScoped.Contains(Text(x["role"])))
                        throw Conflict("Phase.artifact_id_conflict", $"artifact_id '{id}'가 Family마다 다른 원본(경로·SHA·역할) 또는 Family 고유 파일을 가리킵니다(하나를 고르지 않음, §2.5·§20.1).");
                    continue;
                }

                if (byPath.TryGetValue(path, out var other))
                    throw Conflict("Phase.path_conflict", $"'{path}'를 서로 다른 artifact_id '{other}'·'{id}'가 가리킵니다.");
                (byId[id], byPath[path]) = (x, id);
            }

        return [.. byId.Values.OrderBy(x => Text(x["artifact_id"]), StringComparer.Ordinal)];
    }

    /// <summary>B1-T11-02: the exact three children, bound to the verified families (A28); returns their targets in order.</summary>
    private static IReadOnlyList<JNode> BindChildren(CertificationSession session, IReadOnlyList<VerifiedFamily> families, IReadOnlyList<IssuedReceipt> children)
    {
        static EvidenceException Child(string cause, string message) => EvidenceCodes.Fail(EvidenceCodes.NotCertifiable, cause, "/child_receipt_ids", "phase", message);
        if (children.Count != 3)
            throw Child("Phase.children_missing", "Phase 영수증에는 세 자식 영수증이 모두 필요합니다(A28).");
        if (children.Select(c => c.ReceiptId).Distinct(StringComparer.Ordinal).Count() != 3)
            throw Child("Phase.children_duplicate", "세 자식 영수증은 서로 달라야 합니다.");
        var a = session.Trust.Approval;
        var targets = new List<JNode>();
        for (var i = 0; i < 3; i++)
        {
            var (child, v) = (children[i], families[i]);
            if (v.Ineligible is { } no)
                throw new EvidenceException(no);                                                           // a family without a receipt of its own can have no child (A28)
            var entry = session.Registry.Get("VERIFICATION_RECEIPT", child.ReceiptId);
            if (entry is null || !entry.RawBytes.AsSpan().SequenceEqual(child.Bytes) || entry.RawSha256 != Archive.Sha(entry.RawBytes))
                throw Child("Phase.child_not_registered", $"자식 영수증 {child.ReceiptId}가 기록부에 같은 바이트로 등록되어 있지 않습니다(A28).");
            var r = Receipts.Validate(entry.RawBytes);                                                     // the registered bytes, not the caller's object
            var t = Obj(Items(r["targets"]).Single());
            var ap = Obj(r["approvals"]);
            var expected = Target(session, v);
            if (Text(t["kind"]) != Kind(v.Family))
                throw Child("Phase.child_role", $"{i + 1}번째 자식은 {Kind(v.Family)} 영수증이어야 합니다(받은 값 {Text(t["kind"])}).");
            if (Text(r["validation_set_id"]) != a.ValidationSetId || (Text(ap["approval_record_id"]), Text(ap["approval_record_sha256"]), Text(ap["freeze_id"])) != (a.Id, a.RawSha256, a.FreezeId))
                throw Child("Phase.child_approval", $"{Kind(v.Family)} 자식이 이 검증 집합·승인 기록의 영수증이 아닙니다.");
            if (!Jcs.Bytes(t).AsSpan().SequenceEqual(Jcs.Bytes(expected)))
                throw Child("Phase.child_target", $"{Kind(v.Family)} 자식의 run_id·evidence_id·A+·상태가 검증된 Family와 다릅니다.");
            if (((JBool)r["test_only"]).Value != session.TestOnly)
                throw Child("Phase.child_test_only", $"{Kind(v.Family)} 자식의 test_only가 이 세션과 다릅니다.");
            Receipts.CheckInventory(r, session.ArchiveRoot);                                               // its RAW_INVENTORY is its own ORIGINAL list (OWNER decision 3 §2)
            targets.Add(t);
        }

        return targets;
    }
}
