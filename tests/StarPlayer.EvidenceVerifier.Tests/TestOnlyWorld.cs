using StarPlayer.SimHarness;
using StarPlayer.SimHarness.Evidence;
using StarPlayer.SimHarness.Tests;
using StarPlayer.SimHarness.Tests.Evidence;

namespace StarPlayer.EvidenceVerifier.Tests;

/// <summary>
/// A TEST_ONLY validation set in one temp root: all 54 seed files (4 calibration, 2 integration, 48 dominance; fixture seeds), a TEST_ONLY approval record pinning them, a TEST_ONLY execution
/// authorization, the official Integration and Dominance runs on the real engine (executed with a separate READY test approvals instance, as the existing family tests do), and the producer's archive of
/// the Dominance evidence. Nothing here is an official seed, pin, cohort, sample size, SID or approval; <see cref="OfficialApprovals.Current"/> is untouched.
/// </summary>
public sealed class TestOnlyWorld : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "simharness-tests", "b1-world-" + Guid.NewGuid().ToString("N"));

    public string Archive => Path.Combine(Root, "archive");

    public ApprovalRecordV1 Approval { get; }

    public JObject Authorization { get; }

    public EvidenceContext Context { get; }

    public DominanceOfficialResult Dominance { get; }

    public FamilyOfficialResult Integration { get; }

    public FamilyOfficialResult Calibration { get; }

    public static string RepositoryRoot { get; } = FindRoot();

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                return d.FullName;
        throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
    }

    /// <summary>The real HEAD commit (approval git_commit_sha, so the Test Profile and its fixtures bind to an actual tree).</summary>
    public static string Head { get; } = GitIntegrity.Run(RepositoryRoot, ["rev-parse", "HEAD"]) is { ExitCode: 0 } r ? System.Text.Encoding.ASCII.GetString(r.Stdout).Trim() : throw new InvalidOperationException("git rev-parse HEAD 실패");

    public JObject DominanceReference { get; }

    public JObject DominanceDocument { get; }

    public TestOnlyWorld()
    {
        // B1 T11 R2-A: the repository originals of the embedded inputs, below this world's verification root (the verifier compares the resource bytes the run read with them)
        foreach (var (_, path) in HarnessData.Inputs)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Root, path))!);
            File.Copy(Path.Combine(RepositoryRoot, path), Path.Combine(Root, path));
        }

        var pins = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var s in CalibrationStrata.All)
            pins[FamilyOfficialWorld.CalPath(s.Id)] = DominanceOfficialWorld.Put(Root, FamilyOfficialWorld.CalPath(s.Id), FamilyOfficialWorld.CalFile(s));
        foreach (var (mode, rules) in FamilyOfficialWorld.IntModes)
            pins[FamilyOfficialWorld.IntPath(mode)] = DominanceOfficialWorld.Put(Root, FamilyOfficialWorld.IntPath(mode), FamilyOfficialWorld.IntFile(mode, rules));
        foreach (var c in DominanceMatrix.Cells)
            pins[DominanceOfficialWorld.SeedPath(c)] = DominanceOfficialWorld.Put(Root, DominanceOfficialWorld.SeedPath(c), DominanceOfficialWorld.SeedFile(c, seedBase: DominanceSeedBase));

        // the record: the catalogue of §13.2 with this world's pins
        JObject Unit(string id, string cohort, string rules, long planned, string path) => J.O(("unit_id", J.S(id)), ("cohort_id", J.S(cohort)), ("rules_profile_id", J.S(rules)),
            ("planned_games", J.N(planned)), ("seed_manifest_path", J.S(path)), ("seed_manifest_sha256", J.S(pins[path])));
        var template = ApprovalFixture.Approval();
        var record = (JObject)JEditT.Set(JEditT.Set(template, "/git_commit_sha", J.S(Head)), "/families", J.A(
            J.O(("family", J.S("fc3-cal")), ("run_id", J.S("run-cal-test")), ("units", J.A(CalibrationStrata.All.Select(s => (JNode)Unit(s.Id, s.CohortId, s.RulesProfileId, 12, FamilyOfficialWorld.CalPath(s.Id)))))),
            J.O(("family", J.S("fc3-int")), ("run_id", J.S("run-int-test")), ("units", J.A(Unit("automated", NeutralCohort.Id, HarnessData.AutomatedProfile, 100, FamilyOfficialWorld.IntPath("a")),
                Unit("non-automated", NeutralCohort.Id, HarnessData.NonAutomatedProfile, 100, FamilyOfficialWorld.IntPath("n"))))),
            J.O(("family", J.S("fc3-dom")), ("run_id", J.S("run-dom-test")), ("units", J.A(DominanceMatrix.Cells.Select(c => (JNode)Unit(c.Id, c.CohortId, c.RulesProfileId, 2, DominanceOfficialWorld.SeedPath(c))))))));
        // B1 T11 R2: the record pins real Build Provenance / Environment Profile / Test Profile bytes (the default TestOnlyTrust files), so the producer can archive the originals it names
        var (build, environment, testProfile) = TestOnlyTrust.Files(TestOnlyTrust.DefaultAnchor(), Schema.Text(((JObject)Schema.Items(record["verifiers"])[0])["binary_sha256"]));
        record = TestOnlyTrust.Repoint(record, build, environment, testProfile);
        Approval = ApprovalFormats.ReadApproval(Jcs.Bytes(record), Root);
        Authorization = ApprovalFormats.ReadAuthorization(Jcs.Bytes(J.O(("format", J.S(ApprovalFormats.AuthorizationFormat)), ("authorization_id", J.S("auth-test-1")),
            ("approval_record_id", J.S(Approval.Id)), ("approval_record_sha256", J.S(Approval.RawSha256)), ("test_only", J.B(true)),
            ("families", J.A(J.S("fc3-cal"), J.S("fc3-int"), J.S("fc3-dom"))))), Approval);
        Context = new EvidenceContext(Approval.ValidationSetId, Approval.Id, Approval.RawSha256, Approval.FreezeId, "bp-test-1", null);

        // producer side: a READY test instance with the same pins runs the official paths (the verifier itself never calls Execute)
        var ready = ApprovalFormats.ToOfficialApprovals(Approval, executionPathReady: true);
        var git = GitIntegrityPolicy.Evaluate([], Head);
        Dominance = DominanceOfficial.Execute(DominancePreflight.Run(new DominanceOfficialRequest(Root, "run-dom-test", null), ready, git), "run-dom-test", ready, Root);
        Integration = FamilyOfficial.Execute(FamilyPreflight.Run(OfficialFamilyPlan.Integration, new DominanceOfficialRequest(Root, "run-int-test", null), ready, git), "run-int-test", ready, Root);
        // 12 real games per stratum against the 100,000-PA target: a legal stop-aware run that ends SEEDS_EXHAUSTED (gate INSUFFICIENT_SAMPLE)
        Calibration = FamilyOfficial.Execute(FamilyPreflight.Run(OfficialFamilyPlan.Calibration, new DominanceOfficialRequest(Root, "run-cal-test", null), ready, git), "run-cal-test", ready, Root);

        Producer = new Production(this, Archive, Context, new PinnedFiles(Approval.Raw, build, environment, testProfile));
        (DominanceReference, DominanceDocument) = Producer.Dominance();
    }

    /// <summary>The producer of this world's own archive and approval context.</summary>
    public Production Producer { get; }

    /// <summary>A producer for another approval (a <see cref="TestOnlyTrust"/>'s re-pointed record) in a fresh archive of this world: its documents bind that record (S4) and archive its originals.</summary>
    public Production ProducerFor(TestOnlyTrust trust) =>
        new(this, Path.Combine(Root, "archive-" + Guid.NewGuid().ToString("N")),
            new EvidenceContext(trust.Approval.ValidationSetId, trust.Approval.Id, trust.Approval.RawSha256, trust.Approval.FreezeId, "bp-test-1", null), trust.Pinned);

    /// <summary>The fictional seed base of this world's 48 cells (2 games each): one whose TEST_ONLY gate is PASS, so a Dominance receipt and a Phase receipt can be issued (B1 T11 R3 positive path).</summary>
    public const long DominanceSeedBase = 991000;

    public string DominanceResultDirectory => Path.Combine(Root, "build", "simharness", "official", "run-dom-test");

    public FamilyVerificationRequest DominanceRequest(ApprovalRecordV1? approval = null, JObject? authorization = null, JObject? reference = null) =>
        new(approval ?? Approval, authorization ?? Authorization, DominanceMatrix.Family, Root, Archive, reference ?? DominanceReference);

    public (JObject Reference, JObject Document) ProduceFamily(FamilyOfficialResult result, string clause, string fileName, JObject? pzl6Reference = null) =>
        Producer.Family(result, clause, fileName, pzl6Reference);

    public (JObject Reference, JObject Document, JObject Pzl6) ProduceIntegration(EventJsonCodec? codec = null, string fileName = "integration-result.json") => Producer.Integration(codec, fileName);

    public FamilyVerificationRequest FamilyRequest(string family, JObject reference, EventJsonCodec? codec = null) =>
        new(Approval, Authorization, family, Root, Archive, reference, codec);

    private readonly List<IDisposable> _owned = [];

    /// <summary>Something holding a file below <see cref="Root"/> open (a registry connection): closed before the world is deleted.</summary>
    public T Own<T>(T disposable) where T : IDisposable
    {
        lock (_owned)
            _owned.Add(disposable);
        return disposable;
    }

    /// <summary>Per-world state shared by the test instances of one class (xUnit makes a new instance per test; the archive never overwrites).</summary>
    public T Shared<T>(string key, Func<T> make) where T : notnull
    {
        lock (_shared)
        {
            if (!_shared.TryGetValue(key, out var value))
                _shared[key] = value = make();
            return (T)value;
        }
    }

    private readonly Dictionary<string, object> _shared = new(StringComparer.Ordinal);

    public void Dispose()
    {
        lock (_owned)
            _owned.ForEach(d => d.Dispose());
        Directory.Delete(Root, true);
    }
}

/// <summary>
/// The producer's write path for one archive and one approval context (§21 order: bundles → Unit PZL → PZL6 and its 20 JSON files → run files and seeds → Family / Dominance document). The archive never
/// overwrites, so each family is produced once per producer (Integration's units, seeds and run files once, its #6 / document per call).
/// </summary>
public sealed class Production(TestOnlyWorld w, string archive, EvidenceContext context, PinnedFiles pinned)
{
    public string ArchiveRoot => archive;

    private IReadOnlyList<JObject>? _trust;

    /// <summary>B1 T11 R2: the validation set's originals, archived once and referenced by every family document — the approval record, the build provenance and the two consumed input profiles.</summary>
    public IReadOnlyList<JObject> Trust()
    {
        lock (_made)
            return _trust ??=
            [
                Archive.WriteNew(archive, "trust/approval-record.json", pinned.Approval, "approval-record", "APPROVAL_RECORD"),
                Archive.WriteNew(archive, "trust/build-provenance.json", pinned.Build, "build-provenance", "BUILD_PROVENANCE"),
                Archive.WriteNew(archive, "trust/environment-profile.json", pinned.Environment, "input-environment-profile", "INPUT_BUNDLE"),
                Archive.WriteNew(archive, "trust/pzl-test-profile.json", pinned.TestProfile, "input-pzl-test-profile", "INPUT_BUNDLE"),
                // B1 T11 R2-A: every embedded data file, the exact bytes the run read (logical name → id input-<name>)
                .. HarnessData.Inputs.Select(i => Archive.WriteNew(archive, $"inputs/{i.RepositoryPath}", HarnessData.ResourceBytes(i.Name),
                    "input-" + Path.GetFileNameWithoutExtension(i.Name).Replace('_', '-'), "INPUT_BUNDLE")),
            ];
    }

    /// <summary>B1 T11 R2: the submitted REGRESSION_PROOF of a Test Profile run (its TestEvidence, the bytes the verifier compares with its own run, §12.3 2).</summary>
    public JObject RegressionProof(TestProfileRun run) =>
        Archive.WriteNew(archive, $"trust/regression-proof-{Guid.NewGuid():N}.json", TestProfiles.RegressionProof(run), "regression-proof", "REGRESSION_PROOF");

    private IReadOnlyList<JObject> WriteRun(FamilyOfficialResult result) => EvidenceWriter.WriteRun(archive, Path.Combine(w.Root, "build", "simharness", "official", result.RunId),
        result.RunId, FamilyOfficial.ResultFile, result.Units.Select(u => (u.Plan.Pinned.Unit.Id, OfficialFamilyPlan.UnitDirectory(u.Plan.Pinned.Unit))));

    public (JObject Reference, JObject Document) Dominance()
    {
        lock (_made)
        {
            if (!_made.TryGetValue("dom", out var made))
                _made["dom"] = made = MakeDominance();
            return ((JObject, JObject))made;
        }
    }

    private (JObject Reference, JObject Document) MakeDominance()
    {
        var units = w.Dominance.Cells.Select(c => EvidenceWriter.Observe(c.Plan.Pinned.Cell.Id, c.Run)).ToList();
        var written = EvidenceWriter.WriteUnits(archive, context, DominanceMatrix.Family, TestOnlyWorld.Head, units);
        var seeds = EvidenceWriter.WriteSeeds(archive, w.Root, DominanceMatrix.Cells.Select(c => (c.Id, DominanceOfficialWorld.SeedPath(c))));
        var run = EvidenceWriter.WriteRun(archive, w.DominanceResultDirectory, "run-dom-test", DominanceOfficial.ResultFile, DominanceMatrix.Cells.Select(c => (c.Id, DominancePreflight.CellDirectory(c))));
        var doc = OfficialDocuments.Dominance(w.Dominance, context, [.. written.Select(x => x.Reference), .. seeds, .. run, .. Trust()]);
        return (EvidenceWriter.WriteDocument(archive, doc, "DOMINANCE_RESULT", "dominance-result.json"), doc);
    }

    /// <summary>Writes a family's units, seeds and run files and its document into the archive (DAG order) and returns the document reference and the document.</summary>
    public (JObject Reference, JObject Document) Family(FamilyOfficialResult result, string clause, string fileName, JObject? pzl6Reference = null)
    {
        lock (_made)
        {
            var key = $"fam|{result.RunId}|{fileName}";
            if (!_made.TryGetValue(key, out var made))
                _made[key] = made = MakeFamily(result, clause, fileName, pzl6Reference);
            return ((JObject, JObject))made;
        }
    }

    private (JObject Reference, JObject Document) MakeFamily(FamilyOfficialResult result, string clause, string fileName, JObject? pzl6Reference)
    {
        var ctx = context with { CombinationPolicyClauseId = clause };
        var family = result.Preflight.Plan.Family;
        var units = result.Units.Select(u => EvidenceWriter.Observe(u.Plan.Pinned.Unit.Id, u.Run)).ToList();
        var written = EvidenceWriter.WriteUnits(archive, ctx, family, TestOnlyWorld.Head, units);
        var seeds = EvidenceWriter.WriteSeeds(archive, w.Root, result.Units.Select(u => (u.Plan.Pinned.Unit.Id, u.Plan.Pinned.SeedManifestPath)));
        var doc = OfficialDocuments.Family(result, ctx, [.. written.Select(x => x.Reference), .. pzl6Reference is null ? [] : new[] { pzl6Reference }, .. seeds, .. WriteRun(result), .. Trust()]);
        return (EvidenceWriter.WriteDocument(archive, doc, "FAMILY_RESULT", fileName), doc);
    }

    private IReadOnlyList<JObject>? _integrationInputs;
    private readonly Dictionary<string, object> _made = new(StringComparer.Ordinal);

    /// <summary>The Integration evidence with its #6 document and the 20 JSON files (§21 PZL6 "+ 20경기 JSON bundle"), produced with <paramref name="codec"/> (default: the real Infrastructure codec).</summary>
    public (JObject Reference, JObject Document, JObject Pzl6) Integration(EventJsonCodec? codec = null, string fileName = "integration-result.json")
    {
        lock (_made)
        {
            var key = $"int|{fileName}|{(codec is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(codec))}";
            if (!_made.TryGetValue(key, out var made))
                _made[key] = made = MakeIntegration(codec, fileName);
            return ((JObject, JObject, JObject))made;
        }
    }

    private (JObject Reference, JObject Document, JObject Pzl6) MakeIntegration(EventJsonCodec? codec, string fileName)
    {
        var ctx = context with { CombinationPolicyClauseId = "clause-int-test" };
        _integrationInputs ??= [.. EvidenceWriter.WriteUnits(archive, ctx, "fc3-int", TestOnlyWorld.Head, w.Integration.Units.Select(u => EvidenceWriter.Observe(u.Plan.Pinned.Unit.Id, u.Run)).ToList())
                .Select(x => x.Reference),
            .. EvidenceWriter.WriteSeeds(archive, w.Root, w.Integration.Units.Select(u => (u.Plan.Pinned.Unit.Id, u.Plan.Pinned.SeedManifestPath))), .. WriteRun(w.Integration)];
        var draft = OfficialDocuments.Family(w.Integration, ctx, []);
        var samples = Enumerable.Range(0, 20).Select(i =>
        {
            var run = w.Integration.Units[i < 10 ? 0 : 1].Run;
            return new Pzl6Sample(i < 10 ? "automated" : "non-automated", run.Config, run.Specs[i % 10], (JObject)((JArray)((JObject)((JObject)((JArray)draft["units"]).Items[i < 10 ? 0 : 1])["technical"])["games"]).Items[i % 10]);
        }).ToList();
        var stem = Path.GetFileNameWithoutExtension(fileName);
        // OWNER decision 3 §7: role PZL6, game_id set, id pzl6-json-<mode>-<index:D2>
        var json = Pzl6Verifier.Bundle(samples, codec ?? EventJsonCodec.Infrastructure).Select((b, i) => Archive.WriteNew(archive, $"pzl6/{stem}/{b.UnitId}-{i % 10 + 1:D2}.json", b.Json,
            $"pzl6-json-{b.UnitId}-{i % 10 + 1:D2}", "PZL6", b.UnitId, b.GameId)).ToList();
        var pzl6 = Pzl6Verifier.Document(draft, samples, codec ?? EventJsonCodec.Infrastructure, json);
        var pzl6Ref = EvidenceWriter.WriteDocument(archive, pzl6, "PZL6", $"pzl6-{stem}.json");
        var doc = OfficialDocuments.Family(w.Integration, ctx, [.. _integrationInputs, pzl6Ref, .. Trust()]);
        return (EvidenceWriter.WriteDocument(archive, doc, "FAMILY_RESULT", fileName), doc, pzl6);
    }
}

/// <summary>The bytes of the originals a producer archives for its approval record (B1 T11 R2).</summary>
public sealed record PinnedFiles(byte[] Approval, byte[] Build, byte[] Environment, byte[] TestProfile);

/// <summary>Copy-on-write pointer edits (the verifier tests' own copy of the harness tests' helper, which is internal there).</summary>
internal static class JEditT
{
    public static JNode Set(JNode root, string pointer, JNode value) => Change(root, pointer.Length == 0 ? [] : pointer[1..].Split('/'), 0, value);

    private static JNode Change(JNode n, string[] t, int at, JNode v)
    {
        if (at == t.Length)
            return v;
        if (n is JObject o)
            return new JObject(o.Members.Select(m => m.Key == t[at] ? new KeyValuePair<string, JNode>(m.Key, Change(m.Value, t, at + 1, v)) : m).ToList());
        var a = (JArray)n;
        var i = int.Parse(t[at]);
        return new JArray(a.Items.Select((x, k) => k == i ? Change(x, t, at + 1, v) : x).ToList());
    }

    public static JNode Get(JNode root, string pointer) =>
        (pointer.Length == 0 ? [] : pointer[1..].Split('/')).Aggregate(root, (n, k) => n is JObject o ? o[k] : ((JArray)n).Items[int.Parse(k)]);
}
